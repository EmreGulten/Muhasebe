using Accounting.Domain.Enums;

namespace Accounting.Application.Abstractions;

/// <summary>İstek kapsamında oturum açan kullanıcı hakkında bilgi.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}

/// <summary>İstek kapsamında çözülen aktif tenant bağlamı (salt okunur).</summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }

    TenantRole? Role { get; }
}

/// <summary>Tenant bağlamını yazma arayüzü; yalnızca tenant middleware'i kullanır.</summary>
public interface ITenantContextWriter
{
    void SetTenant(Guid tenantId, TenantRole role);

    void ClearTenant();
}
