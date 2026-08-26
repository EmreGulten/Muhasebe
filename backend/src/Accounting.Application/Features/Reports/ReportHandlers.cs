using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Products;
using Accounting.Contracts.Reports;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Reports;

internal static class ReportQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
            ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");

    /// <summary>
    /// Raporlara giren satışlar: onaylanmış belgeler (Confirmed/PartiallyPaid/Paid).
    /// Taslak henüz gerçekleşmemiş, iptal ise dengelemiştir.
    /// </summary>
    public static readonly SaleStatus[] ConfirmedStatuses =
        [SaleStatus.Confirmed, SaleStatus.PartiallyPaid, SaleStatus.Paid];

    /// <summary>Cari bakiyeleri tek grup sorgusunda üretir: bakiye = Σborç − Σalacak.</summary>
    public static async Task<Dictionary<Guid, decimal>> BalancesByPartyAsync(
        IApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await db.PartyTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.PartyId)
            .Select(g => new { PartyId = g.Key, Balance = g.Sum(t => t.Debit) - g.Sum(t => t.Credit) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.PartyId, r => r.Balance);
    }

    /// <summary>
    /// Müşteri bazlı gecikmiş alacak: vadesi bugünden önce geçmiş ve kalanı olan
    /// onaylı satışların ödenmemiş tutarları (Total − PaidAmount).
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> OverdueByPartyAsync(
        IApplicationDbContext db, Guid tenantId, DateTime today, CancellationToken cancellationToken)
    {
        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId
                && ConfirmedStatuses.Contains(s.Status)
                && s.PartyId != null
                && s.DueDate != null
                && s.DueDate < today
                && s.Total - s.PaidAmount > 0m)
            .GroupBy(s => s.PartyId)
            .Select(g => new { PartyId = g.Key, Amount = g.Sum(s => s.Total - s.PaidAmount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.PartyId!.Value, r => r.Amount);
    }
}

/// <summary>
/// Dashboard: 10 KPI kartı ve beş grafik.
/// Tüm sorgular salt okunur; ayrıntılar rapor modülündedir, dashboard sadedir.
/// </summary>
public sealed class GetDashboardHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<DashboardResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = ReportQueries.RequireTenantId(currentTenant);
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var statuses = ReportQueries.ConfirmedStatuses;

        var sales = db.Sales.AsNoTracking()
            .Where(s => s.TenantId == tenantId && statuses.Contains(s.Status));

        var dailySales = await sales
            .Where(s => s.Date >= today && s.Date < tomorrow)
            .SumAsync(s => s.Total, cancellationToken);
        var monthlySales = await sales
            .Where(s => s.Date >= monthStart && s.Date < tomorrow)
            .SumAsync(s => s.Total, cancellationToken);

        var monthlyExpense = await db.IncomeExpenseRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.Status == IncomeExpenseStatus.Active
                && r.Type == IncomeExpenseType.Expense
                && r.Date >= monthStart
                && r.Date < tomorrow)
            .SumAsync(r => r.Amount, cancellationToken);

        // Cari bakiyeler: pozitif = alacak, negatif = borç (tüm cariler).
        var balances = await ReportQueries.BalancesByPartyAsync(db, tenantId, cancellationToken);
        var totalReceivable = balances.Values.Where(b => b > 0m).Sum();
        var totalPayable = balances.Values.Where(b => b < 0m).Sum(b => -b);

        // En yüksek borçlu 5 müşteri (müşteri ya da iki taraflı cariler).
        var debtorIds = balances.Where(kv => kv.Value > 0m).Select(kv => kv.Key).ToList();
        var customerRows = await db.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && debtorIds.Contains(p.Id)
                && (p.Type == PartyType.Customer || p.Type == PartyType.Both))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);
        var topDebtors = customerRows
            .Select(p => new DashboardTopDebtorDto(p.Id, p.Name, balances[p.Id]))
            .OrderByDescending(d => d.Balance)
            .ThenBy(d => d.PartyName)
            .Take(5)
            .ToList();

        // Kasa/banka toplamları: hesap hareketlerinin işaretli toplamı.
        var accounts = db.Accounts.AsNoTracking().Where(a => a.TenantId == tenantId);
        var accountTotals = await (
            from t in db.AccountTransactions.AsNoTracking().Where(t => t.TenantId == tenantId)
            join a in accounts on t.AccountId equals a.Id
            group t by a.Type into g
            select new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);
        var cashTotal = accountTotals.Where(t => t.Type == AccountType.Cash).Sum(t => t.Total);
        var bankTotal = accountTotals
            .Where(t => t.Type != AccountType.Cash)
            .Sum(t => t.Total);

        // Kritik stok: hizmet olmayan, eşiği olan ve stoğu eşik altında olan ürünler.
        var stocks = db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(t => t.Quantity) });
        var criticalStockCount = await (
            from p in db.Products.AsNoTracking()
                .Where(p => p.TenantId == tenantId && !p.IsService && p.MinimumStock > 0)
            join s in stocks on p.Id equals s.ProductId into stockGroup
            from s in stockGroup.DefaultIfEmpty()
            where ((decimal?)s.Stock ?? 0m) <= p.MinimumStock
            select p.Id)
            .CountAsync(cancellationToken);

        var overdue = await ReportQueries.OverdueByPartyAsync(db, tenantId, today, cancellationToken);

        // Grafik 1 — son 30 gün gelir/gider akışı (boş günler sıfırla).
        var flowStart = today.AddDays(-29);
        var flows = await db.IncomeExpenseRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.Status == IncomeExpenseStatus.Active
                && r.Date >= flowStart
                && r.Date < tomorrow)
            .GroupBy(r => r.Date)
            .Select(g => new
            {
                Date = g.Key,
                Income = g.Where(r => r.Type == IncomeExpenseType.Income).Sum(r => r.Amount),
                Expense = g.Where(r => r.Type == IncomeExpenseType.Expense).Sum(r => r.Amount),
            })
            .ToListAsync(cancellationToken);

        var last30DaysFlow = new List<DashboardDailyFlowDto>();
        for (var day = flowStart; day <= today; day = day.AddDays(1))
        {
            var entry = flows.FirstOrDefault(f => f.Date == day);
            last30DaysFlow.Add(new DashboardDailyFlowDto(
                day, entry?.Income ?? 0m, entry?.Expense ?? 0m));
        }

        // Grafik 2 — son 12 ay ciro (bu ay dahil; boş aylar sıfırla).
        var revenueStart = monthStart.AddMonths(-11);
        var monthlyRevenue = await sales
            .Where(s => s.Date >= revenueStart && s.Date < tomorrow)
            .GroupBy(s => new { s.Date.Year, s.Date.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(s => s.Total) })
            .ToListAsync(cancellationToken);

        var last12MonthsRevenue = new List<DashboardMonthlyRevenueDto>();
        var cursor = revenueStart;
        while (cursor <= monthStart)
        {
            var entry = monthlyRevenue.FirstOrDefault(m => m.Year == cursor.Year && m.Month == cursor.Month);
            last12MonthsRevenue.Add(new DashboardMonthlyRevenueDto(
                cursor.Year, cursor.Month, entry?.Total ?? 0m));
            cursor = cursor.AddMonths(1);
        }

        // Grafik 3 + 4 — en çok satan / en kârlı ürünler (son 12 ay, onaylı kalemler).
        // Kâr tahmini: satır toplamı − (miktar × ürün kartındaki alış fiyatı); ürün
        // silinmişse alış fiyatı 0 kabul edilir (kalem snapshot'ı yine gösterilir).
        var productRows = await (
            from s in sales.Where(s => s.Date >= revenueStart && s.Date < tomorrow)
            join i in db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
            join p in db.Products.AsNoTracking() on i.ProductId equals p.Id into productGroup
            from p in productGroup.DefaultIfEmpty()
            group i by new
            {
                i.ProductId,
                i.ProductName,
                PurchasePrice = (decimal?)p.PurchasePrice ?? 0m,
            }
            into g
            select new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.PurchasePrice,
                Quantity = g.Sum(i => i.Quantity),
                Total = g.Sum(i => i.LineTotal),
            })
            .ToListAsync(cancellationToken);

        var topSellingProducts = productRows
            .OrderByDescending(p => p.Quantity)
            .ThenBy(p => p.ProductName)
            .Take(5)
            .Select(p => new DashboardTopProductDto(p.ProductId, p.ProductName, p.Quantity, p.Total))
            .ToList();

        var mostProfitableProducts = productRows
            .Select(p => new { p.ProductId, p.ProductName, p.Total, Profit = p.Total - p.Quantity * p.PurchasePrice })
            .OrderByDescending(p => p.Profit)
            .ThenBy(p => p.ProductName)
            .Take(5)
            .Select(p => new DashboardProfitableProductDto(p.ProductId, p.ProductName, p.Profit, p.Total))
            .ToList();

        return new DashboardResponse(
            dailySales,
            monthlySales,
            monthlyExpense,
            monthlySales - monthlyExpense,
            totalReceivable,
            totalPayable,
            cashTotal,
            bankTotal,
            criticalStockCount,
            overdue.Count,
            last30DaysFlow,
            last12MonthsRevenue,
            topSellingProducts,
            mostProfitableProducts,
            topDebtors);
    }
}

/// <summary>
/// Alacaklar raporu: pozitif bakiyeli müşteriler ve
/// müşteri bazlı gecikmiş alacaklar. Satıcılar borç raporunun konusudur.
/// </summary>
public sealed class GetReceivablesReportHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<ReceivablesReportResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = ReportQueries.RequireTenantId(currentTenant);
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);

        var balances = await ReportQueries.BalancesByPartyAsync(db, tenantId, cancellationToken);
        var overdue = await ReportQueries.OverdueByPartyAsync(db, tenantId, today, cancellationToken);

        var customers = await db.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && (p.Type == PartyType.Customer || p.Type == PartyType.Both))
            .Select(p => new { p.Id, p.Name, p.Phone })
            .ToListAsync(cancellationToken);

        var items = customers
            .Select(p => new ReceivableRowDto(
                p.Id,
                p.Name,
                p.Phone,
                balances.TryGetValue(p.Id, out var balance) ? balance : 0m,
                overdue.TryGetValue(p.Id, out var amount) ? amount : 0m))
            .Where(r => r.Balance > 0m)
            .OrderByDescending(r => r.Balance)
            .ThenBy(r => r.PartyName)
            .ToList();

        return new ReceivablesReportResponse(
            items,
            items.Sum(r => r.Balance),
            items.Sum(r => r.OverdueAmount),
            items.Count(r => r.OverdueAmount > 0m));
    }
}

/// <summary>
/// Stok raporu: tüm stoklu ürünler (pasifler dahil),
/// eldeki miktar ve maliyet değeri (eldeki × alış fiyatı). Hizmetler yoktur.
/// </summary>
public sealed class GetStockReportHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, IFeatureGuard featureGuard)
{
    public async Task<StockReportResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = ReportQueries.RequireTenantId(currentTenant);

        // Stok raporu stok modülüne bağlı.
        await featureGuard.EnsureFeatureAsync(tenantId, PlanFeatures.Stock, cancellationToken);

        var stocks = db.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(t => t.Quantity) });

        var rows = await (
            from p in db.Products.AsNoTracking().Where(p => p.TenantId == tenantId && !p.IsService)
            join s in stocks on p.Id equals s.ProductId into stockGroup
            from s in stockGroup.DefaultIfEmpty()
            select new
            {
                p.Id,
                p.Name,
                p.Sku,
                Stock = (decimal?)s.Stock ?? 0m,
                p.MinimumStock,
                p.PurchasePrice,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new StockRowDto(
                r.Id,
                r.Name,
                r.Sku,
                r.Stock,
                r.MinimumStock,
                ProductQueries.IsCritical(r.MinimumStock, r.Stock),
                r.Stock * r.PurchasePrice))
            .OrderByDescending(r => r.StockValue)
            .ThenBy(r => r.ProductName)
            .ToList();

        return new StockReportResponse(
            items,
            items.Sum(r => r.StockValue),
            items.Count(r => r.IsCritical));
    }
}

/// <summary>
/// Satış raporu: dönem toplamları (adet, tutar, KDV,
/// ortalama) ve gün / müşteri / ürün bazlı dökümler. Varsayılan dönem içinde
/// bulunulan aydır; taslak ve iptaller girmez.
/// </summary>
public sealed class GetSalesReportHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<SalesReportResponse> HandleAsync(
        DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var tenantId = ReportQueries.RequireTenantId(currentTenant);
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);

        var toDate = to is { } t ? Dates.ToUtcDate(t) : today;
        var fromDate = from is { } f
            ? Dates.ToUtcDate(f)
            : new DateTime(toDate.Year, toDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (fromDate > toDate)
        {
            throw new AppException("Dönem başlangıcı bitişten sonra olamaz.");
        }

        var statuses = ReportQueries.ConfirmedStatuses;
        var sales = db.Sales.AsNoTracking()
            .Where(s => s.TenantId == tenantId
                && statuses.Contains(s.Status)
                && s.Date >= fromDate
                && s.Date < toDate.AddDays(1));

        var totals = await sales
            .GroupBy(s => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Amount = g.Sum(s => s.Total),
                Vat = g.Sum(s => s.VatTotal),
            })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = totals?.Count ?? 0;
        var totalAmount = totals?.Amount ?? 0m;
        var totalVat = totals?.Vat ?? 0m;

        // Gün bazlı döküm — dönem içindeki her gün listelenir (boş günler sıfırla).
        var byDayRaw = await sales
            .GroupBy(s => s.Date)
            .Select(g => new { Date = g.Key, Count = g.Count(), Total = g.Sum(s => s.Total) })
            .ToListAsync(cancellationToken);
        var byDay = new List<SalesByDayDto>();
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            var entry = byDayRaw.FirstOrDefault(d => d.Date == day);
            byDay.Add(new SalesByDayDto(day, entry?.Count ?? 0, entry?.Total ?? 0m));
        }

        // Müşteri bazlı döküm — müşterisiz belgeler "Nakit Satış" satırında toplanır.
        var byCustomerRaw = await sales
            .GroupBy(s => s.PartyId)
            .Select(g => new { PartyId = g.Key, Count = g.Count(), Total = g.Sum(s => s.Total) })
            .ToListAsync(cancellationToken);
        var partyIds = byCustomerRaw.Where(r => r.PartyId != null).Select(r => r.PartyId!.Value).ToList();
        var partyNames = await db.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && partyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        var byCustomer = byCustomerRaw
            .Select(r => new SalesByCustomerDto(
                r.PartyId,
                r.PartyId is { } id && partyNames.TryGetValue(id, out var name) ? name : "Nakit Satış",
                r.Count,
                r.Total))
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.PartyName)
            .ToList();

        // Ürün bazlı döküm — belge kalemlerinin snapshot adlarıyla.
        var byProductRaw = await (
            from s in sales
            join i in db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
            group i by new { i.ProductId, i.ProductName } into g
            select new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                Quantity = g.Sum(i => i.Quantity),
                Total = g.Sum(i => i.LineTotal),
            })
            .ToListAsync(cancellationToken);
        var byProduct = byProductRaw
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.ProductName)
            .Select(r => new SalesByProductDto(r.ProductId, r.ProductName, r.Quantity, r.Total))
            .ToList();

        return new SalesReportResponse(
            fromDate,
            toDate,
            totalCount,
            totalAmount,
            totalVat,
            totalCount > 0 ? decimal.Round(totalAmount / totalCount, 2) : 0m,
            byDay,
            byCustomer,
            byProduct);
    }
}
