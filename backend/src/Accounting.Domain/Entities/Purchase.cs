using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Alış belgesi (muhasebe.md bölüm 7). Satışın ayna görüntüsü: onay tek
/// transaction'da stok girişi + tedarikçi borcu (+ varsa anlık ödeme) üretir.
/// Tutarlar numeric(18,2).
/// </summary>
public class Purchase : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    /// <summary>Tenant içinde benzersiz belge numarası (P-000001...).</summary>
    public string Number { get; set; } = null!;

    /// <summary>Tedarikçi; null = tedarikçisiz (nakit) alış, cari hareketi yazılmaz.</summary>
    public Guid? PartyId { get; set; }

    public Party? Party { get; set; }

    /// <summary>Stoğun gireceği depo (tüm kalemler tek depoya).</summary>
    public Guid WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public DateTime Date { get; set; }

    /// <summary>Vade tarihi — cari hareketine vade olarak geçer.</summary>
    public DateTime? DueDate { get; set; }

    public string? Description { get; set; }

    /// <summary>Kalem net toplamları (iskonto sonrası, KDV hariç).</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Kalem iskonto tutarlarının toplamı.</summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>Kalem KDV tutarlarının toplamı.</summary>
    public decimal VatTotal { get; set; }

    /// <summary>Genel toplam = SubTotal + VatTotal.</summary>
    public decimal Total { get; set; }

    /// <summary>Ödenen tutar (PurchasePayment toplamı; iptalde iade düşülür).</summary>
    public decimal PaidAmount { get; set; }

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

    public DateTime? ConfirmedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>İptal gerekçesi (iptal kaydında zorunlu).</summary>
    public string? CancelReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = [];

    public ICollection<PurchasePayment> Payments { get; set; } = [];
}
