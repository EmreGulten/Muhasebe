using Accounting.Api.Authorization;
using Accounting.Api.Middleware;
using Accounting.Application.Features.Accounts;
using Accounting.Contracts.Accounts;
using Accounting.Domain.Authorization;

namespace Accounting.Api.Endpoints;

/// <summary>Kasa/banka uç noktaları (muhasebe.md bölüm 9).</summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts").WithTags("Kasa & Banka");

        accounts.MapGet("/", async (
                ListAccountsHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("ListAccounts")
            .WithSummary("Hesap listesi — varsayılan önce, bakiye ve hareket sayısı dahil")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsView);

        accounts.MapPost("/", async (
                CreateAccountRequest request,
                CreateAccountHandler handler,
                CancellationToken cancellationToken) =>
        {
            var account = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/v1/accounts/{account.Id}", account);
        })
            .WithName("CreateAccount")
            .WithSummary("Yeni hesap (açılış bakiyesi varsa tek seferlik hareketle açılır)")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsCreate);

        accounts.MapPost("/transfer", async (
                TransferRequest request,
                CreateTransferHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("TransferBetweenAccounts")
            .WithSummary("Hesaplar arası transfer — tek işlemde çıkış + giriş çifti")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsEdit);

        accounts.MapGet("/{id:guid}", async (
                Guid id,
                GetAccountHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetAccount")
            .WithSummary("Hesap detayı (bakiye ve hareket sayısı)")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsView);

        accounts.MapPut("/{id:guid}", async (
                Guid id,
                UpdateAccountRequest request,
                UpdateAccountHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateAccount")
            .WithSummary("Hesabı düzenle (ad + aktiflik; tür/açılış sabit)")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsEdit);

        accounts.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteAccountHandler handler,
                CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteAccount")
            .WithSummary("Hareketsiz hesabı sil (varsayılan kasa silinemez)")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsEdit);

        accounts.MapGet("/{id:guid}/statement", async (
                Guid id,
                int? page,
                int? pageSize,
                GetAccountStatementHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, page ?? 1, pageSize ?? 50, cancellationToken)))
            .WithName("GetAccountStatement")
            .WithSummary("Hesap ekstresi — tarih sırası, sayfa içi çalışan bakiye")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsView);

        accounts.MapPost("/{id:guid}/transactions", async (
                Guid id,
                CreateAccountTransactionRequest request,
                CreateAccountTransactionHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("CreateAccountTransaction")
            .WithSummary("Manuel hesap hareketi — In (giriş) / Out (çıkış)")
            .RequireTenant()
            .RequirePermission(Permissions.AccountsEdit);
    }
}
