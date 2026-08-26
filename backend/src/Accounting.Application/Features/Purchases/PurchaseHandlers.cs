using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Sales;
using Accounting.Contracts;
using Accounting.Contracts.Purchases;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Purchases;

/// <summary>Alış özelliğinin paylaşılan sorgu yardımcıları.</summary>
internal static class PurchaseQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
        ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");

    /// <summary>
    /// Sonraki belge numarası (P-000001...). Tenant içinde benzersizlik DB
    /// unique index ile korunur; eşzamanlı kayıtta index ihlali oluşur ve
    /// istemciye çakışma olarak döner.
    /// </summary>
    public static async Task<string> NextNumberAsync(
        IApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await db.Purchases.CountAsync(p => p.TenantId == tenantId, cancellationToken);
        return FormattableString.Invariant($"P-{count + 1:D6}");
    }

    public static Task<Purchase?> FindPurchaseAsync(
        IApplicationDbContext db, Guid tenantId, Guid purchaseId, CancellationToken cancellationToken) =>
        db.Purchases
            .Include(p => p.Items)
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == purchaseId, cancellationToken);

    /// <summary>Belge satırlarını okunabilir yanıta çevirir (cari/kasa adları çözülür).</summary>
    public static async Task<PurchaseResponse> MaterializeAsync(
        IApplicationDbContext db, Guid tenantId, Purchase purchase, CancellationToken cancellationToken)
    {
        var partyName = purchase.PartyId is null
            ? null
            : await db.Parties.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.Id == purchase.PartyId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var accountIds = purchase.Payments.Select(p => p.AccountId).Distinct().ToList();
        var accountNames = accountIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Accounts.AsNoTracking()
                .Where(a => a.TenantId == tenantId && accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var warehouseName = await db.Warehouses.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == purchase.WarehouseId)
            .Select(w => w.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        var items = purchase.Items
            .OrderBy(i => i.CreatedAtUtc).ThenBy(i => i.Id)
            .Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.DiscountRate,
                i.NetAmount, i.VatRate, i.VatAmount, i.LineTotal))
            .ToList();

        var payments = purchase.Payments
            .OrderBy(p => p.Date).ThenBy(p => p.Id)
            .Select(p => new PurchasePaymentDto(
                p.Id, p.AccountId, accountNames.TryGetValue(p.AccountId, out var name) ? name : "",
                p.Date, p.Amount, p.Description, p.PaidOnConfirm, p.CreatedAtUtc))
            .ToList();

        return new PurchaseResponse(
            purchase.Id, purchase.Number, purchase.PartyId, partyName, purchase.WarehouseId, warehouseName,
            purchase.Date, purchase.DueDate, purchase.Description,
            purchase.SubTotal, purchase.DiscountTotal, purchase.VatTotal, purchase.Total, purchase.PaidAmount,
            purchase.Total - purchase.PaidAmount, purchase.Status.ToString(),
            purchase.ConfirmedAtUtc, purchase.CancelledAtUtc, purchase.CancelReason, purchase.CreatedAtUtc,
            items, payments);
    }

    /// <summary>Talep kalemlerini çözümleyip belge satırlarına çevirir; toplamları hesaplar.</summary>
    public static async Task<List<PurchaseItem>> BuildItemsAsync(
        IApplicationDbContext db,
        Guid tenantId,
        IReadOnlyList<PurchaseItemRequest> requests,
        CancellationToken cancellationToken)
    {
        var productIds = requests.Select(r => r.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var items = new List<PurchaseItem>(requests.Count);
        foreach (var request in requests)
        {
            if (!products.TryGetValue(request.ProductId, out var product) || !product.IsActive)
            {
                throw new AppException("Alınamayan (pasif/yok) ürün içeren kalem var.");
            }

            var (net, vat) = LineMath.Line(request.Quantity, request.UnitPrice, request.DiscountRate, request.VatRate);
            items.Add(new PurchaseItem
            {
                TenantId = tenantId,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                DiscountRate = request.DiscountRate,
                NetAmount = net,
                VatRate = request.VatRate,
                VatAmount = vat,
                LineTotal = net + vat,
            });
        }

        return items;
    }

    /// <summary>Belge baş toplamlarını kalemlerden türetir ve Purchase'a yazar.</summary>
    public static void ApplyTotals(Purchase purchase, List<PurchaseItem> items)
    {
        purchase.Items = items;
        purchase.SubTotal = items.Sum(i => i.NetAmount);
        purchase.DiscountTotal = items.Sum(i => decimal.Round(i.Quantity * i.UnitPrice, 2) - i.NetAmount);
        purchase.VatTotal = items.Sum(i => i.VatAmount);
        purchase.Total = purchase.SubTotal + purchase.VatTotal;
    }
}

/// <summary>Yeni alış belgesi — Draft olarak oluşur, stok/cari etkisi yoktur.</summary>
public sealed class CreatePurchaseHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider,
    IFeatureGuard featureGuard)
{
    public async Task<PurchaseResponse> HandleAsync(CreatePurchaseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);

        // Alış modülü plana bağlı: Başlangıç planında kapalı.
        await featureGuard.EnsureFeatureAsync(tenantId, PlanFeatures.Purchases, cancellationToken);

        var partyId = await ResolvePartyAsync(db, tenantId, request.PartyId, cancellationToken);
        var warehouseId = await CreateSaleHandler.ResolveWarehouseAsync(db, tenantId, request.WarehouseId, timeProvider, cancellationToken);
        var items = await PurchaseQueries.BuildItemsAsync(db, tenantId, request.Items, cancellationToken);

        var purchase = new Purchase
        {
            TenantId = tenantId,
            Number = await PurchaseQueries.NextNumberAsync(db, tenantId, cancellationToken),
            PartyId = partyId,
            WarehouseId = warehouseId,
            Date = Dates.ToUtcDate(request.Date),
            DueDate = request.DueDate is { } due ? Dates.ToUtcDate(due) : null,
            Description = request.Description.NullIfEmpty(),
            Status = PurchaseStatus.Draft,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        PurchaseQueries.ApplyTotals(purchase, items);

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);

        return await PurchaseQueries.MaterializeAsync(db, tenantId, purchase, cancellationToken);
    }

    /// <summary>Tedarikçi verildiyse tenant'a ait ve alış yapılabilecek türde olmalı.</summary>
    internal static async Task<Guid?> ResolvePartyAsync(
        IApplicationDbContext db, Guid tenantId, Guid? partyId, CancellationToken cancellationToken)
    {
        if (partyId is not { } id)
        {
            return null;
        }

        var party = await db.Parties
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .Select(p => new { p.Id, p.Type, p.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("Tedarikçi bulunamadı.");

        if (party.Type == PartyType.Customer)
        {
            throw new AppException("Müşteri cariden alış yapılamaz; tedarikçi seçin.");
        }

        if (!party.IsActive)
        {
            throw new AppException("Pasif tedarikçiden alış yapılamaz.");
        }

        return party.Id;
    }
}

/// <summary>Taslak düzenleme — yalnız Draft durumunda; kalemler yeniden yazılır.</summary>
public sealed class UpdatePurchaseHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider,
    IFeatureGuard featureGuard)
{
    public async Task<PurchaseResponse> HandleAsync(Guid purchaseId, UpdatePurchaseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);

        await featureGuard.EnsureFeatureAsync(tenantId, PlanFeatures.Purchases, cancellationToken);

        var purchase = await PurchaseQueries.FindPurchaseAsync(db, tenantId, purchaseId, cancellationToken)
            ?? throw new NotFoundException("Alış bulunamadı.");

        if (purchase.Status != PurchaseStatus.Draft)
        {
            throw new ConflictException(
                "Onaylanmış alış değiştirilemez; düzeltme için belgeyi iptal edip yeniden oluşturun.");
        }

        purchase.PartyId = await CreatePurchaseHandler.ResolvePartyAsync(db, tenantId, request.PartyId, cancellationToken);
        purchase.WarehouseId = await CreateSaleHandler.ResolveWarehouseAsync(db, tenantId, request.WarehouseId, timeProvider, cancellationToken);
        purchase.Date = Dates.ToUtcDate(request.Date);
        purchase.DueDate = request.DueDate is { } due ? Dates.ToUtcDate(due) : null;
        purchase.Description = request.Description.NullIfEmpty();
        purchase.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        var items = await PurchaseQueries.BuildItemsAsync(db, tenantId, request.Items, cancellationToken);
        if (purchase.Items.Count > 0)
        {
            // Draft kalemleri finansal kayıt değildir; yeniden yazım güvenlidir.
            db.PurchaseItems.RemoveRange(purchase.Items);
        }

        PurchaseQueries.ApplyTotals(purchase, items);
        await db.SaveChangesAsync(cancellationToken);

        return await PurchaseQueries.MaterializeAsync(db, tenantId, purchase, cancellationToken);
    }
}

/// <summary>Taslak silme. Onaylı belge silinemez — iptal edilir.</summary>
public sealed class DeletePurchaseHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task HandleAsync(Guid purchaseId, CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);

        var purchase = await PurchaseQueries.FindPurchaseAsync(db, tenantId, purchaseId, cancellationToken)
            ?? throw new NotFoundException("Alış bulunamadı.");

        if (purchase.Status != PurchaseStatus.Draft)
        {
            throw new ConflictException("Onaylanmış alış silinemez; belgeyi iptal edin.");
        }

        // Interceptor fiziksel DELETE'i soft-delete'e çevirir.
        db.Purchases.Remove(purchase);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Alış detayı.</summary>
public sealed class GetPurchaseHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<PurchaseResponse> HandleAsync(Guid purchaseId, CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);

        var purchase = await PurchaseQueries.FindPurchaseAsync(db, tenantId, purchaseId, cancellationToken)
            ?? throw new NotFoundException("Alış bulunamadı.");

        return await PurchaseQueries.MaterializeAsync(db, tenantId, purchase, cancellationToken);
    }
}

/// <summary>Alış listesi: durum, tedarikçi ve numara araması; en yeni önce.</summary>
public sealed class ListPurchasesHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<PurchaseSummaryDto>> HandleAsync(
        string? status,
        Guid? partyId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var tenantId = PurchaseQueries.RequireTenantId(currentTenant);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize);

        var query =
            from p in db.Purchases.AsNoTracking().Where(p => p.TenantId == tenantId)
            join party in db.Parties.AsNoTracking() on p.PartyId equals party.Id into partyGroup
            from party in partyGroup.DefaultIfEmpty()
            select new { Purchase = p, PartyName = party != null ? party.Name : null };

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = Enum.TryParse<PurchaseStatus>(status, true, out var statusFilter)
                ? statusFilter
                : throw new AppException("Geçersiz alış durumu.");
            query = query.Where(row => row.Purchase.Status == parsed);
        }

        if (partyId is { } supplierId)
        {
            query = query.Where(row => row.Purchase.PartyId == supplierId);
        }

        var term = search?.Trim().ToLowerInvariant() ?? string.Empty;
        if (term.Length > 0)
        {
            // string.ToLower() bilinçli: EF Core SQL LOWER()'a çevirir, kültür
            // overload'u çevrilemediği için kullanılamaz (cari/ürün listesiyle aynı).
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(row => row.Purchase.Number.ToLower().Contains(term)
                || (row.PartyName != null && row.PartyName.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(row => row.Purchase.Date)
            .ThenByDescending(row => row.Purchase.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new PurchaseSummaryDto(
                row.Purchase.Id,
                row.Purchase.Number,
                row.Purchase.Date,
                row.Purchase.PartyId,
                row.PartyName,
                row.Purchase.Items.Count,
                row.Purchase.Total,
                row.Purchase.PaidAmount,
                row.Purchase.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PurchaseSummaryDto>(items, page, pageSize, totalCount);
    }
}
