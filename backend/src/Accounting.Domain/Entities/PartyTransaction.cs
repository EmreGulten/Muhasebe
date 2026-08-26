using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Cari hareket — ekstrenin satırı.
/// Debit (borç) tarafı bize olan alacağı, Credit (alacak) tarafı bizim
/// borcumuzu artırır. Bakiye = ΣDebit − ΣCredit (pozitif = taraf bize borçlu).
///
/// Kayıtlar değişmez (immutable): hatalar "Manuel düzeltme" hareketiyle
/// telafi edilir, fiziksel güncelleme/silme yapılmaz.
/// </summary>
public class PartyTransaction : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid PartyId { get; set; }

    public PartyTransactionType Type { get; set; }

    /// <summary>Borç tutarı. Credit ile birlikte yalnızca biri sıfır farklı olabilir.</summary>
    public decimal Debit { get; set; }

    /// <summary>Alacak tutarı.</summary>
    public decimal Credit { get; set; }

    /// <summary>Belge/hareket tarihi (UTC). Ekstre bu tarihe göre sıralanır.</summary>
    public DateTime Date { get; set; }

    /// <summary>Vade tarihi — gecikmiş bakiye hesabında kullanılır (opsiyonel).</summary>
    public DateTime? DueDate { get; set; }

    public string? Description { get; set; }

    /// <summary>Referansı üreten kayıt (satış/alış/kasa). Manuel girişlerde null.</summary>
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Party Party { get; set; } = null!;
}
