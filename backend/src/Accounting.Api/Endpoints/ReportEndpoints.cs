using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Reports;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Rapor uç noktaları. Tümü salt okunur.</summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports").WithTags("Raporlar");

        group.MapGet("/dashboard", async (
                GetDashboardHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("GetDashboard")
            .WithSummary("Dashboard — 10 KPI kartı ve beş grafik")
            .RequireTenant()
            .RequirePermission(Permissions.ReportsView);

        group.MapGet("/receivables", async (
                GetReceivablesReportHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("GetReceivablesReport")
            .WithSummary("Alacaklar raporu — borçlu müşteriler ve gecikmiş alacaklar")
            .RequireTenant()
            .RequirePermission(Permissions.ReportsView);

        group.MapGet("/stock", async (
                GetStockReportHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("GetStockReport")
            .WithSummary("Stok raporu — eldeki miktar, maliyet değeri ve kritik stok")
            .RequireTenant()
            .RequirePermission(Permissions.ReportsView);

        group.MapGet("/sales", async (
                DateTime? from,
                DateTime? to,
                GetSalesReportHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(from, to, cancellationToken)))
            .WithName("GetSalesReport")
            .WithSummary("Satış raporu — dönem toplamları ve gün/müşteri/ürün dökümü")
            .RequireTenant()
            .RequirePermission(Permissions.ReportsView);
    }
}
