using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Auth;
using Accounting.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Accounting.Application.Features.Auth;

/// <summary>
/// Giriş. E-posta/şifre hatalarında aynı mesaj döner (kullanıcı sayımı sızdırmaz).
/// Başarısız denemeler Identity lockout sayaçını besler.
/// </summary>
public sealed class LoginHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenService refreshTokenService,
    IApplicationDbContext db)
{
    public async Task<AuthResponse> HandleAsync(LoginRequest request, string? requestIp, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            throw new UnauthorizedException("E-posta veya şifre hatalı.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException("Çok fazla başarısız deneme. Hesabınız geçici olarak kilitlendi, lütfen bir süre sonra tekrar deneyin.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException("E-posta veya şifre hatalı.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var (accessToken, accessTokenExpiresAt) = tokenService.CreateAccessToken(user);
        var refresh = await refreshTokenService.IssueAsync(user.Id, requestIp, cancellationToken);
        var memberships = await db.ForUserAsync(user.Id, cancellationToken);

        return new AuthResponse(
            accessToken,
            accessTokenExpiresAt,
            refresh.RawToken,
            refresh.ExpiresAtUtc,
            new UserDto(user.Id, user.Email!, user.FullName),
            memberships);
    }
}
