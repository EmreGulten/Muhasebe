using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Products;
using Accounting.Contracts.Products;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Ürün/stok uç noktaları.</summary>
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/products").WithTags("Ürünler");
        var inventory = app.MapGroup("/inventory").WithTags("Stok");
        var categories = app.MapGroup("/categories").WithTags("Tanımlar");
        var units = app.MapGroup("/units").WithTags("Tanımlar");
        var warehouses = app.MapGroup("/warehouses").WithTags("Tanımlar");

        // ---- Ürünler

        products.MapGet("/", async (
                string? search,
                Guid? categoryId,
                bool? includeInactive,
                bool? criticalOnly,
                int? page,
                int? pageSize,
                ListProductsHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(
                search, categoryId,
                includeInactive ?? true, criticalOnly ?? false,
                page ?? 1, pageSize ?? 20, cancellationToken)))
            .WithName("ListProducts")
            .WithSummary("Ürün listesi (arama, kategori, kritik stok filtresi, güncel stok)")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsView);

        products.MapPost("/", async (
                CreateProductRequest request,
                CreateProductHandler handler,
                CancellationToken cancellationToken) =>
        {
            var product = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/products/{product.Id}", product);
        })
            .WithName("CreateProduct")
            .WithSummary("Yeni ürün/hizmet kartı")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsCreate);

        products.MapGet("/critical", async (
                GetCriticalStockHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("GetCriticalStock")
            .WithSummary("Kritik stok listesi (güncel stok ≤ eşik)")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryView);

        products.MapGet("/{id:guid}", async (
                Guid id,
                GetProductHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetProduct")
            .WithSummary("Ürün detayı ve stok özeti")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsView);

        products.MapPut("/{id:guid}", async (
                Guid id,
                UpdateProductRequest request,
                UpdateProductHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateProduct")
            .WithSummary("Ürün kartını günceller")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        products.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteProductHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteProduct")
            .WithSummary("Ürünü siler (stok hareketi varsa reddedilir)")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        products.MapGet("/{id:guid}/stock", async (
                Guid id,
                GetProductStockHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetProductStock")
            .WithSummary("Ürünün depo bazında stok dökümü")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryView);

        products.MapGet("/{id:guid}/inventory", async (
                Guid id,
                Guid? warehouseId,
                int? page,
                int? pageSize,
                ListInventoryTransactionsHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, warehouseId, page ?? 1, pageSize ?? 50, cancellationToken)))
            .WithName("ListInventoryTransactions")
            .WithSummary("Ürünün stok hareket geçmişi (en yeni önce)")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryView);

        // ---- Stok hareketleri

        inventory.MapPost("/transactions", async (
                CreateInventoryTransactionRequest request,
                CreateInventoryTransactionHandler handler,
                CancellationToken cancellationToken) =>
            Results.Created($"/api/v1/products/{request.ProductId}/inventory",
                await handler.HandleAsync(request, cancellationToken)))
            .WithName("CreateInventoryTransaction")
            .WithSummary("Manuel stok hareketi (sayım / manuel giriş-çıkış / iade)")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryEdit);

        inventory.MapPost("/transfers", async (
                CreateInventoryTransferRequest request,
                CreateInventoryTransferHandler handler,
                CancellationToken cancellationToken) =>
            Results.Created($"/api/v1/products/{request.ProductId}/inventory",
                await handler.HandleAsync(request, cancellationToken)))
            .WithName("CreateInventoryTransfer")
            .WithSummary("Depolar arası transfer (çıkış + giriş çifti)")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryEdit);

        // ---- Kategoriler

        categories.MapGet("/", async (
                ListCategoriesHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("ListCategories")
            .WithSummary("Kategori listesi (ürün sayılarıyla)")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsView);

        categories.MapPost("/", async (
                CreateCategoryRequest request,
                CreateCategoryHandler handler,
                CancellationToken cancellationToken) =>
        {
            var category = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/categories/{category.Id}", category);
        })
            .WithName("CreateCategory")
            .WithSummary("Yeni kategori")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        categories.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCategoryRequest request,
                UpdateCategoryHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateCategory")
            .WithSummary("Kategoriyi yeniden adlandırır")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        categories.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteCategoryHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteCategory")
            .WithSummary("Kategoriyi siler (ürünü varsa reddedilir)")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        // ---- Birimler

        units.MapGet("/", async (
                ListUnitsHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("ListUnits")
            .WithSummary("Ölçü birimi listesi (ürün sayılarıyla)")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsView);

        units.MapPost("/", async (
                CreateUnitRequest request,
                CreateUnitHandler handler,
                CancellationToken cancellationToken) =>
        {
            var unit = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/units/{unit.Id}", unit);
        })
            .WithName("CreateUnit")
            .WithSummary("Yeni ölçü birimi")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        units.MapPut("/{id:guid}", async (
                Guid id,
                UpdateUnitRequest request,
                UpdateUnitHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateUnit")
            .WithSummary("Birimi günceller")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        units.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteUnitHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteUnit")
            .WithSummary("Birimi siler (kullanan ürün varsa reddedilir)")
            .RequireTenant()
            .RequirePermission(Permissions.ProductsEdit);

        // ---- Depolar

        warehouses.MapGet("/", async (
                ListWarehousesHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("ListWarehouses")
            .WithSummary("Depo listesi (ilk çağrıda varsayılan depo oluşur)")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryView);

        warehouses.MapPost("/", async (
                CreateWarehouseRequest request,
                CreateWarehouseHandler handler,
                CancellationToken cancellationToken) =>
        {
            var warehouse = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/warehouses/{warehouse.Id}", warehouse);
        })
            .WithName("CreateWarehouse")
            .WithSummary("Yeni depo")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryEdit);

        warehouses.MapPut("/{id:guid}", async (
                Guid id,
                UpdateWarehouseRequest request,
                UpdateWarehouseHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateWarehouse")
            .WithSummary("Depoyu günceller (varsayılan değiştirme dahil)")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryEdit);

        warehouses.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteWarehouseHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteWarehouse")
            .WithSummary("Depoyu siler (hareketi veya varsayılanıysa reddedilir)")
            .RequireTenant()
            .RequirePermission(Permissions.InventoryEdit);
    }
}
