using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Accounts;
using Accounting.Application.Features.Products;
using Accounting.Application.Features.Purchases;
using Accounting.Application.Validators;
using Accounting.Contracts.Accounts;
using Accounting.Contracts.Products;
using Accounting.Contracts.Purchases;
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
/// kasa/banka özelliği: hesap türleri, açılış bakiyesi, manuel
/// giriş/çıkış, hesaplar arası transfer, ekstre çalışan bakiyesi,
/// satış/alış ödemelerinin kasaya akışı, tenant izolasyonu ve izin
/// matrisi.
/// </summary>
public sealed class AccountFeatureTests : IDisposable
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

    private static Task<AccountDto> NewAccountAsync(
        IServiceScope scope, string name, string type = "Bank",
        decimal openingBalance = 0m, string? currency = null) =>
        scope.ServiceProvider.GetRequiredService<CreateAccountHandler>()
            .HandleAsync(new CreateAccountRequest(name, type, currency, openingBalance), default);

    // ---- Hesap oluşumu ve açılış bakiyesi

    [Fact]
    public async Task CreateAccount_WithOpeningBalance_WritesOpeningTransaction()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-acilis@test.local");

        var account = await NewAccountAsync(scope, "Banka Hesabı", openingBalance: 5000m);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Bank", account.Type);
        Assert.Equal("TRY", account.Currency);
        Assert.Equal(5000m, account.OpeningBalance);
        Assert.Equal(5000m, account.CurrentBalance);
        Assert.Equal(1, account.TransactionCount);
        Assert.False(account.IsDefault);
        Assert.True(account.IsActive);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;
        var transaction = await db.AccountTransactions.AsNoTracking()
            .SingleAsync(t => t.TenantId == tenantId && t.AccountId == account.Id);
        Assert.Equal(AccountTransactionType.OpeningBalance, transaction.Type);
        Assert.Equal(5000m, transaction.Amount);
    }

    [Fact]
    public async Task CreateAccount_ZeroOpeningBalance_WritesNoTransaction()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-sifir@test.local");

        var account = await NewAccountAsync(scope, "Sanal POS", type: "VirtualPOS");

        Assert.Equal(0m, account.CurrentBalance);
        Assert.Equal(0, account.TransactionCount);
    }

    [Fact]
    public async Task CreateAccount_DefaultsCurrency_AndRejectsInvalidType()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-tur@test.local");

        var account = await NewAccountAsync(scope, "Kredi Kartı", type: "CreditCard", currency: null);
        Assert.Equal("TRY", account.Currency);
        Assert.Equal("CreditCard", account.Type);

        // Geçersiz tür uygulama istisnası: parser mesajı geçerli değerleri sayar.
        var ex = await Assert.ThrowsAsync<AppException>(() => NewAccountAsync(scope, "Bozuk", type: "Cash2"));
        Assert.Contains("Cash, Bank, CreditCard, VirtualPOS", ex.Message);
    }

    // ---- Manuel hareketler

    [Fact]
    public async Task ManualTransaction_InAndOut_UpdatesSignedBalance()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-manuel@test.local");
        var account = await NewAccountAsync(scope, "Kasa 2", type: "Cash", openingBalance: 100m);
        var handler = scope.ServiceProvider.GetRequiredService<CreateAccountTransactionHandler>();

        var @in = await handler.HandleAsync(
            account.Id, new CreateAccountTransactionRequest("In", DateTime.UtcNow.Date, 200m, "Tahsilat"), default);
        var @out = await handler.HandleAsync(
            account.Id, new CreateAccountTransactionRequest("Out", DateTime.UtcNow.Date, 50m, "Ödeme"), default);

        Assert.Equal(300m, @in.Balance);   // 100 + 200
        Assert.Equal(250m, @out.Balance);  // 300 − 50

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;
        var manual = await db.AccountTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.AccountId == account.Id && t.ReferenceType == "Manual")
            .OrderBy(t => t.Id)
            .ToListAsync();
        Assert.Equal(AccountTransactionType.ManualCollection, manual[0].Type);
        Assert.Equal(200m, manual[0].Amount);
        Assert.Equal(AccountTransactionType.ManualPayment, manual[1].Type);
        Assert.Equal(-50m, manual[1].Amount);
    }

    [Fact]
    public async Task ManualTransaction_RejectsInactiveAccount_AndBadDirection()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-pasif@test.local");
        var account = await NewAccountAsync(scope, "Eski Banka");
        await scope.ServiceProvider.GetRequiredService<UpdateAccountHandler>()
            .HandleAsync(account.Id, new UpdateAccountRequest("Eski Banka", false), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreateAccountTransactionHandler>();
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            account.Id, new CreateAccountTransactionRequest("In", DateTime.UtcNow.Date, 10m, null), default));
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            account.Id, new CreateAccountTransactionRequest("Up", DateTime.UtcNow.Date, 10m, null), default));
    }

    // ---- Transfer

    [Fact]
    public async Task Transfer_MovesBalance_WithPairedRows()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-transfer@test.local");
        var cash = await NewAccountAsync(scope, "Ana Kasa", type: "Cash", openingBalance: 1000m);
        var bank = await NewAccountAsync(scope, "Ziraat", type: "Bank");

        var result = await scope.ServiceProvider.GetRequiredService<CreateTransferHandler>()
            .HandleAsync(new TransferRequest(cash.Id, bank.Id, DateTime.UtcNow.Date, 400m, "Bankaya yatırım"), default);

        Assert.Equal(600m, result.FromBalance);
        Assert.Equal(400m, result.ToBalance);

        // Çift aynı ReferenceId ile bağlı; işaretler zıt.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().TenantId!.Value;
        var rows = await db.AccountTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Type == AccountTransactionType.Transfer)
            .OrderBy(t => t.Amount)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(-400m, rows[0].Amount);
        Assert.Equal(400m, rows[1].Amount);
        Assert.Equal(rows[0].ReferenceId, rows[1].ReferenceId);
        Assert.NotNull(rows[0].ReferenceId);
    }

    [Fact]
    public async Task Transfer_RejectsSameAccount_AndMissingAccount()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-transfer-ret@test.local");
        var cash = await NewAccountAsync(scope, "Kasa X", type: "Cash");
        var handler = scope.ServiceProvider.GetRequiredService<CreateTransferHandler>();

        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            new TransferRequest(cash.Id, cash.Id, DateTime.UtcNow.Date, 10m, null), default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(
            new TransferRequest(cash.Id, Guid.NewGuid(), DateTime.UtcNow.Date, 10m, null), default));
    }

    // ---- Güncelleme ve silme kuralları

    [Fact]
    public async Task UpdateAccount_Renames_AndRejectsDefaultDeactivation()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-guncelle@test.local");
        var account = await NewAccountAsync(scope, "Banka");
        var updated = await scope.ServiceProvider.GetRequiredService<UpdateAccountHandler>()
            .HandleAsync(account.Id, new UpdateAccountRequest("Garanti", true), default);
        Assert.Equal("Garanti", updated.Name);

        // Default kasa hesabı satış/alış ödemelerinin hedefidir; pasifleştirilemez.
        var cashAccount = await SeedDefaultCashViaSaleAsync(scope);
        await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<UpdateAccountHandler>()
                .HandleAsync(cashAccount.Id, new UpdateAccountRequest("Kasa", false), default));
    }

    [Fact]
    public async Task DeleteAccount_EnforcesLedgerRules()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-sil@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<DeleteAccountHandler>();

        // Hareketli hesap silinemez — kayıt zinciri korunur.
        var withMovement = await NewAccountAsync(scope, "Dolu Hesap", openingBalance: 100m);
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(withMovement.Id, default));

        // Default kasa silinemez.
        var cash = await SeedDefaultCashViaSaleAsync(scope);
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(cash.Id, default));

        // Hareketsiz hesap soft delete ile listeden kalkar.
        var empty = await NewAccountAsync(scope, "Boş Hesap");
        await handler.HandleAsync(empty.Id, default);
        var list = await scope.ServiceProvider.GetRequiredService<ListAccountsHandler>()
            .HandleAsync(default);
        Assert.DoesNotContain(list, a => a.Id == empty.Id);
    }

    // ---- Ekstre

    [Fact]
    public async Task GetAccountStatement_RunsBalanceAcrossPages()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-ekstre@test.local");
        var account = await NewAccountAsync(scope, "Ekstre Hesabı");
        var handler = scope.ServiceProvider.GetRequiredService<CreateAccountTransactionHandler>();

        // Üç hareket üç farklı günde: gün farkı sırayı belirleyici kılar (aynı
        // güne düşen açılış + manuel satır çifti aynı milisaniye v7 Id'leriyle
        // SQLite'ta rastgele sıralanabiliyor).
        var today = DateTime.UtcNow.Date;
        await handler.HandleAsync(
            account.Id, new CreateAccountTransactionRequest("In", today, 500m, null), default);
        await handler.HandleAsync(
            account.Id, new CreateAccountTransactionRequest("In", today.AddDays(1), 200m, null), default);
        await handler.HandleAsync(
            account.Id, new CreateAccountTransactionRequest("Out", today.AddDays(2), 150m, null), default);

        // Sayfa boyutu 2: açılış + giriş → çalışan bakiyeler 500 ve 700.
        var firstPage = await scope.ServiceProvider.GetRequiredService<GetAccountStatementHandler>()
            .HandleAsync(account.Id, 1, 2, default);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(0m, firstPage.BalanceBeforePage);
        Assert.True(firstPage.Items[0].Date <= firstPage.Items[1].Date);
        Assert.Equal(account.Name, firstPage.Items[0].AccountName);
        Assert.Equal("TRY", firstPage.Currency);
        Assert.Equal(
            new List<decimal> { 500m, 700m },
            firstPage.Items.Select(t => t.Balance).OrderBy(b => b).ToList());

        // Sayfa 2: çıkış → 550.
        var secondPage = await scope.ServiceProvider.GetRequiredService<GetAccountStatementHandler>()
            .HandleAsync(account.Id, 2, 2, default);
        Assert.Equal(700m, secondPage.BalanceBeforePage);
        Assert.Equal(550m, secondPage.Items[0].Balance);
        Assert.Equal(AccountTransactionType.ManualPayment.ToString(), secondPage.Items[0].Type);
    }

    // ---- Satış/alış ödemelerinin kasaya akışı

    [Fact]
    public async Task SaleAndPurchasePayments_FeedDefaultCashAccount()
    {
        using var scope = await CreateOwnerScopeAsync("kasa-akis@test.local");

        // Alış onayı + anlık ödeme → default "Kasa" (Cash, TRY) oluşur ve −tutar yazar.
        var goods = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(new CreateProductRequest(
                "Aygücü", null, null, null, null, null, 60m, 100m, 20m, 0m, false), default);
        var warehouseList = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>()
            .HandleAsync(default);
        var warehouse = warehouseList.First(w => w.IsDefault);
        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(new CreatePurchaseRequest(
                null, warehouse.Id, DateTime.UtcNow.Date, null, null,
                [new PurchaseItemRequest(goods.Id, 1m, 60m, 0m, 20m)]), default);
        await scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
            .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(
                new PurchaseConfirmPaymentRequest(DateTime.UtcNow.Date, 60m, "Peşin")), default);

        var accounts = await scope.ServiceProvider.GetRequiredService<ListAccountsHandler>()
            .HandleAsync(default);

        var cash = Assert.Single(accounts, a => a.IsDefault);
        Assert.Equal("Kasa", cash.Name);
        Assert.Equal("Cash", cash.Type);
        Assert.Equal("TRY", cash.Currency);
        Assert.Equal(-60m, cash.CurrentBalance); // Alış ödemesi kasadan çıkar.
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task Accounts_Are_Isolated_Between_Tenants()
    {
        using var scopeA = await CreateOwnerScopeAsync("kasa-a@test.local");
        var accountInA = await NewAccountAsync(scopeA, "A Bankası", openingBalance: 100m);

        using var scopeB = await CreateOwnerScopeAsync("kasa-b@test.local");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scopeB.ServiceProvider.GetRequiredService<GetAccountHandler>()
                .HandleAsync(accountInA.Id, default));

        var listInB = await scopeB.ServiceProvider.GetRequiredService<ListAccountsHandler>()
            .HandleAsync(default);
        Assert.DoesNotContain(listInB, a => a.Id == accountInA.Id);
    }

    // ---- İzin matrisi ve doğrulama

    [Fact]
    public void Accounts_PermissionMatrix()
    {
        // Kasa yönetimi Owner/Admin ve muhasebecidedir; çalışan yalnızca görüntüler.
        var employee = RolePermissions.For(TenantRole.Employee);
        Assert.Contains(Permissions.AccountsView, employee);
        Assert.DoesNotContain(Permissions.AccountsCreate, employee);
        Assert.DoesNotContain(Permissions.AccountsEdit, employee);

        var accountant = RolePermissions.For(TenantRole.Accountant);
        Assert.Contains(Permissions.AccountsCreate, accountant);
        Assert.Contains(Permissions.AccountsEdit, accountant);

        var viewer = RolePermissions.For(TenantRole.Viewer);
        Assert.Contains(Permissions.AccountsView, viewer);
        Assert.DoesNotContain(Permissions.AccountsCreate, viewer);
    }

    [Fact]
    public async Task AccountValidator_RejectsInvalidInput()
    {
        var create = new CreateAccountValidator();
        Assert.False((await create.ValidateAsync(
            new CreateAccountRequest("", "Cash", null, 0m), default)).IsValid);
        Assert.False((await create.ValidateAsync(
            new CreateAccountRequest("Kasa", "Vault", null, 0m), default)).IsValid);
        Assert.False((await create.ValidateAsync(
            new CreateAccountRequest("Kasa", "Cash", "TL", 0m), default)).IsValid);
        Assert.False((await create.ValidateAsync(
            new CreateAccountRequest("Kasa", "Cash", null, -1m), default)).IsValid);
        Assert.False((await create.ValidateAsync(
            new CreateAccountRequest("Kasa", "Cash", null, 10.123m), default)).IsValid);

        var transaction = new CreateAccountTransactionValidator();
        Assert.False((await transaction.ValidateAsync(
            new CreateAccountTransactionRequest("Up", DateTime.UtcNow.Date, 10m, null), default)).IsValid);
        Assert.False((await transaction.ValidateAsync(
            new CreateAccountTransactionRequest("In", DateTime.UtcNow.Date, 0m, null), default)).IsValid);

        var transfer = new TransferValidator();
        Assert.False((await transfer.ValidateAsync(
            new TransferRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date, 0m, null), default)).IsValid);
        Assert.False((await transfer.ValidateAsync(
            new TransferRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date, 10.123m, null), default)).IsValid);
    }

    /// <summary>
    /// Default "Kasa" hesabını gerçek üretim yoluyla (alış onayı + anlık ödeme)
    /// oluşturur — lazy kaydın Type=Cash açtığını doğrulamak için.
    /// </summary>
    private static async Task<AccountDto> SeedDefaultCashViaSaleAsync(IServiceScope scope)
    {
        var goods = await scope.ServiceProvider.GetRequiredService<CreateProductHandler>()
            .HandleAsync(new CreateProductRequest(
                "Aygücü", null, null, null, null, null, 10m, 100m, 20m, 0m, false), default);
        var warehouses = await scope.ServiceProvider.GetRequiredService<ListWarehousesHandler>()
            .HandleAsync(default);
        var warehouse = warehouses.First(w => w.IsDefault);

        var purchase = await scope.ServiceProvider.GetRequiredService<CreatePurchaseHandler>()
            .HandleAsync(new CreatePurchaseRequest(
                null, warehouse.Id, DateTime.UtcNow.Date, null, null,
                [new PurchaseItemRequest(goods.Id, 1m, 10m, 0m, 20m)]), default);
        await scope.ServiceProvider.GetRequiredService<ConfirmPurchaseHandler>()
            .HandleAsync(purchase.Id, new ConfirmPurchaseRequest(
                new PurchaseConfirmPaymentRequest(DateTime.UtcNow.Date, 1m, "Kasa açılışı")), default);

        var accounts = await scope.ServiceProvider.GetRequiredService<ListAccountsHandler>()
            .HandleAsync(default);
        return accounts.First(a => a.IsDefault);
    }
}
