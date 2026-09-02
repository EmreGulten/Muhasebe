using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Accounting.UnitTests;

public sealed class TenantBackupTests : IDisposable
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task Export_And_Restore_Remaps_Relationships_Into_Empty_Tenant()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await SeedTenantsAndInventoryAsync(sourceId, targetId);

        TenantBackupFile backup;
        using (var scope = TenantScope(sourceId))
        {
            backup = await scope.ServiceProvider.GetRequiredService<ITenantBackupService>().ExportAsync();
        }

        TenantRestoreResult result;
        using (var scope = TenantScope(targetId))
        {
            result = await scope.ServiceProvider.GetRequiredService<ITenantBackupService>().RestoreAsync(backup.Content);
        }

        Assert.Equal(4, result.ImportedRowCount);
        using var verify = _app.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.TenantId == targetId);
        var category = await db.Categories.IgnoreQueryFilters().SingleAsync(x => x.TenantId == targetId);
        var warehouse = await db.Warehouses.IgnoreQueryFilters().SingleAsync(x => x.TenantId == targetId);
        var movement = await db.InventoryTransactions.SingleAsync(x => x.TenantId == targetId);

        Assert.Equal(category.Id, product.CategoryId);
        Assert.Equal(product.Id, movement.ProductId);
        Assert.Equal(warehouse.Id, movement.WarehouseId);
        Assert.NotEqual(sourceId, product.TenantId);
    }

    [Fact]
    public async Task Restore_Rejects_NonEmpty_Tenant()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await SeedTenantsAndInventoryAsync(sourceId, targetId);

        TenantBackupFile backup;
        using (var scope = TenantScope(sourceId))
        {
            backup = await scope.ServiceProvider.GetRequiredService<ITenantBackupService>().ExportAsync();
        }

        using (var scope = _app.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Categories.Add(new Category
            {
                TenantId = targetId,
                Name = "Mevcut",
            });
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }

        using var restoreScope = TenantScope(targetId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            restoreScope.ServiceProvider.GetRequiredService<ITenantBackupService>().RestoreAsync(backup.Content));
    }

    [Fact]
    public async Task Restore_Rejects_Tampered_Backup()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await SeedTenantsAndInventoryAsync(sourceId, targetId);

        TenantBackupFile backup;
        using (var scope = TenantScope(sourceId))
        {
            backup = await scope.ServiceProvider.GetRequiredService<ITenantBackupService>().ExportAsync();
        }

        var tampered = backup.Content.ToArray();
        var index = Array.IndexOf(tampered, (byte)'D');
        Assert.True(index >= 0);
        tampered[index] = (byte)'X';

        using var restoreScope = TenantScope(targetId);
        await Assert.ThrowsAsync<AppException>(() =>
            restoreScope.ServiceProvider.GetRequiredService<ITenantBackupService>().RestoreAsync(tampered));
    }

    private IServiceScope TenantScope(Guid tenantId)
    {
        var scope = _app.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextWriter>().SetTenant(tenantId, TenantRole.Owner);
        return scope;
    }

    private async Task SeedTenantsAndInventoryAsync(Guid sourceId, Guid targetId)
    {
        using var scope = _app.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.AddRange(
            new Tenant { Id = sourceId, Name = "Kaynak" },
            new Tenant { Id = targetId, Name = "Hedef" });
        var category = new Category { TenantId = sourceId, Name = "Ofis" };
        var warehouse = new Warehouse { TenantId = sourceId, Name = "Merkez", IsDefault = true };
        var product = new Product
        {
            TenantId = sourceId,
            Name = "Kalem",
            CategoryId = category.Id,
            Sku = "KLM-1",
        };
        db.AddRange(category, warehouse, product, new InventoryTransaction
        {
            TenantId = sourceId,
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Type = InventoryTransactionType.ManualIn,
            Quantity = 10,
            Date = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public void Dispose() => _app.Dispose();
}
