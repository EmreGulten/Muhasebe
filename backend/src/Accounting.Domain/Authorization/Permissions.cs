using Accounting.Domain.Enums;

namespace Accounting.Domain.Authorization;

/// <summary>Sistemdeki izin sabitleri.</summary>
public static class Permissions
{
    // Satış
    public const string SalesView = "Sales.View";
    public const string SalesCreate = "Sales.Create";
    public const string SalesEdit = "Sales.Edit";
    public const string SalesDelete = "Sales.Delete";

    // Alış
    public const string PurchasesView = "Purchases.View";
    public const string PurchasesCreate = "Purchases.Create";
    public const string PurchasesEdit = "Purchases.Edit";
    public const string PurchasesDelete = "Purchases.Delete";

    // Gelir / gider
    public const string ExpensesView = "Expenses.View";
    public const string ExpensesCreate = "Expenses.Create";
    public const string ExpensesEdit = "Expenses.Edit";
    public const string ExpensesDelete = "Expenses.Delete";

    // Ürün / stok
    public const string ProductsView = "Products.View";
    public const string ProductsCreate = "Products.Create";
    public const string ProductsEdit = "Products.Edit";
    public const string InventoryView = "Inventory.View";
    public const string InventoryEdit = "Inventory.Edit";

    // Cari
    public const string PartiesView = "Parties.View";
    public const string PartiesCreate = "Parties.Create";
    public const string PartiesEdit = "Parties.Edit";
    public const string PartiesDelete = "Parties.Delete";

    // Kasa / banka
    public const string AccountsView = "Accounts.View";
    public const string AccountsCreate = "Accounts.Create";
    public const string AccountsEdit = "Accounts.Edit";

    // Rapor / AI
    public const string ReportsView = "Reports.View";
    public const string AiAssistantUse = "AiAssistant.Use";

    // Yönetim
    public const string UsersManage = "Users.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string TenantManage = "Tenant.Manage";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal)
        {
            SalesView, SalesCreate, SalesEdit, SalesDelete,
            PurchasesView, PurchasesCreate, PurchasesEdit, PurchasesDelete,
            ExpensesView, ExpensesCreate, ExpensesEdit, ExpensesDelete,
            ProductsView, ProductsCreate, ProductsEdit,
            InventoryView, InventoryEdit,
            PartiesView, PartiesCreate, PartiesEdit, PartiesDelete,
            AccountsView, AccountsCreate, AccountsEdit,
            ReportsView, AiAssistantUse,
            UsersManage, SettingsManage, TenantManage,
        };
}

/// <summary>Rol → izin eşlemesi. İş modülleri bu haritayı yetki kontrolünde kullanır.</summary>
public static class RolePermissions
{
    private static readonly IReadOnlySet<string> ViewOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        Permissions.SalesView, Permissions.PurchasesView, Permissions.ExpensesView,
        Permissions.ProductsView, Permissions.InventoryView, Permissions.PartiesView,
        Permissions.AccountsView, Permissions.ReportsView,
    };

    private static readonly Dictionary<TenantRole, IReadOnlySet<string>> Map =
        new()
        {
            // Owner: tam yetki.
            [TenantRole.Owner] = Permissions.All,

            // Admin: işletme yönetimi — işletmenin kendisini devretme/silme hariç her şey.
            [TenantRole.Admin] = new HashSet<string>(Permissions.All.Except([Permissions.TenantManage], StringComparer.Ordinal)),

            // Accountant: her şeyi görüntüler, rapor/AI/gider tarafında işlem yapar.
            [TenantRole.Accountant] = new HashSet<string>(
                ViewOnly.Concat([Permissions.ExpensesCreate, Permissions.ExpensesEdit,
                    Permissions.PurchasesCreate, Permissions.PurchasesEdit,
                    Permissions.AccountsCreate, Permissions.AccountsEdit,
                    Permissions.AiAssistantUse]), StringComparer.Ordinal),

            // Employee: operasyonel akış — satış, cari, stok girişi.
            [TenantRole.Employee] = new HashSet<string>(
                ViewOnly.Concat([Permissions.SalesCreate, Permissions.SalesEdit,
                    Permissions.PartiesCreate, Permissions.PartiesEdit]), StringComparer.Ordinal),

            // Viewer: yalnızca görüntüleme.
            [TenantRole.Viewer] = ViewOnly,
        };

    public static IReadOnlySet<string> For(TenantRole role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : ViewOnly;
}
