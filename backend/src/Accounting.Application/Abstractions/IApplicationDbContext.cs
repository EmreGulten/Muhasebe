using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Abstractions;

/// <summary>
/// Application katmanının veri erişimi sözleşmesi.
/// Uygulama EF Core'a yalnızca bu arayüz üzerinden dokunur; somut
/// DbContext Infrastructure katmanında yaşar.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<UserTenant> UserTenants { get; }

    DbSet<Party> Parties { get; }

    DbSet<PartyTransaction> PartyTransactions { get; }

    DbSet<Category> Categories { get; }

    DbSet<Unit> Units { get; }

    DbSet<Warehouse> Warehouses { get; }

    DbSet<Product> Products { get; }

    DbSet<InventoryTransaction> InventoryTransactions { get; }

    DbSet<Sale> Sales { get; }

    DbSet<SaleItem> SaleItems { get; }

    DbSet<SalePayment> SalePayments { get; }

    DbSet<Account> Accounts { get; }

    DbSet<AccountTransaction> AccountTransactions { get; }

    DbSet<Purchase> Purchases { get; }

    DbSet<PurchaseItem> PurchaseItems { get; }

    DbSet<PurchasePayment> PurchasePayments { get; }
    DbSet<IncomeExpenseCategory> IncomeExpenseCategories { get; }

    DbSet<IncomeExpenseRecord> IncomeExpenseRecords { get; }

    DbSet<AiMessage> AiMessages { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
