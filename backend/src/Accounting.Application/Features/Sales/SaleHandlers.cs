using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts;
using Accounting.Contracts.Sales;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Sales;

/// <summary>Satış kalemi hesaplamaları — yuvarlama kuralı tek yerde tanımlanır.</summary>
internal static class SaleMath
{
    /// <summary>
    /// Kalem tutarları: brüt = Round(miktar × fiyat, 2); net = Round(brüt × (1 − iskonto), 2);
    /// KDV = Round(net × oran, 2). Tutarlar numeric(18,2).
    /// </summary>
    public static (decimal Net, decimal Vat) Line(
        decimal quantity, decimal unitPrice, decimal discountRate, decimal vatRate)
    {
        var gross = decimal.Round(quantity * unitPrice, 2);
        var net = decimal.Round(gross * (1 - discountRate / 100m), 2);
        var vat = decimal.Round(net * vatRate / 100m, 2);
        return (net, vat);
    }
}

/// <summary>Satış özelliğinin paylaşılan sorgu yardımcıları.</summary>
internal static class SaleQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
        ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");

    /// <summary>
    /// Sonraki belge numarası (S-000001...). Tenant içinde benzersizlik DB
    /// unique index ile korunur; eşzamanlı kayıtta index ihlali oluşur ve
    /// istemciye çakışma olarak döner.
    /// </summary>
    public static async Task<string> NextNumberAsync(
        IApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await db.Sales.CountAsync(s => s.TenantId == tenantId, cancellationToken);
        return FormattableString.Invariant($"S-{count + 1:D6}");
    }

    public static Task<Sale?> FindSaleAsync(
        IApplicationDbContext db, Guid tenantId, Guid saleId, CancellationToken cancellationToken) =>
        db.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == saleId, cancellationToken);

    /// <summary>Belge satırlarını okunabilir yanıta çevirir (cari/kasa adları çözülür).</summary>
    public static async Task<SaleResponse> MaterializeAsync(
        IApplicationDbContext db, Guid tenantId, Sale sale, CancellationToken cancellationToken)
    {
        var partyName = sale.PartyId is null
            ? null
            : await db.Parties.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.Id == sale.PartyId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var accountIds = sale.Payments.Select(p => p.AccountId).Distinct().ToList();
        var accountNames = accountIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Accounts.AsNoTracking()
                .Where(a => a.TenantId == tenantId && accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var warehouseName = await db.Warehouses.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == sale.WarehouseId)
            .Select(w => w.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        var items = sale.Items
            .OrderBy(i => i.CreatedAtUtc).ThenBy(i => i.Id)
            .Select(i => new SaleItemDto(
                i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.DiscountRate,
                i.NetAmount, i.VatRate, i.VatAmount, i.LineTotal))
            .ToList();

        var payments = sale.Payments
            .OrderBy(p => p.Date).ThenBy(p => p.Id)
            .Select(p => new SalePaymentDto(
                p.Id, p.AccountId, accountNames.TryGetValue(p.AccountId, out var name) ? name : "",
                p.Date, p.Amount, p.Description, p.PaidOnConfirm, p.CreatedAtUtc))
            .ToList();

        return new SaleResponse(
            sale.Id, sale.Number, sale.PartyId, partyName, sale.WarehouseId, warehouseName,
            sale.Date, sale.DueDate, sale.Description,
            sale.SubTotal, sale.DiscountTotal, sale.VatTotal, sale.Total, sale.PaidAmount,
            sale.Total - sale.PaidAmount, sale.Status.ToString(),
            sale.ConfirmedAtUtc, sale.CancelledAtUtc, sale.CancelReason, sale.CreatedAtUtc,
            items, payments);
    }

    /// <summary>Talep kalemlerini çözümleyip belge satırlarına çevirir; toplamları hesaplar.</summary>
    public static async Task<List<SaleItem>> BuildItemsAsync(
        IApplicationDbContext db,
        Guid tenantId,
        IReadOnlyList<Contracts.Sales.SaleItemRequest> requests,
        CancellationToken cancellationToken)
    {
        var productIds = requests.Select(r => r.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var items = new List<SaleItem>(requests.Count);
        foreach (var request in requests)
        {
            if (!products.TryGetValue(request.ProductId, out var product) || !product.IsActive)
            {
                throw new AppException("Satılamayan (pasif/yok) ürün içeren kalem var.");
            }

            var (net, vat) = SaleMath.Line(request.Quantity, request.UnitPrice, request.DiscountRate, request.VatRate);
            items.Add(new SaleItem
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

    /// <summary>Belge baş toplamlarını kalemlerden türetir ve Sale'e yazar.</summary>
    public static void ApplyTotals(Sale sale, List<SaleItem> items)
    {
        sale.Items = items;
        sale.SubTotal = items.Sum(i => i.NetAmount);
        sale.DiscountTotal = items.Sum(i => decimal.Round(i.Quantity * i.UnitPrice, 2) - i.NetAmount);
        sale.VatTotal = items.Sum(i => i.VatAmount);
        sale.Total = sale.SubTotal + sale.VatTotal;
    }
}

/// <summary>Yeni satış belgesi — Draft olarak oluşur, stok/cari etkisi yoktur.</summary>
public sealed class CreateSaleHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<SaleResponse> HandleAsync(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);

        var partyId = await ResolvePartyAsync(db, tenantId, request.PartyId, cancellationToken);
        var warehouseId = await ResolveWarehouseAsync(db, tenantId, request.WarehouseId, timeProvider, cancellationToken);
        var items = await SaleQueries.BuildItemsAsync(db, tenantId, request.Items, cancellationToken);

        var sale = new Sale
        {
            TenantId = tenantId,
            Number = await SaleQueries.NextNumberAsync(db, tenantId, cancellationToken),
            PartyId = partyId,
            WarehouseId = warehouseId,
            Date = Dates.ToUtcDate(request.Date),
            DueDate = request.DueDate is { } due ? Dates.ToUtcDate(due) : null,
            Description = request.Description.NullIfEmpty(),
            Status = SaleStatus.Draft,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        SaleQueries.ApplyTotals(sale, items);

        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);

        return await SaleQueries.MaterializeAsync(db, tenantId, sale, cancellationToken);
    }

    /// <summary>
    /// Belgenin deposunu çözer: verilmediyse varsayılan depo (gerekirse "Ana Depo"
    /// oluşturulur — ürün modülündeki desenle aynı). Depo pasifse reddedilir.
    /// </summary>
    internal static async Task<Guid> ResolveWarehouseAsync(
        IApplicationDbContext db, Guid tenantId, Guid? warehouseId, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (warehouseId is null)
        {
            await Accounting.Application.Features.Products.ListWarehousesHandler
                .EnsureDefaultWarehouseAsync(db, tenantId, timeProvider, cancellationToken);
            warehouseId = await db.Warehouses
                .Where(w => w.TenantId == tenantId && w.IsDefault)
                .Select(w => w.Id)
                .FirstAsync(cancellationToken);
        }

        var warehouse = await db.Warehouses
            .Where(w => w.TenantId == tenantId && w.Id == warehouseId)
            .Select(w => new { w.Id, w.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("Depo bulunamadı.");

        if (!warehouse.IsActive)
        {
            throw new AppException("Pasif depodan satış yapılamaz.");
        }

        return warehouse.Id;
    }

    /// <summary>Müşteri verildiyse tenant'a ait ve satılabilir türde olmalı.</summary>
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
            ?? throw new AppException("Müşteri bulunamadı.");

        if (party.Type == PartyType.Supplier)
        {
            throw new AppException("Tedarikçi carine satış yapılamaz; müşteri seçin.");
        }

        if (!party.IsActive)
        {
            throw new AppException("Pasif müşteriye satış yapılamaz.");
        }

        return party.Id;
    }
}

/// <summary>Taslak düzenleme — yalnız Draft durumunda; kalemler yeniden yazılır.</summary>
public sealed class UpdateSaleHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<SaleResponse> HandleAsync(Guid saleId, UpdateSaleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);

        var sale = await SaleQueries.FindSaleAsync(db, tenantId, saleId, cancellationToken)
            ?? throw new NotFoundException("Satış bulunamadı.");

        if (sale.Status != SaleStatus.Draft)
        {
            throw new ConflictException(
                "Onaylanmış satış değiştirilemez; düzeltme için belgeyi iptal edip yeniden oluşturun.");
        }

        sale.PartyId = await CreateSaleHandler.ResolvePartyAsync(db, tenantId, request.PartyId, cancellationToken);
        sale.WarehouseId = await CreateSaleHandler.ResolveWarehouseAsync(db, tenantId, request.WarehouseId, timeProvider, cancellationToken);
        sale.Date = Dates.ToUtcDate(request.Date);
        sale.DueDate = request.DueDate is { } due ? Dates.ToUtcDate(due) : null;
        sale.Description = request.Description.NullIfEmpty();
        sale.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        var items = await SaleQueries.BuildItemsAsync(db, tenantId, request.Items, cancellationToken);
        if (sale.Items.Count > 0)
        {
            // Draft kalemleri finansal kayıt değildir; yeniden yazım güvenlidir.
            db.SaleItems.RemoveRange(sale.Items);
        }

        SaleQueries.ApplyTotals(sale, items);
        await db.SaveChangesAsync(cancellationToken);

        return await SaleQueries.MaterializeAsync(db, tenantId, sale, cancellationToken);
    }
}

/// <summary>Taslak silme. Onaylı belge silinemez — iptal edilir (muhasebe.md bölüm 23).</summary>
public sealed class DeleteSaleHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task HandleAsync(Guid saleId, CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);

        var sale = await SaleQueries.FindSaleAsync(db, tenantId, saleId, cancellationToken)
            ?? throw new NotFoundException("Satış bulunamadı.");

        if (sale.Status != SaleStatus.Draft)
        {
            throw new ConflictException("Onaylanmış satış silinemez; belgeyi iptal edin.");
        }

        // Interceptor fiziksel DELETE'i soft-delete'e çevirir.
        db.Sales.Remove(sale);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Satış detayı.</summary>
public sealed class GetSaleHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<SaleResponse> HandleAsync(Guid saleId, CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);

        var sale = await SaleQueries.FindSaleAsync(db, tenantId, saleId, cancellationToken)
            ?? throw new NotFoundException("Satış bulunamadı.");

        return await SaleQueries.MaterializeAsync(db, tenantId, sale, cancellationToken);
    }
}

/// <summary>Satış listesi: durum, müşteri ve numara araması; en yeni önce.</summary>
public sealed class ListSalesHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<SaleSummaryDto>> HandleAsync(
        string? status,
        Guid? partyId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var tenantId = SaleQueries.RequireTenantId(currentTenant);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize);

        var query =
            from s in db.Sales.AsNoTracking().Where(s => s.TenantId == tenantId)
            join p in db.Parties.AsNoTracking() on s.PartyId equals p.Id into partyGroup
            from p in partyGroup.DefaultIfEmpty()
            select new { Sale = s, PartyName = p != null ? p.Name : null };

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = Enum.TryParse<SaleStatus>(status, true, out var statusFilter)
                ? statusFilter
                : throw new AppException("Geçersiz satış durumu.");
            query = query.Where(row => row.Sale.Status == parsed);
        }

        if (partyId is { } party)
        {
            query = query.Where(row => row.Sale.PartyId == party);
        }

        var term = search?.Trim().ToLowerInvariant() ?? string.Empty;
        if (term.Length > 0)
        {
            // string.ToLower() bilinçli: EF Core SQL LOWER()'a çevirir, kültür
            // overload'u çevrilemediği için kullanılamaz (cari/ürün listesiyle aynı).
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(row => row.Sale.Number.ToLower().Contains(term)
                || (row.PartyName != null && row.PartyName.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(row => row.Sale.Date)
            .ThenByDescending(row => row.Sale.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new SaleSummaryDto(
                row.Sale.Id,
                row.Sale.Number,
                row.Sale.Date,
                row.Sale.PartyId,
                row.PartyName,
                row.Sale.Items.Count,
                row.Sale.Total,
                row.Sale.PaidAmount,
                row.Sale.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedResponse<SaleSummaryDto>(items, page, pageSize, totalCount);
    }
}

/// <summary>Kasa yardımcıları — PHASE 4'te default hesap; yönetimi PHASE 6'da açılır.</summary>
internal static class SaleAccounts
{
    public const string DefaultAccountName = "Kasa";

    /// <summary>
    /// Tenant'ın varsayılan kasa hesabını döndürür; yoksa değişiklik izleyicisine
    /// ekler (SaveChanges ÇAĞIRMaz — Id istemci taraflı Guid olduğundan çevreleyen
    /// kayıt onay transaction'ı hesabı da atomik olarak yazar; bölüm 24).
    /// </summary>
    public static async Task<Account> EnsureDefaultAccountAsync(
        IApplicationDbContext db, Guid tenantId, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsDefault, cancellationToken);

        if (account is not null)
        {
            return account;
        }

        account = new Account
        {
            TenantId = tenantId,
            Name = DefaultAccountName,
            IsDefault = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Accounts.Add(account);
        return account;
    }
}

internal static class SaleMappingExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
