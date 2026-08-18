using Accounting.Application.Abstractions;
using Accounting.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Auth;

internal static class TenantMembershipQueries
{
    /// <summary>Kullanıcının aktif ve silinmemiş işletmelerindeki üyeliklerini listeler.</summary>
    public static async Task<IReadOnlyList<TenantMembershipDto>> ForUserAsync(
        this IApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var memberships = await db.UserTenants
            .Where(m => m.UserId == userId && m.IsActive && !m.Tenant.IsDeleted)
            .OrderBy(m => m.JoinedAtUtc)
            .Select(m => new TenantMembershipDto(
                m.TenantId,
                m.Tenant.Name,
                m.Role.ToString(),
                m.JoinedAtUtc))
            .ToListAsync(cancellationToken);

        return memberships;
    }
}
