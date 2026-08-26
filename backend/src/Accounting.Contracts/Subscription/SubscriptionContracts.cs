namespace Accounting.Contracts.Subscription;

/// <summary>Abonelik planı kartı.</summary>
public sealed record SubscriptionPlanDto(
    string Code,
    string Name,
    decimal MonthlyPrice,
    int MaxUsers,
    int MaxWarehouses,
    int AiMonthlyQuestionLimit,
    IReadOnlyList<string> Features);

/// <summary>İşletmenin geçerli abonelik durumu — feature guard'ın çözümlediği anlık görüntü.</summary>
public sealed record SubscriptionResponse(
    SubscriptionPlanDto Plan,
    string Status,
    bool IsActive,
    bool IsTrial,
    DateTime? TrialEndsAtUtc,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    int DaysRemaining,
    IReadOnlyList<string> EffectiveFeatures);

/// <summary>Plan değiştirme isteği; MVP'de ödeme sağlayıcısı soyutlaması üzerinden ilerler.</summary>
public sealed record ChangePlanRequest(string PlanCode);
