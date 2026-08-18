using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// Stok hareketi (muhasebe.md bölüm 5.2). Defter modeli: kayıtlar
/// değiştirilemez/silinemez; düzeltmeler yeni (Sayım/Düzeltme) hareketiyle
/// yapılır. Quantity işaretlidir: pozitif = stoğa giriş, negatif = çıkış.
/// Miktar numeric(18,4).
/// </summary>
public class InventoryTransaction : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid WarehouseId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>Üretilen hareket türü (Alış/Satış modüllerden, diğerleri manuel).</summary>
    public Domain.Enums.InventoryTransactionType Type { get; set; }

    /// <summary>İşaretli miktar: giriş pozitif, çıkış negatif.</summary>
    public decimal Quantity { get; set; }

    public DateTime Date { get; set; }

    public string? Description { get; set; }

    /// <summary>Hareketi üreten kayıt (Sale, Purchase, Transfer...).</summary>
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
