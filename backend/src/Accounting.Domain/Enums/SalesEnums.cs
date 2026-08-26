namespace Accounting.Domain.Enums;

/// <summary>
/// Satış belgesi durumu. Draft düzenlenebilir;
/// onaydan sonra belge değiştirilemez, düzeltme iptal + yeni belge ile yapılır.
/// Ödeme durumu (Confirmed/PartiallyPaid/Paid) tahsilat toplamından türetilir.
/// </summary>
public enum SaleStatus
{
    /// <summary>Taslak — kalem/düzenleme açık, stok ve cari etkisi yok.</summary>
    Draft = 1,

    /// <summary>Onaylı, ödeme yok.</summary>
    Confirmed = 2,

    /// <summary>Kısmen tahsil edildi.</summary>
    PartiallyPaid = 3,

    /// <summary>Tamamı tahsil edildi.</summary>
    Paid = 4,

    /// <summary>İptal — terminal durum, ters hareketler yazılmıştır.</summary>
    Cancelled = 5,
}

