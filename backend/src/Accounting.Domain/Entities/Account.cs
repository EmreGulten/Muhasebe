using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Kasa/banka hesabı (muhasebe.md bölüm 9). Her tenant'ta ilk tahsilat ya da
/// ödemede "Kasa" varsayılan hesabı lazy oluşur (satış/alış modülleri yazar);
/// hesap yönetimi (tür, para birimi, açılış bakiyesi, transfer, döküm)
/// PHASE 6 ile açıktır. Bakiye = Σ hesap hareketleri (açılış dahil).
/// </summary>
public class Account : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Hesap türü (bölüm 9): kasa, banka, kredi kartı, sanal POS.</summary>
    public AccountType Type { get; set; } = AccountType.Cash;

    /// <summary>ISO 4217 para birimi (MVP'de tek para birimi: TRY).</summary>
    public string Currency { get; set; } = "TRY";

    /// <summary>
    /// Açılış bakiyesi — hesap oluşturulurken tek seferlik OpeningBalance
    /// hareketi olarak deftere yazılır; güncel bakiye hareketlerin toplamıdır.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Tenant başına tek varsayılan hesap; tahsilat/ödeme önceliklidir.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<AccountTransaction> Transactions { get; set; } = [];
}
