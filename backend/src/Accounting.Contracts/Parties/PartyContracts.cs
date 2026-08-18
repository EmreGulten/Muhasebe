namespace Accounting.Contracts.Parties;

/// <summary>
/// Yeni cari kartı. Type: "Customer" | "Supplier" | "Both".
/// OpeningBalance işaretli: pozitif = taraf bize borçlu, negatif = biz borçluyuz.
/// </summary>
public sealed record CreatePartyRequest(
    string Name,
    string Type,
    string? TaxNumber,
    string? TaxOffice,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? District,
    string? ContactName,
    decimal OpeningBalance,
    decimal CreditLimit,
    string? Notes);

/// <summary>Cari kartı güncelleme. Açılış bakiyesi değiştirilemez (hareket olarak girilmeli).</summary>
public sealed record UpdatePartyRequest(
    string Name,
    string Type,
    string? TaxNumber,
    string? TaxOffice,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? District,
    string? ContactName,
    decimal CreditLimit,
    string? Notes,
    bool IsActive);

/// <summary>Liste satırı — bakiye ve son hareket dahil.</summary>
public sealed record PartySummaryDto(
    Guid Id,
    string Type,
    string Name,
    string? Phone,
    string? Email,
    string? City,
    decimal Balance,
    bool IsActive,
    DateTime? LastTransactionDateUtc);

/// <summary>Cari kartı detayı + hesap özeti.</summary>
public sealed record PartyResponse(
    Guid Id,
    string Type,
    string Name,
    string? TaxNumber,
    string? TaxOffice,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? District,
    string? ContactName,
    decimal OpeningBalance,
    decimal CreditLimit,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    decimal Balance,
    decimal TotalDebit,
    decimal TotalCredit,
    DateTime? LastTransactionDateUtc);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// Manuel cari hareket girişi. Type: "OpeningBalance" | "Debit" | "Credit" | "Adjustment".
/// Amount işaretli: pozitif = borç, negatif = alacak.
/// </summary>
public sealed record CreatePartyTransactionRequest(
    string Type,
    DateTime Date,
    decimal Amount,
    DateTime? DueDate,
    string? Description);

/// <summary>Ekstre satırı — Balance sayfa içindeki çalışan bakiyedir.</summary>
public sealed record PartyTransactionDto(
    Guid Id,
    string Type,
    DateTime Date,
    DateTime? DueDate,
    decimal Debit,
    decimal Credit,
    string? Description,
    string? ReferenceType,
    Guid? ReferenceId,
    decimal Balance,
    DateTime CreatedAtUtc);

/// <summary>Ekstre sayfası — BalanceBeforePage sayfadaki ilk satırdan önceki kümülatif bakiye.</summary>
public sealed record PartyStatementResponse(
    Guid PartyId,
    string PartyName,
    decimal BalanceBeforePage,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<PartyTransactionDto> Items);
