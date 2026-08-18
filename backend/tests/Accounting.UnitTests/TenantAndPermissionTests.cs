using Accounting.Application.Common;
using Accounting.Application.Features.Tenants;
using Accounting.Contracts.Tenants;
using Accounting.Domain.Authorization;
using Accounting.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Accounting.UnitTests;

public sealed class TenantHandlerTests : IDisposable
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task CreateTenant_Creates_Tenant_With_Owner_Role()
    {
        var user = await _app.RegisterUserAsync();

        using var scope = _app.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateTenantHandler>();

        var tenant = await handler.HandleAsync(user.Id, new CreateTenantRequest("İkinci Şube"), default);

        Assert.Equal("İkinci Şube", tenant.Name);
        Assert.Equal("Owner", tenant.Role);

        var listHandler = scope.ServiceProvider.GetRequiredService<ListTenantsHandler>();
        var tenants = await listHandler.HandleAsync(user.Id, default);
        Assert.Equal(2, tenants.Count);
    }

    [Fact]
    public async Task GetTenant_Returns_Forbidden_For_NonMember()
    {
        var owner = await _app.RegisterUserAsync(email: "sahip@test.local");
        var outsider = await _app.RegisterUserAsync(email: "disaridakı@test.local");

        using var scope = _app.CreateScope();
        var createHandler = scope.ServiceProvider.GetRequiredService<CreateTenantHandler>();
        var tenant = await createHandler.HandleAsync(owner.Id, new CreateTenantRequest("Sahibin İşletmesi"), default);

        var getHandler = scope.ServiceProvider.GetRequiredService<GetTenantHandler>();

        // Üye olmayan kullanıcı işletmeye erişemez.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            getHandler.HandleAsync(outsider.Id, tenant.Id, default));

        // Üye olan kullanıcı erişebilir.
        var result = await getHandler.HandleAsync(owner.Id, tenant.Id, default);
        Assert.Equal("Sahibin İşletmesi", result.Name);
    }

    public void Dispose() => _app.Dispose();
}

public sealed class RolePermissionTests
{
    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Admin)]
    public void Yonetim_Rolleri_UsersManage_Yetkisine_Sahip(TenantRole role)
    {
        Assert.Contains(Permissions.UsersManage, RolePermissions.For(role));
    }

    [Fact]
    public void Viewer_Yalnizca_Goruntuleme_Yetkilerine_Sahip()
    {
        var permissions = RolePermissions.For(TenantRole.Viewer);

        Assert.Contains(Permissions.SalesView, permissions);
        Assert.DoesNotContain(Permissions.SalesCreate, permissions);
        Assert.DoesNotContain(Permissions.UsersManage, permissions);
    }

    [Fact]
    public void Accountant_Rapor_Ve_Gider_Islemleri_Yapabilir()
    {
        var permissions = RolePermissions.For(TenantRole.Accountant);

        Assert.Contains(Permissions.ReportsView, permissions);
        Assert.Contains(Permissions.ExpensesCreate, permissions);
        Assert.DoesNotContain(Permissions.SalesDelete, permissions);
    }

    [Fact]
    public void Employee_Satis_Yapabilir_Amma_Ayar_Yonetemez()
    {
        var permissions = RolePermissions.For(TenantRole.Employee);

        Assert.Contains(Permissions.SalesCreate, permissions);
        Assert.DoesNotContain(Permissions.SettingsManage, permissions);
        Assert.DoesNotContain(Permissions.UsersManage, permissions);
    }

    [Fact]
    public void Owner_Tum_Yetkilere_Sahip()
    {
        var permissions = RolePermissions.For(TenantRole.Owner);

        Assert.True(permissions.SetEquals(Permissions.All));
    }
}
