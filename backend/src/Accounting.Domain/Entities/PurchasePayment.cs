using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// Alış ödemesi. Onay anında ve sonradan girilebilir; her ödeme kasadan
/// çıkış (AccountTransaction/PurchasePayment, negatif işaretli) ve
/// tedarikçili alışta bir borç kapatan cari hareketi üretir. Defter
/// modeli: silinemez, iptal iade hareketiyle düzeltilir.
/// </summary>
public class PurchasePayment : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid PurchaseId { get; set; }

    public Purchase Purchase { get; set; } = null!;

    /// <summary>Ödemenin yazıldığı kasa/banka hesabı.</summary>
    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public DateTime Date { get; set; }

    /// <summary>Pozitif ödeme tutarı (kasa hareketine negatif işaretle yazılır).</summary>
    public decimal Amount { get; set; }

    public string? Description { get; set; }

    /// <summary>Ödemeyi üreten işlem onayı mı (Confirm) yoksa sonradan mı girildi.</summary>
    public bool PaidOnConfirm { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
