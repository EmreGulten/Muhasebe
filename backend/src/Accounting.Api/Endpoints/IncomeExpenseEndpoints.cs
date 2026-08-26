using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.IncomeExpenses;
using Accounting.Contracts.IncomeExpenses;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Gelir/gider uç noktaları.</summary>
public static class IncomeExpenseEndpoints
{
    public static void MapIncomeExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/income-expense").WithTags("Gelir & Gider");

        group.MapGet("/categories", async (
                string? type,
                ListIncomeExpenseCategoriesHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(type, cancellationToken)))
            .WithName("ListIncomeExpenseCategories")
            .WithSummary("Kategori listesi (ilk çağrıda plandaki varsayılanlar oluşturulur)")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesView);

        group.MapPost("/categories", async (
                CreateIncomeExpenseCategoryRequest request,
                CreateIncomeExpenseCategoryHandler handler,
                CancellationToken cancellationToken) =>
        {
            var category = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/income-expense/categories/{category.Id}", category);
        })
            .WithName("CreateIncomeExpenseCategory")
            .WithSummary("Yeni gelir/gider kategorisi (ad tenant ve tür içinde benzersiz)")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesEdit);

        group.MapPut("/categories/{id:guid}", async (
                Guid id,
                UpdateIncomeExpenseCategoryRequest request,
                UpdateIncomeExpenseCategoryHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateIncomeExpenseCategory")
            .WithSummary("Kategori düzenle (yalnızca ad ve aktiflik; tür sabittir)")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesEdit);

        group.MapDelete("/categories/{id:guid}", async (
                Guid id,
                DeleteIncomeExpenseCategoryHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteIncomeExpenseCategory")
            .WithSummary("Kategoriyi sil (yalnızca kaydı olmayanlar; yoksa pasifleştirin)")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesEdit);

        group.MapGet("/records", async (
                string? type,
                Guid? categoryId,
                DateTime? from,
                DateTime? to,
                int? page,
                int? pageSize,
                ListIncomeExpenseRecordsHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(
                type, categoryId, from, to, page ?? 1, pageSize ?? 20, cancellationToken)))
            .WithName("ListIncomeExpenseRecords")
            .WithSummary("Gelir/gider listesi (tür, kategori, dönem filtreleri; en yeni önce)")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesView);

        group.MapPost("/records", async (
                CreateIncomeExpenseRecordRequest request,
                CreateIncomeExpenseRecordHandler handler,
                CancellationToken cancellationToken) =>
        {
            var record = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/income-expense/records/{record.Id}", record);
        })
            .WithName("CreateIncomeExpenseRecord")
            .WithSummary("Gelir/gider kaydı — kasa hareketiyle tek transaction'da yazılır")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesCreate);

        group.MapGet("/records/{id:guid}", async (
                Guid id,
                GetIncomeExpenseRecordHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetIncomeExpenseRecord")
            .WithSummary("Gelir/gider kaydı detayı")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesView);

        group.MapPost("/records/{id:guid}/cancel", async (
                Guid id,
                CancelIncomeExpenseRecordHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("CancelIncomeExpenseRecord")
            .WithSummary("Kaydı iptal et — kasa hareketinin tersi yazılır, terminal durum")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesEdit);

        group.MapGet("/summary", async (
                DateTime? from,
                DateTime? to,
                GetIncomeExpenseSummaryHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(from, to, cancellationToken)))
            .WithName("GetIncomeExpenseSummary")
            .WithSummary("Dönem özeti — toplamlar, aylık ve kategori bazlı döküm")
            .RequireTenant()
            .RequirePermission(Permissions.ExpensesView);
    }
}
