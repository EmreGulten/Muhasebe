using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Subscription;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Subscriptions;

internal static class SubscriptionQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
            ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");
}

/// <summary>Plan kataloğu (muhasebe.md bölüm 29) — fiyata göre artan sırada.</summary>
public sealed class ListSubscriptionPlansHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> HandleAsync(CancellationToken cancellationToken)
    {
        // Features CSV'si bellekte ayrıştırılır (EF Split'i SQL'e çevirmez).
        var plans = await db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync(cancellationToken);

        return plans.Select(ToDto).ToList();
    }

    internal static SubscriptionPlanDto ToDto(Domain.Entities.SubscriptionPlan plan) => new(
        plan.Code,
        plan.Name,
        plan.MonthlyPrice,
        plan.MaxUsers,
        plan.MaxWarehouses,
        plan.AiMonthlyQuestionLimit,
        [.. plan.FeatureSet().Order(StringComparer.Ordinal)]);
}

/// <summary>İşletmenin geçerli aboneliği — feature guard'ın görüntüsü.</summary>
public sealed class GetSubscriptionHandler(
    ICurrentTenant currentTenant,
    IFeatureGuard featureGuard)
{
    public async Task<SubscriptionResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = SubscriptionQueries.RequireTenantId(currentTenant);
        var snapshot = await featureGuard.ResolveAsync(tenantId, cancellationToken);

        return ToResponse(snapshot);
    }

    internal static SubscriptionResponse ToResponse(SubscriptionSnapshot snapshot) => new(
        new SubscriptionPlanDto(
            snapshot.Plan.Code,
            snapshot.Plan.Name,
            snapshot.Plan.MonthlyPrice,
            snapshot.Plan.MaxUsers,
            snapshot.Plan.MaxWarehouses,
            snapshot.Plan.AiMonthlyQuestionLimit,
            [.. snapshot.EffectiveFeatures.Order(StringComparer.Ordinal)]),
        snapshot.EffectiveStatus,
        snapshot.IsActive,
        snapshot.IsTrial,
        snapshot.TrialEndsAtUtc,
        snapshot.CurrentPeriodStartUtc,
        snapshot.CurrentPeriodEndUtc,
        snapshot.DaysRemaining,
        [.. snapshot.EffectiveFeatures.Order(StringComparer.Ordinal)]);
}

/// <summary>
/// Plan değiştirme (muhasebe.md bölüm 30–31). MVP'de doğrudan dönem açar;
/// ödeme sağlayıcısı soyutlaması (IPaymentProvider) arkasına iyzico/PayTR
/// takılınca checkout akışına bağlanır. Uç nokta Tenant.Manage izni ister.
/// </summary>
public sealed class ChangePlanHandler(
    IApplicationDbContext db,
    ICurrentTenant currentTenant,
    SubscriptionService subscriptions,
    IPaymentProvider paymentProvider,
    IFeatureGuard featureGuard)
{
    public async Task<SubscriptionResponse> HandleAsync(ChangePlanRequest request, CancellationToken cancellationToken)
    {
        var tenantId = SubscriptionQueries.RequireTenantId(currentTenant);
        var planCode = request.PlanCode?.Trim();
        if (string.IsNullOrWhiteSpace(planCode))
        {
            throw new AppException("Plan kodu boş olamaz.");
        }

        var plan = await db.SubscriptionPlans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Code == planCode && p.IsActive, cancellationToken)
            ?? throw new AppException($"'{planCode}' adında bir plan yok.", 404, "Plan bulunamadı");

        // Ödeme sağlayıcısına checkout oturumu açtırılır (MVP: fake provider).
        // Plan değişimi MVP'de ödemeyi beklemeden uygulanır.
        await paymentProvider.CreateCheckoutAsync(
            new CheckoutRequest(tenantId, plan.Code, plan.MonthlyPrice, "TRY", CustomerEmail: string.Empty),
            cancellationToken);

        await subscriptions.ChangePlanAsync(tenantId, plan, cancellationToken);

        var snapshot = await featureGuard.ResolveAsync(tenantId, cancellationToken);
        return GetSubscriptionHandler.ToResponse(snapshot);
    }
}
