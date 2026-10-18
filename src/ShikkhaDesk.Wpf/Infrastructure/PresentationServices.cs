using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Infrastructure;
using ShikkhaDesk.Wpf.ViewModels;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// Wires the presentation layer. Every page is built by the container, so a page can never silently
/// miss a dependency, and every navigation key maps to exactly one view model.
/// </summary>
public static class PresentationServices
{
    public static IServiceCollection AddShikkhaDeskPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPrintService, PrintService>();

        // Academic
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StudentsViewModel>();
        services.AddSingleton<AdmissionsViewModel>();
        services.AddSingleton<AcademicsViewModel>();
        services.AddSingleton<BatchesViewModel>();
        services.AddSingleton<StaffViewModel>();
        services.AddSingleton<AttendanceViewModel>();
        services.AddSingleton<ExamsViewModel>();

        // Fees and payments
        services.AddSingleton<FeePlansViewModel>();
        services.AddSingleton<FeeAssessmentViewModel>();
        services.AddSingleton<DuesViewModel>();
        services.AddSingleton<CollectFeeViewModel>();
        services.AddSingleton<PaymentsViewModel>();
        services.AddSingleton<ReceiptsViewModel>();
        services.AddSingleton<CreditLedgerViewModel>();
        services.AddSingleton<RefundsViewModel>();

        // Accounting
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<IncomeViewModel>();
        services.AddSingleton<ExpensesViewModel>();
        services.AddSingleton<JournalViewModel>();
        services.AddSingleton<CashbookViewModel>();
        services.AddSingleton<BankbookViewModel>();
        services.AddSingleton<LedgerViewModel>();
        services.AddSingleton<TransferViewModel>();
        services.AddSingleton<ReceivablesViewModel>();
        services.AddSingleton<SuppliersViewModel>();
        services.AddSingleton<PayablesViewModel>();
        services.AddSingleton<PayrollViewModel>();
        services.AddSingleton<TrialBalanceViewModel>();
        services.AddSingleton<ProfitLossViewModel>();
        services.AddSingleton<BalanceSheetViewModel>();
        services.AddSingleton<CashFlowViewModel>();
        services.AddSingleton<ReconciliationViewModel>();

        // Reports and administration
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<UsersViewModel>();
        services.AddSingleton<AuditViewModel>();
        services.AddSingleton<BackupViewModel>();
        services.AddSingleton<LanSettingsViewModel>();
        services.AddSingleton<LicenseViewModel>();
        services.AddSingleton<InstitutionViewModel>();
        services.AddSingleton<GeneralSettingsViewModel>();
        services.AddSingleton<AboutViewModel>();

        services.AddSingleton<NavigationService>(provider => new NavigationService(
            provider.GetRequiredService<AppState>(),
            key => CreatePage(provider, key)));

        return services;
    }

    private static ViewModelBase CreatePage(IServiceProvider provider, string key) => key switch
    {
        "dashboard" => provider.GetRequiredService<DashboardViewModel>(),
        "students" => provider.GetRequiredService<StudentsViewModel>(),
        "admissions" => provider.GetRequiredService<AdmissionsViewModel>(),
        "academics" => provider.GetRequiredService<AcademicsViewModel>(),
        "batches" => provider.GetRequiredService<BatchesViewModel>(),
        "staff" => provider.GetRequiredService<StaffViewModel>(),
        "attendance" => provider.GetRequiredService<AttendanceViewModel>(),
        "exams" => provider.GetRequiredService<ExamsViewModel>(),

        "feeplans" => provider.GetRequiredService<FeePlansViewModel>(),
        "assessment" => provider.GetRequiredService<FeeAssessmentViewModel>(),
        "dues" => provider.GetRequiredService<DuesViewModel>(),
        "collect" => provider.GetRequiredService<CollectFeeViewModel>(),
        "payments" => provider.GetRequiredService<PaymentsViewModel>(),
        "receipts" => provider.GetRequiredService<ReceiptsViewModel>(),
        "credit" => provider.GetRequiredService<CreditLedgerViewModel>(),
        "refunds" => provider.GetRequiredService<RefundsViewModel>(),

        "accounts" => provider.GetRequiredService<AccountsViewModel>(),
        "income" => provider.GetRequiredService<IncomeViewModel>(),
        "expenses" => provider.GetRequiredService<ExpensesViewModel>(),
        "journal" => provider.GetRequiredService<JournalViewModel>(),
        "cashbook" => provider.GetRequiredService<CashbookViewModel>(),
        "bankbook" => provider.GetRequiredService<BankbookViewModel>(),
        "ledger" => provider.GetRequiredService<LedgerViewModel>(),
        "transfers" => provider.GetRequiredService<TransferViewModel>(),
        "receivables" => provider.GetRequiredService<ReceivablesViewModel>(),
        "suppliers" => provider.GetRequiredService<SuppliersViewModel>(),
        "payables" => provider.GetRequiredService<PayablesViewModel>(),
        "payroll" => provider.GetRequiredService<PayrollViewModel>(),
        "trialbalance" => provider.GetRequiredService<TrialBalanceViewModel>(),
        "profitloss" => provider.GetRequiredService<ProfitLossViewModel>(),
        "balancesheet" => provider.GetRequiredService<BalanceSheetViewModel>(),
        "cashflow" => provider.GetRequiredService<CashFlowViewModel>(),
        "reconciliation" => provider.GetRequiredService<ReconciliationViewModel>(),

        "reports" => provider.GetRequiredService<ReportsViewModel>(),
        "users" => provider.GetRequiredService<UsersViewModel>(),
        "audit" => provider.GetRequiredService<AuditViewModel>(),
        "backup" => provider.GetRequiredService<BackupViewModel>(),
        "lan" => provider.GetRequiredService<LanSettingsViewModel>(),
        "license" => provider.GetRequiredService<LicenseViewModel>(),
        "institution" => provider.GetRequiredService<InstitutionViewModel>(),
        "settings" => provider.GetRequiredService<GeneralSettingsViewModel>(),
        "about" => provider.GetRequiredService<AboutViewModel>(),

        _ => throw new NotFoundException($"'{key}' নামের কোনো পৃষ্ঠা নেই।")
    };
}
