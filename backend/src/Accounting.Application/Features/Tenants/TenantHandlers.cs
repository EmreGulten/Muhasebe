using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Auth;
using Accounting.Contracts.Auth;
using Accounting.Contracts.Tenants;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Tenants;

/// <summary>Yeni işletme oluşturur; oluşturan kullanıcı Owner olur.</summary>
public sealed class CreateTenantHandler(IApplicationDbContext db)
{
    public async Task<TenantResponse> HandleAsync(Guid userId, CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var joinedAt = DateTime.UtcNow;

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            CreatedAtUtc = joinedAt,
        };
        db.Tenants.Add(tenant);

        db.UserTenants.Add(new UserTenant
        {
            UserId = userId,
            TenantId = tenant.Id,
            Role = TenantRole.Owner,
            JoinedAtUtc = joinedAt,
            CreatedAtUtc = joinedAt,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new TenantResponse(tenant.Id, tenant.Name, TenantRole.Owner.ToString(), joinedAt);
    }
}

/// <summary>Kullanıcının üye olduğu işletmeleri listeler.</summary>
public sealed class ListTenantsHandler(IApplicationDbContext db)
{
    public Task<IReadOnlyList<TenantMembershipDto>> HandleAsync(Guid userId, CancellationToken cancellationToken) =>
        db.ForUserAsync(userId, cancellationToken);
}

/// <summary>
/// Tek bir işletmenin bilgisini döner. Kullanıcı o işletmenin üyesi değilse
/// ForbiddenException — işletme varlığı dışarıya sızdırılmaz.
/// </summary>
public sealed class GetTenantHandler(IApplicationDbContext db)
{
    public async Task<TenantResponse> HandleAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var membership = await db.UserTenants
            .Where(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive && !m.Tenant.IsDeleted)
            .Select(m => new { m.Tenant.Name, m.Role, m.JoinedAtUtc })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("Bu işletmeye erişim yetkiniz yok.");

        return new TenantResponse(tenantId, membership.Name, membership.Role.ToString(), membership.JoinedAtUtc);
    }
}
