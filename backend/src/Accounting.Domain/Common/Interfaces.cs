namespace Accounting.Domain.Common;

/// <summary>Oluşturma/güncelleme zaman damgalarını taşıyan entity'ler.</summary>
public interface IHasTimestamps
{
    DateTime CreatedAtUtc { get; set; }

    DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>Soft delete (IsDeleted / DeletedAtUtc) destekleyen entity'ler.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAtUtc { get; set; }
}

/// <summary>
/// Tenant'a ait verileri taşıyan entity'ler. Tüm işletme tablolarında
/// TenantId zorunludur.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
