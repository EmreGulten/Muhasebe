using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Kasa/banka hesap hareketi. Defter modeli: kayıtlar değiştirilemez/
/// silinemez. Amount işaretlidir: pozitif = hesaba giriş (tahsilat, gelir),
/// negatif = çıkış (ödeme, gider, iade). Tutar numeric(18,2).
/// </summary>
public class AccountTransaction : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public AccountTransactionType Type { get; set; }

    /// <summary>İşaretli tutar: giriş pozitif, çıkış negatif.</summary>
    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    public string? Description { get; set; }

    /// <summary>Hareketi üreten kayıt (Sale, SalePayment, Purchase...).</summary>
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
