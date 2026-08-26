using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// AI asistan sohbet mesajı. Kullanıcı soruları
/// ve asistan yanıtları işletme (tenant) + kullanıcı bazında saklanır; sohbet
/// geçmişi sonraki sorulara bağlam sağlar. Mesajlar değiştirilemez.
/// </summary>
public class AiMessage : ITenantScoped, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    /// <summary>Soruyu soran kullanıcı (Identity).</summary>
    public Guid UserId { get; set; }

    public AiMessageRole Role { get; set; } = AiMessageRole.User;

    /// <summary>Soru ya da yanıt metni (en fazla 4000 karakter).</summary>
    public string Content { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
