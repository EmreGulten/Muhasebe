using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Sales;
using Accounting.Contracts.Sales;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Satış uç noktaları.</summary>
public static class SaleEndpoints
{
    public static void MapSaleEndpoints(this IEndpointRouteBuilder app)
    {
        var sales = app.MapGroup("/sales").WithTags("Satışlar");

        sales.MapGet("/", async (
                string? status,
                Guid? partyId,
                string? search,
                int? page,
                int? pageSize,
                ListSalesHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(status, partyId, search, page ?? 1, pageSize ?? 20, cancellationToken)))
            .WithName("ListSales")
            .WithSummary("Satış listesi (durum, müşteri, numara araması; en yeni önce)")
            .RequireTenant()
            .RequirePermission(Permissions.SalesView);

        sales.MapPost("/", async (
                CreateSaleRequest request,
                CreateSaleHandler handler,
                CancellationToken cancellationToken) =>
        {
            var sale = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/sales/{sale.Id}", sale);
        })
            .WithName("CreateSale")
            .WithSummary("Yeni satış belgesi (taslak; onayda stok/cari etkisi oluşur)")
            .RequireTenant()
            .RequirePermission(Permissions.SalesCreate);

        sales.MapGet("/{id:guid}", async (
                Guid id,
                GetSaleHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetSale")
            .WithSummary("Satış belgesi detayı (kalemler ve tahsilatlar)")
            .RequireTenant()
            .RequirePermission(Permissions.SalesView);

        sales.MapPut("/{id:guid}", async (
                Guid id,
                UpdateSaleRequest request,
                UpdateSaleHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateSale")
            .WithSummary("Taslak satışı düzenle (yalnızca Draft)")
            .RequireTenant()
            .RequirePermission(Permissions.SalesEdit);

        sales.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteSaleHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteSale")
            .WithSummary("Taslak satışı sil (yalnızca Draft; onaylı belge iptal edilir)")
            .RequireTenant()
            .RequirePermission(Permissions.SalesDelete);

        sales.MapPost("/{id:guid}/confirm", async (
                Guid id,
                ConfirmSaleRequest request,
                ConfirmSaleHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("ConfirmSale")
            .WithSummary("Satışı onayla — stok düşümü + cari borç (+ istenirse anlık tahsilat), tek transaction")
            .RequireTenant()
            .RequirePermission(Permissions.SalesEdit);

        sales.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelSaleRequest request,
                CancelSaleHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("CancelSale")
            .WithSummary("Satışı iptal et — ters stok/cari/kasa hareketleri, terminal durum")
            .RequireTenant()
            .RequirePermission(Permissions.SalesEdit);

        sales.MapPost("/{id:guid}/payments", async (
                Guid id,
                AddSalePaymentRequest request,
                AddSalePaymentHandler handler,
                CancellationToken cancellationToken) =>
        {
            var sale = await handler.HandleAsync(id, request, cancellationToken);
            return Results.Created($"/api/v1/sales/{id}", sale);
        })
            .WithName("AddSalePayment")
            .WithSummary("Onaylı satışa tahsilat ekle (kasa hareketi + cari alacak)")
            .RequireTenant()
            .RequirePermission(Permissions.SalesEdit);
    }
}
