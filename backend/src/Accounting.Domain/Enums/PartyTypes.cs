namespace Accounting.Domain.Enums;

/// <summary>
/// Cari türü. "Both" hem müşteri hem tedarikçi olan taraflar içindir.
/// </summary>
public enum PartyType
{
    Customer = 1,
    Supplier = 2,
    Both = 3,
}

/// <summary>
/// Cari hareket türü.
/// Satış/Tahsilat/Alış/Ödeme türleri ileride ilgili modüller
/// (satış onayı, kasa hareketi) tarafından otomatik üretilir;
/// 'de yalnızca manuel türler API'den kabul edilir.
/// </summary>
public enum PartyTransactionType
{
    /// <summary>Satıştan doğan borç (satış modülü üretir).</summary>
    Sale = 1,

    /// <summary>Tahsilat — müşteriden alınan para (kasa/banka modülü üretir).</summary>
    Collection = 2,

    /// <summary>Alıştan doğan borç (alış modülü üretir).</summary>
    Purchase = 3,

    /// <summary>Ödeme — tedarikçiye ödenen para (kasa/banka modülü üretir).</summary>
    Payment = 4,

    /// <summary>Manuel borçlandırma (müşterinin bakiyesini artırır).</summary>
    Debit = 5,

    /// <summary>Manuel alacaklandırma (bakiyeyi lehe çevirir).</summary>
    Credit = 6,

    /// <summary>Açılış bakiyesi (parti oluşturulurken girilir).</summary>
    OpeningBalance = 7,

    /// <summary>Manuel düzeltme (hatalı kayıt telafisi).</summary>
    Adjustment = 8,
}
