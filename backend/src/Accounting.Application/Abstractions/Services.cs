using Accounting.Domain.Entities;

namespace Accounting.Application.Abstractions;

/// <summary>JWT access token üretir.</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user);
}

/// <summary>Ham refresh token bilgisidir; veritabanında yalnızca özeti saklanır.</summary>
public sealed record RefreshTokenIssue(string RawToken, string TokenHash, DateTime ExpiresAtUtc);

public sealed record RotationResult(Guid UserId, string RawToken, DateTime RefreshTokenExpiresAtUtc);

/// <summary>Refresh token yaşam döngüsü: üret, döndür (rotate), iptal et.</summary>
public interface IRefreshTokenService
{
    /// <summary>Yeni token üretir ve kaydeder.</summary>
    Task<RefreshTokenIssue> IssueAsync(Guid userId, string? createdByIp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Token'ı döndürür: eski token iptal edilir, yenisi üretilir.
    /// İptal edilmiş bir token tekrar kullanılırsa (reuse) kullanıcının tüm
    /// aktif token'ları iptal edilir ve UnauthorizedException fırlatılır.
    /// </summary>
    Task<RotationResult> RotateAsync(string rawToken, string? requestIp, CancellationToken cancellationToken = default);

    /// <summary>Tek bir token'ı iptal eder. Token bulunamazsa sessizce geçer.</summary>
    Task RevokeAsync(string rawToken, string reason, CancellationToken cancellationToken = default);

    /// <summary>Kullanıcının tüm aktif token'larını iptal eder (parola değişimi vb.).</summary>
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>Geliştirme e-posta göndericisi sözleşmesi (MVP: log'a yazar).</summary>
public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
