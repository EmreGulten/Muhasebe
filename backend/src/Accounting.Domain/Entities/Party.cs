using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Cari taraf — müşteri ve/veya tedarikçi (muhasebe.md bölüm 4.1).
/// Tek yapı hem müşteri hem tedarikçiyi karşılar; Type ayırt eder.
/// </summary>
public class Party : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public PartyType Type { get; set; } = PartyType.Customer;

    public string Name { get; set; } = null!;

    /// <summary>Vergi/TCKN. Zorunlu değil — esnaf müşterileri genelde girmez.</summary>
    public string? TaxNumber { get; set; }

    public string? TaxOffice { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public string? ContactName { get; set; }

    /// <summary>
    /// Açılış bakiyesi (pozitif = taraf bize borçlu, negatif = biz borçluyuz).
    /// Parti oluşturulurken tek seferlik OpeningBalance hareketine dönüşür;
    /// güncellenemez.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Kredi limiti (müşteriler için uyarı eşiği). 0 = limitsiz.</summary>
    public decimal CreditLimit { get; set; }

    public string? Notes { get; set; }

    /// <summary>Pasif cari listede görünür ama yeni işlem girişinde uyarılır.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<PartyTransaction> Transactions { get; set; } = [];
}
