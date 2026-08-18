using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>Ürün kategorisi (tenant bazında). Silinen kategorinin ürünleri etkilenmez.</summary>
public class Category : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>Kategorideki ürün sayısı listelerde gösterilir; ürün kaldırılamaz, pasifleşir.</summary>
    public ICollection<Product> Products { get; set; } = [];
}
