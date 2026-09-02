namespace Accounting.Application.Abstractions;

public sealed record TenantBackupFile(byte[] Content, string FileName);

public sealed record TenantRestoreResult(int ImportedRowCount, int ImportedTableCount);

/// <summary>Bir işletmenin taşınabilir veri yedeğini üretir ve boş bir işletmeye geri yükler.</summary>
public interface ITenantBackupService
{
    Task<TenantBackupFile> ExportAsync(CancellationToken cancellationToken = default);

    Task<TenantRestoreResult> RestoreAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);
}
