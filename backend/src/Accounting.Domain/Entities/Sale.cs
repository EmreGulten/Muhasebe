using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Satış belgesi. Draft aşamasında düzenlenebilir;
/// onay (Confirm) tek transaction'da stok düşümü + cari borç (+ varsa tahsilat)
/// üretir. Onaydan sonra belge değiştirilemez; düzeltme iptal ile yapılır.
/// Tutarlar numeric(18,2).
/// </summary>
public class Sale : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    /// <summary>Tenant içinde benzersiz belge numarası (S-000001...).</summary>
    public string Number { get; set; } = null!;

    /// <summary>Müşteri; null = müşterisiz (nakit) satış, cari hareketi yazılmaz.</summary>
    public Guid? PartyId { get; set; }

    public Party? Party { get; set; }

    /// <summary>Stoğun düşüleceği depo (tüm kalemler tek depodan).</summary>
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

    /// <summary>Tahsil edilen tutar (SalePayment toplamı; iptalde iade düşülür).</summary>
    public decimal PaidAmount { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Draft;

    public DateTime? ConfirmedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>İptal gerekçesi (iptal kaydında zorunlu).</summary>
    public string? CancelReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<SaleItem> Items { get; set; } = [];

    public ICollection<SalePayment> Payments { get; set; } = [];
}
