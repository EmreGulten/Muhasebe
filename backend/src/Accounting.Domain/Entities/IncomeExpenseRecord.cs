using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Gelir ya da gider kaydı (muhasebe.md bölüm 8). Oluşturulduğunda ilgili
/// kasa/banka hesabına işaretli hareket yazar (gelir +, gider −); kayıt ve
/// hareket tek transaction'da yazılır (bölüm 24). Defter modeli: değiştirilemez
/// ve silinemez; düzeltme iptal (ters hareket) ile yapılır (bölüm 23).
/// </summary>
public class IncomeExpenseRecord : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public IncomeExpenseType Type { get; set; }

    public Guid CategoryId { get; set; }

    public IncomeExpenseCategory Category { get; set; } = null!;

    /// <summary>Pozitif tutar; yön Type'tan gelir. numeric(18,2).</summary>
    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    /// <summary>Paranın girdiği/çıktığı kasa/banka hesabı.</summary>
    public Guid PaymentAccountId { get; set; }

    public Account PaymentAccount { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Belge/fatura numarası (serbest metin).</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Makbuz/fatura görseli — MVP'de yükleme yok, alan hazır (bölüm 8).</summary>
    public string? AttachmentUrl { get; set; }

    public IncomeExpenseStatus Status { get; set; } = IncomeExpenseStatus.Active;

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
