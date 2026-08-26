using System.Security.Claims;
using System.Text.Json;
using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Assistant;
using Accounting.Application.Features.IncomeExpenses;
using Accounting.Application.Features.Parties;
using Accounting.Application.Features.Products;
using Accounting.Application.Features.Purchases;
using Accounting.Application.Features.Sales;
using Accounting.Contracts.IncomeExpenses;
using Accounting.Contracts.Parties;
using Accounting.Contracts.Products;
using Accounting.Contracts.Purchases;
using Accounting.Contracts.Sales;
using Accounting.Domain.Authorization;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.MultiTenancy;
using Accounting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Accounting.UnitTests;

/// <summary>
/// AI asistanı: sekiz onaylı aracın gerçek
/// hareket verisiyle doğruluğu, offline sağlayıcı akışı, sohbet geçmişi,
/// aylık kullanım limiti, tenant izolasyonu, izin matrisi ve validasyon.
/// AI hiçbir zaman SQL üretmez — yalnızca bu araçlar çalışır.
/// </summary>
public sealed class AssistantFeatureTests : IDisposable
{
    private readonly TestApp _app = new();

    // ---- Test altyapısı

    private sealed class OwnerScope : IDisposable
    {
        public required IServiceScope Scope { get; init; }
        public required Guid UserId { get; init; }
        public required IHttpContextAccessor Accessor { get; init; }

        /// <summary>
        /// AskAssistantHandler soruyu soran kullanıcıyı HttpContext'ten okur.
        /// HttpContextAccessor AsyncLocal kullandığı için atama, test metodunun
        /// senkron çerçevesinde yapılmalı — async helper içinden geri akmaz.
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
            UserId = user.Id,
            Accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
        };
    }

    public void Dispose() => _app.Dispose();

    private static CreateProductRequest NewProduct(
        string name, decimal purchasePrice = 40m, decimal salePrice = 100m,
        decimal minimumStock = 0m, bool isService = false) =>
        new(name, null, null, null, null, null, purchasePrice, salePrice, 20m, minimumStock, isService);

    private static CreatePartyRequest NewParty(
        string name, string type = "Customer", decimal openingBalance = 0m) =>
        new(name, type, null, null, null, null, null, null, null, null, openingBalance, 0m, null);

    private static async Task<WarehouseDto> GetDefaultWarehouseAsync(IServiceScope scope)
    {
        var warehouses = await scope.ServiceProvider.GetRequiredService<
            Accounting.Application.Features.Products.ListWarehousesHandler>()
            .HandleAsync(default);
        return warehouses.First(w => w.IsDefault);
    }

    private static async Task<Guid> SeededProductAsync(
        IServiceScope scope, string name, decimal stock,
        decimal minimumStock = 0m, bool isService = false)
    {
        var product = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewProduct(name, minimumStock: minimumStock, isService: isService), default);

        if (stock != 0m)
        {
            var warehouse = await GetDefaultWarehouseAsync(scope);
            await scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>()
                .HandleAsync(new CreateInventoryTransactionRequest(
                    product.Id, warehouse.Id, "ManualIn", DateTime.UtcNow, stock, "test stoğu"), default);
        }

        return product.Id;
    }

    private static SaleItemRequest Item(Guid productId, decimal quantity, decimal price, decimal vat = 0m) =>
        new(productId, quantity, price, 0m, vat);

    /// <summary>Onaylı satış üretir; dueDate verilirse vadeli satılır (ödenmez).</summary>
    private static async Task<SaleResponse> ConfirmedSaleAsync(
        IServiceScope scope, Guid? partyId, Guid productId,
        decimal quantity, decimal price, DateTime date, DateTime? dueDate = null, decimal vat = 0m)
    {
        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(new CreateSaleRequest(partyId, null, date, dueDate, null,
                [Item(productId, quantity, price, vat)]), default);
        return await scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
            .HandleAsync(sale.Id, new ConfirmSaleRequest(null), default);
    }

    /// <summary>Onaylı alış üretir; ödeme eklenmez → kalan tutar vardır.</summary>
    private static async Task<PurchaseResponse> ConfirmedPurchaseAsync(
        IServiceScope scope, Guid partyId, Guid productId,
        decimal quantity, decimal price, DateTime date, DateTime? dueDate = null, decimal vat = 0m)
    {
        var warehouse = await GetDefaultWarehouseAsync(scope);
        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(new CreatePurchaseRequest(partyId, warehouse.Id, date, dueDate, null,
                [new PurchaseItemRequest(productId, quantity, price, 0m, vat)]), default);
        return await scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
            .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(null), default);
    }

    private static async Task<IncomeExpenseRecordDto> RecordAsync(
        IServiceScope scope, string type, string categoryName, decimal amount, DateTime date)
    {
        var categories = await scope.ServiceProvider.GetRequiredService<ListIncomeExpenseCategoriesHandler>()
            .HandleAsync(type, default);
        var categoryId = categories.First(c => c.Name == categoryName).Id;
        return await scope.ServiceProvider.GetRequiredService<CreateIncomeExpenseRecordHandler>()
            .HandleAsync(new CreateIncomeExpenseRecordRequest(
                type, categoryId, amount, date, null, null, null), default);
    }

    /// <summary>Kayıt defterinden adıyla araç çalıştırır (tenant bağlamından).</summary>
    private static async Task<JsonElement> RunToolAsync(
        IServiceScope scope, string name, string argumentsJson = "{}")
    {
        var tool = scope.ServiceProvider.GetRequiredService<IEnumerable<IAiTool>>()
            .Single(t => t.Name == name);
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;
        using var document = JsonDocument.Parse(argumentsJson);
        return await tool.ExecuteAsync(tenantId, document.RootElement.Clone(), default);
    }

    private static Task<Contracts.Assistant.AskAssistantResponse> AskAsync(
        IServiceScope scope, string question) =>
        scope.ServiceProvider.GetRequiredService<AskAssistantHandler>()
            .HandleAsync(new Contracts.Assistant.AskAssistantRequest(question), default);

    // ---- Araç: get_monthly_profit

    [Fact]
    public async Task MonthlyProfitTool_ComputesMonthTotals_AndIgnoresOtherMonths()
    {
        using var owner = await CreateOwnerScopeAsync("ai-profit@test.local");
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;
        var lastMonth = today.AddMonths(-1);

        await RecordAsync(scope, "Income", "Hizmet", 600m, today);
        await RecordAsync(scope, "Expense", "Kira", 50m, today);
        await RecordAsync(scope, "Expense", "Elektrik", 999m, lastMonth);

        var current = await RunToolAsync(scope, "get_monthly_profit");
        Assert.Equal(600m, current.GetProperty("income").GetDecimal());
        Assert.Equal(50m, current.GetProperty("expense").GetDecimal());
        Assert.Equal(550m, current.GetProperty("net").GetDecimal());
        Assert.Contains("550,00 TL", current.GetProperty("summary").GetString());

        // Geçen ay sorgusu: yalnızca o ayın kayıtları.
        var previous = await RunToolAsync(scope, "get_monthly_profit",
            $$"""{"month":"{{lastMonth.Year:D4}}-{{lastMonth.Month:D2}}"}""");
        Assert.Equal(0m, previous.GetProperty("income").GetDecimal());
        Assert.Equal(999m, previous.GetProperty("expense").GetDecimal());
    }

    // ---- Araç: get_overdue_receivables

    [Fact]
    public async Task OverdueReceivablesTool_ListsOnlyOverdueConfirmedSales()
    {
        using var owner = await CreateOwnerScopeAsync("ai-overdue@test.local");
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;

        var ayse = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ayşe"), default);
        var mehmet = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Mehmet"), default);
        var product = await SeededProductAsync(scope, "Kitap", 10m);

        // Ayşe: vadesi dün geçen ödenmemiş satış → 100 gecikmiş.
        await ConfirmedSaleAsync(scope, ayse.Id, product, 1m, 100m,
            today.AddDays(-5), dueDate: today.AddDays(-1));
        // Mehmet: vadesi gelmemiş güncel satış → gecikmiş değil.
        await ConfirmedSaleAsync(scope, mehmet.Id, product, 1m, 100m, today);
        // Taslak belge hiç sayılmaz.
        await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(new CreateSaleRequest(ayse.Id, null, today, null, null,
                [Item(product, 1m, 100m)]), default);

        var result = await RunToolAsync(scope, "get_overdue_receivables");

        var item = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("Ayşe", item.GetProperty("customer").GetString());
        Assert.Equal(100m, item.GetProperty("amount").GetDecimal());
        Assert.Equal(100m, result.GetProperty("total").GetDecimal());
        Assert.Contains("Ayşe", result.GetProperty("summary").GetString());
    }

    // ---- Araç: get_top_products

    [Fact]
    public async Task TopProductsTool_RanksByQuantity_Last12MonthsConfirmedOnly()
    {
        using var owner = await CreateOwnerScopeAsync("ai-top@test.local");
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;

        var kitap = await SeededProductAsync(scope, "Kitap", 20m);
        var kalem = await SeededProductAsync(scope, "Kalem", 10m);

        await ConfirmedSaleAsync(scope, null, kitap, 5m, 100m, today);
        await ConfirmedSaleAsync(scope, null, kalem, 2m, 100m, today);
        // 13 ay önceki onaylı satış pencere dışı.
        await ConfirmedSaleAsync(scope, null, kitap, 7m, 100m, today.AddMonths(-13));
        // Taslak hiç sayılmaz.
        await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(new CreateSaleRequest(null, null, today, null, null,
                [Item(kitap, 10m, 100m)]), default);

        var result = await RunToolAsync(scope, "get_top_products");

        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Kitap", items[0].GetProperty("ProductName").GetString());
        Assert.Equal(5m, items[0].GetProperty("quantity").GetDecimal());
        Assert.Equal(500m, items[0].GetProperty("total").GetDecimal());
        Assert.Equal("Kalem", items[1].GetProperty("ProductName").GetString());
    }

    // ---- Araç: get_low_stock_products

    [Fact]
    public async Task LowStockProductsTool_ListsCriticalNonServiceProducts()
    {
        using var owner = await CreateOwnerScopeAsync("ai-stok@test.local");
        var scope = owner.Scope;

        await SeededProductAsync(scope, "Kalem", 2m, minimumStock: 5m); // kritik
        await SeededProductAsync(scope, "Defter", 10m); // eşiği yok → listeye girmez
        await SeededProductAsync(scope, "Silgi", 10m, minimumStock: 3m); // eşik üstü
        await SeededProductAsync(scope, "Danışmanlık", 0m, minimumStock: 5m, isService: true); // hizmet

        var result = await RunToolAsync(scope, "get_low_stock_products");

        var item = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("Kalem", item.GetProperty("Name").GetString());
        Assert.Equal(2m, item.GetProperty("Stock").GetDecimal());
        Assert.Equal(5m, item.GetProperty("MinimumStock").GetDecimal());
    }

    // ---- Araç: get_customer_balance

    [Fact]
    public async Task CustomerBalanceTool_SearchesByName_AndListsDebtors()
    {
        using var owner = await CreateOwnerScopeAsync("ai-bakiye@test.local");
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;

        var ayse = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ayşe Yılmaz", openingBalance: 200m), default);
        await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Mehmet"), default); // hareketi yok
        var product = await SeededProductAsync(scope, "Kitap", 5m);
        await ConfirmedSaleAsync(scope, ayse.Id, product, 1m, 100m, today); // 200 + 100

        // Ada göre arama: büyük/küçük harf duyarsız parça eşleşmesi.
        var byName = await RunToolAsync(scope, "get_customer_balance",
            """{"customerName":"ayşe yılmaz"}""");
        var matched = Assert.Single(byName.GetProperty("items").EnumerateArray());
        Assert.Equal("Ayşe Yılmaz", matched.GetProperty("customer").GetString());
        Assert.Equal(300m, matched.GetProperty("balance").GetDecimal());
        Assert.Contains("300,00 TL", byName.GetProperty("summary").GetString());

        // Bulunmayan ad.
        var missing = await RunToolAsync(scope, "get_customer_balance",
            """{"customerName":"cem"}""");
        Assert.Empty(missing.GetProperty("items").EnumerateArray());
        Assert.Contains("bulunamadı", missing.GetProperty("summary").GetString());

        // Adsız: yalnızca borçlu müşteriler.
        var debtors = await RunToolAsync(scope, "get_customer_balance");
        Assert.Single(debtors.GetProperty("items").EnumerateArray());
    }

    // ---- Araç: get_expense_breakdown

    [Fact]
    public async Task ExpenseBreakdownTool_GroupsByCategory_ForRequestedMonth()
    {
        using var owner = await CreateOwnerScopeAsync("ai-gider@test.local");
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;

        await RecordAsync(scope, "Income", "Hizmet", 600m, today); // gelirdir, döküme girmez
        await RecordAsync(scope, "Expense", "Kira", 50m, today);
        await RecordAsync(scope, "Expense", "Elektrik", 30m, today);

        var result = await RunToolAsync(scope, "get_expense_breakdown");

        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count); // tutar sırasıyla
        Assert.Equal("Kira", items[0].GetProperty("Category").GetString());
        Assert.Equal(50m, items[0].GetProperty("Total").GetDecimal());
        Assert.Equal("Elektrik", items[1].GetProperty("Category").GetString());
        Assert.Equal(80m, result.GetProperty("total").GetDecimal());

        // Boş ay.
        var empty = await RunToolAsync(scope, "get_expense_breakdown", """{"month":"2020-01"}""");
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Contains("gideri yok", empty.GetProperty("summary").GetString());
    }

    // ---- Araç: compare_months

    [Fact]
    public async Task CompareMonthsTool_ComputesBothMonths_AndChanges()
    {
        using var owner = await CreateOwnerScopeAsync("ai-kiyas@test.local");
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;

        await RecordAsync(scope, "Expense", "Kira", 40m, today.AddMonths(-1));
        await RecordAsync(scope, "Income", "Hizmet", 600m, today);
        await RecordAsync(scope, "Expense", "Kira", 50m, today);

        var result = await RunToolAsync(scope, "compare_months");

        Assert.Equal(600m, result.GetProperty("thisMonth").GetProperty("income").GetDecimal());
        Assert.Equal(50m, result.GetProperty("thisMonth").GetProperty("expense").GetDecimal());
        Assert.Equal(0m, result.GetProperty("lastMonth").GetProperty("income").GetDecimal());
        Assert.Equal(40m, result.GetProperty("lastMonth").GetProperty("expense").GetDecimal());

        var summary = result.GetProperty("summary").GetString();
        Assert.Contains("geçen ay sıfırdı", summary); // gelir 0 → 600
        Assert.Contains("%25 arttı", summary); // gider 40 → 50
    }

    // ---- Araç: get_upcoming_payments

    [Fact]
    public async Task UpcomingPaymentsTool_ListsUnpaidPurchasesInWindow()
    {
        using var owner = await CreateOwnerScopeAsync("ai-odeme@test.local");
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;

        var supplier = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ted Ltd", "Supplier"), default);
        var product = await SeededProductAsync(scope, "Defter", 5m);

        // Pencere içinde, ödenmemiş → listede.
        await ConfirmedPurchaseAsync(scope, supplier.Id, product, 1m, 100m,
            today, dueDate: today.AddDays(3));
        // Vadesi geçmiş → gecikmiş, bu araçta değil.
        await ConfirmedPurchaseAsync(scope, supplier.Id, product, 1m, 100m,
            today, dueDate: today.AddDays(-1));
        // Taslak → sayılmaz.
        var warehouse = await GetDefaultWarehouseAsync(scope);
        await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(new CreatePurchaseRequest(supplier.Id, warehouse.Id, today,
                today.AddDays(3), null,
                [new PurchaseItemRequest(product, 1m, 100m, 0m, 0m)]), default);

        var result = await RunToolAsync(scope, "get_upcoming_payments");

        var item = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("Ted Ltd", item.GetProperty("Supplier").GetString());
        Assert.Equal(today.AddDays(3), item.GetProperty("DueDate").GetDateTime());
        Assert.Equal(100m, item.GetProperty("Amount").GetDecimal());
        Assert.Equal(100m, result.GetProperty("total").GetDecimal());
        Assert.Contains("Ted Ltd", result.GetProperty("summary").GetString());
    }

    // ---- AskAssistant: offline akış

    [Fact]
    public async Task AskAssistant_OfflineProvider_AnswersWithRealNumbers()
    {
        using var owner = await CreateOwnerScopeAsync("ai-sohbet@test.local");
        owner.Activate();
        var scope = owner.Scope;
        var today = DateTime.UtcNow.Date;

        var ayse = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ayşe"), default);
        var product = await SeededProductAsync(scope, "Kitap", 5m);
        await ConfirmedSaleAsync(scope, ayse.Id, product, 1m, 100m,
            today.AddDays(-5), dueDate: today.AddDays(-1));

        var response = await AskAsync(scope, "Bana borcu olan müşterileri göster.");

        Assert.Equal("offline", response.Provider);
        Assert.Contains("100,00 TL", response.Answer); // aracın özetinden gerçek rakam
        Assert.Contains("Offline asistan", response.Answer);

        // Eşleşmeyen soru: yönlendirme metni.
        var guide = await AskAsync(scope, "Bugün hava nasıl?");
        Assert.Contains("sorabilirsiniz", guide.Answer);
    }

    // ---- AskAssistant: geçmiş

    [Fact]
    public async Task AskAssistant_PersistsHistory_NewestFirst()
    {
        using var owner = await CreateOwnerScopeAsync("ai-gecmis@test.local");
        owner.Activate();
        var scope = owner.Scope;

        await AskAsync(scope, "Bu ay ne kadar kazandım?");
        await AskAsync(scope, "Geçen aya göre giderim nasıl değişti?");

        var history = await scope.ServiceProvider.GetRequiredService<ListAssistantHistoryHandler>()
            .HandleAsync(1, 20, default);

        Assert.Equal(4, history.TotalCount); // soru + yanıt çiftleri
        Assert.Equal(4, history.Items.Count);
        Assert.Equal("Assistant", history.Items[0].Role); // en yeni önce
        Assert.Equal("User", history.Items[1].Role);
        Assert.Equal("Geçen aya göre giderim nasıl değişti?", history.Items[1].Content);
    }

    // ---- AskAssistant: kullanım limiti

    [Fact]
    public async Task AskAssistant_MonthlyLimitExceeded_Throws429()
    {
        using var owner = await CreateOwnerScopeAsync("ai-limit@test.local");
        owner.Activate();
        var scope = owner.Scope;

        // Kayıt Pro denemesi açar → plan limiti 100.
        // Bu ayın 99 kullanıcı sorusu tohumlanır: 100. soru geçer, 101. 429.
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;
        var now = DateTime.UtcNow;
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AiMessages.AddRange(Enumerable.Range(0, 99).Select(i => new Accounting.Domain.Entities.AiMessage
        {
            TenantId = tenantId,
            UserId = owner.UserId,
            Role = Accounting.Domain.Enums.AiMessageRole.User,
            Content = $"geçmiş soru {i}",
            CreatedAtUtc = now.AddMinutes(-i),
        }));
        await db.SaveChangesAsync();

        var handler = scope.ServiceProvider.GetRequiredService<AskAssistantHandler>();
        var request = new Contracts.Assistant.AskAssistantRequest("Bu ay ne kadar kazandım?");
        Assert.NotNull(await handler.HandleAsync(request, default));

        var excess = await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(request, default));
        Assert.Equal(429, excess.StatusCode);
        Assert.Contains("100", excess.Message);
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task Assistant_HistoryAndTools_AreIsolatedBetweenTenants()
    {
        using var ownerA = await CreateOwnerScopeAsync("ai-iso-a@test.local");
        ownerA.Activate();
        var today = DateTime.UtcNow.Date;

        var ayse = await ownerA.Scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ayşe", openingBalance: 300m), default);
        Assert.NotNull(ayse);

        var balanceA = await RunToolAsync(ownerA.Scope, "get_customer_balance");
        Assert.Single(balanceA.GetProperty("items").EnumerateArray());
        await AskAsync(ownerA.Scope, "Müşterilerim kimlerin bakiyesi var?");

        using var ownerB = await CreateOwnerScopeAsync("ai-iso-b@test.local");
        ownerB.Activate();

        // Aynı araç B'nin bağlamında boş döner; B'nin asistanı da borçlu görmez.
        var balanceB = await RunToolAsync(ownerB.Scope, "get_customer_balance");
        Assert.Empty(balanceB.GetProperty("items").EnumerateArray());

        var answerB = await AskAsync(ownerB.Scope, "Müşterilerim kimlerin bakiyesi var?");
        Assert.Contains("Borçlu müşteri yok", answerB.Answer);

        var historyB = await ownerB.Scope.ServiceProvider
            .GetRequiredService<ListAssistantHistoryHandler>()
            .HandleAsync(1, 20, default);
        Assert.Equal(2, historyB.TotalCount); // yalnızca B'nin kendi sorusu + yanıtı
    }

    // ---- İzinler + validasyon

    [Fact]
    public void RolePermissions_AiAssistantUse_ForOwnerAdminAccountantOnly()
    {
        foreach (var role in new[] { TenantRole.Owner, TenantRole.Admin, TenantRole.Accountant })
        {
            Assert.Contains(Permissions.AiAssistantUse, RolePermissions.For(role));
        }

        // Employee ve Viewer salt okunur kullanıcılar — asistan kullanamaz.
        foreach (var role in new[] { TenantRole.Employee, TenantRole.Viewer })
        {
            Assert.DoesNotContain(Permissions.AiAssistantUse, RolePermissions.For(role));
        }
    }

    [Fact]
    public void AskAssistantValidator_Rejects_EmptyAndOverlongQuestions()
    {
        var validator = new Accounting.Application.Validators.AskAssistantValidator();

        Assert.False(validator.Validate(new Contracts.Assistant.AskAssistantRequest("")).IsValid);
        Assert.False(validator.Validate(
            new Contracts.Assistant.AskAssistantRequest(new string('a', 501))).IsValid);
        Assert.True(validator.Validate(
            new Contracts.Assistant.AskAssistantRequest("Bu ay ne kadar kazandım?")).IsValid);
    }
}
