using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts;
using Accounting.Contracts.Products;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Products;

/// <summary>
/// Manuel stok hareketi (muhasebe.md bölüm 5.2). Sayım, manuel giriş/çıkış ve
/// iade kullanıcı tarafından girilir; Alış/Satış hareketleri ilgili modüllerin
/// onay akışından, Transfer ayrı uç noktadan üretilir.
/// </summary>
public sealed class CreateInventoryTransactionHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider)
{
    private static readonly HashSet<InventoryTransactionType> ManualTypes =
    [
        InventoryTransactionType.Count,
        InventoryTransactionType.ManualIn,
        InventoryTransactionType.ManualOut,
        InventoryTransactionType.Return,
    ];

    public async Task<InventoryTransactionDto> HandleAsync(
        CreateInventoryTransactionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var product = await UpdateProductHandler.FindProductAsync(db, tenantId, request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Ürün bulunamadı.");

        if (product.IsService)
        {
            throw new ConflictException("Hizmetlere stok hareketi girilemez.");
        }

        if (!product.IsActive)
        {
            throw new ConflictException("Pasif ürüne stok hareketi girilemez. Önce ürünü aktifleştirin.");
        }

        var warehouse = await FindActiveWarehouseAsync(db, tenantId, request.WarehouseId, cancellationToken);

        var type = ParseType(request.Type);
        if (!ManualTypes.Contains(type))
        {
            throw new AppException(
                "Bu hareket türü manuel girilemez; alış/satış hareketleri ilgili modüllerin onayından, " +
                "transfer için /inventory/transfers uç noktasını kullanın. Manuel türler: Count, ManualIn, ManualOut, Return.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var quantity = request.Quantity;

        // Sayım: girilen mutlak miktar ile güncel stok arasındaki fark yazılır.
        if (type == InventoryTransactionType.Count)
        {
            var current = await WarehouseStockAsync(db, tenantId, request.ProductId, request.WarehouseId, cancellationToken);
            quantity = decimal.Round(request.Quantity, 4) - current;
            if (quantity == 0)
            {
                throw new ConflictException(
                    $"Sayım sonucu ({request.Quantity}) güncel stokla ({current}) aynı; fark hareketi oluşmadı.");
            }
        }
        else if (type is InventoryTransactionType.ManualIn or InventoryTransactionType.Return && quantity <= 0)
        {
            throw new AppException("Giriş hareketlerinin miktarı pozitif olmalı.");
        }
        else if (type == InventoryTransactionType.ManualOut && quantity >= 0)
        {
            throw new AppException("Manuel çıkış hareketinin miktarı negatif olmalı (örn. -5).");
        }

        var transaction = new InventoryTransaction
        {
            TenantId = tenantId,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            Type = type,
            Quantity = quantity,
            Date = Dates.ToUtcDate(request.Date),
            Description = request.Description.NullIfEmpty(),
            ReferenceType = null,
            ReferenceId = null,
            CreatedAtUtc = now,
        };
        db.InventoryTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        return transaction.ToDto(product.Name, warehouse.Name);
    }

    internal static async Task<Warehouse> FindActiveWarehouseAsync(
        IApplicationDbContext db, Guid tenantId, Guid warehouseId, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException("Depo bulunamadı.");

        if (!warehouse.IsActive)
        {
            throw new ConflictException("Pasif depoya stok hareketi girilemez.");
        }

        return warehouse;
    }

    internal static Task<decimal> WarehouseStockAsync(
        IApplicationDbContext db, Guid tenantId, Guid productId, Guid warehouseId, CancellationToken cancellationToken) =>
        db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProductId == productId && t.WarehouseId == warehouseId)
            .Select(t => t.Quantity)
            .SumAsync(cancellationToken);

    private static InventoryTransactionType ParseType(string value) =>
        Enum.TryParse<InventoryTransactionType>(value, ignoreCase: false, out var type)
            ? type
            : throw new AppException("Hareket türü geçersiz. Geçerli değerler: Count, ManualIn, ManualOut, Return.");
}

/// <summary>
/// Depolar arası transfer: kaynak depodan çıkış + hedef depoya giriş olmak
/// üzere iki hareket tek SaveChanges'ta atomik yazılır.
/// </summary>
public sealed class CreateInventoryTransferHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InventoryTransactionDto>> HandleAsync(
        CreateInventoryTransferRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        if (request.SourceWarehouseId == request.TargetWarehouseId)
        {
            throw new AppException("Kaynak ve hedef depo aynı olamaz.");
        }

        var product = await UpdateProductHandler.FindProductAsync(db, tenantId, request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Ürün bulunamadı.");

        if (product.IsService)
        {
            throw new ConflictException("Hizmetlere stok hareketi girilemez.");
        }

        if (!product.IsActive)
        {
            throw new ConflictException("Pasif ürüne stok hareketi girilemez.");
        }

        var source = await CreateInventoryTransactionHandler.FindActiveWarehouseAsync(
            db, tenantId, request.SourceWarehouseId, cancellationToken);
        var target = await CreateInventoryTransactionHandler.FindActiveWarehouseAsync(
            db, tenantId, request.TargetWarehouseId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var referenceId = Guid.CreateVersion7();

        InventoryTransaction ExitRow() => new()
        {
            TenantId = tenantId,
            ProductId = request.ProductId,
            WarehouseId = request.SourceWarehouseId,
            Type = InventoryTransactionType.Transfer,
            Quantity = -request.Quantity,
            Date = Dates.ToUtcDate(request.Date),
            Description = request.Description.NullIfEmpty(),
            ReferenceType = "Transfer",
            ReferenceId = referenceId,
            CreatedAtUtc = now,
        };

        InventoryTransaction EntryRow() => new()
        {
            TenantId = tenantId,
            ProductId = request.ProductId,
            WarehouseId = request.TargetWarehouseId,
            Type = InventoryTransactionType.Transfer,
            Quantity = request.Quantity,
            Date = Dates.ToUtcDate(request.Date),
            Description = request.Description.NullIfEmpty(),
            ReferenceType = "Transfer",
            ReferenceId = referenceId,
            CreatedAtUtc = now,
        };

        db.InventoryTransactions.AddRange(ExitRow(), EntryRow());
        await db.SaveChangesAsync(cancellationToken);

        // Oluşan çifti kaynak sırasıyla döndür.
        var rows = await db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ReferenceId == referenceId)
            .OrderBy(t => t.Quantity)
            .ToListAsync(cancellationToken);

        return rows.Select(t => t.ToDto(product.Name, t.WarehouseId == source.Id ? source.Name : target.Name)).ToList();
    }
}

/// <summary>Ürünün stok hareket geçmişi — en yeni önce, depo filtresi seçmeli.</summary>
public sealed class ListInventoryTransactionsHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 200;

    public async Task<PagedResponse<InventoryTransactionDto>> HandleAsync(
        Guid productId,
        Guid? warehouseId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var exists = await db.Products.AnyAsync(p => p.TenantId == tenantId && p.Id == productId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Ürün bulunamadı.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);

        var query = db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProductId == productId);

        if (warehouseId is { } warehouse)
        {
            query = query.Where(t => t.WarehouseId == warehouse);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.ProductId,
                ProductName = t.Product.Name,
                t.WarehouseId,
                WarehouseName = t.Warehouse.Name,
                t.Type,
                t.Date,
                t.Quantity,
                t.Description,
                t.ReferenceType,
                t.ReferenceId,
                t.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var dtos = items.Select(t => new InventoryTransactionDto(
                t.Id, t.ProductId, t.ProductName, t.WarehouseId, t.WarehouseName,
                t.Type.ToString(), t.Date, t.Quantity, t.Description, t.ReferenceType, t.ReferenceId, t.CreatedAtUtc))
            .ToList();

        return new PagedResponse<InventoryTransactionDto>(dtos, page, pageSize, totalCount);
    }
}

internal static class InventoryMappingExtensions
{
    public static InventoryTransactionDto ToDto(this InventoryTransaction transaction, string productName, string warehouseName) =>
        new(
            transaction.Id,
            transaction.ProductId,
            productName,
            transaction.WarehouseId,
            warehouseName,
            transaction.Type.ToString(),
            transaction.Date,
            transaction.Quantity,
            transaction.Description,
            transaction.ReferenceType,
            transaction.ReferenceId,
            transaction.CreatedAtUtc);
}
