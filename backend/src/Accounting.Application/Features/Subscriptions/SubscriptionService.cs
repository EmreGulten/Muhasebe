using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accounting.Application.Features.Subscriptions;

/// <summary>
/// Feature guard uygulaması (muhasebe.md bölüm 30): işletmenin aboneliğini
/// çözümler, plana göre özellik/kota kontrolü yapar. Aboneliği olmayan ya da
/// dönemi bitmiş işletmeler core özelliklerle çalışır (başlangıç varsayılanı).
/// </summary>
public sealed class SubscriptionService(
    IApplicationDbContext db,
    TimeProvider timeProvider,
    IOptions<SubscriptionOptions> subscriptionOptions) : IFeatureGuard
{
    /// <summary>Abonelik kaydı olmayan işletmelere uygulanan varsayılan plan.</summary>
    public const string FallbackPlanCode = SubscriptionPlans.StarterCode;

    public async Task<SubscriptionSnapshot> ResolveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);
        var plan = await ResolvePlanAsync(tenantId, cancellationToken);

        var subscription = await db.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Abonelik yoksa: core varsayılanı.
        if (subscription is null)
        {
            return new SubscriptionSnapshot(
                HasSubscription: false,
                plan,
                EffectiveStatus: SubscriptionStatus.Expired.ToString(),
                IsActive: false,
                IsTrial: false,
                TrialEndsAtUtc: null,
                CurrentPeriodStartUtc: now,
                CurrentPeriodEndUtc: now,
                DaysRemaining: 0,
                EffectiveFeatures: CoreOnly());
        }

        // Dönem bitmişse etkin durum Expired; iptal edilmiş olsa da dönem
        // sonuna dek plan özellikleri yaşar (geldiği gün için hizmet verir).
        var periodEnd = DateTime.SpecifyKind(subscription.CurrentPeriodEndUtc, DateTimeKind.Utc);
        var isActive = subscription.Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue
            && periodEnd > now;
        var effectiveStatus = isActive ? subscription.Status : SubscriptionStatus.Expired;
        var daysRemaining = Math.Max(0, (int)(periodEnd.Date - now.Date).Days);

        return new SubscriptionSnapshot(
            HasSubscription: true,
            plan,
            effectiveStatus.ToString(),
            isActive,
            IsTrial: subscription.Status == SubscriptionStatus.Trialing && isActive,
            subscription.TrialEndsAtUtc,
            DateTime.SpecifyKind(subscription.CurrentPeriodStartUtc, DateTimeKind.Utc),
            periodEnd,
            daysRemaining,
            isActive ? MergeFeatures(plan) : CoreOnly());
    }

    public async Task EnsureFeatureAsync(Guid tenantId, string feature, CancellationToken cancellationToken)
    {
        var snapshot = await ResolveAsync(tenantId, cancellationToken);
        if (!snapshot.HasFeature(feature))
        {
            var featureName = FeatureLabels.TryGetValue(feature, out var label) ? label : feature;
            throw new AppException(
                $"'{featureName}' özelliği planınızda yer almıyor. Pro veya İşletme planına geçebilirsiniz.",
                403,
                "Plan kısıtı");
        }
    }

    public async Task<int> AiMonthlyLimitAsync(Guid tenantId, int fallbackLimit, CancellationToken cancellationToken)
    {
        var snapshot = await ResolveAsync(tenantId, cancellationToken);
        if (snapshot.Plan.AiMonthlyQuestionLimit <= 0 || !snapshot.HasFeature(PlanFeatures.AiAssistant))
        {
            return fallbackLimit;
        }

        // Plan tavanı belirler; operatör ayarı (AI__MONTHLYQUESTIONLIMIT) bu
        // tavanı yalnızca aşağı çekebilir — küresel kısıtlama için.
        return fallbackLimit > 0
            ? Math.Min(snapshot.Plan.AiMonthlyQuestionLimit, fallbackLimit)
            : snapshot.Plan.AiMonthlyQuestionLimit;
    }

    /// <summary>Yeni işletme için deneme aboneliği açar (kayıtta çağrılır).</summary>
    public async Task StartTrialAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var plan = await db.SubscriptionPlans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Code == subscriptionOptions.Value.TrialPlanCode, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Deneme planı bulunamadı: {subscriptionOptions.Value.TrialPlanCode}");

        var trialEnd = now.AddDays(subscriptionOptions.Value.TrialDays);
        db.Subscriptions.Add(new Subscription
        {
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Trialing,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = trialEnd,
            TrialEndsAtUtc = trialEnd,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Plan değiştirir: yeni dönem açar, deneme/iptal bayraklarını temizler.</summary>
    public async Task<Subscription> ChangePlanAsync(
        Guid tenantId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var periodEnd = now.AddDays(subscriptionOptions.Value.BillingPeriodDays);

        var subscription = await db.Subscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            subscription = new Subscription
            {
                TenantId = tenantId,
                CreatedAtUtc = now,
            };
            db.Subscriptions.Add(subscription);
        }

        subscription.PlanId = plan.Id;
        subscription.Status = SubscriptionStatus.Active;
        subscription.CurrentPeriodStartUtc = now;
        subscription.CurrentPeriodEndUtc = periodEnd;
        subscription.TrialEndsAtUtc = null;
        subscription.CancelledAtUtc = null;
        subscription.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    private async Task<SubscriptionPlan> ResolvePlanAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var planId = await db.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ThenByDescending(s => s.Id)
            .Select(s => (Guid?)s.PlanId)
            .FirstOrDefaultAsync(cancellationToken);

        return planId is null
            ? await db.SubscriptionPlans.AsNoTracking()
                .SingleAsync(p => p.Code == FallbackPlanCode, cancellationToken)
            : await db.SubscriptionPlans.AsNoTracking()
                .SingleAsync(p => p.Id == planId, cancellationToken);
    }

    private static HashSet<string> CoreOnly() => [PlanFeatures.Core];

    private static HashSet<string> MergeFeatures(SubscriptionPlan plan)
    {
        var set = plan.FeatureSet();
        set.Add(PlanFeatures.Core);
        return set;
    }

    /// <summary>Hata iletilerinde özellik anahtarı → insan-readable ad.</summary>
    private static readonly Dictionary<string, string> FeatureLabels = new(StringComparer.Ordinal)
    {
        [PlanFeatures.Stock] = "Stok yönetimi",
        [PlanFeatures.Purchases] = "Alış yönetimi",
        [PlanFeatures.AdvancedReports] = "Gelişmiş raporlar",
        [PlanFeatures.AiAssistant] = "AI Asistan",
        [PlanFeatures.MultiWarehouse] = "Çoklu depo",
        [PlanFeatures.Api] = "API erişimi",
        [PlanFeatures.Integrations] = "Entegrasyonlar",
        [PlanFeatures.Quotes] = "Teklif yönetimi",
    };
}
