using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Sales;
using Accounting.Contracts.Purchases;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Purchases;

/// <summary>
/// Alış onayı. Tek SaveChanges — dolayısıyla tek
/// transaction — içinde: stok girişi (hizmet hariç), tedarikçili alışta cari
/// borç (biz borçlanırız — alacak), istenirse anlık ödeme (kasadan çıkış +
/// cari borç düşümü). Onaydan sonra belge değiştirilemez.
/// </summary>
public sealed class ConfirmPurchaseHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider,
    IFeatureGuard featureGuard)
{
    public async Task<PurchaseResponse> HandleAsync(Guid purchaseId, ConfirmPurchaseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);

        // Alış modülü plana bağlı.
        await featureGuard.EnsureFeatureAsync(tenantId, PlanFeatures.Purchases, cancellationToken);

        var purchase = await PurchaseQueries.FindPurchaseAsync(db, tenantId, purchaseId, cancellationToken)
            ?? throw new NotFoundException("Alış bulunamadı.");

        if (purchase.Status != PurchaseStatus.Draft)
        {
            throw new ConflictException("Yalnızca taslak alış onaylanabilir.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var productIds = purchase.Items.Select(i => i.ProductId).Distinct().ToList();
        var serviceIds = await db.Products
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id) && p.IsService)
            .Select(p => p.Id)
            .ToHashSetAsync(cancellationToken);

        // Stok girişi: her mal kalemi için pozitif alış hareketi. Hizmet stoğa girmez.
        foreach (var item in purchase.Items.Where(i => !serviceIds.Contains(i.ProductId)))
        {
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                ProductId = item.ProductId,
                WarehouseId = purchase.WarehouseId,
                Type = InventoryTransactionType.Purchase,
                Quantity = item.Quantity,
                Date = purchase.Date,
                Description = $"Alış {purchase.Number}",
                ReferenceType = "Purchase",
                ReferenceId = purchase.Id,
                CreatedAtUtc = now,
            });
        }

        // Cari borç: tedarikçili alışta belge tutarı kadar borçlanma (alacak).
        if (purchase.PartyId is { } partyId)
        {
            db.PartyTransactions.Add(new PartyTransaction
            {
                TenantId = tenantId,
                PartyId = partyId,
                Type = PartyTransactionType.Purchase,
                Debit = 0,
                Credit = purchase.Total,
                Date = purchase.Date,
                DueDate = purchase.DueDate,
                Description = $"Alış {purchase.Number}",
                ReferenceType = "Purchase",
                ReferenceId = purchase.Id,
                CreatedAtUtc = now,
            });
        }

        // Anlık ödeme: kasadan çıkış + (tedarikçiliyse) cari borç düşümü.
        if (request.Payment is { } payment)
        {
            if (payment.Amount <= 0 || payment.Amount > purchase.Total)
            {
                throw new AppException("Ödeme tutarı 0'dan büyük ve belge toplamını aşmayan bir değer olmalı.");
            }

            await AddPaymentAsync(db, tenantId, purchase, payment.Date, payment.Amount, payment.Description,
                paidOnConfirm: true, timeProvider, now, cancellationToken);
        }

        purchase.ConfirmedAtUtc = now;
        purchase.Status = purchase.PaidAmount == purchase.Total
            ? PurchaseStatus.Paid
            : purchase.PaidAmount > 0
                ? PurchaseStatus.PartiallyPaid
                : PurchaseStatus.Confirmed;
        purchase.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return await PurchaseQueries.MaterializeAsync(db, tenantId, purchase, cancellationToken);
    }

    /// <summary>Ödeme yazımı — onay anı ve sonradan ödeme ortaktır.</summary>
    internal static async Task AddPaymentAsync(
        IApplicationDbContext db,
        Guid tenantId,
        Purchase purchase,
        DateTime date,
        decimal amount,
        string? description,
        bool paidOnConfirm,
        TimeProvider timeProvider,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var account = await SaleAccounts.EnsureDefaultAccountAsync(db, tenantId, timeProvider, cancellationToken);

        var payment = new PurchasePayment
        {
            TenantId = tenantId,
            PurchaseId = purchase.Id,
            AccountId = account.Id,
            Date = Dates.ToUtcDate(date),
            Amount = amount,
            Description = description.NullIfEmpty() ?? $"Ödeme — {purchase.Number}",
            PaidOnConfirm = paidOnConfirm,
            CreatedAtUtc = now,
        };
        db.PurchasePayments.Add(payment);

        // Kasa çıkışı: negatif işaretli tutar.
        db.AccountTransactions.Add(new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = account.Id,
            Type = AccountTransactionType.PurchasePayment,
            Amount = -amount,
            Date = payment.Date,
            Description = payment.Description,
            ReferenceType = "PurchasePayment",
            ReferenceId = payment.Id,
            CreatedAtUtc = now,
        });

        if (purchase.PartyId is { } partyId)
        {
            // Tedarikçiye ödeme cari borcu düşürür (borç hareketi).
            db.PartyTransactions.Add(new PartyTransaction
            {
                TenantId = tenantId,
                PartyId = partyId,
                Type = PartyTransactionType.Payment,
                Debit = amount,
                Credit = 0,
                Date = payment.Date,
                DueDate = null,
                Description = payment.Description,
                ReferenceType = "PurchasePayment",
                ReferenceId = payment.Id,
                CreatedAtUtc = now,
            });
        }

        purchase.PaidAmount += amount;
    }
}

/// <summary>
/// Alış iptali. Onaylı belgeyi ters hareketlerle
/// denkleştirir: stok geri düşülür, tedarikçi borcu ve ödemelerin cari borcu
/// ters işaretle kapanır, kasa çıkışları iade edilir. Kayıtlar silinmez;
/// Cancelled terminal durumdur. Tek SaveChanges = tek transaction.
/// </summary>
public sealed class CancelPurchaseHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<PurchaseResponse> HandleAsync(Guid purchaseId, CancelPurchaseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);

        var purchase = await PurchaseQueries.FindPurchaseAsync(db, tenantId, purchaseId, cancellationToken)
            ?? throw new NotFoundException("Alış bulunamadı.");

        if (purchase.Status == PurchaseStatus.Draft)
        {
            throw new ConflictException("Taslak alış iptal edilmez; silinebilir.");
        }

        if (purchase.Status == PurchaseStatus.Cancelled)
        {
            throw new ConflictException("Alış zaten iptal edilmiş.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cancelRef = $"Alış iptali {purchase.Number}";

        // 1) Stok geri düşme (alış hareketinin tersi; hizmet kalemleri hariç).
        var productIds = purchase.Items.Select(i => i.ProductId).Distinct().ToList();
        var serviceIds = await db.Products
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id) && p.IsService)
            .Select(p => p.Id)
            .ToHashSetAsync(cancellationToken);

        foreach (var item in purchase.Items.Where(i => !serviceIds.Contains(i.ProductId)))
        {
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                ProductId = item.ProductId,
                WarehouseId = purchase.WarehouseId,
                Type = InventoryTransactionType.Purchase,
                Quantity = -item.Quantity,
                Date = now,
                Description = cancelRef,
                ReferenceType = "PurchaseCancel",
                ReferenceId = purchase.Id,
                CreatedAtUtc = now,
            });
        }

        // 2) Cari borcun tersine çevrilmesi (tedarikçili alışta).
        if (purchase.PartyId is { } partyId)
        {
            db.PartyTransactions.Add(new PartyTransaction
            {
                TenantId = tenantId,
                PartyId = partyId,
                Type = PartyTransactionType.Purchase,
                Debit = purchase.Total,
                Credit = 0,
                Date = now,
                DueDate = null,
                Description = cancelRef,
                ReferenceType = "PurchaseCancel",
                ReferenceId = purchase.Id,
                CreatedAtUtc = now,
            });
        }

        // 3) Ödemelerin tersine çevrilmesi: kasa çıkışı geri alınır ve tedarikçi
        // cari borcu da ters işaretle döner. Her hareket kendi tersini aldığı için
        // rapor toplamları otomatik sıfırlanır.
        foreach (var payment in purchase.Payments)
        {
            db.AccountTransactions.Add(new AccountTransaction
            {
                TenantId = tenantId,
                AccountId = payment.AccountId,
                Type = AccountTransactionType.PurchasePayment,
                Amount = payment.Amount,
                Date = now,
                Description = $"{cancelRef} — ödeme iadesi",
                ReferenceType = "PurchaseCancel",
                ReferenceId = purchase.Id,
                CreatedAtUtc = now,
            });

            if (purchase.PartyId is { } paymentPartyId)
            {
                db.PartyTransactions.Add(new PartyTransaction
                {
                    TenantId = tenantId,
                    PartyId = paymentPartyId,
                    Type = PartyTransactionType.Payment,
                    Debit = 0,
                    Credit = payment.Amount,
                    Date = now,
                    DueDate = null,
                    Description = $"{cancelRef} — ödeme iadesi",
                    ReferenceType = "PurchaseCancel",
                    ReferenceId = purchase.Id,
                    CreatedAtUtc = now,
                });
            }
        }

        purchase.Status = PurchaseStatus.Cancelled;
        purchase.CancelledAtUtc = now;
        purchase.CancelReason = request.Reason.NullIfEmpty();
        purchase.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return await PurchaseQueries.MaterializeAsync(db, tenantId, purchase, cancellationToken);
    }
}

/// <summary>Sonradan ödeme ekleme — kalan borcu düşürür, durumu günceller.</summary>
public sealed class AddPurchasePaymentHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider,
    IFeatureGuard featureGuard)
{
    public async Task<PurchaseResponse> HandleAsync(Guid purchaseId, AddPurchasePaymentRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);

        await featureGuard.EnsureFeatureAsync(tenantId, PlanFeatures.Purchases, cancellationToken);

        var purchase = await PurchaseQueries.FindPurchaseAsync(db, tenantId, purchaseId, cancellationToken)
            ?? throw new NotFoundException("Alış bulunamadı.");

        if (purchase.Status is not (PurchaseStatus.Confirmed or PurchaseStatus.PartiallyPaid))
        {
            throw new ConflictException("Ödeme yalnızca onaylanmış ve iptal edilmemiş alışa girilebilir.");
        }

        var due = purchase.Total - purchase.PaidAmount;
        if (request.Amount <= 0 || request.Amount > due)
        {
            throw new AppException($"Ödeme tutarı 0'dan büyük ve kalan borcu ({due}) aşmayan bir değer olmalı.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await ConfirmPurchaseHandler.AddPaymentAsync(
            db, tenantId, purchase, request.Date, request.Amount, request.Description,
            paidOnConfirm: false, timeProvider, now, cancellationToken);

        purchase.Status = purchase.PaidAmount == purchase.Total ? PurchaseStatus.Paid : PurchaseStatus.PartiallyPaid;
        purchase.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return await PurchaseQueries.MaterializeAsync(db, tenantId, purchase, cancellationToken);
    }
}
