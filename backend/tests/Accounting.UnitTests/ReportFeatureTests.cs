using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Accounts;
using Accounting.Application.Features.IncomeExpenses;
using Accounting.Application.Features.Parties;
using Accounting.Application.Features.Products;
using Accounting.Application.Features.Reports;
using Accounting.Application.Features.Sales;
using Accounting.Contracts.Accounts;
using Accounting.Contracts.IncomeExpenses;
using Accounting.Contracts.Parties;
using Accounting.Contracts.Products;
using Accounting.Contracts.Reports;
using Accounting.Contracts.Sales;
using Accounting.Domain.Authorization;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.MultiTenancy;
using Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Accounting.UnitTests;

/// <summary>
/// raporları: dashboard 10 KPI + beş grafik, alacaklar,
/// stok ve satış raporları — gerçek hareket zincirinden üretilen
/// değerlerle, tenant izolasyonu ve izin matrisi dahil.
/// </summary>
public sealed class ReportFeatureTests : IDisposable
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

    public void Dispose() => _app.Dispose();

    private static CreateProductRequest NewProduct(
        string name, decimal purchasePrice = 40m, decimal salePrice = 100m,
        decimal minimumStock = 0m, bool isService = false) =>
        new(name, null, null, null, null, null, purchasePrice, salePrice, 20m, minimumStock, isService);

    private static CreatePartyRequest NewParty(
        string name, string type = "Customer", decimal openingBalance = 0m, string? phone = null) =>
        new(name, type, null, null, phone, null, null, null, null, null, openingBalance, 0m, null);

    private static async Task<WarehouseDto> GetDefaultWarehouseAsync(IServiceScope scope)
    {
        var warehouses = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>()
            .HandleAsync(default);
        return warehouses.First(w => w.IsDefault);
    }

    private static async Task<Guid> SeededProductAsync(
        IServiceScope scope, string name, decimal stock,
        decimal purchasePrice = 40m, decimal salePrice = 100m,
        decimal minimumStock = 0m, bool isService = false)
    {
        var product = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(NewProduct(name, purchasePrice, salePrice, minimumStock, isService), default);

        if (stock != 0m)
        {
            var warehouse = await GetDefaultWarehouseAsync(scope);
            await scope.ServiceProvider.GetRequiredService<CreateInventoryTransactionHandler>()
                .HandleAsync(new CreateInventoryTransactionRequest(
                    product.Id, warehouse.Id, "ManualIn", DateTime.UtcNow, stock, "test stoğu"), default);
        }

        return product.Id;
    }

    private static SaleItemRequest Item(Guid productId, decimal quantity, decimal price, decimal vat = 20m) =>
        new(productId, quantity, price, 0m, vat);

    /// <summary>Onaylı satış üretir; dueDate verilirse vadeli satılır (ödenmez).</summary>
    private static async Task<SaleResponse> ConfirmedSaleAsync(
        IServiceScope scope, Guid? partyId, Guid productId,
        decimal quantity, decimal price, DateTime date, DateTime? dueDate = null, decimal vat = 20m)
    {
        var sale = await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(new CreateSaleRequest(partyId, null, date, dueDate, null,
                [Item(productId, quantity, price, vat)]), default);
        return await scope.ServiceProvider.GetRequiredService<ConfirmSaleHandler>()
            .HandleAsync(sale.Id, new ConfirmSaleRequest(null), default);
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

    // ---- Dashboard

    [Fact]
    public async Task Dashboard_ComputesAllKpis_FromLedgerData()
    {
        using var scope = await CreateOwnerScopeAsync("rapor-kpi@test.local");
        var today = DateTime.UtcNow.Date;
        var fiveDaysAgo = today.AddDays(-5);

        // Cari: müşteri alacağı + tedarikçi borcu (negatif açılış = biz borçluyuz).
        var customer = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ayşe", openingBalance: 200m), default);
        await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ted Ltd", "Supplier", openingBalance: -300m), default);

        // Ürün: 10 adet stok, alış 40, eşik 5 → 6 satılınca kritiğe düşer.
        var product = await SeededProductAsync(scope, "Ürün A", 10m, minimumStock: 5m);

        // Bugünkü satış: 6 × 100 + %20 KDV = 720 (ödenmedi → alacak).
        await ConfirmedSaleAsync(scope, customer.Id, product, 6m, 100m, today);

        // Vadeli geçmiş satış: 1 × 100 (KDV'siz), vadesi dün → gecikmiş alacak.
        await ConfirmedSaleAsync(scope, customer.Id, product, 1m, 100m, fiveDaysAgo,
            dueDate: today.AddDays(-1), vat: 0m);

        // Bugün kasa hareketleri: +600 gelir (lazy Kasa), −50 gider.
        await RecordAsync(scope, "Income", "Hizmet", 600m, today);
        await RecordAsync(scope, "Expense", "Kira", 50m, today);

        // Banka hesabı: 1000 açılış.
        await scope.ServiceProvider.GetRequiredService<CreateAccountHandler>()
            .HandleAsync(new CreateAccountRequest("Banka", "Bank", null, 1000m), default);

        var dashboard = await scope.ServiceProvider.GetRequiredService<GetDashboardHandler>()
            .HandleAsync(default);

        // Geçmiş satış ay başına denk gelirse aylık ciroya girer; kiracı verisi
        // gerçek saatle üretilir, beklenti tarihe göre hesaplanır.
        var pastSaleInThisMonth = fiveDaysAgo.Month == today.Month && fiveDaysAgo.Year == today.Year;
        var monthlySales = 720m + (pastSaleInThisMonth ? 100m : 0m);

        Assert.Equal(720m, dashboard.DailySales);
        Assert.Equal(monthlySales, dashboard.MonthlySales);
        Assert.Equal(50m, dashboard.MonthlyExpense);
        Assert.Equal(monthlySales - 50m, dashboard.EstimatedNet);
        Assert.Equal(200m + 720m + 100m, dashboard.TotalReceivable); // açılış + iki belge
        Assert.Equal(300m, dashboard.TotalPayable);
        Assert.Equal(550m, dashboard.CashTotal); // +600 gelir − 50 gider
        Assert.Equal(1000m, dashboard.BankTotal);
        Assert.Equal(1, dashboard.CriticalStockCount); // 10 − 7 = 3 ≤ 5
        Assert.Equal(1, dashboard.OverdueReceivableCount);

        // Grafik 1 — son 30 gün: bugünün satırı ve uzunluk.
        Assert.Equal(30, dashboard.Last30DaysFlow.Count);
        var todayFlow = dashboard.Last30DaysFlow[^1];
        Assert.Equal(today, todayFlow.Date);
        Assert.Equal(600m, todayFlow.Income);
        Assert.Equal(50m, todayFlow.Expense);

        // Grafik 2 — son 12 ay ciro: bu ayın satırı ve liste uzunluğu.
        Assert.Equal(12, dashboard.Last12MonthsRevenue.Count);
        Assert.Equal(monthlySales, dashboard.Last12MonthsRevenue[^1].Total);
        Assert.Equal(today.Month, dashboard.Last12MonthsRevenue[^1].Month);

        // Grafik 3 + 4 — en çok satan / en kârlı: tek ürün, 7 adet.
        var top = Assert.Single(dashboard.TopSellingProducts);
        Assert.Equal("Ürün A", top.ProductName);
        Assert.Equal(7m, top.Quantity);
        Assert.Equal(820m, top.Total); // 720 (KDV'li) + 100

        var profit = Assert.Single(dashboard.MostProfitableProducts);
        Assert.Equal(820m - 7m * 40m, profit.EstimatedProfit);

        // Grafik 5 — borçlular: tek müşteri, tam bakiye.
        var debtor = Assert.Single(dashboard.TopDebtors);
        Assert.Equal("Ayşe", debtor.PartyName);
        Assert.Equal(1020m, debtor.Balance);
    }

    [Fact]
    public async Task Dashboard_EmptyTenant_AllZerosWithoutError()
    {
        using var scope = await CreateOwnerScopeAsync("rapor-bos@test.local");

        var dashboard = await scope.ServiceProvider.GetRequiredService<GetDashboardHandler>()
            .HandleAsync(default);

        Assert.Equal(0m, dashboard.DailySales);
        Assert.Equal(0m, dashboard.MonthlySales);
        Assert.Equal(0m, dashboard.MonthlyExpense);
        Assert.Equal(0m, dashboard.EstimatedNet);
        Assert.Equal(0m, dashboard.TotalReceivable);
        Assert.Equal(0m, dashboard.TotalPayable);
        Assert.Equal(0m, dashboard.CashTotal);
        Assert.Equal(0m, dashboard.BankTotal);
        Assert.Equal(0, dashboard.CriticalStockCount);
        Assert.Equal(0, dashboard.OverdueReceivableCount);
        Assert.Equal(30, dashboard.Last30DaysFlow.Count);
        Assert.All(dashboard.Last30DaysFlow, d => Assert.Equal(0m, d.Income + d.Expense));
        Assert.Equal(12, dashboard.Last12MonthsRevenue.Count);
        Assert.Empty(dashboard.TopSellingProducts);
        Assert.Empty(dashboard.MostProfitableProducts);
        Assert.Empty(dashboard.TopDebtors);
    }

    // ---- Alacaklar raporu

    [Fact]
    public async Task ReceivablesReport_ListsDebtors_WithOverdueAmounts()
    {
        using var scope = await CreateOwnerScopeAsync("rapor-alacak@test.local");
        var today = DateTime.UtcNow.Date;
        var fiveDaysAgo = today.AddDays(-5);

        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var ayse = await parties.HandleAsync(
            NewParty("Ayşe", openingBalance: 500m, phone: "05550000000"), default);
        var mehmet = await parties.HandleAsync(NewParty("Mehmet"), default);
        await parties.HandleAsync(NewParty("Ali"), default); // hareketi yok → listede olmaz
        await parties.HandleAsync(NewParty("Her İki", "Both", openingBalance: 50m), default);
        await parties.HandleAsync(NewParty("Ted Ltd", "Supplier", openingBalance: -300m), default);

        var product = await SeededProductAsync(scope, "Kitap", 10m);

        // Ayşe: ödenmemiş bugünkü satış → 500 + 120 = 620, vadesi yok.
        await ConfirmedSaleAsync(scope, ayse.Id, product, 1m, 100m, today);

        // Mehmet: vadesi dün geçen ödenmemiş satış → 100'ün tamamı gecikmiş.
        await ConfirmedSaleAsync(scope, mehmet.Id, product, 1m, 100m, fiveDaysAgo,
            dueDate: today.AddDays(-1), vat: 0m);

        var report = await scope.ServiceProvider.GetRequiredService<GetReceivablesReportHandler>()
            .HandleAsync(default);

        // Sıra bakiye göredir: Ayşe 620, Mehmet 100, Her İki 50. Ali (0) ve
        // tedarikçi (−300) alacak raporuna girmez.
        Assert.Equal(3, report.Items.Count);
        Assert.Equal("Ayşe", report.Items[0].PartyName);
        Assert.Equal(620m, report.Items[0].Balance);
        Assert.Equal("05550000000", report.Items[0].Phone);
        Assert.Equal(0m, report.Items[0].OverdueAmount);
        Assert.Equal("Mehmet", report.Items[1].PartyName);
        Assert.Equal(100m, report.Items[1].OverdueAmount);
        Assert.Equal("Her İki", report.Items[2].PartyName);

        Assert.Equal(770m, report.TotalReceivable);
        Assert.Equal(100m, report.TotalOverdue);
        Assert.Equal(1, report.OverdueCount);
    }

    // ---- Stok raporu

    [Fact]
    public async Task StockReport_ComputesValue_CriticalAndExcludesServices()
    {
        using var scope = await CreateOwnerScopeAsync("rapor-stok@test.local");

        var a = await SeededProductAsync(scope, "Defter", 10m, purchasePrice: 40m); // 400
        var b = await SeededProductAsync(scope, "Kalem", 2m, purchasePrice: 50m, minimumStock: 5m); // 100, kritik
        await SeededProductAsync(scope, "Danışmanlık", 0m, isService: true); // hizmet → rapor dışı
        await SeededProductAsync(scope, "Yeni Ürün", 0m); // stoğu yok, eşiği yok

        var report = await scope.ServiceProvider.GetRequiredService<GetStockReportHandler>()
            .HandleAsync(default);

        Assert.Equal(3, report.Items.Count); // hizmet yok
        Assert.Equal("Defter", report.Items[0].ProductName); // değere göre sıralı
        Assert.Equal(400m, report.Items[0].StockValue);
        Assert.False(report.Items[0].IsCritical);
        Assert.Equal("Kalem", report.Items[1].ProductName);
        Assert.Equal(2m, report.Items[1].OnHand);
        Assert.Equal(5m, report.Items[1].CriticalLevel);
        Assert.True(report.Items[1].IsCritical);
        Assert.Equal(0m, report.Items[2].StockValue); // hareketi olmayan ürün
        Assert.False(report.Items[2].IsCritical); // eşik 0 → uyarı yok

        Assert.Equal(500m, report.TotalValue);
        Assert.Equal(1, report.CriticalCount);
        Assert.Contains(report.Items, i => i.ProductId == a);
        Assert.Contains(report.Items, i => i.ProductId == b);
    }

    // ---- Satış raporu

    [Fact]
    public async Task SalesReport_TotalsAndBreakdowns_ExcludeDraftAndCancelled()
    {
        using var scope = await CreateOwnerScopeAsync("rapor-satis@test.local");
        var today = DateTime.UtcNow.Date;
        var fiveDaysAgo = today.AddDays(-5);

        var customer = await scope.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ayşe"), default);
        var product = await SeededProductAsync(scope, "Kitap", 20m);
        var warehouse = await GetDefaultWarehouseAsync(scope);

        // Bugün: nakit satış 3 × 100 + %20 KDV = 360.
        await ConfirmedSaleAsync(scope, null, product, 3m, 100m, today);
        // Bugün: müşterili satış 1 × 100 (KDV'siz).
        await ConfirmedSaleAsync(scope, customer.Id, product, 1m, 100m, today, vat: 0m);
        // Geçmiş: müşterili satış 2 × 50 (KDV'siz).
        await ConfirmedSaleAsync(scope, customer.Id, product, 2m, 50m, fiveDaysAgo, vat: 0m);

        // Taslak rapora girmez.
        await scope.ServiceProvider.GetRequiredService<CreateSaleHandler>()
            .HandleAsync(new CreateSaleRequest(null, warehouse.Id, today, null, null,
                [Item(product, 1m, 100m)]), default);

        // Onaylanıp iptal edilen belge de girmez (dengeleme ters hareketle, bölüm 23).
        var cancelled = await ConfirmedSaleAsync(scope, customer.Id, product, 1m, 100m, today, vat: 0m);
        await scope.ServiceProvider.GetRequiredService<CancelSaleHandler>()
            .HandleAsync(cancelled.Id, new CancelSaleRequest("müşteri vazgeçti"), default);

        var report = await scope.ServiceProvider.GetRequiredService<GetSalesReportHandler>()
            .HandleAsync(fiveDaysAgo, today, default);

        Assert.Equal(3, report.TotalCount);
        Assert.Equal(560m, report.TotalAmount);
        Assert.Equal(60m, report.TotalVat);
        Assert.Equal(186.67m, report.AverageSale); // round(560 / 3, 2)

        // Gün dökümü: dönem içindeki her gün sıfırla listelenir.
        Assert.Equal(6, report.ByDay.Count);
        Assert.Equal(today, report.ByDay[^1].Date);
        Assert.Equal(460m, report.ByDay[^1].Total); // 360 + 100
        Assert.Equal(fiveDaysAgo, report.ByDay[0].Date);
        Assert.Equal(100m, report.ByDay[0].Total);

        // Müşteri dökümü: nakit satışlar tek satırda.
        Assert.Equal(2, report.ByCustomer.Count);
        Assert.Equal("Nakit Satış", report.ByCustomer[0].PartyName);
        Assert.Null(report.ByCustomer[0].PartyId);
        Assert.Equal(360m, report.ByCustomer[0].Total);
        Assert.Equal("Ayşe", report.ByCustomer[1].PartyName);
        Assert.Equal(200m, report.ByCustomer[1].Total);

        // Ürün dökümü: tek ürün, 6 adet.
        var productRow = Assert.Single(report.ByProduct);
        Assert.Equal("Kitap", productRow.ProductName);
        Assert.Equal(6m, productRow.Quantity);
        Assert.Equal(560m, productRow.Total);
    }

    [Fact]
    public async Task SalesReport_DefaultsToCurrentMonth_AndRejectsReversedRange()
    {
        using var scope = await CreateOwnerScopeAsync("rapor-donem@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<GetSalesReportHandler>();
        var today = DateTime.UtcNow.Date;

        // Varsayılan dönem: ayın biri → bugün.
        var report = await handler.HandleAsync(null, null, default);
        Assert.Equal(new DateTime(today.Year, today.Month, 1), report.From);
        Assert.Equal(today, report.To);
        Assert.Equal(0, report.TotalCount);

        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(today, today.AddDays(-1), default));
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task Reports_AreIsolatedBetweenTenants()
    {
        using var scopeA = await CreateOwnerScopeAsync("rapor-iso-a@test.local");
        var today = DateTime.UtcNow.Date;

        var customer = await scopeA.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("Ayşe", openingBalance: 400m), default);
        var product = await SeededProductAsync(scopeA, "Kitap", 5m);
        await ConfirmedSaleAsync(scopeA, customer.Id, product, 1m, 100m, today);

        using var scopeB = await CreateOwnerScopeAsync("rapor-iso-b@test.local");

        var dashboardB = await scopeB.ServiceProvider.GetRequiredService<GetDashboardHandler>()
            .HandleAsync(default);
        Assert.Equal(0m, dashboardB.MonthlySales);
        Assert.Equal(0m, dashboardB.TotalReceivable);
        Assert.Empty(dashboardB.TopDebtors);

        var receivablesB = await scopeB.ServiceProvider.GetRequiredService<GetReceivablesReportHandler>()
            .HandleAsync(default);
        Assert.Empty(receivablesB.Items);

        var stockB = await scopeB.ServiceProvider.GetRequiredService<GetStockReportHandler>()
            .HandleAsync(default);
        Assert.Empty(stockB.Items);

        var salesB = await scopeB.ServiceProvider.GetRequiredService<GetSalesReportHandler>()
            .HandleAsync(null, null, default);
        Assert.Equal(0, salesB.TotalCount);
    }

    // ---- İzinler

    [Fact]
    public void RolePermissions_ReportsViewForAllRoles()
    {
        // Raporlar salt okunur: ViewOnly setinde (Employee + Viewer dahil).
        foreach (var role in new[] { TenantRole.Owner, TenantRole.Accountant, TenantRole.Employee, TenantRole.Viewer })
        {
            Assert.Contains(Permissions.ReportsView, RolePermissions.For(role));
        }
    }
}
