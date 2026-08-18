using Accounting.Application.Abstractions;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Identity;
using Accounting.Infrastructure.MultiTenancy;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Accounting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // İstek kapsamı bağlamları
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<TenantContext>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextWriter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Default"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ASP.NET Core Identity — parola politikası güçlü, e-posta benzersiz.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // UserName olarak e-posta kullanılıyor; varsayılan karakter listesi
                // Türkçe karakterli e-postaları reddediyor. Format validasyonu
                // zaten FluentValidation tarafında yapılıyor.
                options.User.AllowedUserNameCharacters = string.Empty;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IEmailSender, DevEmailSender>();

        services
            .AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("Default")!,
                name: "postgresql",
                tags: ["ready"]);

        return services;
    }
}
