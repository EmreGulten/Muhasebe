namespace Accounting.Domain.Enums;

/// <summary>
/// Kasa/banka hesabı türü: kasa, banka, kredi kartı,
/// sanal POS.
/// </summary>
public enum AccountType
{
    /// <summary>Nakit kasa.</summary>
    Cash = 1,

    /// <summary>Banka hesabı.</summary>
    Bank = 2,

    /// <summary>Kurumsal kredi kartı.</summary>
    CreditCard = 3,

    /// <summary>Sanal POS tahsilat hesabı.</summary>
    VirtualPOS = 4,
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

    /// <summary>Manuel kasa girişi (tahsilat; kasa modülü üretir).</summary>
    ManualCollection = 8,

    /// <summary>Manuel kasa çıkışı (ödeme; kasa modülü üretir).</summary>
    ManualPayment = 9,
}
