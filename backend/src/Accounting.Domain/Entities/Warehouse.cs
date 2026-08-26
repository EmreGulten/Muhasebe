using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// Depo. MVP tek depoyla başlar; veri modeli çoklu depoya hazırdır.
/// Her tenant'ta bir varsayılan depo bulunur.
/// </summary>
public class Warehouse : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    /// <summary>Tenant başına tek varsayılan depo; hareket girişinde önceliklidir.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<InventoryTransaction> Transactions { get; set; } = [];
}
