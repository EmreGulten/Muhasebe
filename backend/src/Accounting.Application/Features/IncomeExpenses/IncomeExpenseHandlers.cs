using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Sales;
using Accounting.Contracts;
using Accounting.Contracts.IncomeExpenses;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Features.IncomeExpenses;

/// <summary>
/// Gelir/gider kaydı oluşturma. Kayıt ve kasa hareketi
/// tek transaction'da yazılır: gelir hesaba +, gider − işaretli hareket düşer.
/// Hesap verilmezse varsayılan "Kasa" lazy oluşur (satış/alış ödemeleriyle aynı).
/// </summary>
public sealed class CreateIncomeExpenseRecordHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<IncomeExpenseRecordDto> HandleAsync(
        CreateIncomeExpenseRecordRequest request, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var type = IncomeExpenseQueries.ParseType(request.Type);
        var date = Dates.ToUtcDate(request.Date);

        var category = await db.IncomeExpenseCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Kategori bulunamadı.");
        if (!category.IsActive)
        {
            throw new AppException("Pasif kategorıyla kayıt girilemez.");
        }
        if (category.Type != type)
        {
            throw new AppException(
                $"Kategori '{category.Name}' {(category.Type == IncomeExpenseType.Income ? "gelir" : "gider")} tarafındadır; kayıt türüyle uyuşmuyor.");
        }

        Account account;
        if (request.PaymentAccountId is { } accountId)
        {
            account = await db.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId, cancellationToken)
                ?? throw new NotFoundException("Hesap bulunamadı.");
            if (!account.IsActive)
            {
                throw new AppException("Pasif hesaba hareket yazılamaz.");
            }
        }
        else
        {
            // Varsayılan kasa lazy oluşur; SaveChanges çevreleyen kayıt ile atomik.
            account = await SaleAccounts.EnsureDefaultAccountAsync(db, tenantId, timeProvider, cancellationToken);
        }

        var record = new IncomeExpenseRecord
        {
            TenantId = tenantId,
            Type = type,
            CategoryId = category.Id,
            Amount = request.Amount,
            Date = date,
            PaymentAccountId = account.Id,
            Description = request.Description.NullIfEmpty(),
            DocumentNumber = request.DocumentNumber.NullIfEmpty(),
            Status = IncomeExpenseStatus.Active,
            CreatedAtUtc = now,
        };
        db.IncomeExpenseRecords.Add(record);

        var typeLabel = type == IncomeExpenseType.Income ? "Gelir" : "Gider";
        db.AccountTransactions.Add(new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = account.Id,
            Type = type == IncomeExpenseType.Income
                ? AccountTransactionType.Income
                : AccountTransactionType.Expense,
            Amount = type == IncomeExpenseType.Income ? request.Amount : -request.Amount,
            Date = date,
            Description = record.Description ?? $"{typeLabel} — {category.Name}",
            ReferenceType = "IncomeExpense",
            ReferenceId = record.Id,
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new IncomeExpenseRecordDto(
            record.Id, record.Type.ToString(), record.CategoryId, category.Name, record.Amount, record.Date,
            record.PaymentAccountId, account.Name, record.Description, record.DocumentNumber,
            record.Status.ToString(), null, record.CreatedAtUtc);
    }
}

/// <summary>Kayıt listesi — tür, kategori ve dönem filtreleri, en yeni önce.</summary>
public sealed class ListIncomeExpenseRecordsHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<IncomeExpenseRecordDto>> HandleAsync(
        string? type,
        Guid? categoryId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize);

        var parsedType = type is null ? null : (IncomeExpenseType?)IncomeExpenseQueries.ParseType(type);

        var query =
            from r in db.IncomeExpenseRecords.AsNoTracking().Where(r => r.TenantId == tenantId)
            join c in db.IncomeExpenseCategories.AsNoTracking() on r.CategoryId equals c.Id into categoryGroup
            from c in categoryGroup.DefaultIfEmpty()
            join a in db.Accounts.AsNoTracking() on r.PaymentAccountId equals a.Id into accountGroup
            from a in accountGroup.DefaultIfEmpty()
            select new
            {
                Record = r,
                CategoryName = c != null ? c.Name : "—",
                AccountName = a != null ? a.Name : "—",
            };

        if (parsedType is { } typeFilter)
        {
            query = query.Where(row => row.Record.Type == typeFilter);
        }
        if (categoryId is { } categoryFilter)
        {
            query = query.Where(row => row.Record.CategoryId == categoryFilter);
        }
        if (from is { } fromDate)
        {
            query = query.Where(row => row.Record.Date >= Dates.ToUtcDate(fromDate));
        }
        if (to is { } toDate)
        {
            query = query.Where(row => row.Record.Date < Dates.ToUtcDate(toDate).AddDays(1));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(row => row.Record.Date)
            .ThenByDescending(row => row.Record.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new IncomeExpenseRecordDto(
                row.Record.Id,
                row.Record.Type.ToString(),
                row.Record.CategoryId,
                row.CategoryName,
                row.Record.Amount,
                row.Record.Date,
                row.Record.PaymentAccountId,
                row.AccountName,
                row.Record.Description,
                row.Record.DocumentNumber,
                row.Record.Status.ToString(),
                row.Record.CancelledAtUtc,
                row.Record.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<IncomeExpenseRecordDto>(items, page, pageSize, totalCount);
    }
}

/// <summary>Kayıt detayı.</summary>
public sealed class GetIncomeExpenseRecordHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant)
{
    public async Task<IncomeExpenseRecordDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);

        var row = await (
            from r in db.IncomeExpenseRecords.AsNoTracking().Where(r => r.TenantId == tenantId && r.Id == id)
            join c in db.IncomeExpenseCategories.AsNoTracking() on r.CategoryId equals c.Id into categoryGroup
            from c in categoryGroup.DefaultIfEmpty()
            join a in db.Accounts.AsNoTracking() on r.PaymentAccountId equals a.Id into accountGroup
            from a in accountGroup.DefaultIfEmpty()
            select new
            {
                Record = r,
                CategoryName = c != null ? c.Name : "—",
                AccountName = a != null ? a.Name : "—",
            }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Kayıt bulunamadı.");

        return new IncomeExpenseRecordDto(
            row.Record.Id, row.Record.Type.ToString(), row.Record.CategoryId, row.CategoryName,
            row.Record.Amount, row.Record.Date, row.Record.PaymentAccountId, row.AccountName,
            row.Record.Description, row.Record.DocumentNumber, row.Record.Status.ToString(),
            row.Record.CancelledAtUtc, row.Record.CreatedAtUtc);
    }
}

/// <summary>
/// Kayıt iptali: kasa hareketinin tersi yazılır, kayıt
/// Cancelled durumuna geçer ve terminaldir. Kayıtlar hiçbir koşulda değiştirilmez.
/// </summary>
public sealed class CancelIncomeExpenseRecordHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<IncomeExpenseRecordDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var row = await (
            from r in db.IncomeExpenseRecords.Where(r => r.TenantId == tenantId && r.Id == id)
            join c in db.IncomeExpenseCategories on r.CategoryId equals c.Id into categoryGroup
            from c in categoryGroup.DefaultIfEmpty()
            select new { Record = r, CategoryName = c != null ? c.Name : "—" })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Kayıt bulunamadı.");

        var record = row.Record;
        if (record.Status == IncomeExpenseStatus.Cancelled)
        {
            throw new ConflictException("İptal edilmiş kayıt tekrar iptal edilemez.");
        }

        var typeLabel = record.Type == IncomeExpenseType.Income ? "Gelir" : "Gider";
        var signedAmount = record.Type == IncomeExpenseType.Income ? record.Amount : -record.Amount;

        // Ters hareket: kasa hesabını kayıt anındaki durumuna döndürür.
        db.AccountTransactions.Add(new AccountTransaction
        {
            TenantId = tenantId,
            AccountId = record.PaymentAccountId,
            Type = record.Type == IncomeExpenseType.Income
                ? AccountTransactionType.Income
                : AccountTransactionType.Expense,
            Amount = -signedAmount,
            Date = now,
            Description = $"İptal — {typeLabel} — {row.CategoryName}",
            ReferenceType = "IncomeExpenseCancel",
            ReferenceId = record.Id,
            CreatedAtUtc = now,
        });

        record.Status = IncomeExpenseStatus.Cancelled;
        record.CancelledAtUtc = now;
        record.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        var accountName = await db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Id == record.PaymentAccountId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return new IncomeExpenseRecordDto(
            record.Id, record.Type.ToString(), record.CategoryId, row.CategoryName, record.Amount,
            record.Date, record.PaymentAccountId, accountName ?? "—", record.Description,
            record.DocumentNumber, record.Status.ToString(), record.CancelledAtUtc, record.CreatedAtUtc);
    }
}

/// <summary>
/// Dönem özeti: toplamlar,
/// aylık döküm (boş aylar sıfırla listelenir) ve kategori bazlı toplamlar.
/// İptal edilmiş kayıtlar dahil edilmez.
/// </summary>
public sealed class GetIncomeExpenseSummaryHandler(
    IApplicationDbContext db, ICurrentTenant currentTenant, TimeProvider timeProvider)
{
    public async Task<IncomeExpenseSummaryResponse> HandleAsync(
        DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var tenantId = IncomeExpenseQueries.RequireTenantId(currentTenant);
        var today = Dates.ToUtcDate(timeProvider.GetUtcNow().UtcDateTime);

        // Varsayılan dönem: içinde bulunulan ay ile birlikte son 6 ay.
        var toDate = to is { } t ? Dates.ToUtcDate(t) : today;
        var fromDate = from is { } f ? Dates.ToUtcDate(f) : toDate.AddMonths(-5);

        if (fromDate > toDate)
        {
            throw new AppException("Dönem başlangıcı bitişten sonra olamaz.");
        }

        var records = db.IncomeExpenseRecords.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.Status == IncomeExpenseStatus.Active
                && r.Date >= fromDate
                && r.Date < toDate.AddDays(1));

        var monthly = await records
            .GroupBy(r => new { r.Date.Year, r.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Income = g.Where(r => r.Type == IncomeExpenseType.Income).Sum(r => r.Amount),
                Expense = g.Where(r => r.Type == IncomeExpenseType.Expense).Sum(r => r.Amount),
            })
            .ToListAsync(cancellationToken);

        var byCategory = await (
            from r in records
            join c in db.IncomeExpenseCategories.AsNoTracking() on r.CategoryId equals c.Id
            group r by new { r.Type, r.CategoryId, c.Name } into g
            select new IncomeExpenseCategoryTotalDto(
                g.Key.Type.ToString(), g.Key.CategoryId, g.Key.Name, g.Sum(r => r.Amount)))
            .ToListAsync(cancellationToken);

        // Boş aylar da listelensin: dönem içindeki her ay sıfırla durur.
        var months = new List<IncomeExpenseMonthlyDto>();
        var cursor = new DateTime(fromDate.Year, fromDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = new DateTime(toDate.Year, toDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (cursor <= lastMonth)
        {
            var entry = monthly.FirstOrDefault(m => m.Year == cursor.Year && m.Month == cursor.Month);
            var income = entry?.Income ?? 0m;
            var expense = entry?.Expense ?? 0m;
            months.Add(new IncomeExpenseMonthlyDto(cursor.Year, cursor.Month, income, expense, income - expense));
            cursor = cursor.AddMonths(1);
        }

        var totalIncome = months.Sum(m => m.Income);
        var totalExpense = months.Sum(m => m.Expense);

        return new IncomeExpenseSummaryResponse(
            fromDate, toDate, totalIncome, totalExpense, totalIncome - totalExpense,
            months,
            byCategory.OrderByDescending(c => c.Total).ToList());
    }
}
