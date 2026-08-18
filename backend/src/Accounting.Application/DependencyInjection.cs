using Accounting.Application.Abstractions;
using Accounting.Application.Features.Auth;
using Accounting.Application.Features.Parties;
using Accounting.Application.Features.Tenants;
using Accounting.Application.Services;
using Accounting.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Accounting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ITokenService, JwtTokenService>();

        // Use case handler'ları
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<ListTenantsHandler>();
        services.AddScoped<GetTenantHandler>();

        // Cari
        services.AddScoped<CreatePartyHandler>();
        services.AddScoped<UpdatePartyHandler>();
        services.AddScoped<DeletePartyHandler>();
        services.AddScoped<GetPartyHandler>();
        services.AddScoped<ListPartiesHandler>();
        services.AddScoped<CreatePartyTransactionHandler>();
        services.AddScoped<GetPartyStatementHandler>();

        services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

        return services;
    }
}
