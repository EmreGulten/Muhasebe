namespace Accounting.Contracts.Accounts;

/// <summary>
/// Yeni kasa/banka hesabı. Type: "Cash" | "Bank" | "CreditCard" | "VirtualPOS".
/// OpeningBalance pozitif ya da 0'dır; 0'dan büyükse hesap tek seferlik
/// açılış hareketiyle deftere girer.
/// </summary>
public sealed record CreateAccountRequest(
    string Name,
    string Type,
    string? Currency,
    decimal OpeningBalance);

/// <summary>Hesap güncelleme. Tür ve açılış bakiyesi değiştirilemez; düzeltme hareketle yapılır.</summary>
public sealed record UpdateAccountRequest(
    string Name,
    bool IsActive);

/// <summary>Hesap satırı — güncel bakiye ve hareket sayısı dahil.</summary>
public sealed record AccountDto(
    Guid Id,
    string Name,
    string Type,
    string Currency,
    decimal OpeningBalance,
    decimal CurrentBalance,
    bool IsDefault,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int TransactionCount);

/// <summary>
/// Manuel hesap hareketi. Direction: "In" (giriş/tahsilat) | "Out" (çıkış/ödeme).
/// Amount pozitif girilir; deftere işaretli yazılır.
/// </summary>
public sealed record CreateAccountTransactionRequest(
    string Direction,
    DateTime Date,
    decimal Amount,
    string? Description);

/// <summary>Ekstre satırı — Balance sayfa içindeki çalışan bakiyedir.</summary>
public sealed record AccountTransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Type,
    decimal Amount,
    DateTime Date,
    string? Description,
    string? ReferenceType,
    Guid? ReferenceId,
    decimal Balance,
    DateTime CreatedAtUtc);

/// <summary>Hesap ekstresi — BalanceBeforePage sayfadaki ilk satırdan önceki kümülatif bakiye.</summary>
public sealed record AccountStatementResponse(
    Guid AccountId,
    string AccountName,
    string Currency,
    decimal BalanceBeforePage,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AccountTransactionDto> Items);

/// <summary>
/// Hesaplar arası transfer tek işlemde çıkış ve giriş çifti yazar.
/// Amount pozitiftir; kaynak hesaptan −amount, hedefe +amount.
/// </summary>
public sealed record TransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    DateTime Date,
    decimal Amount,
    string? Description);

/// <summary>Transfer sonrası iki hesabın güncel bakiyeleri.</summary>
public sealed record TransferResponse(
    Guid FromAccountId,
    decimal FromBalance,
    Guid ToAccountId,
    decimal ToBalance);
