using Accounting.Application.Abstractions;
using Accounting.Domain.Enums;

namespace Accounting.Infrastructure.MultiTenancy;

/// <summary>
/// İstek kapsamında aktif tenant. Tenant middleware'i X-Tenant-Id başlığından
/// çözüp üyeliği doğruladıktan sonra set eder; handler'lar salt okur erişir.
/// </summary>
public sealed class TenantContext : ICurrentTenant, ITenantContextWriter
{
    public Guid? TenantId { get; private set; }

    public TenantRole? Role { get; private set; }

    public void SetTenant(Guid tenantId, TenantRole role)
    {
        TenantId = tenantId;
        Role = role;
    }

    public void ClearTenant()
    {
        TenantId = null;
        Role = null;
    }
}
