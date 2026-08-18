using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// Satış tahsilatı. Onay anında ve sonradan girilebilir; her tahsilat bir
/// kasa hareketi (AccountTransaction/SaleCollection) ve müşterili satışta
/// bir alacak cari hareketi üretir. Defter modeli: silinemez, iptal iade
/// hareketiyle düzeltilir.
/// </summary>
public class SalePayment : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    /// <summary>Tahsilatın yazıldığı kasa/banka hesabı.</summary>
    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public DateTime Date { get; set; }

    /// <summary>Pozitif tahsilat tutarı.</summary>
    public decimal Amount { get; set; }

    public string? Description { get; set; }

    /// <summary>Tahsilatı üreten işlem onayı mı (Confirm) yoksa sonradan mı girildi.</summary>
    public bool PaidOnConfirm { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
