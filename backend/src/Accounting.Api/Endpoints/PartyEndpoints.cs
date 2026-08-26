using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Parties;
using Accounting.Contracts.Parties;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Cari hesap uç noktaları.</summary>
public static class PartyEndpoints
{
    public static void MapPartyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/parties").WithTags("Cari");

        group.MapGet("/", async (
                string? search,
                string? type,
                bool? includeInactive,
                int? page,
                int? pageSize,
                ListPartiesHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(search, type, includeInactive ?? true, page ?? 1, pageSize ?? 20, cancellationToken)))
            .WithName("ListParties")
            .WithSummary("Cari listesi (arama, tür filtresi, sayfalama, bakiye)")
            .RequireTenant()
            .RequirePermission(Permissions.PartiesView);

        group.MapPost("/", async (
                CreatePartyRequest request,
                CreatePartyHandler handler,
                CancellationToken cancellationToken) =>
        {
            var party = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/parties/{party.Id}", party);
        })
            .WithName("CreateParty")
            .WithSummary("Yeni cari kartı (açılış bakiyesi hareket üretir)")
            .RequireTenant()
            .RequirePermission(Permissions.PartiesCreate);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetPartyHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetParty")
            .WithSummary("Cari kartı detayı ve hesap özeti")
            .RequireTenant()
            .RequirePermission(Permissions.PartiesView);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdatePartyRequest request,
                UpdatePartyHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateParty")
            .WithSummary("Cari kartını günceller")
            .RequireTenant()
            .RequirePermission(Permissions.PartiesEdit);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeletePartyHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteParty")
            .WithSummary("Cari kartını siler (hareketi varsa reddedilir)")
            .RequireTenant()
            .RequirePermission(Permissions.PartiesDelete);

        group.MapGet("/{id:guid}/statement", async (
                Guid id,
                int? page,
                int? pageSize,
                GetPartyStatementHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, page ?? 1, pageSize ?? 50, cancellationToken)))
            .WithName("GetPartyStatement")
            .WithSummary("Cari ekstre — hareketler ve çalışan bakiye")
            .RequireTenant()
            .RequirePermission(Permissions.PartiesView);

        group.MapPost("/{id:guid}/transactions", async (
                Guid id,
                CreatePartyTransactionRequest request,
                CreatePartyTransactionHandler handler,
                CancellationToken cancellationToken) =>
            Results.Created($"/api/v1/parties/{id}/statement",
                await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("CreatePartyTransaction")
            .WithSummary("Manuel cari hareketi (açılış/borçlandırma/alacaklandırma/düzeltme)")
            .RequireTenant()
            .RequirePermission(Permissions.PartiesEdit);
    }
}
