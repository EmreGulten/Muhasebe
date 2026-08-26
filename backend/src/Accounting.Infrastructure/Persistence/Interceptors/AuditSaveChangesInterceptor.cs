using System.Text.Json;
using Accounting.Application.Abstractions;
using Accounting.Domain.Common;
using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Accounting.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Kaydetme anında:
///  - IHasTimestamps alanlarını otomatik set eder,
///  - ISoftDeletable silinmelerini soft-delete'e çevirir,
///  - denetlenebilir entity değişikliklerini AuditLogs'a yazar.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyConventions(eventData.Context);
            WriteAuditLogs(eventData.Context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyConventions(DbContext context)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IHasTimestamps timestamps)
            {
                if (entry.State == EntityState.Added && timestamps.CreatedAtUtc == default)
                {
                    timestamps.CreatedAtUtc = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    timestamps.UpdatedAtUtc = now;
                }
            }

            // Fiziksel DELETE yerine soft delete; query filter zaten süzer.
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable soft)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAtUtc = now;
            }
        }
    }

    private void WriteAuditLogs(DbContext context)
    {
        // AuditLog'lar aynı ChangeTracker'a ekleneceği için enumerable'ı önce somutlaştır;
        // aksi halde enumerate sırasında "collection was modified" fırlar.
        var auditableEntries = context.ChangeTracker.Entries().Where(IsAuditable).ToList();

        foreach (var entry in auditableEntries)
        {
            var isSoftDeleted = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == nameof(ISoftDeletable.IsDeleted))?
                .IsModified == true;

            if (entry.State is not (EntityState.Added or EntityState.Modified) || isSoftDeleted)
            {
                continue;
            }

            var log = new AuditLog
            {
                TenantId = currentTenant.TenantId,
                UserId = currentUser.UserId,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? string.Empty,
                Action = isSoftDeleted ? "Deleted" : entry.State.ToString(),
                IpAddress = currentUser.IpAddress,
                UserAgent = currentUser.UserAgent,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            };

            if (entry.State == EntityState.Added)
            {
                log.NewValues = Serialize(entry.Properties
                    .Where(p => !p.Metadata.IsPrimaryKey())
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
            }
            else
            {
                var changed = entry.Properties.Where(p => p.IsModified).ToList();
                if (changed.Count == 0)
                {
                    continue;
                }

                log.OldValues = Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                log.NewValues = Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
            }

            context.Set<AuditLog>().Add(log);
        }
    }

    private static bool IsAuditable(EntityEntry entry) =>
        entry.Entity is not (AuditLog or RefreshToken)
        && entry.Entity is IHasTimestamps or ISoftDeletable or ITenantScoped;

    private static string? Serialize(Dictionary<string, object?> values) =>
        values.Count == 0 ? null : JsonSerializer.Serialize(values, JsonOptions);
}
