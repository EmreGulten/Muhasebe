namespace Accounting.Domain.Enums;

/// <summary>
/// Satış belgesi durumu (muhasebe.md bölüm 6). Draft düzenlenebilir;
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

/// <summary>
/// Kasa/banka hesap hareketi türü. Satış/Alış modülleri ve manuel türler
/// üretir; Amount işaretlidir (pozitif = hesaba giriş, negatif = çıkış).
/// </summary>
public enum AccountTransactionType
{
    /// <summary>Satış tahsilatı (satış modülü üretir).</summary>
    SaleCollection = 1,

    /// <summary>Alış ödemesi (alış modülü üretir).</summary>
    PurchasePayment = 2,

    /// <summary>Manuel gelir kaydı.</summary>
    Income = 3,

    /// <summary>Manuel gider kaydı.</summary>
    Expense = 4,

    /// <summary>Hesaplar arası transfer (kasa modülü üretir; iki satır).</summary>
    Transfer = 5,

    /// <summary>Açılış bakiyesi.</summary>
    OpeningBalance = 6,

    /// <summary>İptal iadesi — iptal edilen belgenin ödemesinin geri alınması.</summary>
    Refund = 7,
}
