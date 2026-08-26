using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// Ürün veya hizmet. Hizmetler stok takibi yapmaz —
/// IsService=true iken stok hareketi girilemez.
/// </summary>
public class Product : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    /// <summary>Tenant içinde benzersiz stok kodu. Boş verilebilir (barkod ya da adla çalışılır).</summary>
    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }

    public Category? Category { get; set; }

    public Guid? UnitId { get; set; }

    public Unit? Unit { get; set; }

    /// <summary>Alış fiyatı (numeric(18,2)).</summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>Satış fiyatı (numeric(18,2)).</summary>
    public decimal SalePrice { get; set; }

    /// <summary>KDV oranı, yüzde (0-100, numeric(5,2)).</summary>
    public decimal VatRate { get; set; }

    /// <summary>Kritik stok eşiği. 0 = kritik stok uyarısı yok.</summary>
    public decimal MinimumStock { get; set; }

    /// <summary>Hizmet ise stok hareketi alamaz.</summary>
    public bool IsService { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = [];
}
