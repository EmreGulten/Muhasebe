using Accounting.Application.Abstractions;

namespace Accounting.Application.Features.Auth;

/// <summary>Çıkış: verilen refresh token'ı iptal eder.</summary>
public sealed class LogoutHandler(IRefreshTokenService refreshTokenService)
{
    public Task HandleAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Task.CompletedTask;
        }

        return refreshTokenService.RevokeAsync(rawToken, "Kullanıcı çıkışı", cancellationToken);
    }
}
