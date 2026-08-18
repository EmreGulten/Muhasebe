using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Kullanıcı ↔ işletme üyeliği. Rol bilgisi bu kayıtta tutulur.
/// </summary>
public class UserTenant : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    public TenantRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;
}
