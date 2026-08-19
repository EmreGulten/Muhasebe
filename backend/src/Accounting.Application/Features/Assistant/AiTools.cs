using System.Text.Json;
using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Reports;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Assistant;

// ---- Onaylı iş araçları (muhasebe.md bölüm 11.1)
//
// Plan bölüm 11.1'deki tool listesinin tamamı: her araç salt okunur bir iş
// sorusunu yanıtlar, daima TenantId filtresiyle çalışır ve yanıtında modelin
// doğrudan kullanabileceği Türkçe bir "summary" taşır.

/// <summary>Ayın gelir/gider/net özeti — "Bu ay ne kadar kazandım?"</summary>
public sealed class GetMonthlyProfitTool(
    IApplicationDbContext db, TimeProvider timeProvider) : IAiTool
{
    public string Name => "get_monthly_profit";

    public string Description =>
        "Belirtilen ayın toplam gelir, gider ve net kazancı (gelir/gider kayıtlarından; iptaller hariç).";

    public string ParametersJsonSchema =>
        """{"type":"object","properties":{"month":{"type":"string","description":"Ay, YYYY-MM biçiminde. Verilmezse içinde bulunulan ay."}},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        arguments.TryGetString("month", out var monthText);
        var (year, month) = AiToolHelpers.ParseMonth(monthText) ?? (today.Year, today.Month);
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

        var records = db.IncomeExpenseRecords.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.Status == IncomeExpenseStatus.Active
                && r.Date >= start
                && r.Date < start.AddMonths(1));

        var income = await records.Where(r => r.Type == IncomeExpenseType.Income).SumAsync(r => r.Amount, cancellationToken);
        var expense = await records.Where(r => r.Type == IncomeExpenseType.Expense).SumAsync(r => r.Amount, cancellationToken);

        var label = AiToolHelpers.MonthLabel(year, month);
        var summary = $"{label}: gelir {AiToolHelpers.Money(income)}, gider {AiToolHelpers.Money(expense)}, net kazanç {AiToolHelpers.Money(income - expense)}.";

        return AiToolHelpers.ToJson(new
        {
            month = $"{year:D4}-{month:D2}",
            income,
            expense,
            net = income - expense,
            summary,
        });
    }
}

/// <summary>Gecikmiş alacaklar — "Bana borcu olan / ödemesi geciken müşteriler".</summary>
public sealed class GetOverdueReceivablesTool(
    IApplicationDbContext db, TimeProvider timeProvider) : IAiTool
{
    public string Name => "get_overdue_receivables";

    public string Description =>
        "Vadesi geçmiş ve ödenmemiş onaylı satışlardan müşteri bazlı gecikmiş alacak listesi.";

    public string ParametersJsonSchema => """{"type":"object","properties":{},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        var overdue = await ReportQueries.OverdueByPartyAsync(db, tenantId, today, cancellationToken);

        var ids = overdue.Keys.ToList();
        var names = await db.Parties.AsNoTracking()
            .Where(p => p.TenantId == tenantId && ids.Contains(p.Id)
                && (p.Type == PartyType.Customer || p.Type == PartyType.Both))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var items = overdue
            .Where(kv => names.ContainsKey(kv.Key))
            .Select(kv => new { customer = names[kv.Key], amount = kv.Value })
            .OrderByDescending(i => i.amount)
            .ToList();

        var total = items.Sum(i => i.amount);
        var summary = items.Count == 0
            ? "Gecikmiş alacağınız yok."
            : $"{items.Count} müşterinin toplam {AiToolHelpers.Money(total)} gecikmiş borcu var: {string.Join(", ", items.Select(i => $"{i.customer} ({AiToolHelpers.Money(i.amount)})"))}.";

        return AiToolHelpers.ToJson(new { items, total, summary });
    }
}

/// <summary>En çok satan ürünler — "En çok satan 10 ürün nedir?"</summary>
public sealed class GetTopProductsTool(IApplicationDbContext db, TimeProvider timeProvider) : IAiTool
{
    public string Name => "get_top_products";

    public string Description =>
        "Son 12 ayın onaylı satış kalemlerinden en çok satan ürünler (miktar sırasıyla).";

    public string ParametersJsonSchema =>
        """{"type":"object","properties":{"limit":{"type":"integer","description":"Liste uzunluğu (1-10, varsayılan 5)."}},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = monthStart.AddMonths(-11);
        arguments.TryGetInt32("limit", out var limit);
        limit = Math.Clamp(limit <= 0 ? 5 : limit, 1, 10);

        var statuses = ReportQueries.ConfirmedStatuses;
        var rows = await (
            from s in db.Sales.AsNoTracking().Where(s => s.TenantId == tenantId
                && statuses.Contains(s.Status)
                && s.Date >= start
                && s.Date < today.AddDays(1))
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

        var items = rows
            .OrderByDescending(r => r.Quantity)
            .ThenBy(r => r.ProductName)
            .Take(limit)
            .Select(r => new { r.ProductName, quantity = r.Quantity, total = r.Total })
            .ToList();

        var summary = items.Count == 0
            ? "Son 12 ayda onaylı satış yok."
            : $"Son 12 ayın en çok satanları: {string.Join(", ", items.Select(i => $"{i.ProductName} — {i.quantity.ToString("0.##", AiToolHelpers.Turkish)} adet / {AiToolHelpers.Money(i.total)}"))}.";

        return AiToolHelpers.ToJson(new { items, summary });
    }
}

/// <summary>Kritik stok — "Hangi ürünlerin stoğu bitmek üzere?"</summary>
public sealed class GetLowStockProductsTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "get_low_stock_products";

    public string Description =>
        "Stoğu kritik eşiğin altında kalan ürünler (hizmetler hariç); en kritik olan önce listelenir.";

    public string ParametersJsonSchema => """{"type":"object","properties":{},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var stocks = db.InventoryTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(t => t.Quantity) });

        var rows = await (
            from p in db.Products.AsNoTracking().Where(p => p.TenantId == tenantId
                && !p.IsService
                && p.MinimumStock > 0)
            join s in stocks on p.Id equals s.ProductId into stockGroup
            from s in stockGroup.DefaultIfEmpty()
            where ((decimal?)s.Stock ?? 0m) <= p.MinimumStock
            orderby ((decimal?)s.Stock ?? 0m) - p.MinimumStock, p.Name
            select new
            {
                p.Name,
                Stock = (decimal?)s.Stock ?? 0m,
                p.MinimumStock,
            })
            .Take(10)
            .ToListAsync(cancellationToken);

        var summary = rows.Count == 0
            ? "Kritik eşiğin altında stoğu olan ürün yok."
            : $"{rows.Count} ürünün stoğu kritik seviyede: {string.Join(", ", rows.Select(r => $"{r.Name} ({r.Stock.ToString("0.##", AiToolHelpers.Turkish)}/{r.MinimumStock.ToString("0.##", AiToolHelpers.Turkish)})"))}.";

        return AiToolHelpers.ToJson(new { items = rows, summary });
    }
}

/// <summary>Cari bakiye — "X müşterinin bakiyesi nedir?" / "Kimin borcu var?"</summary>
public sealed class GetCustomerBalanceTool(
    IApplicationDbContext db) : IAiTool
{
    public string Name => "get_customer_balance";

    public string Description =>
        "Müşteri bakiyeleri. customerName verilirse adı bunu içeren müşteriler; verilmezse bakiyesi pozitif (size borcu olan) müşteriler listelenir.";

    public string ParametersJsonSchema =>
        """{"type":"object","properties":{"customerName":{"type":"string","description":"Müşteri adının bir parçası; verilmezse borçlu müşteriler listelenir."}},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var balances = await ReportQueries.BalancesByPartyAsync(db, tenantId, cancellationToken);

        var parties = db.Parties.AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && (p.Type == PartyType.Customer || p.Type == PartyType.Both));

        // string.ToLower() bilinçli: EF Core SQL LOWER()'a çevirir (cari aramayla aynı).
        if (arguments.TryGetString("customerName", out var name))
        {
#pragma warning disable CA1304, CA1311, CA1862
            parties = parties.Where(p => p.Name.ToLower().Contains(name.Trim().ToLower()));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var rows = await parties
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(p => new
            {
                customer = p.Name,
                balance = balances.TryGetValue(p.Id, out var balance) ? balance : 0m,
            })
            .Where(i => arguments.TryGetString("customerName", out _) || i.balance > 0m)
            .OrderByDescending(i => i.balance)
            .ThenBy(i => i.customer)
            .Take(10)
            .ToList();

        string summary;
        if (arguments.TryGetString("customerName", out var queried))
        {
            summary = items.Count == 0
                ? $"'{queried.Trim()}' adını içeren müşteri bulunamadı."
                : string.Join(", ", items.Select(i =>
                    $"{i.customer}: {(i.balance >= 0 ? $"{AiToolHelpers.Money(i.balance)} alacağınız" : $"{AiToolHelpers.Money(-i.balance)} borcunuz")} var"));
            summary += ".";
        }
        else
        {
            var total = items.Sum(i => i.balance);
            summary = items.Count == 0
                ? "Borçlu müşteri yok."
                : $"{items.Count} müşterinin toplam {AiToolHelpers.Money(total)} borcu var: {string.Join(", ", items.Select(i => $"{i.customer} ({AiToolHelpers.Money(i.balance)})"))}.";
        }

        return AiToolHelpers.ToJson(new { items, summary });
    }
}

/// <summary>Gider kategori dökümü — "Bu ay en yüksek gider kategorim nedir?"</summary>
public sealed class GetExpenseBreakdownTool(
    IApplicationDbContext db, TimeProvider timeProvider) : IAiTool
{
    public string Name => "get_expense_breakdown";

    public string Description =>
        "Belirtilen ayın (varsayılan içinde bulunulan ay) gider kategorileri bazında dökümü.";

    public string ParametersJsonSchema =>
        """{"type":"object","properties":{"month":{"type":"string","description":"Ay, YYYY-MM biçiminde. Verilmezse içinde bulunulan ay."}},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        arguments.TryGetString("month", out var monthText);
        var (year, month) = AiToolHelpers.ParseMonth(monthText) ?? (today.Year, today.Month);
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rows = await (
            from r in db.IncomeExpenseRecords.AsNoTracking().Where(r => r.TenantId == tenantId
                && r.Status == IncomeExpenseStatus.Active
                && r.Type == IncomeExpenseType.Expense
                && r.Date >= start
                && r.Date < start.AddMonths(1))
            join c in db.IncomeExpenseCategories.AsNoTracking() on r.CategoryId equals c.Id
            group r by c.Name into g
            select new { Category = g.Key, Total = g.Sum(r => r.Amount) })
            .ToListAsync(cancellationToken);

        var items = rows.OrderByDescending(r => r.Total).ToList();
        var total = items.Sum(r => r.Total);

        var summary = items.Count == 0
            ? $"{AiToolHelpers.MonthLabel(year, month)} gideri yok."
            : $"{AiToolHelpers.MonthLabel(year, month)} giderleri ({AiToolHelpers.Money(total)}): {string.Join(", ", items.Select(i => $"{i.Category} {AiToolHelpers.Money(i.Total)}"))}.";

        return AiToolHelpers.ToJson(new { items, total, summary });
    }
}

/// <summary>Ay kıyası — "Geçen aya göre giderim neden arttı?"</summary>
public sealed class CompareMonthsTool(
    IApplicationDbContext db, TimeProvider timeProvider) : IAiTool
{
    public string Name => "compare_months";

    public string Description =>
        "İçinde bulunulan ay ile bir önceki ayın gelir/gider/net karşılaştırması (yüzdesel değişimle).";

    public string ParametersJsonSchema => """{"type":"object","properties":{},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        var thisStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastStart = thisStart.AddMonths(-1);

        var rows = await db.IncomeExpenseRecords.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.Status == IncomeExpenseStatus.Active
                && r.Date >= lastStart
                && r.Date < thisStart.AddMonths(1))
            .GroupBy(r => new { r.Date.Year, r.Date.Month, r.Type })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(r => r.Amount) })
            .ToListAsync(cancellationToken);

        decimal Sum(int year, int month, IncomeExpenseType type) =>
            rows.Where(r => r.Year == year && r.Month == month && r.Type == type).Sum(r => r.Total);

        var lastIncome = Sum(lastStart.Year, lastStart.Month, IncomeExpenseType.Income);
        var lastExpense = Sum(lastStart.Year, lastStart.Month, IncomeExpenseType.Expense);
        var thisIncome = Sum(thisStart.Year, thisStart.Month, IncomeExpenseType.Income);
        var thisExpense = Sum(thisStart.Year, thisStart.Month, IncomeExpenseType.Expense);

        var summary =
            $"Bu ay: gelir {AiToolHelpers.Money(thisIncome)}, gider {AiToolHelpers.Money(thisExpense)}, net {AiToolHelpers.Money(thisIncome - thisExpense)}. " +
            $"Geçen ay: gelir {AiToolHelpers.Money(lastIncome)}, gider {AiToolHelpers.Money(lastExpense)}, net {AiToolHelpers.Money(lastIncome - lastExpense)}. " +
            $"Gelir {AiToolHelpers.Change(lastIncome, thisIncome)}, gider {AiToolHelpers.Change(lastExpense, thisExpense)}.";

        return AiToolHelpers.ToJson(new
        {
            thisMonth = new { income = thisIncome, expense = thisExpense, net = thisIncome - thisExpense },
            lastMonth = new { income = lastIncome, expense = lastExpense, net = lastIncome - lastExpense },
            summary,
        });
    }
}

/// <summary>Yaklaşan ödemeler — "Önümüzdeki 7 günde ne kadar ödeme yapmam gerekir?"</summary>
public sealed class GetUpcomingPaymentsTool(
    IApplicationDbContext db, TimeProvider timeProvider) : IAiTool
{
    public string Name => "get_upcoming_payments";

    public string Description =>
        "Önümüzdeki N günde (varsayılan 7) vadesi gelen ve kalanı olan onaylı alış belgeleri — tedarikçi ödemeleri.";

    public string ParametersJsonSchema =>
        """{"type":"object","properties":{"days":{"type":"integer","description":"Kaç günlük pencere (1-31, varsayılan 7)."}},"required":[]}""";

    public async Task<JsonElement> ExecuteAsync(Guid tenantId, JsonElement arguments, CancellationToken cancellationToken)
    {
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        arguments.TryGetInt32("days", out var days);
        days = Math.Clamp(days <= 0 ? 7 : days, 1, 31);
        var end = today.AddDays(days + 1);

        var statuses = new[] { PurchaseStatus.Confirmed, PurchaseStatus.PartiallyPaid };
        var rows = await (
            from p in db.Purchases.AsNoTracking().Where(p => p.TenantId == tenantId
                && statuses.Contains(p.Status)
                && p.PartyId != null
                && p.DueDate != null
                && p.DueDate >= today
                && p.DueDate < end
                && p.Total - p.PaidAmount > 0m)
            join party in db.Parties.AsNoTracking() on p.PartyId equals party.Id
            orderby p.DueDate
            select new
            {
                Supplier = party.Name,
                Document = p.Number,
                p.DueDate,
                Amount = p.Total - p.PaidAmount,
            })
            .ToListAsync(cancellationToken);

        var total = rows.Sum(r => r.Amount);
        var summary = rows.Count == 0
            ? $"Önümüzdeki {days} günde vadesi gelen ödeme yok."
            : $"Önümüzdeki {days} günde {rows.Count} belge için toplam {AiToolHelpers.Money(total)} ödeme var: {string.Join(", ", rows.Select(r => $"{r.Supplier} — {r.Document} · {AiToolHelpers.Money(r.Amount)} ({r.DueDate!.Value.ToString("d MMM", AiToolHelpers.Turkish)})"))}.";

        return AiToolHelpers.ToJson(new { items = rows, total, summary });
    }
}
