using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Sales;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Sales;

/// <summary>
/// Satış onayı (muhasebe.md bölüm 6 ve 24). Tek SaveChanges — dolayısıyla tek
/// transaction — içinde: stok düşümü (hizmet hariç), müşterili satışta cari borç,
/// istenirse anlık tahsilat (kasa hareketi + cari alacak). Onaydan sonra belge
/// değiştirilemez; düzeltme iptal + yeni belge ile yapılır.
/// </summary>
public sealed class ConfirmSaleHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<SaleResponse> HandleAsync(Guid saleId, ConfirmSaleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);

        var sale = await SaleQueries.FindSaleAsync(db, tenantId, saleId, cancellationToken)
            ?? throw new NotFoundException("Satış bulunamadı.");

        if (sale.Status != SaleStatus.Draft)
        {
            throw new ConflictException("Yalnızca taslak satış onaylanabilir.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Stok düşümü: her kalem için satış hareketi (işaretli, negatif). Hizmet düşmez.
        foreach (var item in sale.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product) && product.IsService)
            {
                continue;
            }

            var stock = await db.InventoryTransactions
                .Where(t => t.TenantId == tenantId && t.ProductId == item.ProductId && t.WarehouseId == sale.WarehouseId)
                .SumAsync(t => (decimal?)t.Quantity, cancellationToken) ?? 0m;

            if (stock < item.Quantity)
            {
                throw new ConflictException(
                    $"'{item.ProductName}' stoğu yetersiz: {stock} var, {item.Quantity} gerekli. " +
                    "Sayım ya da manuel giriş ile stoğu düzeltin ya da miktarı azaltın.");
            }

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                ProductId = item.ProductId,
                WarehouseId = sale.WarehouseId,
                Type = InventoryTransactionType.Sale,
                Quantity = -item.Quantity,
                Date = sale.Date,
                Description = $"Satış {sale.Number}",
                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                CreatedAtUtc = now,
            });
        }

        // Cari borç: müşterili satışta belge tutarı kadar borçlandırma.
        if (sale.PartyId is { } partyId)
        {
            db.PartyTransactions.Add(new PartyTransaction
            {
                TenantId = tenantId,
                PartyId = partyId,
                Type = PartyTransactionType.Sale,
                Debit = sale.Total,
                Credit = 0,
                Date = sale.Date,
                DueDate = sale.DueDate,
                Description = $"Satış {sale.Number}",
                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                CreatedAtUtc = now,
            });
        }

        // Anlık tahsilat: kasa hareketi + (müşteriliyse) cari alacak.
        if (request.Payment is { } payment)
        {
            if (payment.Amount <= 0 || payment.Amount > sale.Total)
            {
                throw new AppException("Tahsilat tutarı 0'dan büyük ve belge toplamını aşmayan bir değer olmalı.");
            }

            await AddPaymentAsync(db, tenantId, sale, payment.Date, payment.Amount, payment.Description,
                paidOnConfirm: true, timeProvider, now, cancellationToken);
        }

        sale.ConfirmedAtUtc = now;
        sale.Status = sale.PaidAmount == sale.Total
            ? SaleStatus.Paid
            : sale.PaidAmount > 0
                ? SaleStatus.PartiallyPaid
                : SaleStatus.Confirmed;
        sale.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return await SaleQueries.MaterializeAsync(db, tenantId, sale, cancellationToken);
    }

    /// <summary>Tahsilat yazımı — onay anı ve sonradan tahsilat ortaktır.</summary>
    internal static async Task AddPaymentAsync(
        IApplicationDbContext db,
        Guid tenantId,
        Sale sale,
        DateTime date,
        decimal amount,
        string? description,
        bool paidOnConfirm,
        TimeProvider timeProvider,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var account = await SaleAccounts.EnsureDefaultAccountAsync(db, tenantId, timeProvider, cancellationToken);

        var payment = new SalePayment
        {
            TenantId = tenantId,
            SaleId = sale.Id,
            AccountId = account.Id,
            Date = Dates.ToUtcDate(date),
            Amount = amount,
            Description = description.NullIfEmpty() ?? $"Tahsilat — {sale.Number}",
            PaidOnConfirm = paidOnConfirm,
            CreatedAtUtc = now,
        };
        db.SalePayments.Add(payment);

        db.AccountTransactions.Add(new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = account.Id,
            Type = AccountTransactionType.SaleCollection,
            Amount = amount,
            Date = payment.Date,
            Description = payment.Description,
            ReferenceType = "SalePayment",
            ReferenceId = payment.Id,
            CreatedAtUtc = now,
        });

        if (sale.PartyId is { } partyId)
        {
            // Tahsilat caride alacak (negatif) hareketi üretir.
            db.PartyTransactions.Add(new PartyTransaction
            {
                TenantId = tenantId,
                PartyId = partyId,
                Type = PartyTransactionType.Collection,
                Debit = 0,
                Credit = amount,
                Date = payment.Date,
                DueDate = null,
                Description = payment.Description,
                ReferenceType = "SalePayment",
                ReferenceId = payment.Id,
                CreatedAtUtc = now,
            });
        }

        sale.PaidAmount += amount;
    }
}

/// <summary>
/// Satış iptali (muhasebe.md bölüm 23). Onaylı belgeyi ters hareketlerle denkleştirir:
/// stok geri eklenir, cari borç ve tahsilatların cari alacağı ters işaretle kapanır,
/// kasa hareketleri iade edilir. Kayıtlar silinmez; Cancelled terminal durumdur.
/// Tek SaveChanges = tek transaction.
/// </summary>
public sealed class CancelSaleHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<SaleResponse> HandleAsync(Guid saleId, CancelSaleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);

        var sale = await SaleQueries.FindSaleAsync(db, tenantId, saleId, cancellationToken)
            ?? throw new NotFoundException("Satış bulunamadı.");

        if (sale.Status == SaleStatus.Draft)
        {
            throw new ConflictException("Taslak satış iptal edilmez; silinebilir.");
        }

        if (sale.Status == SaleStatus.Cancelled)
        {
            throw new ConflictException("Satış zaten iptal edilmiş.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cancelRef = $"Satış iptali {sale.Number}";

        // 1) Stok geri ekleme (satış hareketinin tersi; hizmet kalemleri hariç).
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var serviceIds = await db.Products
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id) && p.IsService)
            .Select(p => p.Id)
            .ToHashSetAsync(cancellationToken);

        foreach (var item in sale.Items.Where(i => !serviceIds.Contains(i.ProductId)))
        {
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                ProductId = item.ProductId,
                WarehouseId = sale.WarehouseId,
                Type = InventoryTransactionType.Sale,
                Quantity = item.Quantity,
                Date = now,
                Description = cancelRef,
                ReferenceType = "SaleCancel",
                ReferenceId = sale.Id,
                CreatedAtUtc = now,
            });
        }

        // 2) Cari borcun tersine çevrilmesi (müşterili satışta).
        if (sale.PartyId is { } partyId)
        {
            db.PartyTransactions.Add(new PartyTransaction
            {
                TenantId = tenantId,
                PartyId = partyId,
                Type = PartyTransactionType.Sale,
                Debit = 0,
                Credit = sale.Total,
                Date = now,
                DueDate = null,
                Description = cancelRef,
                ReferenceType = "SaleCancel",
                ReferenceId = sale.Id,
                CreatedAtUtc = now,
            });
        }

        // 3) Ödemelerin tersine çevrilmesi: kasa hareketi ve tahsilatın cari
        // alacağı da ters işaretle döner (bölüm 23 — her hareket kendi tersini
        // alır; böylece rapor toplamları otomatik sıfırlanır).
        foreach (var payment in sale.Payments)
        {
            db.AccountTransactions.Add(new AccountTransaction
            {
                TenantId = tenantId,
                AccountId = payment.AccountId,
                Type = AccountTransactionType.SaleCollection,
                Amount = -payment.Amount,
                Date = now,
                Description = $"{cancelRef} — ödeme iadesi",
                ReferenceType = "SaleCancel",
                ReferenceId = sale.Id,
                CreatedAtUtc = now,
            });

            if (sale.PartyId is { } paymentPartyId)
            {
                db.PartyTransactions.Add(new PartyTransaction
                {
                    TenantId = tenantId,
                    PartyId = paymentPartyId,
                    Type = PartyTransactionType.Collection,
                    Debit = payment.Amount,
                    Credit = 0,
                    Date = now,
                    DueDate = null,
                    Description = $"{cancelRef} — tahsilat iadesi",
                    ReferenceType = "SaleCancel",
                    ReferenceId = sale.Id,
                    CreatedAtUtc = now,
                });
            }
        }

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledAtUtc = now;
        sale.CancelReason = request.Reason.NullIfEmpty();
        sale.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return await SaleQueries.MaterializeAsync(db, tenantId, sale, cancellationToken);
    }
}

/// <summary>Sonradan tahsilat ekleme — kalan borcu düşürür, durumu günceller.</summary>
public sealed class AddSalePaymentHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<SaleResponse> HandleAsync(Guid saleId, AddSalePaymentRequest request, CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);

        var sale = await SaleQueries.FindSaleAsync(db, tenantId, saleId, cancellationToken)
            ?? throw new NotFoundException("Satış bulunamadı.");

        if (sale.Status is not (SaleStatus.Confirmed or SaleStatus.PartiallyPaid))
        {
            throw new ConflictException("Tahsilat yalnızca onaylanmış ve iptal edilmemiş satışa girilebilir.");
        }

        var due = sale.Total - sale.PaidAmount;
        if (request.Amount <= 0 || request.Amount > due)
        {
            throw new AppException($"Tahsilat tutarı 0'dan büyük ve kalan borcu ({due}) aşmayan bir değer olmalı.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await ConfirmSaleHandler.AddPaymentAsync(
            db, tenantId, sale, request.Date, request.Amount, request.Description,
            paidOnConfirm: false, timeProvider, now, cancellationToken);

        sale.Status = sale.PaidAmount == sale.Total ? SaleStatus.Paid : SaleStatus.PartiallyPaid;
        sale.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return await SaleQueries.MaterializeAsync(db, tenantId, sale, cancellationToken);
    }
}
