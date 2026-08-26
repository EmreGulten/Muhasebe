namespace Accounting.Domain.Enums;

/// <summary>
/// Stok hareket türleri.
/// Alış/Satış hareketleri ilgili modüller tarafından üretilir;
/// kalan türler kullanıcı tarafından manuel girilebilir.
/// </summary>
public enum InventoryTransactionType
{
    /// <summary>Alış belgesi onayından oluşur.</summary>
    Purchase = 1,

    /// <summary>Satış belgesi onayından oluşur.</summary>
    Sale = 2,

    /// <summary>Sayım sonucu fark hareketi (girilen miktar − güncel stok).</summary>
    Count = 3,

    /// <summary>Manuel giriş (fire ekleme, hediye vb.).</summary>
    ManualIn = 4,

    /// <summary>Manuel çıkış (fire, zayi vb.).</summary>
    ManualOut = 5,

    /// <summary>İade — müşteri iadesi stoğa ekler.</summary>
    Return = 6,

    /// <summary>Depolar arası transfer — kaynak çıkış + hedef giriş olmak üzere iki satır.</summary>
    Transfer = 7,
}
