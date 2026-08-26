namespace Accounting.Application.Abstractions;

/// <summary>JWT yapılandırması (appsettings "Jwt" bölümü).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public int RefreshTokenLifetimeDays { get; init; } = 14;
}

/// <summary>Genel uygulama yapılandırması (appsettings "App" bölümü).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Frontend'in kök adresi; e-posta linkleri buraya üretilir.</summary>
    public string FrontendUrl { get; init; } = "http://localhost:3000";
}

/// <summary>
/// AI asistan yapılandırması (appsettings "Ai" bölümü).
/// ApiKey boşsa asistan offline moda düşer: anahtar kelime eşleştirmesiyle
/// yalnızca onaylı araçları çağırır, dış ağa hiç çıkmaz.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>OpenAI uyumlu API anahtarı; boş = offline mod.</summary>
    public string? ApiKey { get; init; }

    /// <summary>OpenAI uyumlu temel adres (OpenRouter/Groq gibi uçlar da bağlanabilir).</summary>
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";

    /// <summary>Chat completions modeli.</summary>
    public string Model { get; init; } = "gpt-4o-mini";

    /// <summary>İşletme başına aylık soru limiti.</summary>
    public int MonthlyQuestionLimit { get; init; } = 100;
}

/// <summary>
/// Abonelik yapılandırması (appsettings "Subscription" bölümü, bölüm 30):
/// yeni işletmeler deneme planıyla açılır.
/// </summary>
public sealed class SubscriptionOptions
{
    public const string SectionName = "Subscription";

    /// <summary>Deneme süresi (gün).</summary>
    public int TrialDays { get; init; } = 14;

    /// <summary>Denemede açılan plan kodu.</summary>
    public string TrialPlanCode { get; init; } = "pro";

    /// <summary>Ücretli dönem uzunluğu (gün) — plan değişiminde yeni dönem.</summary>
    public int BillingPeriodDays { get; init; } = 30;
}
