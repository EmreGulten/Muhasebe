using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts;
using Accounting.Contracts.Products;
using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Products;

/// <summary>Ürün özelliğinin paylaşılan sorgu yardımcıları.</summary>
internal static class ProductQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
        ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");

    /// <summary>Ürünün toplam stoğu (tüm depolar, işaretli miktar toplamı).</summary>
    public static Task<decimal> TotalStockAsync(
        IApplicationDbContext db, Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
        db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProductId == productId)
            .Select(t => t.Quantity)
            .SumAsync(cancellationToken);

    /// <summary>Kritik stok kuralı: eşik > 0 ve güncel stok eşiğin altında (muhasebe.md 5.3).</summary>
    public static bool IsCritical(decimal minimumStock, decimal currentStock) =>
        minimumStock > 0 && currentStock <= minimumStock;

    /// <summary>Tenant içinde aynı SKU'lu silinmemiş başka ürün var mı? (soft-delete query filter uygular)</summary>
    public static Task<bool> SkuInUseAsync(
        IApplicationDbContext db, Guid tenantId, string sku, Guid? excludeProductId, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(
            p => p.TenantId == tenantId && p.Sku == sku && p.Id != excludeProductId,
            cancellationToken);
}

/// <summary>
/// Yeni ürün/hizmet kartı. SKU tenant içinde benzersizdir; boş bırakılabilir.
/// Ürün oluşturma stok hareketi üretmez — stok sayım/manuel hareketle başlar.
/// </summary>
public sealed class CreateProductHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<ProductResponse> HandleAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);
        var sku = request.Sku.NullIfEmpty();

        if (sku is not null && await ProductQueries.SkuInUseAsync(db, tenantId, sku, null, cancellationToken))
        {
            throw new ConflictException($"'{sku}' stok kodu başka bir üründe kullanılıyor.");
        }

        await EnsureReferencesAsync(db, tenantId, request.CategoryId, request.UnitId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var product = new Product
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Sku = sku,
            Barcode = request.Barcode.NullIfEmpty(),
            Description = request.Description.NullIfEmpty(),
            CategoryId = request.CategoryId,
            UnitId = request.UnitId,
            PurchasePrice = request.PurchasePrice,
            SalePrice = request.SalePrice,
            VatRate = request.VatRate,
            MinimumStock = request.MinimumStock,
            IsService = request.IsService,
            IsActive = true,
            CreatedAtUtc = now,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return await MaterializeAsync(db, tenantId, product, cancellationToken);
    }

    /// <summary>Kategori/birim verildiyse bu işletmeye ait olmalı.</summary>
    internal static async Task EnsureReferencesAsync(
        IApplicationDbContext db, Guid tenantId, Guid? categoryId, Guid? unitId, CancellationToken cancellationToken)
    {
        if (categoryId is { } category && !await db.Categories.AnyAsync(c => c.TenantId == tenantId && c.Id == category, cancellationToken))
        {
            throw new AppException("Seçili kategori bulunamadı.");
        }

        if (unitId is { } unit && !await db.Units.AnyAsync(u => u.TenantId == tenantId && u.Id == unit, cancellationToken))
        {
            throw new AppException("Seçili birim bulunamadı.");
        }
    }

    internal static async Task<ProductResponse> MaterializeAsync(
        IApplicationDbContext db, Guid tenantId, Product product, CancellationToken cancellationToken)
    {
        var categoryName = product.CategoryId is null
            ? null
            : await db.Categories.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.Id == product.CategoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var unitName = product.UnitId is null
            ? null
            : await db.Units.AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.Id == product.UnitId)
                .Select(u => u.Code ?? u.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var stock = await ProductQueries.TotalStockAsync(db, tenantId, product.Id, cancellationToken);

        return product.ToResponse(categoryName, unitName, stock);
    }
}

/// <summary>Ürün güncelleme. Stok hareketleri dokunulmaz; kart alanları güncellenir.</summary>
public sealed class UpdateProductHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<ProductResponse> HandleAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var product = await FindProductAsync(db, tenantId, productId, cancellationToken)
            ?? throw new NotFoundException("Ürün bulunamadı.");

        var sku = request.Sku.NullIfEmpty();
        if (sku is not null && await ProductQueries.SkuInUseAsync(db, tenantId, sku, productId, cancellationToken))
        {
            throw new ConflictException($"'{sku}' stok kodu başka bir üründe kullanılıyor.");
        }

        await CreateProductHandler.EnsureReferencesAsync(db, tenantId, request.CategoryId, request.UnitId, cancellationToken);

        product.Name = request.Name.Trim();
        product.Sku = sku;
        product.Barcode = request.Barcode.NullIfEmpty();
        product.Description = request.Description.NullIfEmpty();
        product.CategoryId = request.CategoryId;
        product.UnitId = request.UnitId;
        product.PurchasePrice = request.PurchasePrice;
        product.SalePrice = request.SalePrice;
        product.VatRate = request.VatRate;
        product.MinimumStock = request.MinimumStock;
        product.IsService = request.IsService;

        // Pasif ürün satış/stok hareketine kapatılır ama geçmişi korunur.
        product.IsActive = request.IsActive;
        product.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);

        return await CreateProductHandler.MaterializeAsync(db, tenantId, product, cancellationToken);
    }

    internal static Task<Product?> FindProductAsync(
        IApplicationDbContext db, Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
        db.Products.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == productId, cancellationToken);
}

/// <summary>
/// Ürün silme. Stok hareketi olan ürün silinemez — finansal/stok kayıt zinciri
/// korunur (muhasebe.md bölüm 23); pasifleştirme önerilir.
/// </summary>
public sealed class DeleteProductHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task HandleAsync(Guid productId, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var product = await UpdateProductHandler.FindProductAsync(db, tenantId, productId, cancellationToken)
            ?? throw new NotFoundException("Ürün bulunamadı.");

        var hasMovements = await db.InventoryTransactions
            .AnyAsync(t => t.TenantId == tenantId && t.ProductId == productId, cancellationToken);

        if (hasMovements)
        {
            throw new ConflictException(
                "Bu ürünün stok hareket geçmişi var; kayıtlar silinemez. " +
                "Ürünü pasifleştirmek için güncellemede IsActive alanını kapatın.");
        }

        // Interceptor fiziksel DELETE'i soft-delete'e çevirir.
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Ürün detayı + stok özeti.</summary>
public sealed class GetProductHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<ProductResponse> HandleAsync(Guid productId, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var product = await UpdateProductHandler.FindProductAsync(db, tenantId, productId, cancellationToken)
            ?? throw new NotFoundException("Ürün bulunamadı.");

        return await CreateProductHandler.MaterializeAsync(db, tenantId, product, cancellationToken);
    }
}

/// <summary>
/// Ürün listesi: arama (ad/stok kodu/barkod), kategori, pasif ve kritik stok
/// filtreleri. Stok, sayfadaki ürünler için tek grup sorgusunda hesaplanır;
/// kritik filtresi sayfalamadan ÖNCE uygulanır.
/// </summary>
public sealed class ListProductsHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<ProductSummaryDto>> HandleAsync(
        string? search,
        Guid? categoryId,
        bool includeInactive,
        bool criticalOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize);

        // Stok toplamları alt sorgusu; LEFT JOIN ile stoğu hiç olmayan ürünlere 0 düşer.
        var stocks = db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(t => t.Quantity) });

        var query =
            from p in db.Products.AsNoTracking().Where(p => p.TenantId == tenantId)
            join s in stocks on p.Id equals s.ProductId into stockGroup
            from s in stockGroup.DefaultIfEmpty()
            // (decimal?)s.Stock ?? 0m → SQL COALESCE: hareketi olmayan ürüne 0 düşer.
            select new { Product = p, Stock = (decimal?)s.Stock ?? 0m };

        if (!includeInactive)
        {
            query = query.Where(row => row.Product.IsActive);
        }

        if (categoryId is { } category)
        {
            query = query.Where(row => row.Product.CategoryId == category);
        }

        var term = search?.Trim().ToLowerInvariant() ?? string.Empty;
        if (term.Length > 0)
        {
            // string.ToLower() bilinçli: EF Core SQL LOWER()'a çevirir, kültür
            // overload'u çevrilemediği için kullanılamaz (cari listesiyle aynı).
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(row =>
                row.Product.Name.ToLower().Contains(term)
                || (row.Product.Sku != null && row.Product.Sku.ToLower().Contains(term))
                || (row.Product.Barcode != null && row.Product.Barcode.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (criticalOnly)
        {
            query = query.Where(row =>
                !row.Product.IsService
                && row.Product.MinimumStock > 0
                && row.Stock <= row.Product.MinimumStock);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(row => row.Product.Name)
            .ThenBy(row => row.Product.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.Product.Id,
                row.Product.Name,
                row.Product.Sku,
                row.Product.Barcode,
                CategoryName = row.Product.Category != null ? row.Product.Category.Name : null,
                UnitName = row.Product.Unit != null ? (row.Product.Unit.Code ?? row.Product.Unit.Name) : null,
                row.Product.SalePrice,
                row.Product.MinimumStock,
                row.Product.IsService,
                row.Product.IsActive,
                row.Stock,
            })
            .ToListAsync(cancellationToken);

        var summaries = items.Select(row => new ProductSummaryDto(
                row.Id, row.Name, row.Sku, row.Barcode, row.CategoryName, row.UnitName,
                row.SalePrice, row.Stock,
                ProductQueries.IsCritical(row.MinimumStock, row.Stock),
                row.IsService, row.IsActive))
            .ToList();

        return new PagedResponse<ProductSummaryDto>(summaries, page, pageSize, totalCount);
    }
}

/// <summary>Depo bazında stok dökümü. Hareketi olmayan aktif depolar 0 ile listelenir.</summary>
public sealed class GetProductStockHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<ProductStockResponse> HandleAsync(Guid productId, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == productId)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ürün bulunamadı.");

        var warehouses = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && !w.IsDeleted && w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new { w.Id, w.Name })
            .ToListAsync(cancellationToken);

        var stocks = await db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProductId == productId)
            .GroupBy(t => t.WarehouseId)
            .Select(g => new { WarehouseId = g.Key, Stock = g.Sum(t => t.Quantity) })
            .ToDictionaryAsync(s => s.WarehouseId, s => s.Stock, cancellationToken);

        // Hareket görmemiş depolar da listede yer alır (stok 0).
        var warehouseIds = warehouses.Select(w => w.Id).ToHashSet();
        foreach (var extra in stocks.Keys.Where(id => !warehouseIds.Contains(id)))
        {
            var missing = await db.Warehouses
                .AsNoTracking()
                .Where(w => w.TenantId == tenantId && w.Id == extra)
                .Select(w => new { w.Id, w.Name })
                .FirstOrDefaultAsync(cancellationToken);
            if (missing is not null)
            {
                warehouses.Add(missing);
            }
        }

        var rows = warehouses
            .Select(w => new WarehouseStockDto(
                w.Id, w.Name, stocks.TryGetValue(w.Id, out var stock) ? stock : 0m))
            .ToList();

        return new ProductStockResponse(product.Id, product.Name, rows.Sum(r => r.Stock), rows);
    }
}

/// <summary>Kritik stok listesi: eşiği olan ve altına inen ürünler.</summary>
public sealed class GetCriticalStockHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<IReadOnlyList<CriticalStockItemDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var stocks = db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(t => t.Quantity) });

        var query =
            from p in db.Products.AsNoTracking().Where(p => p.TenantId == tenantId)
            join s in stocks on p.Id equals s.ProductId into stockGroup
            from s in stockGroup.DefaultIfEmpty()
            let stock = (decimal?)s.Stock ?? 0m
            where !p.IsService && p.MinimumStock > 0 && stock <= p.MinimumStock
            orderby stock - p.MinimumStock, p.Name
            select new CriticalStockItemDto(
                p.Id,
                p.Name,
                p.Sku,
                stock,
                p.MinimumStock,
                p.Unit != null ? (p.Unit.Code ?? p.Unit.Name) : null);

        return await query.ToListAsync(cancellationToken);
    }
}

internal static class ProductMappingExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static ProductResponse ToResponse(
        this Product product, string? categoryName, string? unitName, decimal currentStock) =>
        new(
            product.Id, product.Name, product.Sku, product.Barcode, product.Description,
            product.CategoryId, categoryName, product.UnitId, unitName,
            product.PurchasePrice, product.SalePrice, product.VatRate, product.MinimumStock,
            product.IsService, product.IsActive,
            product.CreatedAtUtc, product.UpdatedAtUtc,
            currentStock, ProductQueries.IsCritical(product.MinimumStock, currentStock));
}
