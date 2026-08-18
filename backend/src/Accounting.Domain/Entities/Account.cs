using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// Kasa/banka hesabı (muhasebe.md bölüm 9). PHASE 4'te satış onayının tahsilat
/// ayağı için minimal olarak gelir: her tenant'ta ilk kullanımda "Kasa"
/// varsayılan hesabı oluşur; hesap yönetimi (ekleme, transfer, döküm)
/// PHASE 6'da açılır.
/// </summary>
public class Account : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Tenant başına tek varsayılan hesap; tahsilat/ödeme önceliklidir.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<AccountTransaction> Transactions { get; set; } = [];
}
