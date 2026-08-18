using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Application.Features.Parties;
using Accounting.Contracts.Parties;
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
/// PHASE 2 cari özelliği: kart CRUD, açılış bakiyesi hareketi, bakiye =
/// Σborç − Σalacak, ekstre çalışan bakiyesi, hareketli cari silinemez,
/// tenant izolasyonu ve rol izin matrisi (muhasebe.md bölüm 4 ve 23).
/// </summary>
public sealed class PartyFeatureTests : IDisposable
{
    private readonly TestApp _app = new();

    // ---- Test altyapısı

    /// <summary>Yeni kullanıcı + işletme kaydı yapar, Owner rolüyle tenant bağlamı kurar.</summary>
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

    private static CreatePartyRequest NewParty(
        string name, string type = "Customer", decimal openingBalance = 0, decimal creditLimit = 0) =>
        new(name, type, null, null, null, null, null, null, null, null, openingBalance, creditLimit, null);

    private static CreatePartyTransactionRequest NewTransaction(
        string type, decimal amount, DateTime date, string? description = null) =>
        new(type, date, amount, null, description);

    // ---- Cari kartı + açılış bakiyesi

    [Fact]
    public async Task CreateParty_WithPositiveOpeningBalance_CreatesOpeningTransaction()
    {
        using var scope = await CreateOwnerScopeAsync("pozitif@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();

        var party = await handler.HandleAsync(NewParty("Acma Mobilya", openingBalance: 1500.50m), default);

        Assert.Equal(1500.50m, party.Balance);
        Assert.Equal(1500.50m, party.TotalDebit);
        Assert.Equal(0m, party.TotalCredit);

        // Kalıcı durum: tek açılış hareketi, borç kolonunda.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transaction = await db.PartyTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(PartyTransactionType.OpeningBalance, transaction.Type);
        Assert.Equal(1500.50m, transaction.Debit);
        Assert.Equal(0m, transaction.Credit);
        Assert.Equal(party.Id, transaction.PartyId);
    }

    [Fact]
    public async Task CreateParty_WithNegativeOpeningBalance_CreditsParty()
    {
        using var scope = await CreateOwnerScopeAsync("negatif@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();

        var party = await handler.HandleAsync(NewParty("Biz Borçluyuz A.Ş.", openingBalance: -750.25m), default);

        Assert.Equal(-750.25m, party.Balance);
        Assert.Equal(0m, party.TotalDebit);
        Assert.Equal(750.25m, party.TotalCredit);
    }

    [Fact]
    public async Task CreateParty_ZeroOpeningBalance_CreatesNoTransaction()
    {
        using var scope = await CreateOwnerScopeAsync("sifir@test.local");
        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();

        var party = await handler.HandleAsync(NewParty("Temiz Cari"), default);

        Assert.Equal(0m, party.Balance);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.PartyTransactions.AnyAsync());
    }

    [Fact]
    public async Task UpdateParty_ChangesFields_ButNotOpeningBalance()
    {
        using var scope = await CreateOwnerScopeAsync("guncelle@test.local");
        var create = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await create.HandleAsync(NewParty("Eski Ad", "Supplier", openingBalance: 500m), default);

        var update = scope.ServiceProvider.GetRequiredService<UpdatePartyHandler>();
        var updated = await update.HandleAsync(party.Id, new UpdatePartyRequest(
            "Yeni Ad", "Customer", null, null, null, null, null, "İzmir", null, null, 1000m, null, IsActive: true), default);

        Assert.Equal("Yeni Ad", updated.Name);
        Assert.Equal("Customer", updated.Type);
        Assert.Equal("İzmir", updated.City);
        Assert.Equal(1000m, updated.CreditLimit);
        Assert.NotNull(updated.UpdatedAtUtc);

        // Açılış bakiyesi ve oluşan hareket güncellemeyle değişmez.
        Assert.Equal(500m, updated.OpeningBalance);
        Assert.Equal(500m, updated.Balance);
    }

    // ---- Cari hareket + bakiye hesaplama

    [Fact]
    public async Task CreatePartyTransaction_ComputesBalance_AsDebitMinusCredit()
    {
        using var scope = await CreateOwnerScopeAsync("bakiye@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("Denge A.Ş."), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        var day = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        // Her çağrı kümülatif bakiyeyi döner: +1000 → 600 → 850.50
        var t1 = await handler.HandleAsync(party.Id, NewTransaction("Debit", 1000m, day), default);
        var t2 = await handler.HandleAsync(party.Id, NewTransaction("Credit", -400m, day.AddDays(1)), default);
        var t3 = await handler.HandleAsync(party.Id, NewTransaction("Debit", 250.50m, day.AddDays(2)), default);

        Assert.Equal(1000m, t1.Balance);
        Assert.Equal(600m, t2.Balance);
        Assert.Equal(850.50m, t3.Balance);

        // Detay sorgusu da aynı toplamı verir.
        var get = scope.ServiceProvider.GetRequiredService<GetPartyHandler>();
        var detail = await get.HandleAsync(party.Id, default);
        Assert.Equal(850.50m, detail.Balance);
        Assert.Equal(1250.50m, detail.TotalDebit);
        Assert.Equal(400m, detail.TotalCredit);
    }

    [Theory]
    [InlineData("Sale", 500)]      // modül üretmeli — manuel reddi
    [InlineData("Collection", 500)]
    [InlineData("Purchase", 500)]
    [InlineData("Payment", 500)]
    [InlineData("TurYok", 500)]    // geçersiz tür
    public async Task CreatePartyTransaction_RejectsNonManualOrUnknownType(string type, decimal amount)
    {
        using var scope = await CreateOwnerScopeAsync($"tur-{type.ToLowerInvariant()}@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("Tür Test"), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            party.Id, NewTransaction(type, amount, DateTime.UtcNow), default));
    }

    [Theory]
    [InlineData("Debit", -100)]   // borç pozitif olmalı
    [InlineData("Credit", 100)]   // alacak negatif olmalı
    [InlineData("Debit", 0)]      // sıfır tutar
    public async Task CreatePartyTransaction_RejectsSignMismatchAndZero(string type, decimal amount)
    {
        using var scope = await CreateOwnerScopeAsync($"isaret-{type.ToLowerInvariant()}-{amount}@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("İşaret Test"), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            party.Id, NewTransaction(type, amount, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task CreatePartyTransaction_RejectsSecondOpeningBalance()
    {
        using var scope = await CreateOwnerScopeAsync("ikinci-acilis@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("Tek Açılış", openingBalance: 100m), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            party.Id, NewTransaction("OpeningBalance", 50m, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task CreatePartyTransaction_RejectsInactiveParty()
    {
        using var scope = await CreateOwnerScopeAsync("pasif@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("Pasif Cari"), default);

        var update = scope.ServiceProvider.GetRequiredService<UpdatePartyHandler>();
        await update.HandleAsync(party.Id, new UpdatePartyRequest(
            "Pasif Cari", "Customer", null, null, null, null, null, null, null, null, 0m, null, IsActive: false), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            party.Id, NewTransaction("Debit", 100m, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task CreatePartyTransaction_RejectsDueDateBeforeDate()
    {
        using var scope = await CreateOwnerScopeAsync("vade@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("Vade Test"), default);

        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        var date = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        await Assert.ThrowsAsync<AppException>(() => handler.HandleAsync(
            party.Id, new CreatePartyTransactionRequest("Debit", date, 100m, DueDate: date.AddDays(-1), null), default));
    }

    // ---- Ekstre

    [Fact]
    public async Task GetPartyStatement_ComputesRunningBalanceAndPaging()
    {
        using var scope = await CreateOwnerScopeAsync("ekstre@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var handler = scope.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        var statement = scope.ServiceProvider.GetRequiredService<GetPartyStatementHandler>();

        var party = await parties.HandleAsync(NewParty("Ekstre A.Ş.", openingBalance: 500m), default);
        // Açılış hareketi bugünün tarihini alır; manuel hareketler sonraki günlerde.
        var day = DateTime.UtcNow.Date;
        await handler.HandleAsync(party.Id, NewTransaction("Debit", 300m, day.AddDays(1)), default);
        await handler.HandleAsync(party.Id, NewTransaction("Credit", -200m, day.AddDays(2)), default);

        // Tümü tek sayfada: çalışan bakiye satır satır birikir.
        var full = await statement.HandleAsync(party.Id, page: 1, pageSize: 50, default);
        Assert.Equal(3, full.TotalCount);
        Assert.Equal(0m, full.BalanceBeforePage);
        Assert.Collection(full.Items,
            t => Assert.Equal(500m, t.Balance),   // açılış
            t => Assert.Equal(800m, t.Balance),   // +300 borç
            t => Assert.Equal(600m, t.Balance));  // −200 alacak
        Assert.Equal(600m, full.Items[^1].Balance);

        // Sayfa 2 (2'şerli): öncesindeki kümülatif bakiye 800, kalan satır 600.
        var page2 = await statement.HandleAsync(party.Id, page: 2, pageSize: 2, default);
        Assert.Equal(2, page2.Page);
        Assert.Equal(800m, page2.BalanceBeforePage);
        var last = Assert.Single(page2.Items);
        Assert.Equal(200m, last.Credit);
        Assert.Equal(600m, last.Balance);
    }

    // ---- Silme kuralları (bölüm 23: finansal kayıt zinciri korunur)

    [Fact]
    public async Task DeleteParty_WithTransactions_ThrowsConflict()
    {
        using var scope = await CreateOwnerScopeAsync("silme-red@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("Hareketli Cari", openingBalance: 100m), default);

        var handler = scope.ServiceProvider.GetRequiredService<DeletePartyHandler>();
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(party.Id, default));
    }

    [Fact]
    public async Task DeleteParty_WithoutTransactions_SoftDeletes()
    {
        using var scope = await CreateOwnerScopeAsync("silme@test.local");
        var parties = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        var party = await parties.HandleAsync(NewParty("Hareketsiz Cari"), default);

        var delete = scope.ServiceProvider.GetRequiredService<DeletePartyHandler>();
        await delete.HandleAsync(party.Id, default);

        var get = scope.ServiceProvider.GetRequiredService<GetPartyHandler>();
        await Assert.ThrowsAsync<NotFoundException>(() => get.HandleAsync(party.Id, default));

        // Soft delete: satır durur ama query filter listelerden çıkarır.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Parties.IgnoreQueryFilters().SingleAsync(p => p.Id == party.Id);
        Assert.True(row.IsDeleted);

        var list = await scope.ServiceProvider.GetRequiredService<ListPartiesHandler>()
            .HandleAsync(null, null, includeInactive: true, 1, 20, default);
        Assert.Empty(list.Items);
    }

    // ---- Liste: arama, tür filtresi, pasif filtresi

    [Fact]
    public async Task ListParties_FiltersBySearchTypeAndActivity()
    {
        using var scope = await CreateOwnerScopeAsync("liste@test.local");
        var create = scope.ServiceProvider.GetRequiredService<CreatePartyHandler>();
        await create.HandleAsync(NewParty("Acma Mobilya", "Customer"), default);
        await create.HandleAsync(NewParty("Zeytin Tedarik", "Supplier"), default);
        var both = await create.HandleAsync(NewParty("Hem Al Hem Sat", "Both"), default);

        var list = scope.ServiceProvider.GetRequiredService<ListPartiesHandler>();

        // Arama ad/telefon/e-posta/vergi no üzerinden, büyük-küçük harf duyarsız.
        var search = await list.HandleAsync("ACMA", null, includeInactive: true, 1, 20, default);
        var hit = Assert.Single(search.Items);
        Assert.Equal("Acma Mobilya", hit.Name);

        // Customer filtresi Customer + Both döner ("bu rolde kullanılabilen").
        var customers = await list.HandleAsync(null, "Customer", includeInactive: true, 1, 20, default);
        Assert.Equal(2, customers.TotalCount);
        Assert.All(customers.Items, p => Assert.NotEqual("Supplier", p.Type));

        var suppliers = await list.HandleAsync(null, "Supplier", includeInactive: true, 1, 20, default);
        Assert.Equal(2, suppliers.TotalCount);

        // Pasif cari yalnızca includeInactive=true ile görünür.
        var update = scope.ServiceProvider.GetRequiredService<UpdatePartyHandler>();
        await update.HandleAsync(both.Id, new UpdatePartyRequest(
            "Hem Al Hem Sat", "Both", null, null, null, null, null, null, null, null, 0m, null, IsActive: false), default);

        var activeOnly = await list.HandleAsync(null, null, includeInactive: false, 1, 20, default);
        Assert.Equal(2, activeOnly.TotalCount);
        Assert.DoesNotContain(activeOnly.Items, p => p.Id == both.Id);
    }

    // ---- Tenant izolasyonu

    [Fact]
    public async Task Party_Is_Isolated_Between_Tenants()
    {
        using var scopeA = await CreateOwnerScopeAsync("a@test.local");
        var partyInA = await scopeA.ServiceProvider.GetRequiredService<CreatePartyHandler>()
            .HandleAsync(NewParty("A İşletmesinin Carisi"), default);

        using var scopeB = await CreateOwnerScopeAsync("b@test.local");
        var get = scopeB.ServiceProvider.GetRequiredService<GetPartyHandler>();
        await Assert.ThrowsAsync<NotFoundException>(() => get.HandleAsync(partyInA.Id, default));

        var transactions = scopeB.ServiceProvider.GetRequiredService<CreatePartyTransactionHandler>();
        await Assert.ThrowsAsync<NotFoundException>(() => transactions.HandleAsync(
            partyInA.Id, NewTransaction("Debit", 100m, DateTime.UtcNow), default));

        var list = await scopeB.ServiceProvider.GetRequiredService<ListPartiesHandler>()
            .HandleAsync(null, null, includeInactive: true, 1, 20, default);
        Assert.DoesNotContain(list.Items, p => p.Id == partyInA.Id);
    }

    // ---- Rol izin matrisi (Viewer cari verisi okur ama kart açamaz)

    [Fact]
    public void ViewerRole_Cannot_Modify_Parties()
    {
        var viewer = RolePermissions.For(TenantRole.Viewer);
        Assert.Contains(Permissions.PartiesView, viewer);
        Assert.DoesNotContain(Permissions.PartiesCreate, viewer);
        Assert.DoesNotContain(Permissions.PartiesEdit, viewer);
        Assert.DoesNotContain(Permissions.PartiesDelete, viewer);

        var owner = RolePermissions.For(TenantRole.Owner);
        Assert.Contains(Permissions.PartiesCreate, owner);
        Assert.Contains(Permissions.PartiesDelete, owner);
    }

    // ---- Doğrulama kuralları (para ölçeği, tür, ad)

    [Fact]
    public async Task CreatePartyValidator_RejectsInvalidInput()
    {
        using var scope = await CreateOwnerScopeAsync("validator@test.local");
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CreatePartyRequest>>();

        Assert.False((await validator.ValidateAsync(NewParty("A"), default)).IsValid);                       // ad < 2 karakter
        Assert.False((await validator.ValidateAsync(NewParty("Ad", "Bilinmeyen"), default)).IsValid);        // geçersiz tür
        Assert.False((await validator.ValidateAsync(NewParty("Ad", "Customer", openingBalance: 10.123m), default)).IsValid); // 3 basamak
        Assert.False((await validator.ValidateAsync(NewParty("Ad", "Customer", creditLimit: -1m), default)).IsValid);       // negatif limit

        Assert.True((await validator.ValidateAsync(NewParty("Geçerli Ad", "Customer", openingBalance: 1250.50m), default)).IsValid);
    }

    [Fact]
    public async Task CreatePartyTransactionValidator_RejectsInvalidInput()
    {
        using var scope = await CreateOwnerScopeAsync("validator-hareket@test.local");
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CreatePartyTransactionRequest>>();

        Assert.False((await validator.ValidateAsync(NewTransaction("Sale", 100m, DateTime.UtcNow), default)).IsValid);      // manuel olmayan tür
        Assert.False((await validator.ValidateAsync(NewTransaction("Debit", 0m, DateTime.UtcNow), default)).IsValid);       // sıfır tutar
        Assert.False((await validator.ValidateAsync(NewTransaction("Debit", 10.999m, DateTime.UtcNow), default)).IsValid);  // 3 basamak
        Assert.False((await validator.ValidateAsync(
            new CreatePartyTransactionRequest("Debit", default, 100m, null, null), default)).IsValid);                      // tarih boş

        Assert.True((await validator.ValidateAsync(NewTransaction("Adjustment", -25.30m, DateTime.UtcNow), default)).IsValid);
    }

    public void Dispose() => _app.Dispose();
}
