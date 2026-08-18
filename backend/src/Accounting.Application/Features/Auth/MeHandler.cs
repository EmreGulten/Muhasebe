using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Auth;
using Accounting.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Accounting.Application.Features.Auth;

/// <summary>Oturum açan kullanıcının profilini ve işletme üyeliklerini döner.</summary>
public sealed class MeHandler(
    UserManager<ApplicationUser> userManager,
    IApplicationDbContext db)
{
    public async Task<MeResponse> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedException("Oturum bulunamadı. Lütfen tekrar giriş yapın.");

        var memberships = await db.ForUserAsync(user.Id, cancellationToken);

        return new MeResponse(
            new UserDto(user.Id, user.Email!, user.FullName),
            memberships);
    }
}
