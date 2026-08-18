using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Purchases;
using Accounting.Contracts.Purchases;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Alış uç noktaları (muhasebe.md bölüm 7, 23, 24).</summary>
public static class PurchaseEndpoints
{
    public static void MapPurchaseEndpoints(this IEndpointRouteBuilder app)
    {
        var purchases = app.MapGroup("/purchases").WithTags("Alışlar");

        purchases.MapGet("/", async (
                string? status,
                Guid? partyId,
                string? search,
                int? page,
                int? pageSize,
                ListPurchasesHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(status, partyId, search, page ?? 1, pageSize ?? 20, cancellationToken)))
            .WithName("ListPurchases")
            .WithSummary("Alış listesi (durum, tedarikçi, numara araması; en yeni önce)")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesView);

        purchases.MapPost("/", async (
                CreatePurchaseRequest request,
                CreatePurchaseHandler handler,
                CancellationToken cancellationToken) =>
        {
            var purchase = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/purchases/{purchase.Id}", purchase);
        })
            .WithName("CreatePurchase")
            .WithSummary("Yeni alış belgesi (taslak; onayda stok girişi + tedarikçi borcu oluşur)")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesCreate);

        purchases.MapGet("/{id:guid}", async (
                Guid id,
                GetPurchaseHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetPurchase")
            .WithSummary("Alış belgesi detayı (kalemler ve ödemeler)")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesView);

        purchases.MapPut("/{id:guid}", async (
                Guid id,
                UpdatePurchaseRequest request,
                UpdatePurchaseHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdatePurchase")
            .WithSummary("Taslak alışı düzenle (yalnızca Draft)")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesEdit);

        purchases.MapDelete("/{id:guid}", async (
                Guid id,
                DeletePurchaseHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeletePurchase")
            .WithSummary("Taslak alışı sil (yalnızca Draft; onaylı belge iptal edilir)")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesDelete);

        purchases.MapPost("/{id:guid}/confirm", async (
                Guid id,
                ConfirmPurchaseRequest request,
                ConfirmPurchaseHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("ConfirmPurchase")
            .WithSummary("Alışı onayla — stok girişi + tedarikçi borcu (+ istenirse anlık ödeme), tek transaction")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesEdit);

        purchases.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelPurchaseRequest request,
                CancelPurchaseHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("CancelPurchase")
            .WithSummary("Alışı iptal et — ters stok/cari/kasa hareketleri, terminal durum")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesEdit);

        purchases.MapPost("/{id:guid}/payments", async (
                Guid id,
                AddPurchasePaymentRequest request,
                AddPurchasePaymentHandler handler,
                CancellationToken cancellationToken) =>
        {
            var purchase = await handler.HandleAsync(id, request, cancellationToken);
            return Results.Created($"/api/v1/purchases/{id}", purchase);
        })
            .WithName("AddPurchasePayment")
            .WithSummary("Onaylı alışa ödeme ekle (kasa çıkışı + cari borç düşümü)")
            .RequireTenant()
            .RequirePermission(Permissions.PurchasesEdit);
    }
}
