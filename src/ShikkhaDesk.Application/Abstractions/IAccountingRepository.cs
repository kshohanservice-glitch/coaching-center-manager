using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Abstractions;

public interface IAccountingRepository
{
    // Chart of accounts
    Task<IReadOnlyList<Account>> GetAccountsAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<Account?> GetAccountAsync(Guid id, CancellationToken ct = default);
    Task<Account?> GetAccountByCodeAsync(string code, CancellationToken ct = default);
    Task InsertAccountAsync(Account account, CancellationToken ct = default);
    Task UpdateAccountAsync(Account account, CancellationToken ct = default);
    Task<int> CountJournalLinesForAccountAsync(Guid accountId, CancellationToken ct = default);

    // Financial (cash/bank) accounts
    Task<IReadOnlyList<FinancialAccount>> GetFinancialAccountsAsync(bool activeOnly = false,
        CancellationToken ct = default);
    Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken ct = default);
    Task InsertFinancialAccountAsync(FinancialAccount account, CancellationToken ct = default);
    Task UpdateFinancialAccountAsync(FinancialAccount account, CancellationToken ct = default);

    // Journals
    Task InsertJournalEntryAsync(JournalEntry entry, IReadOnlyList<JournalLine> lines,
        CancellationToken ct = default);
    Task UpdateJournalEntryAsync(JournalEntry entry, CancellationToken ct = default);
    Task<JournalEntryDto?> GetJournalEntryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> GetJournalEntriesAsync(DateRange range, CancellationToken ct = default);
    Task<IReadOnlyList<JournalLine>> GetJournalLinesAsync(Guid journalEntryId, CancellationToken ct = default);
    Task<PagedResult<LedgerEntryRow>> GetAccountLedgerAsync(LedgerQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<AccountBalanceRow>> GetAccountBalancesAsync(DateRange range,
        CancellationToken ct = default);
    Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateRange range, CancellationToken ct = default);
    Task<Money> GetAccountBalanceAsync(Guid accountId, DateTime? asOf, CancellationToken ct = default);
    Task<Money> GetFinancialAccountBalanceAsync(Guid financialAccountId, DateTime? asOf,
        CancellationToken ct = default);

    // Transfers
    Task InsertTransferAsync(Transfer transfer, CancellationToken ct = default);
    Task<IReadOnlyList<Transfer>> GetTransfersAsync(DateRange range, CancellationToken ct = default);

    // Income & expense
    Task InsertIncomeAsync(IncomeRecord income, CancellationToken ct = default);
    Task<IReadOnlyList<IncomeRecord>> GetIncomeAsync(DateRange range, CancellationToken ct = default);
    Task InsertExpenseAsync(ExpenseRecord expense, CancellationToken ct = default);
    Task UpdateExpenseAsync(ExpenseRecord expense, CancellationToken ct = default);
    Task<ExpenseRecord?> GetExpenseAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseRecord>> GetExpensesAsync(DateRange range, CancellationToken ct = default);

    // Suppliers & payables
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default);
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken ct = default);
    Task InsertSupplierAsync(Supplier supplier, CancellationToken ct = default);
    Task UpdateSupplierAsync(Supplier supplier, CancellationToken ct = default);
    Task<Payable?> GetPayableAsync(Guid id, CancellationToken ct = default);
    Task InsertPayableAsync(Payable payable, CancellationToken ct = default);
    Task UpdatePayableAsync(Payable payable, CancellationToken ct = default);
    Task<IReadOnlyList<PayableRow>> GetPayablesAsync(PayableStatus? status = null,
        CancellationToken ct = default);
    Task InsertPayableSettlementAsync(PayableSettlement settlement, CancellationToken ct = default);
    Task<IReadOnlyList<PayableSettlement>> GetSettlementsAsync(Guid payableId, CancellationToken ct = default);

    // Payroll
    Task<PayrollPeriod?> GetPayrollPeriodAsync(string periodKey, CancellationToken ct = default);
    Task InsertPayrollPeriodAsync(PayrollPeriod period, CancellationToken ct = default);
    Task UpdatePayrollPeriodAsync(PayrollPeriod period, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollPeriod>> GetPayrollPeriodsAsync(CancellationToken ct = default);
    Task<PayrollRecord?> GetPayrollRecordAsync(Guid periodId, Guid staffId, CancellationToken ct = default);
    Task InsertPayrollRecordAsync(PayrollRecord record, CancellationToken ct = default);
    Task UpdatePayrollRecordAsync(PayrollRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollRecord>> GetPayrollRecordsAsync(Guid periodId, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollRow>> GetPayrollRowsAsync(Guid periodId, CancellationToken ct = default);

    // Cashbook & bankbook
    Task<IReadOnlyList<CashbookRow>> GetCashbookAsync(Guid financialAccountId, DateRange range,
        CancellationToken ct = default);
}
