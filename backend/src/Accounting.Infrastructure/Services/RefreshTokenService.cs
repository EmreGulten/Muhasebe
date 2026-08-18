using System.Security.Cryptography;
using System.Text;
using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accounting.Infrastructure.Services;

/// <summary>
/// Refresh token yaşam döngüsü.
/// Ham token 64 baytlık kriptografik rastgele değerdir; veritabanında
/// yalnızca SHA-256 özeti saklanır. Rotasyonda eski token iptal edilir;
/// iptal edilmiş token'ın tekrar kullanımı tüm oturumları düşürür.
/// </summary>
public sealed class RefreshTokenService(
    IApplicationDbContext db,
    TimeProvider timeProvider,
    IOptions<JwtOptions> jwtOptions) : IRefreshTokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public async Task<RefreshTokenIssue> IssueAsync(Guid userId, string? createdByIp, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var issue = new RefreshTokenIssue(rawToken, Hash(rawToken), now.AddDays(_options.RefreshTokenLifetimeDays));

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = issue.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = issue.ExpiresAtUtc,
            CreatedByIp = createdByIp,
        });

        await db.SaveChangesAsync(cancellationToken);
        return issue;
    }

    public async Task<RotationResult> RotateAsync(string rawToken, string? requestIp, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(rawToken);

        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            ?? throw new UnauthorizedException("Oturum geçersiz. Lütfen tekrar giriş yapın.");

        if (token.IsRevoked)
        {
            // İptal edilmiş token tekrar kullanıldı → olası token çalınması;
            // kullanıcının tüm oturumları düşürülür.
            await RevokeAllForUserAsync(token.UserId, "Refresh token yeniden kullanımı tespit edildi", cancellationToken);
            throw new UnauthorizedException("Oturum güvenlik nedeniyle sonlandırıldı. Lütfen tekrar giriş yapın.");
        }

        if (token.IsExpired)
        {
            throw new UnauthorizedException("Oturum süresi doldu. Lütfen tekrar giriş yapın.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var newHash = Hash(newRaw);
        var newExpiry = now.AddDays(_options.RefreshTokenLifetimeDays);

        token.RevokedAtUtc = now;
        token.RevokedReason = "Döndürüldü";
        token.ReplacedByTokenHash = newHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = newHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = newExpiry,
            CreatedByIp = requestIp,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new RotationResult(token.UserId, newRaw, newExpiry);
    }

    public async Task RevokeAsync(string rawToken, string reason, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(rawToken);

        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null || !token.IsActive)
        {
            return;
        }

        token.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        token.RevokedReason = reason;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
            token.RevokedReason = reason;
        }

        if (activeTokens.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Hash(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }
}
