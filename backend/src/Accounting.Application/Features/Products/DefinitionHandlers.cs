using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Products;
using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Products;

// ---- Kategori

/// <summary>Kategori listesi — ürün sayılarıyla.</summary>
public sealed class ListCategoriesHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<IReadOnlyList<CategoryDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        return await db.Categories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Products.Count(p => !p.IsDeleted)))
            .ToListAsync(cancellationToken);
    }
}

public sealed class CreateCategoryHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<CategoryDto> HandleAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);
        var name = request.Name.Trim();

        if (await NameInUseAsync(db, tenantId, name, null, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında bir kategori zaten var.");
        }

        var category = new Category
        {
            TenantId = tenantId,
            Name = name,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, 0);
    }

    internal static Task<bool> NameInUseAsync(
        IApplicationDbContext db, Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(
            c => c.TenantId == tenantId && c.Name == name && c.Id != excludeId,
            cancellationToken);
}

public sealed class UpdateCategoryHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<CategoryDto> HandleAsync(Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);
        var name = request.Name.Trim();

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException("Kategori bulunamadı.");

        if (await CreateCategoryHandler.NameInUseAsync(db, tenantId, name, categoryId, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında bir kategori zaten var.");
        }

        category.Name = name;
        category.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);

        var productCount = await db.Products.CountAsync(
            p => p.TenantId == tenantId && p.CategoryId == categoryId && !p.IsDeleted, cancellationToken);

        return new CategoryDto(category.Id, category.Name, productCount);
    }
}

/// <summary>Kullanan ürün varsa kategori silinemez.</summary>
public sealed class DeleteCategoryHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task HandleAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException("Kategori bulunamadı.");

        var inUse = await db.Products.AnyAsync(
            p => p.TenantId == tenantId && p.CategoryId == categoryId && !p.IsDeleted, cancellationToken);
        if (inUse)
        {
            throw new ConflictException("Bu kategoride ürünler var; önce ürünleri başka kategoriye taşıyın.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---- Birim

public sealed class ListUnitsHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<IReadOnlyList<UnitDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        return await db.Units
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(
                u.Id,
                u.Name,
                u.Code,
                u.Products.Count(p => !p.IsDeleted)))
            .ToListAsync(cancellationToken);
    }
}

public sealed class CreateUnitHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<UnitDto> HandleAsync(CreateUnitRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);
        var name = request.Name.Trim();

        if (await NameInUseAsync(db, tenantId, name, null, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında bir birim zaten var.");
        }

        var unit = new Unit
        {
            TenantId = tenantId,
            Name = name,
            Code = request.Code.NullIfEmpty(),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);

        return new UnitDto(unit.Id, unit.Name, unit.Code, 0);
    }

    internal static Task<bool> NameInUseAsync(
        IApplicationDbContext db, Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken) =>
        db.Units.AnyAsync(
            u => u.TenantId == tenantId && u.Name == name && u.Id != excludeId,
            cancellationToken);
}

public sealed class UpdateUnitHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<UnitDto> HandleAsync(Guid unitId, UpdateUnitRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);
        var name = request.Name.Trim();

        var unit = await db.Units
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == unitId, cancellationToken)
            ?? throw new NotFoundException("Birim bulunamadı.");

        if (await CreateUnitHandler.NameInUseAsync(db, tenantId, name, unitId, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında bir birim zaten var.");
        }

        unit.Name = name;
        unit.Code = request.Code.NullIfEmpty();
        unit.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);

        var productCount = await db.Products.CountAsync(
            p => p.TenantId == tenantId && p.UnitId == unitId && !p.IsDeleted, cancellationToken);

        return new UnitDto(unit.Id, unit.Name, unit.Code, productCount);
    }
}

public sealed class DeleteUnitHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task HandleAsync(Guid unitId, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var unit = await db.Units
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == unitId, cancellationToken)
            ?? throw new NotFoundException("Birim bulunamadı.");

        var inUse = await db.Products.AnyAsync(
            p => p.TenantId == tenantId && p.UnitId == unitId && !p.IsDeleted, cancellationToken);
        if (inUse)
        {
            throw new ConflictException("Bu birimi kullanan ürünler var; önce ürünleri başka birime taşıyın.");
        }

        db.Units.Remove(unit);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---- Depo

/// <summary>
/// Depo listesi. İlk çağrıda tenant'ın varsayılan deposu ("Ana Depo") yoksa
/// oluşturulur — MVP tek depoyla başlar (muhasebe.md bölüm 5.2).
/// </summary>
public sealed class ListWarehousesHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public const string DefaultWarehouseName = "Ana Depo";

    public async Task<IReadOnlyList<WarehouseDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        await EnsureDefaultWarehouseAsync(db, tenantId, timeProvider, cancellationToken);

        return await db.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .Select(w => new WarehouseDto(w.Id, w.Name, w.Address, w.IsDefault, w.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Varsayılan depo yoksa oluşturur (idempotent).</summary>
    public static async Task<Warehouse> EnsureDefaultWarehouseAsync(
        IApplicationDbContext db, Guid tenantId, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var existing = await db.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.IsDefault, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var warehouse = new Warehouse
        {
            TenantId = tenantId,
            Name = DefaultWarehouseName,
            IsDefault = true,
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);
        return warehouse;
    }
}

public sealed class CreateWarehouseHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<WarehouseDto> HandleAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        // Tenant'ta hiç depo yoksa önce varsayılanı garanti et: ilk depo her zaman varsayılan.
        var hasAny = await db.Warehouses.AnyAsync(w => w.TenantId == tenantId, cancellationToken);
        if (!hasAny)
        {
            request = request with { IsDefault = true };
        }

        var warehouse = new Warehouse
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Address = request.Address.NullIfEmpty(),
            IsDefault = request.IsDefault,
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

        if (warehouse.IsDefault)
        {
            await ClearOtherDefaultsAsync(db, tenantId, null, cancellationToken);
        }

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Address, warehouse.IsDefault, warehouse.IsActive);
    }

    internal static async Task ClearOtherDefaultsAsync(
        IApplicationDbContext db, Guid tenantId, Guid? excludeId, CancellationToken cancellationToken)
    {
        var others = await db.Warehouses
            .Where(w => w.TenantId == tenantId && w.IsDefault && w.Id != excludeId)
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }
}

public sealed class UpdateWarehouseHandler(IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<WarehouseDto> HandleAsync(Guid warehouseId, UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var warehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException("Depo bulunamadı.");

        warehouse.Name = request.Name.Trim();
        warehouse.Address = request.Address.NullIfEmpty();
        warehouse.IsActive = request.IsActive;

        if (request.IsDefault && !warehouse.IsDefault)
        {
            await CreateWarehouseHandler.ClearOtherDefaultsAsync(db, tenantId, warehouseId, cancellationToken);
            warehouse.IsDefault = true;
        }
        else if (!request.IsDefault && warehouse.IsDefault)
        {
            throw new ConflictException(
                "Varsayılan depo kaldırılamaz; önce başka bir depoyu varsayılan yapın.");
        }

        warehouse.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);

        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Address, warehouse.IsDefault, warehouse.IsActive);
    }
}

/// <summary>Hareketi olan depo silinemez; varsayılan depo silinemez.</summary>
public sealed class DeleteWarehouseHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task HandleAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var tenantId = ProductQueries.RequireTenantId(currentTenant);

        var warehouse = await db.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException("Depo bulunamadı.");

        if (warehouse.IsDefault)
        {
            throw new ConflictException("Varsayılan depo silinemez; önce başka bir depoyu varsayılan yapın.");
        }

        var hasMovements = await db.InventoryTransactions
            .AnyAsync(t => t.TenantId == tenantId && t.WarehouseId == warehouseId, cancellationToken);
        if (hasMovements)
        {
            throw new ConflictException("Bu depoda stok hareket geçmişi var; depo silinemez, pasifleştirin.");
        }

        db.Warehouses.Remove(warehouse);
        await db.SaveChangesAsync(cancellationToken);
    }
}
