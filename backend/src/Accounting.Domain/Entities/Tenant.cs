using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

/// <summary>
/// İşletme (tenant). Sistemdeki tüm işletme verileri bu kayda bağlanır.
/// </summary>
public class Tenant : ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<UserTenant> Members { get; set; } = [];
}
