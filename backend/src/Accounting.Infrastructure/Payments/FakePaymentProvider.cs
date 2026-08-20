using System.Text.Json;
using Accounting.Application.Abstractions;

namespace Accounting.Infrastructure.Payments;

/// <summary>
/// MVP ödeme sağlayıcısı (muhasebe.md bölüm 31): gerçek para hareketi yok.
/// Checkout oturumu açar, sahte bir yönlendirme adresi döner; plan değişimi
/// ChangePlanHandler'da ödemeyi beklemeden uygulanır. iyzico/PayTR/Stripe
/// aynı IPaymentProvider sözleşmesiyle arkasına takılır — domain katmanına
/// sağlayıcı bağımlılığı girmez.
/// </summary>
public sealed class FakePaymentProvider : IPaymentProvider
{
    public string Name => "fake";

    public Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var paymentId = JsonSerializer.Serialize(new
        {
            request.TenantId,
            request.PlanCode,
            request.Amount,
            request.Currency,
        });
        return Task.FromResult(new CheckoutResult(
            paymentId,
            $"/subscription?checkout=ok&plan={request.PlanCode}&provider=fake"));
    }
}
