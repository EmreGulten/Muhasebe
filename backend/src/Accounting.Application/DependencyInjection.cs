using Accounting.Application.Abstractions;
using Accounting.Application.Features.Accounts;
using Accounting.Application.Features.IncomeExpenses;
using Accounting.Application.Features.Auth;
using Accounting.Application.Features.Parties;
using Accounting.Application.Features.Products;
using Accounting.Application.Features.Sales;
using Accounting.Application.Features.Purchases;
using Accounting.Application.Features.Reports;
using Accounting.Application.Features.Assistant;
using Accounting.Application.Features.Subscriptions;
using Accounting.Application.Features.Tenants;
using Accounting.Application.Services;
using Accounting.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Accounting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ITokenService, JwtTokenService>();

        // Use case handler'ları
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<ListTenantsHandler>();
        services.AddScoped<GetTenantHandler>();

        // Cari
        services.AddScoped<CreatePartyHandler>();
        services.AddScoped<UpdatePartyHandler>();
        services.AddScoped<DeletePartyHandler>();
        services.AddScoped<GetPartyHandler>();
        services.AddScoped<ListPartiesHandler>();
        services.AddScoped<CreatePartyTransactionHandler>();
        services.AddScoped<GetPartyStatementHandler>();

        // Ürün + stok
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<ListProductsHandler>();
        services.AddScoped<GetProductStockHandler>();
        services.AddScoped<GetCriticalStockHandler>();
        services.AddScoped<CreateInventoryTransactionHandler>();
        services.AddScoped<CreateInventoryTransferHandler>();
        services.AddScoped<ListInventoryTransactionsHandler>();

        // Tanımlar: kategori / birim / depo
        services.AddScoped<ListCategoriesHandler>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<UpdateCategoryHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddScoped<ListUnitsHandler>();
        services.AddScoped<CreateUnitHandler>();
        services.AddScoped<UpdateUnitHandler>();
        services.AddScoped<DeleteUnitHandler>();
        services.AddScoped<ListWarehousesHandler>();
        services.AddScoped<CreateWarehouseHandler>();
        services.AddScoped<UpdateWarehouseHandler>();
        services.AddScoped<DeleteWarehouseHandler>();

        // Satış
        services.AddScoped<CreateSaleHandler>();
        services.AddScoped<UpdateSaleHandler>();
        services.AddScoped<DeleteSaleHandler>();
        services.AddScoped<GetSaleHandler>();
        services.AddScoped<ListSalesHandler>();
        services.AddScoped<ConfirmSaleHandler>();
        services.AddScoped<CancelSaleHandler>();
        services.AddScoped<AddSalePaymentHandler>();

        // Alış
        services.AddScoped<CreatePurchaseHandler>();
        services.AddScoped<UpdatePurchaseHandler>();
        services.AddScoped<DeletePurchaseHandler>();
        services.AddScoped<GetPurchaseHandler>();
        services.AddScoped<ListPurchasesHandler>();
        services.AddScoped<ConfirmPurchaseHandler>();
        services.AddScoped<CancelPurchaseHandler>();
        services.AddScoped<AddPurchasePaymentHandler>();

        // Kasa / banka
        services.AddScoped<ListAccountsHandler>();
        services.AddScoped<GetAccountHandler>();
        services.AddScoped<CreateAccountHandler>();
        services.AddScoped<UpdateAccountHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<GetAccountStatementHandler>();
        services.AddScoped<CreateAccountTransactionHandler>();
        services.AddScoped<CreateTransferHandler>();

        // Gelir / gider
        services.AddScoped<ListIncomeExpenseCategoriesHandler>();
        services.AddScoped<CreateIncomeExpenseCategoryHandler>();
        services.AddScoped<UpdateIncomeExpenseCategoryHandler>();
        services.AddScoped<DeleteIncomeExpenseCategoryHandler>();
        services.AddScoped<CreateIncomeExpenseRecordHandler>();
        services.AddScoped<ListIncomeExpenseRecordsHandler>();
        services.AddScoped<GetIncomeExpenseRecordHandler>();
        services.AddScoped<CancelIncomeExpenseRecordHandler>();
        services.AddScoped<GetIncomeExpenseSummaryHandler>();

        // Raporlar
        services.AddScoped<GetDashboardHandler>();
        services.AddScoped<GetReceivablesReportHandler>();
        services.AddScoped<GetStockReportHandler>();
        services.AddScoped<GetSalesReportHandler>();

        // AI asistan (bölüm 11): onaylı iş araçları + sohbet.
        services.AddScoped<IAiTool, GetMonthlyProfitTool>();
        services.AddScoped<IAiTool, GetOverdueReceivablesTool>();
        services.AddScoped<IAiTool, GetTopProductsTool>();
        services.AddScoped<IAiTool, GetLowStockProductsTool>();
        services.AddScoped<IAiTool, GetCustomerBalanceTool>();
        services.AddScoped<IAiTool, GetExpenseBreakdownTool>();
        services.AddScoped<IAiTool, CompareMonthsTool>();
        services.AddScoped<IAiTool, GetUpcomingPaymentsTool>();
        services.AddScoped<AskAssistantHandler>();
        services.AddScoped<ListAssistantHistoryHandler>();

        // Abonelik (bölüm 30): feature guard + plan yönetimi.
        services.AddScoped<SubscriptionService>();
        services.AddScoped<IFeatureGuard>(sp => sp.GetRequiredService<SubscriptionService>());
        services.AddScoped<ListSubscriptionPlansHandler>();
        services.AddScoped<GetSubscriptionHandler>();
        services.AddScoped<ChangePlanHandler>();

        services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

        return services;
    }
}
