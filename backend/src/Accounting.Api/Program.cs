using System.Text;
using System.Threading.RateLimiting;
using Accounting.Api.Authorization;
using Accounting.Api.Endpoints;
using Accounting.Api.Middleware;
using Accounting.Application;
using Accounting.Application.Abstractions;
using Accounting.Infrastructure;
using Accounting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Globalization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- Loglama
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .Enrich.FromLogContext());

// ---- Options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));

// ---- Katmanlar
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- JWT (fail fast: secret yapılandırılmamışsa başlamasın)
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret yapılandırılmamış veya 32 karakterden kısa. " +
        "Örnek üretim komutu: openssl rand -base64 48. Ortam değişkeni: Jwt__Secret");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // "sub" claim'i ClaimTypes.NameIdentifier'a maplenmesin.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization();

// "perm:<izin>" politikaları roller üzerinden değerlendirilir (muhasebe.md bölüm 14).
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

// ---- Rate limiting (auth endpoint'leri IP başına dakikada 20 istek)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen-ip",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ---- CORS (frontend proxy kullanır; doğrudan erişim için güvenilir origin'ler)
var allowedOrigins = (builder.Configuration["AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// ---- OpenAPI + hata işleme
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar
}

app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// ---- Migration'ları başlangıçta uygula (ApplyMigrations=false ile kapatılabilir)
if (app.Configuration.GetValue("ApplyMigrations", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// ---- Health checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// ---- API
var api = app.MapGroup("/api/v1").AddEndpointFilter<ValidationEndpointFilter>();
api.MapAuthEndpoints();
api.MapTenantEndpoints();
api.MapPartyEndpoints();
api.MapProductEndpoints();
api.MapSaleEndpoints();
api.MapPurchaseEndpoints();
api.MapAccountEndpoints();
api.MapIncomeExpenseEndpoints();
api.MapReportEndpoints();

app.Run();

// Integration test'lerin erişebilmesi için.
public partial class Program;
