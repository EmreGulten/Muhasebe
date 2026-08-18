using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>Ölçü birimi (adet, kg, saat, metre...). Tenant bazında benzersiz ad.</summary>
public class Unit : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Kısa gösterim (ad, kg, sa). Listelerde ve belgelerde kullanılır.</summary>
    public string? Code { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
