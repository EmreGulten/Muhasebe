using System.Security.Claims;
using System.Text.Json;
using Accounting.Application.Abstractions;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Middleware;

/// <summary>
/// [RequireTenant] işaretli endpoint'lerde X-Tenant-Id başlığını çözer,
/// kullanıcının o işletmede üyeliğini doğrular ve istek kapsamı TenantContext'i
/// set eder. Tenant izolasyonunun backend seviyesinde zorlandığı noktadır
/// (muhasebe.md bölüm 13).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string TenantHeader = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, ITenantContextWriter tenantContext, IApplicationDbContext db)
    {
        var requiresTenant = context.GetEndpoint()?.Metadata.GetMetadata<RequireTenantAttribute>() is not null;

        if (requiresTenant)
        {
            if (!TryGetUserId(context, out var userId))
            {
                await WriteProblem(context, StatusCodes.Status401Unauthorized, "Yetkisiz", "Önce giriş yapmalısınız.");
                return;
            }

            if (!Guid.TryParse(context.Request.Headers[TenantHeader].ToString(), out var tenantId))
            {
                await WriteProblem(context, StatusCodes.Status400BadRequest, "Eksik işletme bilgisi",
                    "İstek geçerli bir X-Tenant-Id başlığı içermiyor.");
                return;
            }

            var membership = await db.UserTenants
                .Where(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive && !m.Tenant.IsDeleted)
                .Select(m => (TenantRole?)m.Role)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (membership is null)
            {
                await WriteProblem(context, StatusCodes.Status403Forbidden, "Erişim engellendi",
                    "Bu işletmeye erişim yetkiniz yok.");
                return;
            }

            tenantContext.SetTenant(tenantId, membership.Value);
        }

        await next(context);
    }

    private static bool TryGetUserId(HttpContext context, out Guid userId)
    {
        var value = context.User.FindFirstValue("sub")
                    ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out userId!) && userId != Guid.Empty;
    }

    private static async Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = $"https://httpstatuses.io/{status}",
            title,
            status,
            detail,
            instance = context.Request.Path.ToString(),
        }, JsonSerializerOptions.Web));
    }
}

/// <summary>Endpoint'in geçerli bir tenant bağlamı gerektirdiğini belirtir.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireTenantAttribute : Attribute;

public static class RequireTenantExtensions
{
    /// <summary>Endpoint'i kimlik doğrulama + tenant üyelik doğrulamasına bağlar.</summary>
    public static RouteHandlerBuilder RequireTenant(this RouteHandlerBuilder builder) =>
        builder
            .WithMetadata(new RequireTenantAttribute())
            .RequireAuthorization();
}
