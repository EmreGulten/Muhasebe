using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// Alış kalemı — satış kaleminin aynası. Ürün adı ve fiyatlar belgeye
/// snapshot edilir. Quantity numeric(18,4), tutarlar numeric(18,2).
/// </summary>
public class PurchaseItem : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid PurchaseId { get; set; }

    public Purchase Purchase { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>Ürün adı snapshot'ı — belge dökümünde kart yerine gösterilir.</summary>
    public string ProductName { get; set; } = null!;

    /// <summary>Pozitif alış miktarı.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Birim alış fiyatı (KDV hariç, iskonto öncesi).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Kalem iskonto oranı, yüzde (0–100).</summary>
    public decimal DiscountRate { get; set; }

    /// <summary>İskonto sonrası net tutar (KDV hariç).</summary>
    public decimal NetAmount { get; set; }

    /// <summary>KDV oranı, yüzde (0–100).</summary>
    public decimal VatRate { get; set; }

    /// <summary>Kalem KDV tutarı.</summary>
    public decimal VatAmount { get; set; }

    /// <summary>Kalem genel toplamı = NetAmount + VatAmount.</summary>
    public decimal LineTotal { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
