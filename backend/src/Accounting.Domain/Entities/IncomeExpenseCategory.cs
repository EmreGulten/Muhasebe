using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

/// <summary>
/// Gelir/gider kategorisi: "Kira", "Elektrik"... İlk
/// listelemede plandaki varsayılan kategoriler tenant'a eklenir. Ad tenant
/// ve tür içinde benzersizdir ("Diğer" hem gelir hem gider tarafında olabilir).
/// </summary>
public class IncomeExpenseCategory : ITenantScoped, ISoftDeletable, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public IncomeExpenseType Type { get; set; } = IncomeExpenseType.Expense;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<IncomeExpenseRecord> Records { get; set; } = [];
}
