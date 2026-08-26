using System.Security.Claims;
using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Assistant;
using Accounting.Application.Features.Parties;
using Accounting.Application.Features.Products;
using Accounting.Application.Features.Purchases;
using Accounting.Application.Features.Sales;
using Accounting.Application.Features.Subscriptions;
using Accounting.Contracts.Parties;
using Accounting.Contracts.Products;
using Accounting.Contracts.Purchases;
using Accounting.Contracts.Subscription;
using Accounting.Domain.Authorization;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.MultiTenancy;
using Accounting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Accounting.UnitTests;

/// <summary>
/// abonelik sistemi: plan kataloğu,
/// kayıtta deneme, feature guard (stok / alış / AI), depo kotası, plan
/// değiştirme ve dönemi biten aboneliğin core'a düşmesi.
/// </summary>
public sealed class SubscriptionFeatureTests : IDisposable
{
    private readonly TestApp _app = new();

    private sealed class OwnerScope : IDisposable
    {
        public required IServiceScope Scope { get; init; }
        public required Guid TenantId { get; init; }
        public required Guid UserId { get; init; }
        public required IHttpContextAccessor Accessor { get; init; }

        /// <summary>
        /// AskAssistantHandler kullanıcıyı HttpContext'ten okur; AsyncLocal
        /// atama test metodunun senkron çerçevesinde yapılmalı (bkz.
        /// AssistantFeatureTests).
        /// </summary>
        public void Activate() => Accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())])),
        };

        public void Dispose() => Scope.Dispose();
    }

    private async Task<OwnerScope> CreateOwnerScopeAsync(string email)
    {
        var user = await _app.RegisterUserAsync(email: email);
        var scope = _app.CreateScope();
        var tenantId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserTenants
            .Where(m => m.UserId == user.Id)
            .Select(m => m.TenantId)
            .FirstAsync();
        scope.ServiceProvider.GetRequiredService<ITenantContextWriter>()
            .SetTenant(tenantId, TenantRole.Owner);
        return new()
        {
            Scope = scope,
            TenantId = tenantId,
            UserId = user.Id,
            Accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
        };
    }

    public void Dispose() => _app.Dispose();

    private static CreatePartyRequest NewSupplier(string name) =>
        new(name, "Supplier", null, null, null, null, null, null, null, null, 0m, 0m, null);

    private static CreateProductRequest NewGoods(string name) =>
        new(name, null, null, null, null, null, 40m, 100m, 20m, 0m, false);

    private static Task<SubscriptionResponse> ChangePlanAsync(OwnerScope owner, string planCode) =>
        owner.Scope.ServiceProvider.GetRequiredService<ChangePlanHandler>()
            .HandleAsync(new ChangePlanRequest(planCode), default);

    // ---- Plan kataloğu + kayıt denemesi

    [Fact]
    public async Task Plans_AreSeeded_FromPlanSection29()
    {
        using var owner = await CreateOwnerScopeAsync("plan-katalog@test.local");

        var plans = await owner.Scope.ServiceProvider.GetRequiredService<ListSubscriptionPlansHandler>()
            .HandleAsync(default);

        Assert.Equal(3, plans.Count);
        Assert.Equal("starter", plans[0].Code);
        Assert.Equal(199m, plans[0].MonthlyPrice);
        Assert.Equal(1, plans[0].MaxWarehouses);
        Assert.Equal("pro", plans[1].Code);
        Assert.Equal(349m, plans[1].MonthlyPrice);
        Assert.Contains(PlanFeatures.AiAssistant, plans[1].Features);
        Assert.Contains(PlanFeatures.Stock, plans[1].Features);
        Assert.Equal("business", plans[2].Code);
        Assert.Equal(-1, plans[2].MaxWarehouses); // sınırsız depo
        Assert.Contains(PlanFeatures.MultiWarehouse, plans[2].Features);
    }

    [Fact]
    public async Task Registration_OpensProTrial_WithFullFeatures()
    {
        using var owner = await CreateOwnerScopeAsync("plan-deneme@test.local");

        var subscription = await owner.Scope.ServiceProvider.GetRequiredService<GetSubscriptionHandler>()
            .HandleAsync(default);

        Assert.Equal("pro", subscription.Plan.Code);
        Assert.Equal("Trialing", subscription.Status);
        Assert.True(subscription.IsActive);
        Assert.True(subscription.IsTrial);
        Assert.NotNull(subscription.TrialEndsAtUtc);
        Assert.Equal(14, subscription.DaysRemaining);
        Assert.Contains(PlanFeatures.Core, subscription.EffectiveFeatures);
        Assert.Contains(PlanFeatures.Stock, subscription.EffectiveFeatures);
        Assert.Contains(PlanFeatures.AiAssistant, subscription.EffectiveFeatures);
    }

    // ---- Feature guard: stok / alış / AI

    [Fact]
    public async Task FeatureGuard_BlocksStokPurchaseAndAi_OnStarterPlan()
    {
        using var owner = await CreateOwnerScopeAsync("plan-koruma@test.local");
        owner.Activate();
        var scope = owner.Scope;
        await ChangePlanAsync(owner, "starter");

        var goods = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewGoods("Defter"), default);

        // Stok hareketi kapalı.
        var warehouses = await scope.ServiceProvider.GetRequiredService<
            Accounting.Application.Features.Products.ListWarehousesHandler>()
            .HandleAsync(default);
        var warehouseId = warehouses.First(w => w.IsDefault).Id;
        var stockEx = await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>()
                .HandleAsync(new CreateInventoryTransactionRequest(
                    goods.Id, warehouseId, "ManualIn", DateTime.UtcNow, 5m, "test"), default));
        Assert.Equal(403, stockEx.StatusCode);

        // Alış belgesi kapalı.
        var purchaseEx = await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
                .HandleAsync(new CreatePurchaseRequest(
                    null, warehouseId, DateTime.UtcNow, null, null,
                    [new PurchaseItemRequest(goods.Id, 1m, 40m, 0m, 0m)]), default));
        Assert.Equal(403, purchaseEx.StatusCode);

        // AI asistan kapalı.
        var aiEx = await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<AskAssistantHandler>()
                .HandleAsync(new Contracts.Assistant.AskAssistantRequest("Bu ay ne kadar kazandım?"), default));
        Assert.Equal(403, aiEx.StatusCode);
        Assert.Contains("planınızda", aiEx.Message);

        // Temel akış yaşar: satış belgesi açılabilir (Draft).
        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(new Contracts.Sales.CreateSaleRequest(
                null, null, DateTime.UtcNow, null, null,
                [new Contracts.Sales.SaleItemRequest(goods.Id, 1m, 100m, 0m, 20m)]), default);
        Assert.NotNull(sale);
    }

    [Fact]
    public async Task FeatureGuard_AllowsEverything_OnProTrial()
    {
        using var owner = await CreateOwnerScopeAsync("plan-pro@test.local");
        var scope = owner.Scope;

        var goods = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewGoods("Defter"), default);
        var warehouses = await scope.ServiceProvider.GetRequiredService<
            Accounting.Application.Features.Products.ListWarehousesHandler>()
            .HandleAsync(default);
        var warehouseId = warehouses.First(w => w.IsDefault).Id;

        // Deneme = tam Pro: stok hareketi ve alış serbest.
        var movement = await scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>()
            .HandleAsync(new CreateInventoryTransactionRequest(
                goods.Id, warehouseId, "ManualIn", DateTime.UtcNow, 5m, "test"), default);
        Assert.Equal(5m, movement.Quantity);

        var supplier = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewSupplier("Ted Ltd"), default);
        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(new CreatePurchaseRequest(
                supplier.Id, warehouseId, DateTime.UtcNow, null, null,
                [new PurchaseItemRequest(goods.Id, 1m, 40m, 0m, 0m)]), default);
        Assert.Equal("Draft", purchase.Status);
    }

    // ---- Deneme süresi bitince

    [Fact]
    public async Task ExpiredTrial_FallsBackToCore()
    {
        using var owner = await CreateOwnerScopeAsync("plan-sure@test.local");
        owner.Activate();
        var scope = owner.Scope;

        // Deneme aboneliğini geçmiş döneme çek.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.Subscriptions.SingleAsync(s => s.TenantId == owner.TenantId);
        subscription.CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(-1);
        subscription.TrialEndsAtUtc = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var resolved = await scope.ServiceProvider.GetRequiredService<GetSubscriptionHandler>()
            .HandleAsync(default);

        Assert.Equal("Expired", resolved.Status);
        Assert.False(resolved.IsActive);
        Assert.Equal(["core"], resolved.EffectiveFeatures.Order().ToArray());

        // AI artık kapalı; temel cari yönetimi yaşar.
        var aiEx = await Assert.ThrowsAsync<AppException>(() =>
            scope.ServiceProvider.GetRequiredService<AskAssistantHandler>()
                .HandleAsync(new Contracts.Assistant.AskAssistantRequest("Bu ay ne kadar kazandım?"), default));
        Assert.Equal(403, aiEx.StatusCode);

        var party = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(new CreatePartyRequest("Ayşe", "Customer", null, null, null, null, null, null, null, null, 0m, 0m, null), default);
        Assert.NotNull(party);
    }

    // ---- Depo kotası

    [Fact]
    public async Task WarehouseQuota_LimitsStarter_BusinessUnlimited()
    {
        using var owner = await CreateOwnerScopeAsync("plan-depo@test.local");
        var scope = owner.Scope;
        var handler = scope.ServiceProvider.GetRequiredService<CreateWarehouseHandler>();

        // Varsayılan depo lazy oluşur (1 depo). Başlangıç planında 2. depo reddi.
        var first = await handler.HandleAsync(new CreateWarehouseRequest("Ana Depo", null, true), default);
        await ChangePlanAsync(owner, "starter");
        var starterEx = await Assert.ThrowsAsync<AppException>(() =>
            handler.HandleAsync(new CreateWarehouseRequest("İkinci Depo", null, false), default));
        Assert.Equal(403, starterEx.StatusCode);

        // İşletme planı: sınırsız depo.
        await ChangePlanAsync(owner, "business");
        var second = await handler.HandleAsync(new CreateWarehouseRequest("Şube Depo", null, false), default);
        Assert.NotNull(second);
    }

    // ---- Plan değiştirme

    [Fact]
    public async Task ChangePlan_OpensNewActivePeriod_AndClearsTrial()
    {
        using var owner = await CreateOwnerScopeAsync("plan-degisim@test.local");

        var changed = await owner.Scope.ServiceProvider.GetRequiredService<ChangePlanHandler>()
            .HandleAsync(new ChangePlanRequest("starter"), default);

        Assert.Equal("starter", changed.Plan.Code);
        Assert.Equal("Active", changed.Status);
        Assert.False(changed.IsTrial);
        Assert.Null(changed.TrialEndsAtUtc);
        Assert.Equal(30, changed.DaysRemaining);
        Assert.Equal(["core"], changed.EffectiveFeatures.Order().ToArray());

        // Geçersiz plan kodu.
        await Assert.ThrowsAsync<AppException>(() =>
            owner.Scope.ServiceProvider.GetRequiredService<ChangePlanHandler>()
                .HandleAsync(new ChangePlanRequest("yok-boyle-plan"), default));

        // Boş plan kodu doğrulamaya takılır.
        var emptyEx = await Assert.ThrowsAsync<AppException>(() =>
            owner.Scope.ServiceProvider.GetRequiredService<ChangePlanHandler>()
                .HandleAsync(new ChangePlanRequest("  "), default));
        Assert.Equal(400, emptyEx.StatusCode);
    }

    // ---- İzin: Tenant.Manage yalnız Owner'da

    [Fact]
    public void RolePermissions_PlanChange_ForOwnerOnly()
    {
        Assert.Contains(Permissions.TenantManage, RolePermissions.For(TenantRole.Owner));
        Assert.DoesNotContain(Permissions.TenantManage, RolePermissions.For(TenantRole.Admin));
        Assert.DoesNotContain(Permissions.TenantManage, RolePermissions.For(TenantRole.Accountant));
    }
}
