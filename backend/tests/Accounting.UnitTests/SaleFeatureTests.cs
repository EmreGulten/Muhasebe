using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Parties;
using Accounting.Application.Features.Products;
using Accounting.Application.Features.Sales;
using Accounting.Application.Validators;
using Accounting.Contracts.Parties;
using Accounting.Contracts.Products;
using Accounting.Contracts.Sales;
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
/// satış özelliği: hesaplamalar (iskonto/KDV), onay atomikliği (stok +
/// cari + kasa tek kayıtta), yetersiz stok reddi, Draft-dışı değişiklik reddi,
/// iptal ters hareketleri, aşırı tahsilat reddi ve tenant izolasyonu.
/// </summary>
public sealed class SaleFeatureTests : IDisposable
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
        string name, bool isService = false, decimal salePrice = 100m) =>
        new(name, null, null, null, null, null, 50m, salePrice, 20m, 0m, isService);

    private static CreatePartyRequest NewCustomer(string name, string type = "Customer") =>
        new(name, type, null, null, null, null, null, null, null, null, 0m, 0m, null);

    private static async Task<WarehouseDto> GetDefaultWarehouseAsync(IServiceScope scope)
    {
        var warehouses = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>()
            .HandleAsync(default);
        return warehouses.First(w => w.IsDefault);
    }

    /// <summary>Ürün + başlangıç stoğu; dönen tuple belge kalemi için hazır değerler verir.</summary>
    private static async Task<Guid> SeededProductAsync(
        IServiceScope scope, string name, decimal stock, bool isService = false, decimal salePrice = 100m)
    {
        var product = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewProduct(name, isService, salePrice), default);

        if (!isService)
        {
            var warehouse = await GetDefaultWarehouseAsync(scope);
            await scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>()
                .HandleAsync(new CreateInventoryTransactionRequest(
                    product.Id, warehouse.Id, "ManualIn", DateTime.UtcNow, stock, "test stoğu"), default);
        }

        return product.Id;
    }

    private static CreateSaleRequest NewSale(
        Guid? partyId, Guid? warehouseId, params SaleItemRequest[] items) =>
        new(partyId, warehouseId, DateTime.UtcNow.Date, null, null, items);

    private static SaleItemRequest Item(Guid productId, decimal quantity, decimal price,
        decimal discount = 0m, decimal vat = 20m) =>
        new(productId, quantity, price, discount, vat);

    // ---- Hesaplamalar ve belge oluşumu

    [Fact]
    public async Task CreateSale_ComputesTotals_WithDiscountAndVat()
    {
        using var scope = await CreateOwnerScopeAsync("satis-hesap@test.local");
        var goods = await SeededProductAsync(scope, "Kalem", 10m);
        var service = await SeededProductAsync(scope, "Montaj", 0m, isService: true, salePrice: 500m);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(NewSale(null, warehouse.Id,
                Item(goods, 3m, 100m, discount: 10m, vat: 20m),
                Item(service, 1m, 500m)), default);

        // Kalem 1: brüt 300 → iskonto %10 → net 270; KDV 54; toplam 324.
        // Kalem 2: net 500; KDV 100; toplam 600.
        Assert.Equal("S-000001", sale.Number);
        Assert.Equal(SaleStatus.Draft.ToString(), sale.Status);
        Assert.Equal(770m, sale.SubTotal);
        Assert.Equal(30m, sale.DiscountTotal);
        Assert.Equal(154m, sale.VatTotal);
        Assert.Equal(924m, sale.Total);
        Assert.Equal(924m, sale.DueAmount);
        Assert.Equal(2, sale.Items.Count);
    }

    [Fact]
    public async Task CreateSale_SecondSaleGetsNextNumber()
    {
        using var scope = await CreateOwnerScopeAsync("satis-seri@test.local");
        var goods = await SeededProductAsync(scope, "Defter", 5m);
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var handler = scope.ServiceProvider.GetRequiredService<CreateSaleHandler>();

        var first = await handler.HandleAsync(NewSale(null, warehouse.Id, Item(goods, 1m, 10m)), default);
        var second = await handler.HandleAsync(NewSale(null, warehouse.Id, Item(goods, 1m, 10m)), default);

        Assert.Equal("S-000001", first.Number);
        Assert.Equal("S-000002", second.Number);
    }

    [Fact]
    public async Task CreateSale_RejectsSupplierParty()
    {
        using var scope = await CreateOwnerScopeAsync("satis-tedarikci@test.local");
        var goods = await SeededProductAsync(scope, "Silgi", 5m);
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var supplier = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewCustomer("Ted", "Supplier"), default);

        await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
                .HandleAsync(NewSale(supplier.Id, warehouse.Id, Item(goods, 1m, 10m)), default));
    }

    // ---- Onay

    [Fact]
    public async Task ConfirmSale_ReducesStock_AddsPartyDebt_AndStaysAtomic()
    {
        using var scope = await CreateOwnerScopeAsync("satis-onay@test.local");
        var goods = await SeededProductAsync(scope, "Kalem", 10m);
        var service = await SeededProductAsync(scope, "Montaj", 0m, isService: true, salePrice: 500m);
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var customer = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewCustomer("Müşteri"), default);

        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(NewSale(customer.Id, warehouse.Id,
                Item(goods, 3m, 100m, discount: 10m), Item(service, 1m, 500m)), default);

        var confirmed = await scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
            .HandleAsync(sale.Id, new ConfirmSaleRequest(null), default);

        Assert.Equal(SaleStatus.Confirmed.ToString(), confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAtUtc);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Sales.First(s => s.Id == sale.Id).TenantId;

        // Stok: yalnızca mal kalemi düşer (10 − 3 = 7); hizmet düşmez.
        var goodsStock = await db.InventoryTransactions
            .Where(t => t.TenantId == tenantId && t.ProductId == goods)
            .SumAsync(t => t.Quantity);
        Assert.Equal(7m, goodsStock);
        var serviceMoves = await db.InventoryTransactions
            .CountAsync(t => t.TenantId == tenantId && t.ProductId == service);
        Assert.Equal(0, serviceMoves);

        // Cari: 924 borç, Referans Sale.
        var debt = await db.PartyTransactions.SingleAsync(t =>
            t.TenantId == tenantId && t.PartyId == customer.Id && t.Type == PartyTransactionType.Sale);
        Assert.Equal(924m, debt.Debit);
        Assert.Equal("Sale", debt.ReferenceType);

        // Onaylı belge tekrar onaylanamaz, değiştirilemez, silinemez.
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
                .HandleAsync(sale.Id, new ConfirmSaleRequest(null), default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<UpdateSaleHandler>()
                .HandleAsync(sale.Id, new UpdateSaleRequest(null, warehouse.Id, DateTime.UtcNow.Date, null, null,
                    [Item(goods, 1m, 10m)]), default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<DeleteSaleHandler>().HandleAsync(sale.Id, default));
    }

    [Fact]
    public async Task ConfirmSale_WithInsufficientStock_RejectsAndWritesNothing()
    {
        using var scope = await CreateOwnerScopeAsync("satis-stok@test.local");
        var goods = await SeededProductAsync(scope, "Kalem", 2m);
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var customer = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewCustomer("Müşteri"), default);

        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(NewSale(customer.Id, warehouse.Id, Item(goods, 3m, 100m)), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
                .HandleAsync(sale.Id, new ConfirmSaleRequest(null), default));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Sales.First(s => s.Id == sale.Id).TenantId;

        // Onay tümüyle geri döner: stok hareketi ve cari hareket yazılmamış olmalı.
        Assert.Equal(0, await db.InventoryTransactions.CountAsync(t =>
            t.TenantId == tenantId && t.ReferenceType == "Sale"));
        Assert.Equal(0, await db.PartyTransactions.CountAsync(t =>
            t.TenantId == tenantId && t.PartyId == customer.Id && t.Type == PartyTransactionType.Sale));

        var reloaded = await scope.ServiceProvider.GetRequiredService<GetSaleHandler>()
            .HandleAsync(sale.Id, default);
        Assert.Equal(SaleStatus.Draft.ToString(), reloaded.Status);
    }

    [Fact]
    public async Task ConfirmSale_WithoutParty_WritesNoPartyTransaction()
    {
        using var scope = await CreateOwnerScopeAsync("satis-nakit@test.local");
        var goods = await SeededProductAsync(scope, "Kalem", 5m);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(NewSale(null, warehouse.Id, Item(goods, 2m, 50m, vat: 0m)), default);

        var confirmed = await scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
            .HandleAsync(sale.Id, new ConfirmSaleRequest(null), default);

        Assert.Equal(SaleStatus.Confirmed.ToString(), confirmed.Status);
        Assert.Null(confirmed.PartyName);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Sales.First(s => s.Id == sale.Id).TenantId;
        Assert.Equal(0, await db.PartyTransactions.CountAsync(t => t.TenantId == tenantId));
    }

    [Fact]
    public async Task ConfirmSale_WithPayment_CreatesCashAndPartyCredit()
    {
        using var scope = await CreateOwnerScopeAsync("satis-odeme@test.local");
        var goods = await SeededProductAsync(scope, "Kalem", 5m);
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var customer = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewCustomer("Müşteri"), default);

        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(NewSale(customer.Id, warehouse.Id, Item(goods, 1m, 100m)), default); // 120,00 KDV %20

        var confirmed = await scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
            .HandleAsync(sale.Id, new ConfirmSaleRequest(
                new ConfirmPaymentRequest(DateTime.UtcNow.Date, 50m, "peşin")), default);

        Assert.Equal(SaleStatus.PartiallyPaid.ToString(), confirmed.Status);
        Assert.Equal(50m, confirmed.PaidAmount);
        Assert.Equal(70m, confirmed.DueAmount);
        Assert.Single(confirmed.Payments);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Sales.First(s => s.Id == sale.Id).TenantId;

        // "Kasa" default hesabı ilk tahsilatta oluşur; işaretli +50.
        var account = await db.Accounts.SingleAsync(a => a.TenantId == tenantId);
        Assert.Equal("Kasa", account.Name);
        Assert.True(account.IsDefault);
        var cash = await db.AccountTransactions.SingleAsync(t => t.TenantId == tenantId);
        Assert.Equal(50m, cash.Amount);
        Assert.Equal(AccountTransactionType.SaleCollection, cash.Type);

        // Cari alacak hareketi (Collection, Credit).
        var credit = await db.PartyTransactions.SingleAsync(t =>
            t.TenantId == tenantId && t.PartyId == customer.Id && t.Type == PartyTransactionType.Collection);
        Assert.Equal(50m, credit.Credit);

        // Aşırı tahsilat reddedilir (kalan 70).
        await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<AddSalePaymentHandler>()
                .HandleAsync(sale.Id, new AddSalePaymentRequest(DateTime.UtcNow.Date, 71m, null), default));

        // Kalanı tahsil et → Paid.
        var paid = await scope.ServiceProvider.GetRequiredService<AddSalePaymentHandler>()
            .HandleAsync(sale.Id, new AddSalePaymentRequest(DateTime.UtcNow.Date, 70m, null), default);
        Assert.Equal(SaleStatus.Paid.ToString(), paid.Status);
        Assert.Equal(0m, paid.DueAmount);
    }

    // ---- İptal

    [Fact]
    public async Task CancelSale_WritesReverseMovements()
    {
        using var scope = await CreateOwnerScopeAsync("satis-iptal@test.local");
        var goods = await SeededProductAsync(scope, "Kalem", 10m);
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var customer = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewCustomer("Müşteri"), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreateSaleHandler>();
        var sale = await handler.HandleAsync(
            NewSale(customer.Id, warehouse.Id, Item(goods, 4m, 100m)), default); // 480,00

        await scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
            .HandleAsync(sale.Id, new ConfirmSaleRequest(
                new ConfirmPaymentRequest(DateTime.UtcNow.Date, 200m, null)), default);

        var cancelled = await scope.ServiceProvider.GetRequiredService<CancelSaleHandler>()
            .HandleAsync(sale.Id, new CancelSaleRequest("müşteri vazgeçti"), default);

        Assert.Equal(SaleStatus.Cancelled.ToString(), cancelled.Status);
        Assert.Equal("müşteri vazgeçti", cancelled.CancelReason);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Sales.First(s => s.Id == sale.Id).TenantId;

        // Stok geri: satış zinciri 10 − 4 + 4 = 10.
        var stock = await db.InventoryTransactions
            .Where(t => t.TenantId == tenantId && t.ProductId == goods)
            .SumAsync(t => t.Quantity);
        Assert.Equal(10m, stock);

        // Cari denge: borç (480 satış + 200 tahsilat iadesi) − alacak (480 iptal +
        // 200 tahsilat) = 0 — tahsilatın cari alacağı da ters işaretle döner.
        var partyBalance = await db.PartyTransactions
            .Where(t => t.TenantId == tenantId && t.PartyId == customer.Id)
            .SumAsync(t => t.Debit - t.Credit);
        Assert.Equal(0m, partyBalance);
        Assert.True(await db.PartyTransactions.AnyAsync(t =>
            t.TenantId == tenantId && t.PartyId == customer.Id
            && t.ReferenceType == "SaleCancel" && t.Debit == 200m));

        // Kasa denge: +200 − 200 = 0; SaleCancel referanslı iade hareketi var.
        var cash = await db.AccountTransactions
            .Where(t => t.TenantId == tenantId)
            .SumAsync(t => t.Amount);
        Assert.Equal(0m, cash);
        Assert.True(await db.AccountTransactions.AnyAsync(t =>
            t.TenantId == tenantId && t.ReferenceType == "SaleCancel" && t.Amount == -200m));

        // İptal terminal: tekrar iptal ve tahsilat reddedilir.
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<CancelSaleHandler>()
                .HandleAsync(sale.Id, new CancelSaleRequest("tekrar"), default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<AddSalePaymentHandler>()
                .HandleAsync(sale.Id, new AddSalePaymentRequest(DateTime.UtcNow.Date, 10m, null), default));
    }

    [Fact]
    public async Task CancelSale_RejectsDraft()
    {
        using var scope = await CreateOwnerScopeAsync("satis-taslak-iptal@test.local");
        var goods = await SeededProductAsync(scope, "Kalem", 5m);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(NewSale(null, warehouse.Id, Item(goods, 1m, 10m)), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<CancelSaleHandler>()
                .HandleAsync(sale.Id, new CancelSaleRequest("taslak"), default));

        // Taslak silinir.
        await scope.ServiceProvider.GetRequiredService<DeleteSaleHandler>().HandleAsync(sale.Id, default);
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task Sales_Are_Isolated_Between_Tenants()
    {
        using var scopeA = await CreateOwnerScopeAsync("satis-a@test.local");
        var goodsA = await SeededProductAsync(scopeA, "A Ürünü", 5m);
        var warehouseA = await GetDefaultWarehouseAsync(scopeA);
        var saleInA = await scopeA.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(NewSale(null, warehouseA.Id, Item(goodsA, 1m, 10m)), default);

        using var scopeB = await CreateOwnerScopeAsync("satis-b@test.local");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scopeB.ServiceProvider.GetRequiredService<GetSaleHandler>().HandleAsync(saleInA.Id, default));

        var listInB = await scopeB.ServiceProvider.GetRequiredService<ListSalesHandler>()
            .HandleAsync(null, null, null, 1, 20, default);
        Assert.DoesNotContain(listInB.Items, s => s.Id == saleInA.Id);
    }

    // ---- İzin matrisi ve doğrulama

    [Fact]
    public void Sales_PermissionMatrix()
    {
        var viewer = RolePermissions.For(TenantRole.Viewer);
        Assert.Contains(Permissions.SalesView, viewer);
        Assert.DoesNotContain(Permissions.SalesCreate, viewer);
        Assert.DoesNotContain(Permissions.SalesEdit, viewer);
        Assert.DoesNotContain(Permissions.SalesDelete, viewer);

        var employee = RolePermissions.For(TenantRole.Employee);
        Assert.Contains(Permissions.SalesCreate, employee);
        Assert.Contains(Permissions.SalesEdit, employee);
        Assert.DoesNotContain(Permissions.SalesDelete, employee);
    }

    [Fact]
    public async Task SaleValidator_RejectsInvalidInput()
    {
        using var scope = _app.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CreateSaleRequest>>();
        var productId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var emptyItems = await validator.ValidateAsync(new CreateSaleRequest(
            null, null, today, null, null, []));
        Assert.False(emptyItems.IsValid);

        var badItem = await validator.ValidateAsync(new CreateSaleRequest(
            null, null, today, null, null, [new SaleItemRequest(productId, 0m, 10m, 0m, 20m)]));
        Assert.False(badItem.IsValid);

        var badRate = await validator.ValidateAsync(new CreateSaleRequest(
            null, null, today, null, null, [new SaleItemRequest(productId, 1m, 10m, 150m, 20m)]));
        Assert.False(badRate.IsValid);

        var overdue = await validator.ValidateAsync(new CreateSaleRequest(
            null, null, today, today.AddDays(-1), null,
            [new SaleItemRequest(productId, 1m, 10m, 0m, 20m)]));
        Assert.False(overdue.IsValid);
    }

    public void Dispose() => _app.Dispose();
}
