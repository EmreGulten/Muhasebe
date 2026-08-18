using System.Security.Claims;
using Accounting.Api.Extensions;
using Accounting.Application.Features.Auth;
using Accounting.Contracts.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Accounting.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (
                RegisterRequest request,
                RegisterHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                RefreshTokenCookie.Set(http, result.RefreshToken, result.RefreshTokenExpiresAtUtc);
                return Results.Ok(result);
            })
            .WithName("Register")
            .WithSummary("Yeni kullanıcı ve işletme kaydı; oturum token'larından döner")
            .RequireRateLimiting("auth");

        group.MapPost("/login", async (
                LoginRequest request,
                LoginHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                RefreshTokenCookie.Set(http, result.RefreshToken, result.RefreshTokenExpiresAtUtc);
                return Results.Ok(result);
            })
            .WithName("Login")
            .WithSummary("Giriş; access + refresh token döner")
            .RequireRateLimiting("auth");

        group.MapPost("/refresh", async (
                [FromBody] RefreshRequest? body,
                RefreshHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var rawToken = body?.RefreshToken ?? RefreshTokenCookie.Read(http);
                var result = await handler.HandleAsync(rawToken, http.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                RefreshTokenCookie.Set(http, result.RefreshToken, result.RefreshTokenExpiresAtUtc);
                return Results.Ok(result);
            })
            .WithName("Refresh")
            .WithSummary("Refresh token'ı döndürür (cookie veya body)")
            .RequireRateLimiting("auth");

        group.MapPost("/logout", async (
                [FromBody] RefreshRequest? body,
                LogoutHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var rawToken = body?.RefreshToken ?? RefreshTokenCookie.Read(http);
                await handler.HandleAsync(rawToken, cancellationToken);
                RefreshTokenCookie.Clear(http);
                return Results.NoContent();
            })
            .WithName("Logout")
            .WithSummary("Refresh token'ı iptal eder")
            .RequireAuthorization();

        group.MapGet("/me", async (
                ClaimsPrincipal principal,
                MeHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(principal.GetUserId(), cancellationToken)))
            .WithName("GetMe")
            .WithSummary("Oturum açan kullanıcının profili ve işletmeleri")
            .RequireAuthorization();

        group.MapPost("/forgot-password", async (
                ForgotPasswordRequest request,
                ForgotPasswordHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("ForgotPassword")
            .WithSummary("Parola sıfırlama bağlantısı talebi")
            .RequireRateLimiting("auth");

        group.MapPost("/reset-password", async (
                ResetPasswordRequest request,
                ResetPasswordHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("ResetPassword")
            .WithSummary("Parolayı sıfırlama token'ı ile değiştirir")
            .RequireRateLimiting("auth");

        return group;
    }
}
