using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Subscriptions;
using Accounting.Contracts.Subscription;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Abonelik uç noktaları (muhasebe.md bölüm 29–31, PHASE 10).</summary>
public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/subscription").WithTags("Abonelik");

        group.MapGet("/plans", async (
                ListSubscriptionPlansHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("ListSubscriptionPlans")
            .WithSummary("Plan kataloğu — Başlangıç / Pro / İşletme (bölüm 29)")
            .RequireTenant();

        group.MapGet("/", async (
                GetSubscriptionHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("GetSubscription")
            .WithSummary("İşletmenin geçerli aboneliği — plan, durum, deneme, kalan gün")
            .RequireTenant();

        group.MapPost("/change", async (
                ChangePlanRequest request,
                ChangePlanHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("ChangePlan")
            .WithSummary("Plan değiştir — yeni dönem açar (ödeme sağlayıcısı soyutlaması üzerinden)")
            .RequireTenant()
            .RequirePermission(Permissions.TenantManage);
    }
}
