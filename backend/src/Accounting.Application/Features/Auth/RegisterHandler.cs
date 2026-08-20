using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Auth;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Accounting.Application.Features.Auth;

/// <summary>
/// Kayıt: kullanıcıyı oluşturur, (istenen adla) işletmesini kurar ve
/// Owner rolüyle üyelik bağlar; ardından oturum token'larından döner.
/// </summary>
public sealed class RegisterHandler(
    UserManager<ApplicationUser> userManager,
    IApplicationDbContext db,
    ITokenService tokenService,
    IRefreshTokenService refreshTokenService,
    Subscriptions.SubscriptionService subscriptions)
{
    public async Task<AuthResponse> HandleAsync(RegisterRequest request, string? requestIp, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToUpperInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("Bu e-posta adresi ile kayıtlı bir kullanıcı zaten var.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            // MVP aşamasında e-posta doğrulaması devre dışı; altyapı hazır (bkz. README).
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var details = string.Join(" ", result.Errors.Select(e => e.Description));
            throw new AppException(details, 400, "Kayıt tamamlanamadı");
        }

        var businessName = request.BusinessName is { Length: > 0 } name
            ? name.Trim()
            : $"{user.FullName} İşletmesi";

        var tenant = new Tenant
        {
            Name = businessName,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);

        db.UserTenants.Add(new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = TenantRole.Owner,
            JoinedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        // Yeni işletme deneme aboneliğiyle açılır (muhasebe.md bölüm 30:
        // SubscriptionOptions.TrialPlanCode planında TrialDays gün).
        await subscriptions.StartTrialAsync(tenant.Id, cancellationToken);

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
