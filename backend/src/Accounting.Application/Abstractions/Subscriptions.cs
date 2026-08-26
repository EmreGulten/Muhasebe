using Accounting.Domain.Entities;
using Accounting.Domain.Enums;

namespace Accounting.Application.Abstractions;

/// <summary>
/// İşletmenin anlık abonelik görüntüsü: geçerli plan, etkin özellikler ve
/// limitler. Aboneliği olmayan işletmeler başlangıç planı varsayımıyla
/// çözümlenir (core özellikler).
/// </summary>
public sealed record SubscriptionSnapshot(
    bool HasSubscription,
    SubscriptionPlan Plan,
    string EffectiveStatus,
    bool IsActive,
    bool IsTrial,
    DateTime? TrialEndsAtUtc,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    int DaysRemaining,
    IReadOnlySet<string> EffectiveFeatures)
{
    /// <summary>Özellik bu işletmede açık mı? Core her çözümlemede kümede bulunur.</summary>
    public bool HasFeature(string feature) => EffectiveFeatures.Contains(feature);
}

/// <summary>
/// Feature guard: plana göre özellik ve kota kontrolü.
/// Uygulama katmanında, handler'ların başında çağrılır.
/// </summary>
public interface IFeatureGuard
{
    /// <summary>İşletmenin geçerli abonelik görüntüsünü çözümler.</summary>
    Task<SubscriptionSnapshot> ResolveAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Özellik kapalıysa AppException(403) fırlatır.</summary>
    Task EnsureFeatureAsync(Guid tenantId, string feature, CancellationToken cancellationToken);

    /// <summary>Planın AI soru limiti; 0 veya özellik kapalıysa genel varsaylana düşer.</summary>
    Task<int> AiMonthlyLimitAsync(Guid tenantId, int fallbackLimit, CancellationToken cancellationToken);
}

/// <summary>
/// Ödeme sağlayıcısı soyutlaması. Sağlayıcı domain'e
/// bağlanmaz; iyzico/PayTR/Stripe bu arayüzle arkasına takılır. MVP'de
/// FakePaymentProvider kayıtlıdır — plan değişimi doğrudan yapılır.
/// </summary>
public interface IPaymentProvider
{
    string Name { get; }

    /// <summary>Ödeme sayfası oturumu açar; dönüş URL'si kullanıcıyı yönlendirir.</summary>
    Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);
}

public sealed record CheckoutRequest(
    Guid TenantId,
    string PlanCode,
    decimal Amount,
    string Currency,
    string CustomerEmail);

public sealed record CheckoutResult(
    string PaymentId,
    string RedirectUrl);
