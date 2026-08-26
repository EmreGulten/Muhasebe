using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Accounts;
using Accounting.Application.Features.IncomeExpenses;
using Accounting.Application.Validators;
using Accounting.Contracts.Accounts;
using Accounting.Contracts.IncomeExpenses;
using Accounting.Domain.Authorization;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.MultiTenancy;
using Accounting.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Accounting.UnitTests;

/// <summary>
/// gelir/gider özelliği: varsayılan kategori tohumlaması, kategori
/// CRUD'u, kaydın kasa hareketiyle atomik yazımı (gelir +/gider −), tip
/// uyumsuzluğu denetimi, iptalin ters hareketi, liste filtreleri, dönem
/// özeti, tenant izolasyonu ve izin matrisi.
/// </summary>
public sealed class IncomeExpenseFeatureTests : IDisposable
{
    private readonly TestApp _app = new();

    // ---- Test altyapısı

    private async Task<IServiceScope> CreateOwnerScopeAsync(string email)
    {
        var user = await _app.RegisterUserAsync(email: email);
        var scope = _app.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.UserTenants
            .Where(m => m.UserId == user.Id)
            .Select(m => m.TenantId)
            .FirstAsync();
        scope.ServiceProvider.GetRequiredService<ITenantContextWriter>()
            .SetTenant(tenantId, TenantRole.Owner);
        return scope;
    }

    public void Dispose() => _app.Dispose();

    private static Task<IReadOnlyList<IncomeExpenseCategoryDto>> ListCategoriesAsync(
        IServiceScope scope, string? type = null) =>
        scope.ServiceProvider.GetRequiredService<ListIncomeExpenseCategoriesHandler>()
            .HandleAsync(type, default);

    private static async Task<IncomeExpenseRecordDto> NewRecordAsync(
        IServiceScope scope, string type, string categoryName, decimal amount,
        DateTime date, Guid? accountId = null, string? description = null)
    {
        var categories = await ListCategoriesAsync(scope, type);
        var categoryId = categories.First(c => c.Name == categoryName).Id;
        return await scope.ServiceProvider.GetRequiredService<CreateIncomeExpenseRecordHandler>()
            .HandleAsync(new CreateIncomeExpenseRecordRequest(
                type, categoryId, amount, date, accountId, description, null), default);
    }

    private static async Task<Guid> CategoryIdAsync(IServiceScope scope, string type, string name)
    {
        var categories = await ListCategoriesAsync(scope, type);
        return categories.First(c => c.Name == name).Id;
    }

    // ---- Kategori tohumlaması ve CRUD

    [Fact]
    public async Task CategoryList_SeedsPlanDefaults_AndIsIdempotent()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-tohum@test.local");

        var first = await ListCategoriesAsync(scope);

        // Varsayılan 13 gider + 4 gelir; "Diğer" iki tarafta da bulunur.
        Assert.Equal(17, first.Count);
        Assert.Equal(13, first.Count(c => c.Type == "Expense"));
        Assert.Equal(4, first.Count(c => c.Type == "Income"));
        Assert.Equal(2, first.Count(c => c.Name == "Diğer"));
        Assert.All(first, c => Assert.True(c.IsActive));

        var second = await ListCategoriesAsync(scope);
        Assert.Equal(first.Count, second.Count); // tohumlama bir kez
    }

    [Fact]
    public async Task CategoryCrud_CreateRenameDeactivateDelete()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-kategori@test.local");
        var create = scope.ServiceProvider.GetRequiredService<CreateIncomeExpenseCategoryHandler>();
        var update = scope.ServiceProvider.GetRequiredService<UpdateIncomeExpenseCategoryHandler>();
        var delete = scope.ServiceProvider.GetRequiredService<DeleteIncomeExpenseCategoryHandler>();

        var category = await create.HandleAsync(
            new CreateIncomeExpenseCategoryRequest("Temizlik", "Expense"), default);
        Assert.Equal("Expense", category.Type);
        Assert.Equal(0, category.RecordCount);

        // Aynı ad aynı tarafta çakışır; diğer tarafta serbest.
        await Assert.ThrowsAsync<ConflictException>(() => create.HandleAsync(
            new CreateIncomeExpenseCategoryRequest("Temizlik", "Expense"), default));
        var incomeSide = await create.HandleAsync(
            new CreateIncomeExpenseCategoryRequest("Temizlik", "Income"), default);
        Assert.Equal("Income", incomeSide.Type);

        var renamed = await update.HandleAsync(
            category.Id, new UpdateIncomeExpenseCategoryRequest("Temizlik ve Hijyen", false), default);
        Assert.Equal("Temizlik ve Hijyen", renamed.Name);
        Assert.False(renamed.IsActive);

        // Kayıtlı kategori silinemez; kayıtsız silinir. Elle eklenen "Temizlik"
        // kategorisinden önce liste hiç çağrılmadı — varsayılanlar ("Kira" dahil)
        // ilk listede eksiksiz tamamlanır.
        await NewRecordAsync(scope, "Expense", "Kira", 100m, DateTime.UtcNow.Date);
        var expenseCategories = await ListCategoriesAsync(scope, "Expense");
        Assert.Equal(14, expenseCategories.Count); // 13 varsayılan + Temizlik ve Hijyen
        var kira = expenseCategories.First(c => c.Name == "Kira");
        await Assert.ThrowsAsync<ConflictException>(() => delete.HandleAsync(kira.Id, default));
        await delete.HandleAsync(incomeSide.Id, default);
        var names = (await ListCategoriesAsync(scope, "Income")).Select(c => c.Name).ToList();
        Assert.DoesNotContain("Temizlik", names);

        // Pasif kategori ve silinmiş kategori adla tekrar oluşturulabilir mi?
        // Pasif adı benzersizlik denetimine takılır (soft-deleted değil).
        await Assert.ThrowsAsync<ConflictException>(() => create.HandleAsync(
            new CreateIncomeExpenseCategoryRequest("Temizlik ve Hijyen", "Expense"), default));
    }

    [Fact]
    public async Task RecordIncome_WritesPositiveTransaction_OnLazyDefaultCash()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-gelir@test.local");

        var record = await NewRecordAsync(scope, "Income", "Hizmet", 250m, DateTime.UtcNow.Date,
            description: "Danışmanlık");

        Assert.Equal("Income", record.Type);
        Assert.Equal("Hizmet", record.CategoryName);
        Assert.Equal(250m, record.Amount);
        Assert.Equal("Active", record.Status);
        Assert.Equal("Kasa", record.PaymentAccountName); // hesap verilmedi → lazy varsayılan

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;

        var kasa = await db.Accounts.AsNoTracking().SingleAsync(a => a.TenantId == tenantId && a.IsDefault);
        Assert.Equal("Kasa", kasa.Name);
        var balance = await db.AccountTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.AccountId == kasa.Id)
            .SumAsync(t => t.Amount);
        Assert.Equal(250m, balance);

        var tx = await db.AccountTransactions.AsNoTracking()
            .SingleAsync(t => t.TenantId == tenantId && t.ReferenceType == "IncomeExpense");
        Assert.Equal(AccountTransactionType.Income, tx.Type);
        Assert.Equal(250m, tx.Amount);
        Assert.Equal(record.Id, tx.ReferenceId);
        Assert.Equal("Danışmanlık", tx.Description);
    }

    [Fact]
    public async Task RecordExpense_OnExplicitAccount_WritesNegativeTransaction()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-gider@test.local");
        var bank = await scope.ServiceProvider.GetRequiredService<CreateAccountHandler>()
            .HandleAsync(new CreateAccountRequest("Garanti", "Bank", null, 1000m), default);

        var record = await NewRecordAsync(scope, "Expense", "Kira", 300m,
            DateTime.UtcNow.Date, accountId: bank.Id, description: "Dükkan kirası");

        Assert.Equal("Expense", record.Type);
        Assert.Equal("Garanti", record.PaymentAccountName);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;
        var tx = await db.AccountTransactions.AsNoTracking()
            .SingleAsync(t => t.TenantId == tenantId && t.ReferenceType == "IncomeExpense");
        Assert.Equal(AccountTransactionType.Expense, tx.Type);
        Assert.Equal(-300m, tx.Amount);

        var current = await scope.ServiceProvider.GetRequiredService<GetAccountHandler>()
            .HandleAsync(bank.Id, default);
        Assert.Equal(700m, current.CurrentBalance); // 1000 açılış − 300 gider
    }

    [Fact]
    public async Task Record_RejectsTypeMismatch_InactiveCategory_AndMissingRefs()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-reddi@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<CreateIncomeExpenseRecordHandler>();
        var kira = await CategoryIdAsync(scope, "Expense", "Kira");

        // Gelir kategorisi gider kaydıyla uyuşmaz.
        var ex = await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            new CreateIncomeExpenseRecordRequest("Income", kira, 10m, DateTime.UtcNow.Date, null, null, null), default));
        Assert.Contains("uyuşmuyor", ex.Message);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new CreateIncomeExpenseRecordRequest("Expense", Guid.NewGuid(), 10m, DateTime.UtcNow.Date, null, null, null), default));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new CreateIncomeExpenseRecordRequest("Expense", kira, 10m, DateTime.UtcNow.Date, Guid.NewGuid(), null, null), default));

        // Pasif kategori ve pasif hesap reddedilir.
        var update = scope.ServiceProvider.GetRequiredService<UpdateIncomeExpenseCategoryHandler>();
        await update.HandleAsync(kira, new UpdateIncomeExpenseCategoryRequest("Kira", false), default);
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            new CreateIncomeExpenseRecordRequest("Expense", kira, 10m, DateTime.UtcNow.Date, null, null, null), default));
    }

    // ---- İptal: ters hareket + terminal durum

    [Fact]
    public async Task RecordCancel_ReversesAccountTransaction_AndIsTerminal()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-iptal@test.local");
        var bank = await scope.ServiceProvider.GetRequiredService<CreateAccountHandler>()
            .HandleAsync(new CreateAccountRequest("Kasa Banka", "Bank", null, 1000m), default);
        var record = await NewRecordAsync(scope, "Expense", "Elektrik", 300m,
            DateTime.UtcNow.Date, accountId: bank.Id);

        var cancelled = await scope.ServiceProvider.GetRequiredService<CancelIncomeExpenseRecordHandler>()
            .HandleAsync(record.Id, default);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.NotNull(cancelled.CancelledAtUtc);

        var current = await scope.ServiceProvider.GetRequiredService<GetAccountHandler>()
            .HandleAsync(bank.Id, default);
        Assert.Equal(1000m, current.CurrentBalance); // ters hareket bakiyeyi döndürür

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;
        var reversal = await db.AccountTransactions.AsNoTracking()
            .SingleAsync(t => t.TenantId == tenantId && t.ReferenceType == "IncomeExpenseCancel");
        Assert.Equal(300m, reversal.Amount); // −300 giderin tersi +300
        Assert.Equal(record.Id, reversal.ReferenceId);

        await Assert.ThrowsAsync<ConflictException>(
            () => scope.ServiceProvider.GetRequiredService<CancelIncomeExpenseRecordHandler>()
                .HandleAsync(record.Id, default));
    }

    // ---- Liste filtreleri ve özet

    [Fact]
    public async Task RecordList_FiltersByTypeCategoryAndRange()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-liste@test.local");
        var today = DateTime.UtcNow.Date;
        await NewRecordAsync(scope, "Income", "Hizmet", 500m, today);
        await NewRecordAsync(scope, "Expense", "Kira", 400m, today);
        await NewRecordAsync(scope, "Income", "Faiz", 50m, today.AddMonths(-2));

        var handler = scope.ServiceProvider.GetRequiredService<ListIncomeExpenseRecordsHandler>();

        var all = await handler.HandleAsync(null, null, null, null, 1, 20, default);
        Assert.Equal(3, all.TotalCount);

        var income = await handler.HandleAsync("Income", null, null, null, 1, 20, default);
        Assert.Equal(2, income.TotalCount);
        Assert.All(income.Items, r => Assert.Equal("Income", r.Type));

        var kira = await CategoryIdAsync(scope, "Expense", "Kira");
        var byCategory = await handler.HandleAsync(null, kira, null, null, 1, 20, default);
        Assert.Single(byCategory.Items);

        var lastQuarter = await handler.HandleAsync(null, null, today.AddMonths(-3), today.AddMonths(-1), 1, 20, default);
        Assert.Single(lastQuarter.Items);
        Assert.Equal(50m, lastQuarter.Items[0].Amount);

        var paged = await handler.HandleAsync(null, null, null, null, 2, 2, default);
        Assert.Equal(3, paged.TotalCount);
        Assert.Single(paged.Items);
    }

    [Fact]
    public async Task Summary_MonthlyAndCategoryTotals_WithZeroMonths()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-ozet@test.local");
        var today = DateTime.UtcNow.Date;
        await NewRecordAsync(scope, "Income", "Hizmet", 600m, today);
        await NewRecordAsync(scope, "Expense", "Kira", 400m, today);
        await NewRecordAsync(scope, "Expense", "Kira", 200m, today.AddMonths(-2));

        var summary = await scope.ServiceProvider.GetRequiredService<GetIncomeExpenseSummaryHandler>()
            .HandleAsync(today.AddMonths(-3), today, default);

        Assert.Equal(600m, summary.TotalIncome);
        Assert.Equal(600m, summary.TotalExpense);
        Assert.Equal(0m, summary.Net);

        // 4 ay listelenir; aradaki boş ay sıfırla durur (M-3 boş, M-2 dolu, M-1 boş, M dolu).
        Assert.Equal(4, summary.Months.Count);
        var empty = summary.Months[^2];
        Assert.Equal(0m, empty.Income);
        Assert.Equal(0m, empty.Expense);

        var last = summary.Months[^1];
        Assert.Equal(600m, last.Income);
        Assert.Equal(400m, last.Expense);
        Assert.Equal(200m, last.Net);

        // Kategori dökümü büyükten küçüğe: Kira 600, Hizmet 600 → başa Kira gelir.
        Assert.Equal(2, summary.Categories.Count);
        Assert.Equal(600m, summary.Categories[0].Total);
        Assert.Equal(600m, summary.Categories[1].Total);

        await Assert.ThrowsAsync<AppException>(
            () => scope.ServiceProvider.GetRequiredService<GetIncomeExpenseSummaryHandler>()
                .HandleAsync(today, today.AddMonths(-1), default));
    }

    // ---- İptal edilmiş kayıt özete girmez

    [Fact]
    public async Task Summary_ExcludesCancelledRecords()
    {
        using var scope = await CreateOwnerScopeAsync("gelir-ozet2@test.local");
        var today = DateTime.UtcNow.Date;
        var kept = await NewRecordAsync(scope, "Income", "Hizmet", 600m, today);
        var cancelled = await NewRecordAsync(scope, "Income", "Faiz", 400m, today);
        await scope.ServiceProvider.GetRequiredService<CancelIncomeExpenseRecordHandler>()
            .HandleAsync(cancelled.Id, default);

        var summary = await scope.ServiceProvider.GetRequiredService<GetIncomeExpenseSummaryHandler>()
            .HandleAsync(null, null, default); // varsayılan son 6 ay

        Assert.Equal(600m, summary.TotalIncome);
        Assert.Single(summary.Categories);
        Assert.Equal(kept.CategoryName, summary.Categories[0].CategoryName);
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task RecordsAndCategories_AreTenantIsolated()
    {
        using var scopeA = await CreateOwnerScopeAsync("gelir-a@test.local");
        using var scopeB = await CreateOwnerScopeAsync("gelir-b@test.local");

        var record = await NewRecordAsync(scopeA, "Expense", "Kira", 100m, DateTime.UtcNow.Date);

        var listB = await scopeB.ServiceProvider.GetRequiredService<ListIncomeExpenseRecordsHandler>()
            .HandleAsync(null, null, null, null, 1, 20, default);
        Assert.Equal(0, listB.TotalCount);

        await Assert.ThrowsAsync<NotFoundException>(
            () => scopeB.ServiceProvider.GetRequiredService<GetIncomeExpenseRecordHandler>()
                .HandleAsync(record.Id, default));

        // B'nin kategorileri kendi tohumlamasıdır; A'nın özel kategorisi görünmez.
        await scopeA.ServiceProvider.GetRequiredService<CreateIncomeExpenseCategoryHandler>()
            .HandleAsync(new CreateIncomeExpenseCategoryRequest("A'ya Özel", "Expense"), default);
        var categoriesB = await ListCategoriesAsync(scopeB);
        Assert.DoesNotContain(categoriesB, c => c.Name == "A'ya Özel");
    }

    // ---- İzin matrisi

    [Fact]
    public void RolePermissions_ExpenseMatrix()
    {
        var employee = RolePermissions.For(TenantRole.Employee);
        Assert.Contains(Permissions.ExpensesView, employee);
        Assert.DoesNotContain(Permissions.ExpensesCreate, employee);
        Assert.DoesNotContain(Permissions.ExpensesEdit, employee);

        var accountant = RolePermissions.For(TenantRole.Accountant);
        Assert.Contains(Permissions.ExpensesCreate, accountant);
        Assert.Contains(Permissions.ExpensesEdit, accountant);

        var viewer = RolePermissions.For(TenantRole.Viewer);
        Assert.Contains(Permissions.ExpensesView, viewer);
        Assert.DoesNotContain(Permissions.ExpensesCreate, viewer);
    }

    // ---- Doğrulayıcılar

    [Fact]
    public async Task Validators_RejectInvalidInput()
    {
        var category = new CreateIncomeExpenseCategoryValidator();
        Assert.False((await category.ValidateAsync(
            new CreateIncomeExpenseCategoryRequest("", "Expense"), default)).IsValid);
        Assert.False((await category.ValidateAsync(
            new CreateIncomeExpenseCategoryRequest("X", "Both"), default)).IsValid);

        var record = new CreateIncomeExpenseRecordValidator();
        Assert.False((await record.ValidateAsync(
            new CreateIncomeExpenseRecordRequest("Cost", Guid.NewGuid(), 10m, DateTime.UtcNow.Date, null, null, null), default)).IsValid);
        Assert.False((await record.ValidateAsync(
            new CreateIncomeExpenseRecordRequest("Income", Guid.Empty, 10m, DateTime.UtcNow.Date, null, null, null), default)).IsValid);
        Assert.False((await record.ValidateAsync(
            new CreateIncomeExpenseRecordRequest("Income", Guid.NewGuid(), 0m, DateTime.UtcNow.Date, null, null, null), default)).IsValid);
        Assert.False((await record.ValidateAsync(
            new CreateIncomeExpenseRecordRequest("Income", Guid.NewGuid(), 10.123m, DateTime.UtcNow.Date, null, null, null), default)).IsValid);
        Assert.True((await record.ValidateAsync(
            new CreateIncomeExpenseRecordRequest("Income", Guid.NewGuid(), 10.12m, DateTime.UtcNow.Date, null, "Açıklama", "FTR-1"), default)).IsValid);
    }
}
