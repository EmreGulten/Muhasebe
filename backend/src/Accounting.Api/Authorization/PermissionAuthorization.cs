using Accounting.Application.Abstractions;
using Accounting.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Accounting.Api.Authorization;

/// <summary>Endpoint'in gerektirdiği izin.</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Aktif tenant bağlamındaki rolü RolePermissions haritasıyla karşılaştırır.
/// RequireTenant middleware'i bağlamı set ettiği için değerlendirme sırasında
/// TenantId/Role doludur.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var currentTenant = context.Resource is HttpContext httpContext
            ? httpContext.RequestServices.GetService<ICurrentTenant>()
            : null;

        if (currentTenant?.TenantId is not { } tenantId)
        {
            context.Fail(new AuthorizationFailureReason(this, "Aktif işletme bağlamı yok."));
            return Task.CompletedTask;
        }

        if (currentTenant.Role is not { } role)
        {
            context.Fail(new AuthorizationFailureReason(this, "Rol bilgisi yok."));
            return Task.CompletedTask;
        }

        if (RolePermissions.For(role).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, $"Bu işlem için '{requirement.Permission}' izni gerekir."));
        }

        return Task.CompletedTask;
    }
}

/// <summary>"perm:&lt;Permission&gt;" biçimli politikaları anında üretir.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var permission = policyName[Prefix.Length..];
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Endpoint'i izin politikasına bağlar. RequireTenant ile birlikte kullanılır;
    /// tenant bağlamı olmadan izin değerlendirilemez.
    /// </summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission) =>
        builder.RequireAuthorization($"{PermissionPolicyProvider.Prefix}{permission}");
}
