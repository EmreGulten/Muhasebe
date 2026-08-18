namespace Accounting.Domain.Entities;

/// <summary>
/// Refresh token. Ham token yalnızca istemciye verilir; veritabanında
/// SHA-256 özeti saklanır. Rotasyon: her yenilemede eski token iptal edilir.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    public bool IsActive => !IsRevoked && !IsExpired;
}
