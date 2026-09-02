using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Domain.Common;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Accounting.Infrastructure.Backups;

public sealed class TenantBackupService(
    AppDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ITenantBackupService
{
    private static readonly MethodInfo TenantQueryMethod = typeof(TenantBackupService)
        .GetMethod(nameof(TenantQueryGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private const int FormatVersion = 1;
    public const int MaximumFileSize = 25 * 1024 * 1024;
    private const int MaximumRowCount = 100_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly HashSet<Type> ExcludedTypes =
    [
        typeof(AiMessage),
        typeof(Subscription),
    ];

    public async Task<TenantBackupFile> ExportAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(x => x.Id == tenantId, cancellationToken);
        var tables = new List<BackupTable>();

        foreach (var entityType in GetBackupEntityTypes())
        {
            var rows = await ReadRowsAsync(entityType, tenantId, cancellationToken);
            tables.Add(new BackupTable(entityType.ClrType.Name, rows));
        }

        var data = new BackupData(
            FormatVersion,
            timeProvider.GetUtcNow().UtcDateTime,
            tenant.Id,
            tenant.Name,
            tables);
        var checksum = ComputeChecksum(data);
        var content = JsonSerializer.SerializeToUtf8Bytes(new BackupEnvelope(data, checksum), JsonOptions);

        db.AuditLogs.Add(CreateAudit(tenantId, "Export", content.Length.ToString(CultureInfo.InvariantCulture)));
        await db.SaveChangesAsync(cancellationToken);

        var safeName = string.Concat(tenant.Name.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        var date = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return new TenantBackupFile(content, $"{(safeName.Length == 0 ? "isletme" : safeName)}-yedek-{date}.json");
    }

    public async Task<TenantRestoreResult> RestoreAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        if (content.IsEmpty || content.Length > MaximumFileSize)
        {
            throw new AppException("Yedek dosyası boş veya 25 MB sınırını aşıyor.");
        }

        BackupEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<BackupEnvelope>(content.Span, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new AppException("Yedek dosyası geçerli bir JSON yedeği değil.");
        }

        if (envelope.Data.FormatVersion != FormatVersion)
        {
            throw new AppException($"Bu yedek sürümü desteklenmiyor: {envelope.Data.FormatVersion}.");
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(envelope.Checksum),
                Encoding.UTF8.GetBytes(ComputeChecksum(envelope.Data))))
        {
            throw new AppException("Yedek dosyasının bütünlük kontrolü başarısız.");
        }

        var modelTypes = GetBackupEntityTypes().ToDictionary(x => x.ClrType.Name, StringComparer.Ordinal);
        if (envelope.Data.Tables.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != envelope.Data.Tables.Count ||
            envelope.Data.Tables.Any(x => !modelTypes.ContainsKey(x.Name)))
        {
            throw new AppException("Yedek dosyasında desteklenmeyen veya tekrarlanan veri tabloları var.");
        }

        var totalRows = envelope.Data.Tables.Sum(x => (long)x.Rows.Count);
        if (totalRows > MaximumRowCount)
        {
            throw new AppException("Yedek dosyası izin verilen kayıt sınırını aşıyor.");
        }

        foreach (var entityType in modelTypes.Values)
        {
            if (await TenantQuery(entityType, tenantId).AnyAsync(cancellationToken))
            {
                throw new ConflictException("Geri yükleme yalnızca veri içermeyen boş bir işletmeye yapılabilir.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var idMaps = BuildIdMaps(envelope.Data, modelTypes);

        foreach (var table in envelope.Data.Tables)
        {
            var entityType = modelTypes[table.Name];
            foreach (var row in table.Rows)
            {
                var entity = Activator.CreateInstance(entityType.ClrType)
                    ?? throw new AppException($"{table.Name} kaydı oluşturulamadı.");
                PopulateEntity(entityType, entity, row, tenantId, idMaps);
                db.Add(entity);
            }
        }

        db.AuditLogs.Add(CreateAudit(tenantId, "Restore", totalRows.ToString(CultureInfo.InvariantCulture)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TenantRestoreResult((int)totalRows, envelope.Data.Tables.Count(x => x.Rows.Count > 0));
    }

    private List<IEntityType> GetBackupEntityTypes() => db.Model.GetEntityTypes()
        .Where(x => typeof(ITenantScoped).IsAssignableFrom(x.ClrType) && !ExcludedTypes.Contains(x.ClrType))
        .OrderBy(x => x.ClrType.Name, StringComparer.Ordinal)
        .ToList();

    private async Task<List<Dictionary<string, JsonElement>>> ReadRowsAsync(
        IEntityType entityType,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var entities = await TenantQuery(entityType, tenantId).AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(entity => entityType.GetProperties()
                .Where(x => x.PropertyInfo is not null)
                .ToDictionary(
                    x => x.Name,
                    x => JsonSerializer.SerializeToElement(x.PropertyInfo!.GetValue(entity), x.ClrType, JsonOptions),
                    StringComparer.Ordinal))
            .ToList();
    }

    private IQueryable<object> TenantQuery(IEntityType entityType, Guid tenantId) =>
        ((IQueryable)TenantQueryMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [tenantId])!).Cast<object>();

    private IQueryable<TEntity> TenantQueryGeneric<TEntity>(Guid tenantId) where TEntity : class =>
        db.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(x => EF.Property<Guid>(x, nameof(ITenantScoped.TenantId)) == tenantId);

    private static Dictionary<Type, Dictionary<Guid, Guid>> BuildIdMaps(
        BackupData data,
        Dictionary<string, IEntityType> modelTypes)
    {
        var result = new Dictionary<Type, Dictionary<Guid, Guid>>();
        foreach (var table in data.Tables)
        {
            var entityType = modelTypes[table.Name];
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey?.Properties.Count != 1 || primaryKey.Properties[0].ClrType != typeof(Guid))
            {
                throw new AppException($"{table.Name} için desteklenmeyen anahtar yapısı.");
            }

            var keyName = primaryKey.Properties[0].Name;
            var map = new Dictionary<Guid, Guid>();
            foreach (var row in table.Rows)
            {
                if (!row.TryGetValue(keyName, out var value) || !Guid.TryParse(value.GetString(), out var oldId) ||
                    !map.TryAdd(oldId, Guid.NewGuid()))
                {
                    throw new AppException($"{table.Name} içinde geçersiz veya tekrarlanan kayıt kimliği var.");
                }
            }
            result[entityType.ClrType] = map;
        }
        return result;
    }

    private static void PopulateEntity(
        IEntityType entityType,
        object entity,
        IReadOnlyDictionary<string, JsonElement> row,
        Guid tenantId,
        Dictionary<Type, Dictionary<Guid, Guid>> idMaps)
    {
        var allowed = entityType.GetProperties().Where(x => x.PropertyInfo is not null)
            .ToDictionary(x => x.Name, StringComparer.Ordinal);
        if (row.Keys.Any(x => !allowed.ContainsKey(x)))
        {
            throw new AppException($"{entityType.ClrType.Name} kaydında bilinmeyen alan var.");
        }

        foreach (var property in allowed.Values)
        {
            if (!row.TryGetValue(property.Name, out var json))
            {
                throw new AppException($"{entityType.ClrType.Name}.{property.Name} alanı eksik.");
            }

            object? value;
            if (property.Name == nameof(ITenantScoped.TenantId))
            {
                value = tenantId;
            }
            else if (property.IsPrimaryKey())
            {
                value = MapGuid(json, idMaps[entityType.ClrType], entityType.ClrType.Name);
            }
            else
            {
                var foreignKey = property.GetContainingForeignKeys().SingleOrDefault();
                if (foreignKey is not null && property.ClrType == typeof(Guid))
                {
                    value = MapGuid(json, idMaps.GetValueOrDefault(foreignKey.PrincipalEntityType.ClrType), property.Name);
                }
                else if (foreignKey is not null && property.ClrType == typeof(Guid?))
                {
                    value = json.ValueKind == JsonValueKind.Null
                        ? null
                        : MapGuid(json, idMaps.GetValueOrDefault(foreignKey.PrincipalEntityType.ClrType), property.Name);
                }
                else
                {
                    value = json.Deserialize(property.ClrType, JsonOptions);
                }
            }
            property.PropertyInfo!.SetValue(entity, value);
        }
    }

    private static Guid MapGuid(JsonElement json, Dictionary<Guid, Guid>? map, string field)
    {
        if (!Guid.TryParse(json.GetString(), out var oldId) || map is null || !map.TryGetValue(oldId, out var newId))
        {
            throw new AppException($"{field} alanındaki ilişki yedekte bulunamadı.");
        }
        return newId;
    }

    private Guid RequireTenant() => currentTenant.TenantId
        ?? throw new ForbiddenException("Aktif işletme seçilmedi.");

    private AuditLog CreateAudit(Guid tenantId, string action, string value) => new()
    {
        TenantId = tenantId,
        UserId = currentUser.UserId,
        EntityType = "TenantBackup",
        EntityId = tenantId.ToString(),
        Action = action,
        NewValues = JsonSerializer.Serialize(new { value }, JsonOptions),
        IpAddress = currentUser.IpAddress,
        UserAgent = currentUser.UserAgent,
        CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
    };

    private static string ComputeChecksum(BackupData data) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions))).ToLowerInvariant();

    private sealed record BackupEnvelope(BackupData Data, string Checksum);
    private sealed record BackupData(
        int FormatVersion,
        DateTime CreatedAtUtc,
        Guid SourceTenantId,
        string SourceTenantName,
        List<BackupTable> Tables);
    private sealed record BackupTable(string Name, List<Dictionary<string, JsonElement>> Rows);
}
