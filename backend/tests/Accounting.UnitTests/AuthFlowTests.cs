using Accounting.Application.Common;
using Accounting.Application.Features.Auth;
using Accounting.Contracts.Auth;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Accounting.UnitTests;

public sealed class AuthFlowTests : IDisposable
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task Register_Creates_User_Tenant_And_Owner_Membership()
    {
        using var scope = _app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterHandler>();

        var result = await handler.HandleAsync(
            new RegisterRequest("emre@test.local", "Parola123", "Emre Gülten", "Gülten Oto Servis"),
            requestIp: "127.0.0.1",
            cancellationToken: default);

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("emre@test.local", result.User.Email);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Tenants.CountAsync());

        var membership = await db.UserTenants.SingleAsync();
        Assert.Equal(Domain.Enums.TenantRole.Owner, membership.Role);
        Assert.Equal("Gülten Oto Servis", membership.Tenant.Name);

        Assert.Single(result.Tenants);
        Assert.Equal("Owner", result.Tenants[0].Role);
    }

    [Fact]
    public async Task Register_Duplicate_Email_Throws_Conflict()
    {
        await _app.RegisterUserAsync(email: "ayni@test.local");

        using var scope = _app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterHandler>();

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new RegisterRequest("ayni@test.local", "Parola1234", "Başka Kullanıcı", null),
            requestIp: null,
            cancellationToken: default));
    }

    [Fact]
    public async Task Register_Weak_Password_Fails_With_BadRequest()
    {
        using var scope = _app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterHandler>();

        // Parolada büyük harf yok → Identity politikası reddeder.
        var exception = await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            new RegisterRequest("zayif@test.local", "parola123", "Zayıf Parola", null),
            requestIp: null,
            cancellationToken: default));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task Login_With_Correct_Password_Returns_Tokens_And_Tenants()
    {
        await _app.RegisterUserAsync(email: "giris@test.local", password: "Parola123", businessName: "Deneme Market");

        using var scope = _app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

        var result = await handler.HandleAsync(new LoginRequest("giris@test.local", "Parola123"), null, default);

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("Deneme Market", result.Tenants.Single().Name);

        // Access token'da sub claim'i kullanıcı kimliğini taşımalı.
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.AccessToken);
        Assert.Equal(result.User.Id.ToString(), jwt.Subject);
        Assert.Equal("giris@test.local", jwt.GetClaim("email")?.Value);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Throws_Unauthorized()
    {
        await _app.RegisterUserAsync(email: "yanlis@test.local", password: "Parola123");

        using var scope = _app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.HandleAsync(new LoginRequest("yanlis@test.local", "YanlisSifre1"), null, default));
    }

    [Fact]
    public async Task Refresh_Rotates_Token_And_Old_Token_Becomes_Invalid()
    {
        var loginResult = await LoginFreshUserAsync();

        using var scope = _app.CreateScope();
        var refreshHandler = scope.ServiceProvider.GetRequiredService<RefreshHandler>();

        var rotated = await refreshHandler.HandleAsync(loginResult.RefreshToken, null, default);
        Assert.NotEqual(loginResult.RefreshToken, rotated.RefreshToken);
        Assert.Equal(loginResult.User.Id, rotated.User.Id);

        // Eski token artık geçersiz olmalı.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            refreshHandler.HandleAsync(loginResult.RefreshToken, null, default));
    }

    [Fact]
    public async Task Refresh_Reuse_Detection_Revokes_All_User_Tokens()
    {
        var loginResult = await LoginFreshUserAsync();

        using var scope = _app.CreateScope();
        var refreshHandler = scope.ServiceProvider.GetRequiredService<RefreshHandler>();

        var rotated = await refreshHandler.HandleAsync(loginResult.RefreshToken, null, default);

        // Çalınan token tekrar kullanılırsa tüm oturumlar düşer.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            refreshHandler.HandleAsync(loginResult.RefreshToken, null, default));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            refreshHandler.HandleAsync(rotated.RefreshToken, null, default));
    }

    [Fact]
    public async Task Logout_Revokes_Refresh_Token()
    {
        var loginResult = await LoginFreshUserAsync();

        using var scope = _app.CreateScope();
        var logoutHandler = scope.ServiceProvider.GetRequiredService<LogoutHandler>();
        var refreshHandler = scope.ServiceProvider.GetRequiredService<RefreshHandler>();

        await logoutHandler.HandleAsync(loginResult.RefreshToken, default);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            refreshHandler.HandleAsync(loginResult.RefreshToken, null, default));
    }

    [Fact]
    public async Task Register_Defaults_BusinessName_When_Missing()
    {
        using var scope = _app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterHandler>();

        var result = await handler.HandleAsync(
            new RegisterRequest("bizsiz@test.local", "Parola123", "Ayşe Yılmaz", null),
            null, default);

        Assert.Equal("Ayşe Yılmaz İşletmesi", result.Tenants.Single().Name);
    }

    private async Task<AuthResponse> LoginFreshUserAsync()
    {
        using var scope = _app.CreateScope();
        var loginHandler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
        await _app.RegisterUserAsync(email: "taze@test.local", password: "Parola123");
        return await loginHandler.HandleAsync(new LoginRequest("taze@test.local", "Parola123"), null, default);
    }

    public void Dispose() => _app.Dispose();
}

public sealed class ValidatorTests
{
    [Fact]
    public void RegisterValidator_Rejects_Invalid_Email()
    {
        var validator = new Accounting.Application.Validators.RegisterValidator();
        var result = validator.Validate(new RegisterRequest("gecersiz-email", "Parola123", "Ad Soyad", null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void RegisterValidator_Rejects_Weak_Password()
    {
        var validator = new Accounting.Application.Validators.RegisterValidator();
        var result = validator.Validate(new RegisterRequest("a@b.com", "kisa", "Ad Soyad", null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void RegisterValidator_Accepts_Valid_Request()
    {
        var validator = new Accounting.Application.Validators.RegisterValidator();
        var result = validator.Validate(new RegisterRequest("a@b.com", "Parola123", "Ad Soyad", "İşletme"));

        Assert.True(result.IsValid);
    }
}
