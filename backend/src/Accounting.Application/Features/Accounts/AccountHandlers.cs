using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Accounts;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.Accounts;

/// <summary>Kasa/banka özelliğinin paylaşılan sorgu yardımcıları.</summary>
internal static class AccountQueries
{
    public static Guid RequireTenantId(ICurrentTenant currentTenant) =>
        currentTenant.TenantId
        ?? throw new ConflictException("Aktif işletme bağlamı bulunamadı. X-Tenant-Id başlığını gönderin.");

    /// <summary>"Cash" | "Bank" | "CreditCard" | "VirtualPOS" → AccountType.</summary>
    public static AccountType ParseType(string value) =>
        Enum.TryParse<AccountType>(value, ignoreCase: false, out var type)
            ? type
            : throw new AppException(
                "Hesap türü geçersiz. Geçerli değerler: Cash, Bank, CreditCard, VirtualPOS.");

    /// <summary>"In" (giriş) | "Out" (çıkış).</summary>
    public static bool IsIncoming(string direction) =>
        direction switch
        {
            "In" => true,
            "Out" => false,
            _ => throw new AppException("Hareket yönü geçersiz. Geçerli değerler: In, Out."),
        };

    public static Task<Account?> FindAccountAsync(
        IApplicationDbContext db, Guid tenantId, Guid accountId, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId, cancellationToken);

    /// <summary>Hesabı bakiye ve hareket sayısıyla yanıta çevirir (bakiye = Σ hareketler).</summary>
    public static async Task<AccountDto> MaterializeAsync(
        IApplicationDbContext db, Guid tenantId, Account account, CancellationToken cancellationToken)
    {
        var aggregate = await db.AccountTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.AccountId == account.Id)
            .GroupBy(_ => 1)
            .Select(g => new { Balance = g.Sum(t => t.Amount), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        return account.ToDto(aggregate?.Balance ?? 0, aggregate?.Count ?? 0);
    }
}

/// <summary>Hesap listesi — varsayılan önce, sonra tür, sonra ad. Bakiyeler tek grup sorgusuyla.</summary>
public sealed class ListAccountsHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<IReadOnlyList<AccountDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);

        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => !a.IsDefault)
            .ThenBy(a => a.Type)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var aggregates = await db.AccountTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(t => t.Amount), Count = g.Count() })
            .ToDictionaryAsync(a => a.AccountId, a => (a.Balance, a.Count), cancellationToken);

        return accounts
            .Select(a =>
            {
                var (balance, count) = aggregates.TryGetValue(a.Id, out var agg) ? agg : (0m, 0);
                return a.ToDto(balance, count);
            })
            .ToList();
    }
}

/// <summary>Tek hesap — bakiye ve hareket sayısı dahil.</summary>
public sealed class GetAccountHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<AccountDto> HandleAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);

        var account = await AccountQueries.FindAccountAsync(db, tenantId, accountId, cancellationToken)
            ?? throw new NotFoundException("Hesap bulunamadı.");

        return await AccountQueries.MaterializeAsync(db, tenantId, account, cancellationToken);
    }
}

/// <summary>
/// Yeni hesap. Açılış bakiyesi 0'dan büyükse tek seferlik OpeningBalance hareketi
/// ile birlikte, tek SaveChanges'te yazılır (bölüm 24).
/// </summary>
public sealed class CreateAccountHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<AccountDto> HandleAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var account = new Account
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Type = AccountQueries.ParseType(request.Type),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "TRY" : request.Currency.Trim().ToUpperInvariant(),
            OpeningBalance = request.OpeningBalance,
            CreatedAtUtc = now,
        };
        db.Accounts.Add(account);

        if (request.OpeningBalance > 0)
        {
            db.AccountTransactions.Add(new AccountTransaction
            {
                TenantId = tenantId,
                AccountId = account.Id,
                Type = AccountTransactionType.OpeningBalance,
                Amount = request.OpeningBalance,
                // Gün bazına normalize: aynı güne girilen hareketlerden önce
                // görünür (Id v7 eşitlikte giriş sırasını korur).
                Date = Dates.ToUtcDate(now),
                Description = "Açılış bakiyesi",
                ReferenceType = "Account",
                ReferenceId = account.Id,
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await AccountQueries.MaterializeAsync(db, tenantId, account, cancellationToken);
    }
}

/// <summary>Hesap güncelleme — ad ve aktiflik. Tür/açılış bakiyesi sabittir; düzeltme hareketle yapılır.</summary>
public sealed class UpdateAccountHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<AccountDto> HandleAsync(
        Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);

        var account = await AccountQueries.FindAccountAsync(db, tenantId, accountId, cancellationToken)
            ?? throw new NotFoundException("Hesap bulunamadı.");

        if (account.IsDefault && !request.IsActive)
        {
            throw new ConflictException(
                "Varsayılan kasa hesabı pasifleştirilemez; satış/alış ödemeleri bu hesaba yazılır.");
        }

        account.Name = request.Name.Trim();
        account.IsActive = request.IsActive;
        account.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);
        return await AccountQueries.MaterializeAsync(db, tenantId, account, cancellationToken);
    }
}

/// <summary>
/// Hesap silme (soft). Hareketi olan hesap silinemez — kayıt zinciri korunur;
/// pasifleştirme önerilir. Varsayılan kasa hesabı da silinemez.
/// </summary>
public sealed class DeleteAccountHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task HandleAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);

        var account = await AccountQueries.FindAccountAsync(db, tenantId, accountId, cancellationToken)
            ?? throw new NotFoundException("Hesap bulunamadı.");

        if (account.IsDefault)
        {
            throw new ConflictException("Varsayılan kasa hesabı silinemez.");
        }

        var hasTransactions = await db.AccountTransactions.AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.AccountId == accountId, cancellationToken);
        if (hasTransactions)
        {
            throw new ConflictException("Hareketi olan hesap silinemez; hesabı pasifleştirin.");
        }

        account.IsDeleted = true;
        account.DeletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Hesap ekstresi — tarih sırasına göre hareketler ve sayfa içi çalışan bakiye.
/// Sıralama (Date, Id) belirleyicidir; Guid v7 kimlikler giriş sırasını korur.
/// </summary>
public sealed class GetAccountStatementHandler(IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 200;

    public async Task<AccountStatementResponse> HandleAsync(
        Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);

        var account = await db.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Id == accountId)
            .Select(a => new { a.Id, a.Name, a.Currency })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Hesap bulunamadı.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);
        var offset = (page - 1) * pageSize;

        var query = db.AccountTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.AccountId == accountId)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Sayfadaki ilk satırdan önceki kümülatif bakiye.
        var balanceBeforePage = await query
            .Take(offset)
            .Select(t => t.Amount)
            .SumAsync(cancellationToken);

        var runningBalance = balanceBeforePage;
        var dtos = items.Select(t =>
        {
            runningBalance += t.Amount;
            return t.ToDto(account.Name, runningBalance);
        }).ToList();

        return new AccountStatementResponse(
            account.Id, account.Name, account.Currency, balanceBeforePage, page, pageSize, totalCount, dtos);
    }
}

/// <summary>
/// Manuel hesap hareketi — giriş (tahsilat) ya da çıkış (ödeme). Defter modeli:
/// hareket sonradan değiştirilemez/silinemez; düzeltme ters hareketle yapılır.
/// </summary>
public sealed class CreateAccountTransactionHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<AccountTransactionDto> HandleAsync(
        Guid accountId, CreateAccountTransactionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var account = await AccountQueries.FindAccountAsync(db, tenantId, accountId, cancellationToken)
            ?? throw new NotFoundException("Hesap bulunamadı.");
        if (!account.IsActive)
        {
            throw new AppException("Pasif hesaba hareket yazılamaz.");
        }

        var isIncoming = AccountQueries.IsIncoming(request.Direction);
        var date = Dates.ToUtcDate(request.Date);

        var transaction = new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = account.Id,
            Type = isIncoming ? AccountTransactionType.ManualCollection : AccountTransactionType.ManualPayment,
            Amount = isIncoming ? request.Amount : -request.Amount,
            Date = date,
            Description = request.Description?.Trim(),
            ReferenceType = "Manual",
            CreatedAtUtc = now,
        };
        db.AccountTransactions.Add(transaction);

        await db.SaveChangesAsync(cancellationToken);

        // Bu hareket dahil kümülatif bakiye (ekstredeki çalışan bakiye).
        var balance = await db.AccountTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.AccountId == account.Id)
            .Select(t => t.Amount)
            .SumAsync(cancellationToken);

        return transaction.ToDto(account.Name, balance);
    }
}

/// <summary>
/// Hesaplar arası transfer — tek işlemde çıkış + giriş çifti (bölüm 9).
/// Çifti ReferenceId üzerinden bağlanır; hesaplar farklı ve aktif olmalıdır.
/// </summary>
public sealed class CreateTransferHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<TransferResponse> HandleAsync(
        TransferRequest request, CancellationToken cancellationToken)
    {
        var tenantId = AccountQueries.RequireTenantId(currentTenant);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (request.FromAccountId == request.ToAccountId)
        {
            throw new AppException("Aynı hesaba transfer yapılamaz.");
        }

        var from = await AccountQueries.FindAccountAsync(db, tenantId, request.FromAccountId, cancellationToken)
            ?? throw new NotFoundException("Kaynak hesap bulunamadı.");
        var to = await AccountQueries.FindAccountAsync(db, tenantId, request.ToAccountId, cancellationToken)
            ?? throw new NotFoundException("Hedef hesap bulunamadı.");
        if (!from.IsActive || !to.IsActive)
        {
            throw new AppException("Pasif hesaba transfer yapılamaz.");
        }

        var date = Dates.ToUtcDate(request.Date);
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Transfer — {from.Name} → {to.Name}"
            : request.Description.Trim();
        var transferId = Guid.CreateVersion7();

        db.AccountTransactions.Add(new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = from.Id,
            Type = AccountTransactionType.Transfer,
            Amount = -request.Amount,
            Date = date,
            Description = description,
            ReferenceType = "Transfer",
            ReferenceId = transferId,
            CreatedAtUtc = now,
        });
        db.AccountTransactions.Add(new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = to.Id,
            Type = AccountTransactionType.Transfer,
            Amount = request.Amount,
            Date = date,
            Description = description,
            ReferenceType = "Transfer",
            ReferenceId = transferId,
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        var balances = await db.AccountTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && (t.AccountId == from.Id || t.AccountId == to.Id))
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(b => b.AccountId, b => b.Balance, cancellationToken);

        return new TransferResponse(
            from.Id, balances.GetValueOrDefault(from.Id),
            to.Id, balances.GetValueOrDefault(to.Id));
    }
}

internal static class AccountMappingExtensions
{
    public static AccountDto ToDto(this Account account, decimal balance, int transactionCount) =>
        new(
            account.Id,
            account.Name,
            account.Type.ToString(),
            account.Currency,
            account.OpeningBalance,
            balance,
            account.IsDefault,
            account.IsActive,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            transactionCount);

    public static AccountTransactionDto ToDto(this AccountTransaction transaction, string accountName, decimal balance) =>
        new(
            transaction.Id,
            transaction.AccountId,
            accountName,
            transaction.Type.ToString(),
            transaction.Amount,
            transaction.Date,
            transaction.Description,
            transaction.ReferenceType,
            transaction.ReferenceId,
            balance,
            transaction.CreatedAtUtc);
}
