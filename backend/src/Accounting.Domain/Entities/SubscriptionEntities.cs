using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Abonelik planı (muhasebe.md bölüm 29) — global katalog tablosu, TenantId'siz.
/// Üç plan tohum veriyle gelir (bölüm 29 fiyatlandırması); kod üzerinden
/// değişmez, limitler ve özellik anahtarları burada tutulur.
/// </summary>
public class SubscriptionPlan
{
    public Guid Id { get; set; }

    /// <summary>"starter" / "pro" / "business" — değişmez benzersiz kod.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Aylık fiyat TL (decimal(18,2)).</summary>
    public decimal MonthlyPrice { get; set; }

    /// <summary>Plan kullanıcı üst sınırı (MVP'de bilgi amaçlı; davet sistemi planda).</summary>
    public int MaxUsers { get; set; }

    /// <summary>Depo üst sınırı; −1 = sınırsız (İşletme).</summary>
    public int MaxWarehouses { get; set; }

    /// <summary>Aylık AI soru limiti; 0 = AI özelliği kapalı.</summary>
    public int AiMonthlyQuestionLimit { get; set; }

    /// <summary>Açık özellik anahtarları, virgülle ayrılmış (PlanFeatures).</summary>
    public string Features { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Features CSV'sini kümeye çevirir (boş toleranslı).</summary>
    public HashSet<string> FeatureSet() =>
    [
        .. (Features ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
    ];
}

/// <summary>
/// İşletmenin aboneliği (muhasebe.md bölüm 30). Bir işletmenin en fazla bir
/// geçerli aboneliği vardır; yeni kayıt Pro planında deneme olarak açılır.
/// Abonelik kaydı yoksa işletme core özelliklerle çalışır (başlangıç varsayılanı).
/// </summary>
public class Subscription : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PlanId { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public DateTime CurrentPeriodStartUtc { get; set; }

    public DateTime CurrentPeriodEndUtc { get; set; }

    /// <summary>Varsa deneme bitiş anı (Status=Trialing iken CurrentPeriodEnd ile aynı).</summary>
    public DateTime? TrialEndsAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
