using System.Security.Claims;
using Accounting.Api.Extensions;
using Accounting.Api.Middleware;
using Accounting.Application.Abstractions;
using Accounting.Application.Features.Tenants;
using Accounting.Contracts.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Accounting.Api.Endpoints;

public static class TenantEndpoints
{
    public static RouteGroupBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants").WithTags("Tenants").RequireAuthorization();

        group.MapGet("/", async (
                ClaimsPrincipal principal,
                ListTenantsHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(principal.GetUserId(), cancellationToken)))
            .WithName("ListTenants")
            .WithSummary("Kullanıcının üye olduğu işletmeler");

        group.MapPost("/", async (
                CreateTenantRequest request,
                ClaimsPrincipal principal,
                CreateTenantHandler handler,
                CancellationToken cancellationToken) =>
            {
                var tenant = await handler.HandleAsync(principal.GetUserId(), request, cancellationToken);
                return Results.Created($"/api/v1/tenants/{tenant.Id}", tenant);
            })
            .WithName("CreateTenant")
            .WithSummary("Yeni işletme oluşturur; oluşturan Owner olur");

        group.MapGet("/current", (
                ICurrentTenant currentTenant) =>
            Results.Ok(new
            {
                currentTenant.TenantId,
                Role = currentTenant.Role?.ToString(),
            }))
            .WithName("GetCurrentTenant")
            .WithSummary("Aktif tenant bağlamı (X-Tenant-Id + üyelik doğrulaması)")
            .RequireTenant();

        group.MapGet("/{id:guid}", async (
                Guid id,
                ClaimsPrincipal principal,
                GetTenantHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(principal.GetUserId(), id, cancellationToken)))
            .WithName("GetTenant")
            .WithSummary("İşletme bilgisi (yalnızca üyeler)");

        return group;
    }
}
