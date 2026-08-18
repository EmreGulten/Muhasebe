using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Abstractions;

/// <summary>
/// Application katmanının veri erişimi sözleşmesi.
/// Uygulama EF Core'a yalnızca bu arayüz üzerinden dokunur; somut
/// DbContext Infrastructure katmanında yaşar.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<UserTenant> UserTenants { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
