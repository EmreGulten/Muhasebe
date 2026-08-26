namespace Accounting.Domain.Enums;

/// <summary>Abonelik durumu.</summary>
public enum SubscriptionStatus
{
    /// <summary>Ücretsiz deneme süresi içinde (tam plan özellikleri).</summary>
    Trialing = 1,

    /// <summary>Ödenmiş, aktif abonelik.</summary>
    Active = 2,

    /// <summary>Ödeme başarısız — yasal süre sonunda Expired'a düşer.</summary>
    PastDue = 3,

    /// <summary>Kullanıcı iptal etti; dönem sonuna dek aktif kalır.</summary>
    Cancelled = 4,

    /// <summary>Dönemi bitmiş abonelik.</summary>
    Expired = 5,
}

/// <summary>
/// Plan özellik anahtarları. Feature guard bu
/// anahtarlarla çalışır: core her planda vardır, diğerleri planın Features
/// listesinde yer alıyorsa açıktır.
/// </summary>
public static class PlanFeatures
{
    /// <summary>Cari, gelir/gider, kasa, temel satış ve temel raporlar — her planda.</summary>
    public const string Core = "core";

    /// <summary>Stok modülü: stok hareketleri, transfer, kritik stok, stok raporu.</summary>
    public const string Stock = "stock";

    /// <summary>Alış belgeleri.</summary>
    public const string Purchases = "purchases";

    /// <summary>Gelişmiş raporlar.</summary>
    public const string AdvancedReports = "reports_advanced";

    /// <summary>AI asistan.</summary>
    public const string AiAssistant = "ai_assistant";

    /// <summary>Çoklu depo (plan depo üst sınırının kaldırılması).</summary>
    public const string MultiWarehouse = "multi_warehouse";

    /// <summary>API erişimi (gelecekte).</summary>
    public const string Api = "api";

    /// <summary>E-ticaret entegrasyonları (gelecekte).</summary>
    public const string Integrations = "integrations";

    /// <summary>Teklif modülü (henüz yapılmadı; plan meta verisi).</summary>
    public const string Quotes = "quotes";
}

/// <summary>Plan kodları ve sabit kimlikleri — tohum veriyle hizalı.</summary>
public static class SubscriptionPlans
{
    public const string StarterCode = "starter";
    public const string ProCode = "pro";
    public const string BusinessCode = "business";

    public static readonly Guid StarterId = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ProId = new("10000000-0000-0000-0000-000000000002");
    public static readonly Guid BusinessId = new("10000000-0000-0000-0000-000000000003");
}
