using Accounting.Application;
using Accounting.Application.Abstractions;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Identity;
using Accounting.Infrastructure.MultiTenancy;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Accounting.UnitTests;

/// <summary>
/// SQLite in-memory üzerinde gerçek Identity + EF Core + handler'larla
/// çalışan test kapsayıcısı. Npgsql'e özgü migration gerektirmez.
/// </summary>
public sealed class TestApp : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestApp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddHttpContextAccessor();

        // Production'daki interceptor zinciri (audit + timestamp + soft delete) testlerden de geçsin.
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<TenantContext>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextWriter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlite(_connection)
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = false; // testlerde lockout'a takılmayalım
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddSingleton<IOptions<JwtOptions>>(_ => Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Secret = new string('k', 64),
            AccessTokenLifetimeMinutes = 5,
            RefreshTokenLifetimeDays = 7,
        }));

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public IServiceProvider Services { get; }

    public IServiceScope CreateScope() => Services.CreateScope();

    public void Dispose()
    {
        _connection.Dispose();
    }
}

public static class TestAppExtensions
{
    public static async Task<ApplicationUser> RegisterUserAsync(
        this TestApp app,
        string email = "kullanici@test.local",
        string password = "Parola123",
        string fullName = "Test Kullanıcı",
        string businessName = "Test İşletmesi")
    {
        using var scope = app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<Application.Features.Auth.RegisterHandler>();
        await handler.HandleAsync(
            new Contracts.Auth.RegisterRequest(email, password, fullName, businessName),
            requestIp: null,
            cancellationToken: default);
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Test kullanıcısı oluşturulamadı: {email}");
    }
}
