namespace Accounting.Domain.Entities;

/// <summary>
/// Denetim kaydı. Finansal ve kritik entity değişiklikleri buraya yazılır.
/// OldValues / NewValues alanları JSON olarak saklanır.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string EntityType { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
