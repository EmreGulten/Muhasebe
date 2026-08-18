using Microsoft.AspNetCore.Http;

namespace Accounting.Api.Extensions;

/// <summary>
/// Refresh token httpOnly cookie'si. JS okuyamaz (XSS'e kapalı);
/// SameSite=Lax ile yalnızca aynı origin isteklerinde gönderilir.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "muhasebe_refresh";

    private const string CookiePath = "/";

    public static void Set(HttpContext context, string token, DateTime expiresAtUtc)
    {
        var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();

        context.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
            Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
            IsEssential = true,
        });
    }

    public static string? Read(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Name, out var token) ? token : null;

    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
        });
}
