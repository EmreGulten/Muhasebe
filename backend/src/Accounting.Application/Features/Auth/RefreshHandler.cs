using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Auth;
using Accounting.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Accounting.Application.Features.Auth;

/// <summary>
/// Refresh token rotasyonu: eski token iptal edilip yenisi verilir,
/// yeni bir access token üretilir.
/// </summary>
public sealed class RefreshHandler(
    IRefreshTokenService refreshTokenService,
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IApplicationDbContext db)
{
    public async Task<AuthResponse> HandleAsync(string? rawToken, string? requestIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new UnauthorizedException("Oturum bulunamadı. Lütfen tekrar giriş yapın.");
        }

        var rotation = await refreshTokenService.RotateAsync(rawToken, requestIp, cancellationToken);

        var user = await userManager.FindByIdAsync(rotation.UserId.ToString());
        if (user is null)
        {
            await refreshTokenService.RevokeAllForUserAsync(rotation.UserId, "Kullanıcı bulunamadı", cancellationToken);
            throw new UnauthorizedException("Oturum geçersiz. Lütfen tekrar giriş yapın.");
        }

        var (accessToken, accessTokenExpiresAt) = tokenService.CreateAccessToken(user);
        var memberships = await db.ForUserAsync(user.Id, cancellationToken);

        return new AuthResponse(
            accessToken,
            accessTokenExpiresAt,
            rotation.RawToken,
            rotation.RefreshTokenExpiresAtUtc,
            new UserDto(user.Id, user.Email!, user.FullName),
            memberships);
    }
}
