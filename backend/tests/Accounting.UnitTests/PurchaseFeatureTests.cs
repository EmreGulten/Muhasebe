using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Parties;
using Accounting.Application.Features.Products;
using Accounting.Application.Features.Purchases;
using Accounting.Contracts.Parties;
using Accounting.Contracts.Products;
using Accounting.Contracts.Purchases;
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
/// PHASE 5 alış özelliği: stok girişi + tedarikçi borcu + kasadan ödeme
/// zinciri, iptal ters hareketleri, ödeme aşırı reddi, tenant izolasyonu
/// ve izin matrisi (muhasebe.md bölüm 7, 23, 24).
/// </summary>
public sealed class PurchaseFeatureTests : IDisposable
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

    private static CreatePartyRequest NewSupplier(string name, string type = "Supplier") =>
        new(name, type, null, null, null, null, null, null, null, null, 0m, 0m, null);

    private static async Task<WarehouseDto> GetDefaultWarehouseAsync(IServiceScope scope)
    {
        var warehouses = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>()
            .HandleAsync(default);
        return warehouses.First(w => w.IsDefault);
    }

    private static async Task<Guid> NewProductAsync(
        IServiceScope scope, string name, bool isService = false, decimal salePrice = 100m)
    {
        var product = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewProduct(name, isService, salePrice), default);
        return product.Id;
    }

    private static CreatePurchaseRequest NewPurchase(
        Guid? partyId, Guid? warehouseId, params PurchaseItemRequest[] items) =>
        new(partyId, warehouseId, DateTime.UtcNow.Date, null, null, items);

    private static PurchaseItemRequest Item(Guid productId, decimal quantity, decimal price,
        decimal discount = 0m, decimal vat = 20m) =>
        new(productId, quantity, price, discount, vat);

    // ---- Hesaplamalar ve belge oluşumu

    [Fact]
    public async Task CreatePurchase_ComputesTotals_WithDiscountAndVat()
    {
        using var scope = await CreateOwnerScopeAsync("alis-hesap@test.local");
        var goods = await NewProductAsync(scope, "Kalem");
        var service = await NewProductAsync(scope, "Montaj", isService: true, salePrice: 500m);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(NewPurchase(null, warehouse.Id,
                Item(goods, 3m, 60m, discount: 10m, vat: 20m),
                Item(service, 1m, 400m)), default);

        // Kalem 1: brüt 180 → iskonto %10 → net 162; KDV 32.40; toplam 194.40.
        // Kalem 2: net 400; KDV 80; toplam 480.
        Assert.Equal("P-000001", purchase.Number);
        Assert.Equal(PurchaseStatus.Draft.ToString(), purchase.Status);
        Assert.Equal(562m, purchase.SubTotal);
        Assert.Equal(18m, purchase.DiscountTotal);
        Assert.Equal(112.40m, purchase.VatTotal);
        Assert.Equal(674.40m, purchase.Total);
        Assert.Equal(674.40m, purchase.DueAmount);
        Assert.Equal(2, purchase.Items.Count);
    }

    [Fact]
    public async Task CreatePurchase_SecondPurchaseGetsNextNumber()
    {
        using var scope = await CreateOwnerScopeAsync("alis-seri@test.local");
        var goods = await NewProductAsync(scope, "Defter");
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var handler = scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>();

        var first = await handler.HandleAsync(NewPurchase(null, warehouse.Id, Item(goods, 1m, 10m)), default);
        var second = await handler.HandleAsync(NewPurchase(null, warehouse.Id, Item(goods, 1m, 10m)), default);

        Assert.Equal("P-000001", first.Number);
        Assert.Equal("P-000002", second.Number);
    }

    [Fact]
    public async Task CreatePurchase_RejectsCustomerParty()
    {
        using var scope = await CreateOwnerScopeAsync("alis-musteri@test.local");
        var goods = await NewProductAsync(scope, "Silgi");
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var customer = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewSupplier("Müşteri", "Customer"), default);

        await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
                .HandleAsync(NewPurchase(customer.Id, warehouse.Id, Item(goods, 1m, 10m)), default));
    }

    // ---- Onay

    [Fact]
    public async Task ConfirmPurchase_AddsStock_SupplierDebt_AndStaysAtomic()
    {
        using var scope = await CreateOwnerScopeAsync("alis-onay@test.local");
        var goods = await NewProductAsync(scope, "Kalem");
        var service = await NewProductAsync(scope, "Montaj", isService: true, salePrice: 500m);
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var supplier = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewSupplier("Ted"), default);

        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(NewPurchase(supplier.Id, warehouse.Id,
                Item(goods, 3m, 100m, discount: 10m), Item(service, 1m, 500m)), default);

        var confirmed = await scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
            .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(null), default);

        Assert.Equal(PurchaseStatus.Confirmed.ToString(), confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAtUtc);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Purchases.First(p => p.Id == purchase.Id).TenantId;

        // Stok: yalnızca mal kalemi girer (+3); hizmet stoğa girmez.
        var goodsStock = await db.InventoryTransactions
            .Where(t => t.TenantId == tenantId && t.ProductId == goods)
            .SumAsync(t => t.Quantity);
        Assert.Equal(3m, goodsStock);
        Assert.Equal(0, await db.InventoryTransactions
            .CountAsync(t => t.TenantId == tenantId && t.ProductId == service));

        // Cari: 924 alacak (biz borçluyuz), Referans Purchase.
        var debt = await db.PartyTransactions.SingleAsync(t =>
            t.TenantId == tenantId && t.PartyId == supplier.Id && t.Type == PartyTransactionType.Purchase);
        Assert.Equal(924m, debt.Credit);
        Assert.Equal("Purchase", debt.ReferenceType);

        // Onaylı belge tekrar onaylanamaz, değiştirilemez, silinemez.
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
                .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(null), default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<UpdatePurchaseHandler>()
                .HandleAsync(purchase.Id, new UpdatePurchaseRequest(null, warehouse.Id, DateTime.UtcNow.Date, null, null,
                    [Item(goods, 1m, 10m)]), default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<DeletePurchaseHandler>().HandleAsync(purchase.Id, default));
    }

    [Fact]
    public async Task ConfirmPurchase_WithoutParty_WritesNoPartyTransaction()
    {
        using var scope = await CreateOwnerScopeAsync("alis-nakit@test.local");
        var goods = await NewProductAsync(scope, "Kalem");
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(NewPurchase(null, warehouse.Id, Item(goods, 2m, 50m, vat: 0m)), default);

        var confirmed = await scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
            .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(null), default);

        Assert.Equal(PurchaseStatus.Confirmed.ToString(), confirmed.Status);
        Assert.Null(confirmed.PartyName);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Purchases.First(p => p.Id == purchase.Id).TenantId;
        Assert.Equal(0, await db.PartyTransactions.CountAsync(t => t.TenantId == tenantId));
        Assert.Equal(2m, await db.InventoryTransactions
            .Where(t => t.TenantId == tenantId && t.ProductId == goods)
            .SumAsync(t => t.Quantity));
    }

    [Fact]
    public async Task ConfirmPurchase_WithPayment_WritesCashOutAndPartyDebit()
    {
        using var scope = await CreateOwnerScopeAsync("alis-odeme@test.local");
        var goods = await NewProductAsync(scope, "Kalem");
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var supplier = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewSupplier("Ted"), default);

        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(NewPurchase(supplier.Id, warehouse.Id, Item(goods, 1m, 100m)), default); // 120,00 KDV %20

        var confirmed = await scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
            .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(
                new PurchaseConfirmPaymentRequest(DateTime.UtcNow.Date, 50m, "peşin")), default);

        Assert.Equal(PurchaseStatus.PartiallyPaid.ToString(), confirmed.Status);
        Assert.Equal(50m, confirmed.PaidAmount);
        Assert.Equal(70m, confirmed.DueAmount);
        Assert.Single(confirmed.Payments);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Purchases.First(p => p.Id == purchase.Id).TenantId;

        // "Kasa" hesabı ilk ödemede oluşur; ödeme kasadan ÇIKIŞ — negatif işaretli.
        var account = await db.Accounts.SingleAsync(a => a.TenantId == tenantId);
        Assert.Equal("Kasa", account.Name);
        var cash = await db.AccountTransactions.SingleAsync(t => t.TenantId == tenantId);
        Assert.Equal(-50m, cash.Amount);
        Assert.Equal(AccountTransactionType.PurchasePayment, cash.Type);

        // Tedarikçi ödemesi cari borcu düşürür (borç hareketi).
        var debit = await db.PartyTransactions.SingleAsync(t =>
            t.TenantId == tenantId && t.PartyId == supplier.Id && t.Type == PartyTransactionType.Payment);
        Assert.Equal(50m, debit.Debit);

        // Aşırı ödeme reddedilir (kalan 70).
        await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<AddPurchasePaymentHandler>()
                .HandleAsync(purchase.Id, new AddPurchasePaymentRequest(DateTime.UtcNow.Date, 71m, null), default));

        // Kalanı öde → Paid.
        var paid = await scope.ServiceProvider.GetRequiredService<AddPurchasePaymentHandler>()
            .HandleAsync(purchase.Id, new AddPurchasePaymentRequest(DateTime.UtcNow.Date, 70m, null), default);
        Assert.Equal(PurchaseStatus.Paid.ToString(), paid.Status);
        Assert.Equal(0m, paid.DueAmount);
    }

    // ---- İptal

    [Fact]
    public async Task CancelPurchase_WritesReverseMovements()
    {
        using var scope = await CreateOwnerScopeAsync("alis-iptal@test.local");
        var goods = await NewProductAsync(scope, "Kalem");
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var supplier = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewSupplier("Ted"), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>();
        var purchase = await handler.HandleAsync(
            NewPurchase(supplier.Id, warehouse.Id, Item(goods, 4m, 100m)), default); // 480,00

        await scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
            .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(
                new PurchaseConfirmPaymentRequest(DateTime.UtcNow.Date, 200m, null)), default);

        var cancelled = await scope.ServiceProvider.GetRequiredService<CancelPurchaseHandler>()
            .HandleAsync(purchase.Id, new CancelPurchaseRequest("fatura iptal edildi"), default);

        Assert.Equal(PurchaseStatus.Cancelled.ToString(), cancelled.Status);
        Assert.Equal("fatura iptal edildi", cancelled.CancelReason);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = db.Purchases.First(p => p.Id == purchase.Id).TenantId;

        // Stok geri düşülür: +4 − 4 = 0.
        var stock = await db.InventoryTransactions
            .Where(t => t.TenantId == tenantId && t.ProductId == goods)
            .SumAsync(t => t.Quantity);
        Assert.Equal(0m, stock);

        // Cari denge: alacak (480 alış + 200 ödeme iadesi) − borç (200 ödeme + 480 iptal) = 0.
        var balance = await db.PartyTransactions
            .Where(t => t.TenantId == tenantId && t.PartyId == supplier.Id)
            .SumAsync(t => t.Debit - t.Credit);
        Assert.Equal(0m, balance);
        Assert.True(await db.PartyTransactions.AnyAsync(t =>
            t.TenantId == tenantId && t.PartyId == supplier.Id
            && t.ReferenceType == "PurchaseCancel" && t.Debit == 480m));

        // Kasa denge: −200 ödeme + 200 iade = 0.
        var cash = await db.AccountTransactions
            .Where(t => t.TenantId == tenantId)
            .SumAsync(t => t.Amount);
        Assert.Equal(0m, cash);
        Assert.True(await db.AccountTransactions.AnyAsync(t =>
            t.TenantId == tenantId && t.ReferenceType == "PurchaseCancel" && t.Amount == 200m));

        // İptal terminal: tekrar iptal ve ödeme reddedilir.
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<CancelPurchaseHandler>()
                .HandleAsync(purchase.Id, new CancelPurchaseRequest("tekrar"), default));
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<AddPurchasePaymentHandler>()
                .HandleAsync(purchase.Id, new AddPurchasePaymentRequest(DateTime.UtcNow.Date, 10m, null), default));
    }

    [Fact]
    public async Task CancelPurchase_RejectsDraft()
    {
        using var scope = await CreateOwnerScopeAsync("alis-taslak-iptal@test.local");
        var goods = await NewProductAsync(scope, "Kalem");
        var warehouse = await GetDefaultWarehouseAsync(scope);

        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(NewPurchase(null, warehouse.Id, Item(goods, 1m, 10m)), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<CancelPurchaseHandler>()
                .HandleAsync(purchase.Id, new CancelPurchaseRequest("taslak"), default));

        // Taslak silinir.
        await scope.ServiceProvider.GetRequiredService<DeletePurchaseHandler>().HandleAsync(purchase.Id, default);
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task Purchases_Are_Isolated_Between_Tenants()
    {
        using var scopeA = await CreateOwnerScopeAsync("alis-a@test.local");
        var goodsA = await NewProductAsync(scopeA, "A Ürünü");
        var warehouseA = await GetDefaultWarehouseAsync(scopeA);
        var purchaseInA = await scopeA.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(NewPurchase(null, warehouseA.Id, Item(goodsA, 1m, 10m)), default);

        using var scopeB = await CreateOwnerScopeAsync("alis-b@test.local");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scopeB.ServiceProvider.GetRequiredService<GetPurchaseHandler>().HandleAsync(purchaseInA.Id, default));

        var listInB = await scopeB.ServiceProvider.GetRequiredService<ListPurchasesHandler>()
            .HandleAsync(null, null, null, 1, 20, default);
        Assert.DoesNotContain(listInB.Items, p => p.Id == purchaseInA.Id);
    }

    // ---- İzin matrisi ve doğrulama

    [Fact]
    public void Purchases_PermissionMatrix()
    {
        // Çalışan satış yapar ama alış yapamaz; muhasebeci alış girer.
        var employee = RolePermissions.For(TenantRole.Employee);
        Assert.Contains(Permissions.PurchasesView, employee);
        Assert.DoesNotContain(Permissions.PurchasesCreate, employee);
        Assert.DoesNotContain(Permissions.PurchasesEdit, employee);

        var accountant = RolePermissions.For(TenantRole.Accountant);
        Assert.Contains(Permissions.PurchasesCreate, accountant);
        Assert.Contains(Permissions.PurchasesEdit, accountant);
        Assert.DoesNotContain(Permissions.PurchasesDelete, accountant);

        var viewer = RolePermissions.For(TenantRole.Viewer);
        Assert.Contains(Permissions.PurchasesView, viewer);
        Assert.DoesNotContain(Permissions.PurchasesCreate, viewer);
    }

    [Fact]
    public async Task PurchaseValidator_RejectsInvalidInput()
    {
        using var scope = _app.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CreatePurchaseRequest>>();
        var productId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var emptyItems = await validator.ValidateAsync(new CreatePurchaseRequest(
            null, null, today, null, null, []));
        Assert.False(emptyItems.IsValid);

        var badItem = await validator.ValidateAsync(new CreatePurchaseRequest(
            null, null, today, null, null, [new PurchaseItemRequest(productId, 0m, 10m, 0m, 20m)]));
        Assert.False(badItem.IsValid);

        var badRate = await validator.ValidateAsync(new CreatePurchaseRequest(
            null, null, today, null, null, [new PurchaseItemRequest(productId, 1m, 10m, 150m, 20m)]));
        Assert.False(badRate.IsValid);

        var overdue = await validator.ValidateAsync(new CreatePurchaseRequest(
            null, null, today, today.AddDays(-1), null,
            [new PurchaseItemRequest(productId, 1m, 10m, 0m, 20m)]));
        Assert.False(overdue.IsValid);
    }

    public void Dispose() => _app.Dispose();
}
