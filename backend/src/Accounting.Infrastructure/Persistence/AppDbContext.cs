using Accounting.Application.Abstractions;
using Accounting.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Infrastructure.Persistence;

// .NET 10: Guid key için IdentityDbContext<TUser, TRole, TKey> kullanılır.
public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<UserTenant> UserTenants => Set<UserTenant>();

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyTransaction> PartyTransactions => Set<PartyTransaction>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    public DbSet<SalePayment> SalePayments => Set<SalePayment>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();

    public DbSet<Purchase> Purchases => Set<Purchase>();

    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<IncomeExpenseCategory> IncomeExpenseCategories => Set<IncomeExpenseCategory>();

    public DbSet<IncomeExpenseRecord> IncomeExpenseRecords => Set<IncomeExpenseRecord>();

    public DbSet<AiMessage> AiMessages => Set<AiMessage>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity tabloları plandaki isimlendirmeyle hizalanır (muhasebe.md bölüm 20).
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Para alanları her zaman numeric(18,2); miktar alanları numeric(18,4) —
        // float/double asla kullanılmaz (muhasebe.md bölüm 21).
        if (Database.IsNpgsql())
        {
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(decimal) &&
                        property.GetColumnType() is null)
                    {
                        property.SetColumnType("numeric(18,2)");
                    }
                }
            }
        }
    }
}
