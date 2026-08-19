namespace Accounting.Contracts.IncomeExpenses;

/// <summary>Kategori listesi öğesi — kayıt sayısı yönetim kararlarında gösterilir.</summary>
public sealed record IncomeExpenseCategoryDto(
    Guid Id,
    string Name,
    string Type,
    bool IsActive,
    int RecordCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateIncomeExpenseCategoryRequest(string Name, string Type);

/// <summary>Yalnızca ad ve aktiflik; tür oluşturulduktan sonra değişmez.</summary>
public sealed record UpdateIncomeExpenseCategoryRequest(string Name, bool IsActive);

/// <summary>
/// Gelir/gider kaydı (muhasebe.md bölüm 8). PaymentAccountId verilmezse
/// varsayılan "Kasa" hesabı kullanılır. Kayıt ilgili hesaba işaretli hareket
/// yazar; sonradan değiştirilemez — düzeltme iptalle yapılır.
/// </summary>
public sealed record CreateIncomeExpenseRecordRequest(
    string Type,
    Guid CategoryId,
    decimal Amount,
    DateTime Date,
    Guid? PaymentAccountId,
    string? Description,
    string? DocumentNumber);

public sealed record IncomeExpenseRecordDto(
    Guid Id,
    string Type,
    Guid CategoryId,
    string CategoryName,
    decimal Amount,
    DateTime Date,
    Guid PaymentAccountId,
    string PaymentAccountName,
    string? Description,
    string? DocumentNumber,
    string Status,
    DateTime? CancelledAtUtc,
    DateTime CreatedAtUtc);

/// <summary>Dönem özeti (muhasebe.md bölüm 25 — gelir gider raporu MVP'si).</summary>
public sealed record IncomeExpenseSummaryResponse(
    DateTime From,
    DateTime To,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Net,
    IReadOnlyList<IncomeExpenseMonthlyDto> Months,
    IReadOnlyList<IncomeExpenseCategoryTotalDto> Categories);

/// <summary>Bir ayın toplamları; gelir ve gider ayrı, Net = gelir − gider.</summary>
public sealed record IncomeExpenseMonthlyDto(int Year, int Month, decimal Income, decimal Expense, decimal Net);

/// <summary>Kategori bazlı döküm — en çok harcanan/erişilen kategoriler.</summary>
public sealed record IncomeExpenseCategoryTotalDto(
    string Type,
    Guid CategoryId,
    string CategoryName,
    decimal Total);
