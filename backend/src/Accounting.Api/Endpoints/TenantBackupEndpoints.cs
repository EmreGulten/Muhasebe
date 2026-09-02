using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

public static class TenantBackupEndpoints
{
    public static RouteGroupBuilder MapTenantBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants/current/backup")
            .WithTags("Tenant Backups")
            .RequireAuthorization();

        group.MapGet("/", async (ITenantBackupService backups, CancellationToken cancellationToken) =>
            {
                var file = await backups.ExportAsync(cancellationToken);
                return Results.File(file.Content, "application/json; charset=utf-8", file.FileName);
            })
            .WithName("ExportTenantBackup")
            .WithSummary("Aktif işletmenin kullanıcı tarafından saklanacak yedeğini indirir")
            .RequireTenant()
            .RequirePermission(Permissions.TenantManage);

        group.MapPost("/restore", async (
                HttpRequest request,
                ITenantBackupService backups,
                CancellationToken cancellationToken) =>
            {
                const int maximumFileSize = 25 * 1024 * 1024;
                if (request.ContentLength is > maximumFileSize)
                {
                    throw new AppException("Yedek dosyası 25 MB sınırını aşıyor.");
                }

                await using var buffer = new MemoryStream();
                await request.Body.CopyToAsync(buffer, cancellationToken);
                if (buffer.Length > maximumFileSize)
                {
                    throw new AppException("Yedek dosyası 25 MB sınırını aşıyor.");
                }

                return Results.Ok(await backups.RestoreAsync(buffer.ToArray(), cancellationToken));
            })
            .Accepts<byte[]>("application/json")
            .WithName("RestoreTenantBackup")
            .WithSummary("Yedeği yalnızca boş olan aktif işletmeye geri yükler")
            .RequireTenant()
            .RequirePermission(Permissions.TenantManage);

        return group;
    }
}
