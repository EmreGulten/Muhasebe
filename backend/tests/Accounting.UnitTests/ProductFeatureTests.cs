using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Products;
using Accounting.Contracts.Products;
using Accounting.Domain.Authorization;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.MultiTenancy;
using Accounting.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Accounting.UnitTests;

/// <summary>
/// ürün/stok özelliği: ürün CRUD + SKU benzersizliği, stok = Σ işaretli
/// miktar, sayım farkı, transfer çifti, kritik stok, tanım (kategori/birim/depo)
/// yaşam döngüleri, tenant izolasyonu ve izin matrisi.
/// </summary>
public sealed class ProductFeatureTests : IDisposable
{
    private readonly TestApp _app = new();

    // ---- Test altyapısı

    private async Task<IServiceScope> CreateOwnerScopeAsync(string email)
    {
        var user = await _app.RegisterUserAsync(email: email);
        var scope = _app.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.UserTenants
            .Where(m => m.UserId == user.Id)
            .Select(m => m.TenantId)
            .FirstAsync();
        scope.ServiceProvider.GetRequiredService<ITenantContextWriter>()
            .SetTenant(tenantId, TenantRole.Owner);
        return scope;
    }

    private static CreateProductRequest NewProduct(
        string name, string? sku = null, decimal minimumStock = 0, bool isService = false, decimal salePrice = 100m) =>
        new(name, sku, null, null, null, null, 50m, salePrice, 20m, minimumStock, isService);

    private static CreateInventoryTransactionRequest NewMovement(
        Guid productId, Guid warehouseId, string type, decimal quantity, DateTime date) =>
        new(productId, warehouseId, type, date, quantity, null);

    /// <summary>Varsayılan depoyu garantiler ve döndürür.</summary>
    private static async Task<WarehouseDto> GetDefaultWarehouseAsync(IServiceScope scope)
    {
        var warehouses = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>()
            .HandleAsync(default);
        return warehouses.First(w => w.IsDefault);
    }

    // ---- Ürün kartı

    [Fact]
    public async Task CreateProduct_SkuMustBeUniquePerTenant()
    {
        using var scope = await CreateOwnerScopeAsync("urun-sku@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();

        var product = await handler.HandleAsync(NewProduct("Kalem", sku: "KLM-1"), default);
        Assert.Equal("KLM-1", product.Sku);
        Assert.Equal(0m, product.CurrentStock);

        // Aynı SKU ikinci kez reddedilir; farklı SKU geçer.
        await Assert.ThrowsAsync<ConflictException>(
            () => handler.HandleAsync(NewProduct("Kalem 2", sku: "KLM-1"), default));
        var other = await handler.HandleAsync(NewProduct("Silgi", sku: "SLG-1"), default);
        Assert.Equal("SLG-1", other.Sku);
    }

    [Fact]
    public async Task CreateProduct_RejectsForeignCategoryAndUnit()
    {
        using var scope = await CreateOwnerScopeAsync("urun-kategori@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();

        var badCategory = NewProduct("Ürün") with { CategoryId = Guid.NewGuid() };
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(badCategory, default));

        var badUnit = NewProduct("Ürün") with { UnitId = Guid.NewGuid() };
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(badUnit, default));
    }

    [Fact]
    public async Task UpdateProduct_RenamesWithoutTouchingStock()
    {
        using var scope = await CreateOwnerScopeAsync("urun-guncelle@test.local");
        var create = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await create.HandleAsync(NewProduct("Eski Ad", sku: "UPD-1"), default);

        var update = scope.ServiceProvider.GetRequiredService<UpdateProductHandler>();
        var updated = await update.HandleAsync(product.Id,
            new UpdateProductRequest("Yeni Ad", "UPD-1", null, null, null, null, 60m, 120m, 20m, 5m, false, IsActive: true),
            default);

        Assert.Equal("Yeni Ad", updated.Name);
        Assert.Equal(60m, updated.PurchasePrice);
        Assert.Equal(5m, updated.MinimumStock);
        Assert.Equal(0m, updated.CurrentStock);
    }

    // ---- Stok hareketleri

    [Fact]
    public async Task Inventory_ManualMovements_SumToCurrentStock()
    {
        using var scope = await CreateOwnerScopeAsync("stok-toplam@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Defter", minimumStock: 5), default);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>();
        var day = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await handler.HandleAsync(NewMovement(product.Id, warehouse.Id, "ManualIn", 10m, day), default);
        await handler.HandleAsync(NewMovement(product.Id, warehouse.Id, "ManualOut", -3m, day.AddDays(1)), default);
        await handler.HandleAsync(NewMovement(product.Id, warehouse.Id, "Return", 1.5m, day.AddDays(2)), default);

        // Toplam stok = 10 − 3 + 1,5 = 8,5; eşik 5'in üstünde → kritik değil.
        var get = scope.ServiceProvider.GetRequiredService<GetProductHandler>();
        var detail = await get.HandleAsync(product.Id, default);
        Assert.Equal(8.5m, detail.CurrentStock);
        Assert.False(detail.IsCritical);

        var stock = await scope.ServiceProvider.GetRequiredService<GetProductStockHandler>()
            .HandleAsync(product.Id, default);
        Assert.Equal(8.5m, stock.TotalStock);
        var row = Assert.Single(stock.Warehouses);
        Assert.Equal(warehouse.Id, row.WarehouseId);
        Assert.Equal(8.5m, row.Stock);
    }

    [Fact]
    public async Task Inventory_Count_WritesDeltaBetweenCountedAndCurrent()
    {
        using var scope = await CreateOwnerScopeAsync("stok-sayim@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Çivi"), default);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>();
        var day = DateTime.UtcNow.Date;
        await handler.HandleAsync(NewMovement(product.Id, warehouse.Id, "ManualIn", 10m, day), default);

        // Sayım 8 buldu → fark −2 hareketi yazılır, stok 8'e iner.
        var count = await handler.HandleAsync(
            NewMovement(product.Id, warehouse.Id, "Count", 8m, day.AddDays(1)), default);
        Assert.Equal(-2m, count.Quantity);

        var detail = await scope.ServiceProvider.GetRequiredService<GetProductHandler>()
            .HandleAsync(product.Id, default);
        Assert.Equal(8m, detail.CurrentStock);

        // Aynı değeri tekrar saymak fark üretmez → reddedilir.
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            NewMovement(product.Id, warehouse.Id, "Count", 8m, day.AddDays(2)), default));
    }

    [Theory]
    [InlineData("Purchase")]  // alış modülünden üretilir
    [InlineData("Sale")]      // satış modülünden üretilir
    [InlineData("Transfer")]  // ayrı uç nokta
    [InlineData("TurYok")]
    public async Task Inventory_RejectsNonManualTypes(string type)
    {
        using var scope = await CreateOwnerScopeAsync($"stok-tur-{type.ToLowerInvariant()}@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Tür Test"), default);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>();
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            NewMovement(product.Id, warehouse.Id, type, 5m, DateTime.UtcNow), default));
    }

    [Theory]
    [InlineData("ManualIn", -5)]   // giriş pozitif olmalı
    [InlineData("ManualOut", 5)]   // çıkış negatif olmalı
    [InlineData("Return", -1)]
    [InlineData("ManualIn", 0)]    // sıfır
    public async Task Inventory_RejectsSignMismatchAndZero(string type, decimal quantity)
    {
        using var scope = await CreateOwnerScopeAsync($"stok-isaret-{type.ToLowerInvariant()}-{quantity}@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("İşaret Test"), default);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>();
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            NewMovement(product.Id, warehouse.Id, type, quantity, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task Inventory_RejectsServiceAndInactiveProducts()
    {
        using var scope = await CreateOwnerScopeAsync("stok-hizmet@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var service = await products.HandleAsync(NewProduct("Montaj Hizmeti", isService: true), default);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>();
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            NewMovement(service.Id, warehouse.Id, "ManualIn", 1m, DateTime.UtcNow), default));

        var update = scope.ServiceProvider.GetRequiredService<UpdateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Pasif Ürün"), default);
        await update.HandleAsync(product.Id,
            new UpdateProductRequest("Pasif Ürün", null, null, null, null, null, 0m, 0m, 20m, 0m, false, IsActive: false),
            default);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            NewMovement(product.Id, warehouse.Id, "ManualIn", 1m, DateTime.UtcNow), default));
    }

    // ---- Transfer

    [Fact]
    public async Task Transfer_CreatesExitAndEntryRowsAtomically()
    {
        using var scope = await CreateOwnerScopeAsync("stok-transfer@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Koli"), default);
        var source = await GetDefaultWarehouseAsync(scope);

        var createWarehouse = scope.ServiceProvider.GetRequiredService<CreateWarehouseHandler>();
        var target = await createWarehouse.HandleAsync(
            new CreateWarehouseRequest("Şube Deposu", null, IsDefault: false), default);

        var movements = scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>();
        var day = DateTime.UtcNow.Date;
        await movements.HandleAsync(NewMovement(product.Id, source.Id, "ManualIn", 10m, day), default);

        var transfer = scope.ServiceProvider.GetRequiredService<CreateInventoryTransferHandler>();
        var rows = await transfer.HandleAsync(new CreateInventoryTransferRequest(
            product.Id, source.Id, target.Id, day, 4m, "Şubeye sevkiyat"), default);

        // Çift: −4 kaynak, +4 hedef, aynı referans.
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Quantity == -4m && r.WarehouseId == source.Id);
        Assert.Contains(rows, r => r.Quantity == 4m && r.WarehouseId == target.Id);
        Assert.All(rows, r => Assert.Equal("Transfer", r.ReferenceType));
        Assert.Single(rows.Select(r => r.ReferenceId).Distinct());

        // Depo bazlı döküm: kaynak 6, hedef 4; toplam değişmez.
        var stock = await scope.ServiceProvider.GetRequiredService<GetProductStockHandler>()
            .HandleAsync(product.Id, default);
        Assert.Equal(10m, stock.TotalStock);
        Assert.Equal(6m, stock.Warehouses.Single(w => w.WarehouseId == source.Id).Stock);
        Assert.Equal(4m, stock.Warehouses.Single(w => w.WarehouseId == target.Id).Stock);
    }

    [Fact]
    public async Task Transfer_RejectsSameSourceAndTarget()
    {
        using var scope = await CreateOwnerScopeAsync("stok-transfer-ayni@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Ürün"), default);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var transfer = scope.ServiceProvider.GetRequiredService<CreateInventoryTransferHandler>();
        await Assert.ThrowsAsync<AppException>(() => transfer.HandleAsync(
            new CreateInventoryTransferRequest(product.Id, warehouse.Id, warehouse.Id, DateTime.UtcNow, 1m, null), default));
    }

    // ---- Kritik stok

    [Fact]
    public async Task CriticalStock_ListsOnlyProductsBelowPositiveThreshold()
    {
        using var scope = await CreateOwnerScopeAsync("stok-kritik@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var low = await products.HandleAsync(NewProduct("Azalan Ürün", minimumStock: 5, salePrice: 10m), default);
        var plenty = await products.HandleAsync(NewProduct("Bol Ürün", minimumStock: 5), default);
        var noThreshold = await products.HandleAsync(NewProduct("Eşiksiz Ürün"), default);
        var service = await products.HandleAsync(NewProduct("Hizmet", minimumStock: 5, isService: true), default);

        var warehouse = await GetDefaultWarehouseAsync(scope);
        var movements = scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>();
        var day = DateTime.UtcNow.Date;
        await movements.HandleAsync(NewMovement(low.Id, warehouse.Id, "ManualIn", 3m, day), default);
        await movements.HandleAsync(NewMovement(plenty.Id, warehouse.Id, "ManualIn", 20m, day), default);
        await movements.HandleAsync(NewMovement(noThreshold.Id, warehouse.Id, "ManualIn", 0.5m, day), default);

        var critical = await scope.ServiceProvider.GetRequiredService<GetCriticalStockHandler>()
            .HandleAsync(default);

        var item = Assert.Single(critical);
        Assert.Equal(low.Id, item.ProductId);
        Assert.Equal(3m, item.CurrentStock);
        Assert.Equal(5m, item.MinimumStock);

        // Liste filtresi de aynı kuralı uygular.
        var list = await scope.ServiceProvider.GetRequiredService<ListProductsHandler>()
            .HandleAsync(null, null, includeInactive: true, criticalOnly: true, 1, 50, default);
        var summary = Assert.Single(list.Items);
        Assert.Equal(low.Id, summary.Id);
        Assert.True(summary.IsCritical);

        // Bol ürün kritik değildir; eşiksiz ve hizmet hiç listelenmez.
        var detail = await scope.ServiceProvider.GetRequiredService<GetProductHandler>()
            .HandleAsync(plenty.Id, default);
        Assert.False(detail.IsCritical);
        Assert.DoesNotContain(critical, c => c.ProductId == noThreshold.Id || c.ProductId == service.Id);
    }

    // ---- Silme kuralları

    [Fact]
    public async Task DeleteProduct_WithMovements_ThrowsConflict()
    {
        using var scope = await CreateOwnerScopeAsync("urun-silme@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Hareketli Ürün"), default);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        await scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>()
            .HandleAsync(NewMovement(product.Id, warehouse.Id, "ManualIn", 1m, DateTime.UtcNow), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<DeleteProductHandler>().HandleAsync(product.Id, default));
    }

    [Fact]
    public async Task DeleteProduct_WithoutMovements_SoftDeletes()
    {
        using var scope = await CreateOwnerScopeAsync("urun-silme-bos@test.local");
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Hareketsiz Ürün"), default);

        await scope.ServiceProvider.GetRequiredService<DeleteProductHandler>().HandleAsync(product.Id, default);

        var get = scope.ServiceProvider.GetRequiredService<GetProductHandler>();
        await Assert.ThrowsAsync<NotFoundException>(() => get.HandleAsync(product.Id, default));

        // Soft delete: satır durur, SKU yeniden kullanılabilir.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == product.Id);
        Assert.True(row.IsDeleted);
        var recreated = await products.HandleAsync(NewProduct("Yeni Ürün", sku: product.Sku), default);
        Assert.NotEqual(product.Id, recreated.Id);
    }

    // ---- Tanımlar

    [Fact]
    public async Task CategoryLifecycle_UniqueNameAndDeleteBlockedWhenInUse()
    {
        using var scope = await CreateOwnerScopeAsync("kategori@test.local");
        var create = scope.ServiceProvider.GetRequiredService<CreateCategoryHandler>();
        var category = await create.HandleAsync(new CreateCategoryRequest("Kırtasiye"), default);

        await Assert.ThrowsAsync<ConflictException>(
            () => create.HandleAsync(new CreateCategoryRequest("Kırtasiye"), default));

        var update = scope.ServiceProvider.GetRequiredService<UpdateCategoryHandler>();
        var renamed = await update.HandleAsync(category.Id, new UpdateCategoryRequest("Ofis"), default);
        Assert.Equal("Ofis", renamed.Name);

        // Ürün kullandığında silinemez.
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(
            NewProduct("Ürün") with { CategoryId = category.Id }, default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<DeleteCategoryHandler>().HandleAsync(category.Id, default));

        // Ürün silinince kategori silinebilir.
        await scope.ServiceProvider.GetRequiredService<DeleteProductHandler>().HandleAsync(product.Id, default);
        await scope.ServiceProvider.GetRequiredService<DeleteCategoryHandler>().HandleAsync(category.Id, default);

        var list = await scope.ServiceProvider.GetRequiredService<ListCategoriesHandler>().HandleAsync(default);
        Assert.DoesNotContain(list, c => c.Id == category.Id);
    }

    [Fact]
    public async Task UnitLifecycle_SoftGuardWhenInUse()
    {
        using var scope = await CreateOwnerScopeAsync("birim@test.local");
        var create = scope.ServiceProvider.GetRequiredService<CreateUnitHandler>();
        var unit = await create.HandleAsync(new CreateUnitRequest("Kilogram", "kg"), default);
        Assert.Equal("kg", unit.Code);

        await Assert.ThrowsAsync<ConflictException>(
            () => create.HandleAsync(new CreateUnitRequest("Kilogram", null), default));

        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        await products.HandleAsync(NewProduct("Şeker") with { UnitId = unit.Id }, default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<DeleteUnitHandler>().HandleAsync(unit.Id, default));
    }

    [Fact]
    public async Task WarehouseLifecycle_DefaultRules()
    {
        using var scope = await CreateOwnerScopeAsync("depo@test.local");
        var list = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>().HandleAsync(default);

        // İlk listeleme varsayılan "Ana Depo"yu oluşturur.
        var main = Assert.Single(list);
        Assert.Equal("Ana Depo", main.Name);
        Assert.True(main.IsDefault);

        // Yeni varsayılan atanınca eski varsayılan düşer.
        var create = scope.ServiceProvider.GetRequiredService<CreateWarehouseHandler>();
        var second = await create.HandleAsync(new CreateWarehouseRequest("Depo 2", null, IsDefault: true), default);
        Assert.True(second.IsDefault);

        var listAgain = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>().HandleAsync(default);
        Assert.Equal(2, listAgain.Count);
        Assert.Single(listAgain, w => w.IsDefault && w.Id == second.Id);

        // Varsayılan depo silinemez / varsayılanlık kaldırılamaz.
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<DeleteWarehouseHandler>().HandleAsync(second.Id, default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<UpdateWarehouseHandler>().HandleAsync(
                second.Id, new UpdateWarehouseRequest("Depo 2", null, IsActive: true, IsDefault: false), default));

        // Hareket gören depo silinemez (varsayılan olmayan depoyla denenir).
        var products = scope.ServiceProvider.GetRequiredService<CreateProductHandler>();
        var product = await products.HandleAsync(NewProduct("Ürün"), default);
        var third = await scope.ServiceProvider.GetRequiredService<CreateWarehouseHandler>()
            .HandleAsync(new CreateWarehouseRequest("Geçici Depo", null, IsDefault: false), default);
        await scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>().HandleAsync(
            NewMovement(product.Id, third.Id, "ManualIn", 2m, DateTime.UtcNow), default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<DeleteWarehouseHandler>().HandleAsync(third.Id, default));
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task Products_Are_Isolated_Between_Tenants()
    {
        using var scopeA = await CreateOwnerScopeAsync("urun-a@test.local");
        var productInA = await scopeA.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewProduct("A İşletmesinin Ürünü", sku: "ORTAK-1", minimumStock: 5m), default);

        using var scopeB = await CreateOwnerScopeAsync("urun-b@test.local");
        var get = scopeB.ServiceProvider.GetRequiredService<GetProductHandler>();
        await Assert.ThrowsAsync<NotFoundException>(() => get.HandleAsync(productInA.Id, default));

        // Liste ve kritik stok sorguları da yalnızca B'nin ürünlerini görmeli
        // (liste sorgusu 2026-08-19 E2E'sinde tenant filtresi kaçırıyordu).
        var listInB = await scopeB.ServiceProvider.GetRequiredService<ListProductsHandler>()
            .HandleAsync(null, null, includeInactive: true, criticalOnly: false, 1, 20, default);
        Assert.DoesNotContain(listInB.Items, p => p.Id == productInA.Id);

        var criticalInB = await scopeB.ServiceProvider.GetRequiredService<GetCriticalStockHandler>()
            .HandleAsync(default);
        Assert.DoesNotContain(criticalInB, c => c.ProductId == productInA.Id);

        // B'de aynı SKU serbesttir (benzersizlik tenant içindedir).
        var productInB = await scopeB.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewProduct("B Ürünü", sku: "ORTAK-1"), default);
        Assert.Equal("ORTAK-1", productInB.Sku);

        var warehouseB = await GetDefaultWarehouseAsync(scopeB);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scopeB.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>().HandleAsync(
                NewMovement(productInA.Id, warehouseB.Id, "ManualIn", 1m, DateTime.UtcNow), default));
    }

    // ---- İzin matrisi

    [Fact]
    public void Viewer_And_Employee_Cannot_Edit_Inventory()
    {
        var viewer = RolePermissions.For(TenantRole.Viewer);
        Assert.Contains(Permissions.ProductsView, viewer);
        Assert.Contains(Permissions.InventoryView, viewer);
        Assert.DoesNotContain(Permissions.ProductsCreate, viewer);
        Assert.DoesNotContain(Permissions.ProductsEdit, viewer);
        Assert.DoesNotContain(Permissions.InventoryEdit, viewer);

        // Employee satış/cari tarafında işlem yapar ama stok düzeltmesi yapamaz.
        var employee = RolePermissions.For(TenantRole.Employee);
        Assert.DoesNotContain(Permissions.InventoryEdit, employee);
        Assert.Contains(Permissions.SalesCreate, employee);
    }

    // ---- Doğrulama kuralları

    [Fact]
    public async Task ProductValidator_RejectsInvalidInput()
    {
        using var scope = await CreateOwnerScopeAsync("urun-validator@test.local");
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CreateProductRequest>>();

        Assert.False((await validator.ValidateAsync(NewProduct("A"), default)).IsValid);                       // ad kısa
        Assert.False((await validator.ValidateAsync(NewProduct("Ürün") with { VatRate = 150m }, default)).IsValid); // KDV > 100
        Assert.False((await validator.ValidateAsync(NewProduct("Ürün") with { SalePrice = -1m }, default)).IsValid); // negatif fiyat
        Assert.False((await validator.ValidateAsync(NewProduct("Ürün") with { MinimumStock = 0.12345m }, default)).IsValid); // 5 basamak
        Assert.False((await validator.ValidateAsync(
            NewProduct("Ürün") with { Sku = new string('x', 51) }, default)).IsValid);                          // SKU uzun

        Assert.True((await validator.ValidateAsync(
            NewProduct("Geçerli Ürün") with { MinimumStock = 2.5m }, default)).IsValid);
    }

    [Fact]
    public async Task InventoryValidator_RejectsInvalidInput()
    {
        using var scope = await CreateOwnerScopeAsync("stok-validator@test.local");
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CreateInventoryTransactionRequest>>();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var day = DateTime.UtcNow;

        Assert.False((await validator.ValidateAsync(NewMovement(productId, warehouseId, "Sale", 1m, day), default)).IsValid);
        Assert.False((await validator.ValidateAsync(NewMovement(productId, warehouseId, "ManualIn", 0m, day), default)).IsValid);
        Assert.False((await validator.ValidateAsync(NewMovement(productId, warehouseId, "ManualIn", 1.23456m, day), default)).IsValid);
        Assert.False((await validator.ValidateAsync(
            NewMovement(Guid.Empty, warehouseId, "ManualIn", 1m, day), default)).IsValid);                     // ürün boş
        Assert.False((await validator.ValidateAsync(
            new CreateInventoryTransactionRequest(productId, warehouseId, "ManualIn", default, 1m, null), default)).IsValid); // tarih boş

        Assert.True((await validator.ValidateAsync(NewMovement(productId, warehouseId, "Count", 10.25m, day), default)).IsValid);
    }

    public void Dispose() => _app.Dispose();
}
