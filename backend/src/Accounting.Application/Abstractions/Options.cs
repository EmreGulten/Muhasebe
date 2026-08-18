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
