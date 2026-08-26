namespace Accounting.Contracts.Reports;

/// <summary>Son 30 günlük gelir/gider çubuğu — gün bazında çizilir.</summary>
public sealed record DashboardDailyFlowDto(DateTime Date, decimal Income, decimal Expense);

/// <summary>Son 12 aylık ciro — onaylı satış toplamları (iptaller hariç).</summary>
public sealed record DashboardMonthlyRevenueDto(int Year, int Month, decimal Total);

/// <summary>En çok satan ürün — son 12 ay, onaylı belge kalemleri.</summary>
public sealed record DashboardTopProductDto(
    Guid ProductId, string ProductName, decimal Quantity, decimal Total);

/// <summary>En kârlı ürün — tahmini kâr = satır toplamı − (miktar × alış fiyatı).</summary>
public sealed record DashboardProfitableProductDto(
    Guid ProductId, string ProductName, decimal EstimatedProfit, decimal Total);

/// <summary>En yüksek borçlu müşteri — pozitif cari bakiye.</summary>
public sealed record DashboardTopDebtorDto(Guid PartyId, string PartyName, decimal Balance);

/// <summary>
/// Dashboard: işletmenin güncel durumu tek ekranda —
/// 10 KPI kartı ve beş grafik. Sade tutulur; ayrıntı rapor modülündedir.
/// </summary>
public sealed record DashboardResponse(
    decimal DailySales,
    decimal MonthlySales,
    decimal MonthlyExpense,
    decimal EstimatedNet,
    decimal TotalReceivable,
    decimal TotalPayable,
    decimal CashTotal,
    decimal BankTotal,
    int CriticalStockCount,
    int OverdueReceivableCount,
    IReadOnlyList<DashboardDailyFlowDto> Last30DaysFlow,
    IReadOnlyList<DashboardMonthlyRevenueDto> Last12MonthsRevenue,
    IReadOnlyList<DashboardTopProductDto> TopSellingProducts,
    IReadOnlyList<DashboardProfitableProductDto> MostProfitableProducts,
    IReadOnlyList<DashboardTopDebtorDto> TopDebtors);

/// <summary>Alacak satırı — pozitif bakiyeli müşteri; gecikmiş tutar vadesi geçen ödenmemiş satışlardan.</summary>
public sealed record ReceivableRowDto(
    Guid PartyId,
    string PartyName,
    string? Phone,
    decimal Balance,
    decimal OverdueAmount);

/// <summary>Alacaklar raporu: borçlu müşteriler ve gecikmiş alacaklar.</summary>
public sealed record ReceivablesReportResponse(
    IReadOnlyList<ReceivableRowDto> Items,
    decimal TotalReceivable,
    decimal TotalOverdue,
    int OverdueCount);

/// <summary>Stok satırı — stok değeri = eldeki × alış fiyatı.</summary>
public sealed record StockRowDto(
    Guid ProductId,
    string ProductName,
    string? Sku,
    decimal OnHand,
    decimal CriticalLevel,
    bool IsCritical,
    decimal StockValue);

/// <summary>Stok raporu: durum, kritik stok ve toplam değer.</summary>
public sealed record StockReportResponse(
    IReadOnlyList<StockRowDto> Items,
    decimal TotalValue,
    int CriticalCount);

/// <summary>Müşteri bazlı satır.</summary>
public sealed record SalesByCustomerDto(Guid? PartyId, string PartyName, int Count, decimal Total);

/// <summary>Ürün bazlı satır.</summary>
public sealed record SalesByProductDto(Guid ProductId, string ProductName, decimal Quantity, decimal Total);

/// <summary>Gün bazlı satış — grafik için.</summary>
public sealed record SalesByDayDto(DateTime Date, int Count, decimal Total);

/// <summary>Satış raporu: dönem toplamları, günlük/müşteri/ürün dökümü.</summary>
public sealed record SalesReportResponse(
    DateTime From,
    DateTime To,
    int TotalCount,
    decimal TotalAmount,
    decimal TotalVat,
    decimal AverageSale,
    IReadOnlyList<SalesByDayDto> ByDay,
    IReadOnlyList<SalesByCustomerDto> ByCustomer,
    IReadOnlyList<SalesByProductDto> ByProduct);
