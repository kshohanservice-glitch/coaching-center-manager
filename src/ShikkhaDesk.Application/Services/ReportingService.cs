using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record ProfitAndLossReport(
    DateRange Range,
    IReadOnlyList<ProfitAndLossRow> Income,
    IReadOnlyList<ProfitAndLossRow> Expenses,
    Money TotalIncome,
    Money TotalExpenses,
    Money NetProfit);

public sealed record BalanceSheetReport(
    DateTime AsOf,
    IReadOnlyList<BalanceSheetRow> Assets,
    IReadOnlyList<BalanceSheetRow> Liabilities,
    IReadOnlyList<BalanceSheetRow> Equity,
    Money TotalAssets,
    Money TotalLiabilitiesAndEquity);

public interface IReportingService
{
    Task<ProfitAndLossReport> GetProfitAndLossAsync(DateRange range, CancellationToken ct = default);
    Task<BalanceSheetReport> GetBalanceSheetAsync(DateTime asOf, CancellationToken ct = default);
    Task<IReadOnlyList<ReconciliationCheck>> RunReconciliationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IncomeRecord>> GetIncomeRegisterAsync(DateRange range, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseRecord>> GetExpenseRegisterAsync(DateRange range, CancellationToken ct = default);
    Task<IReadOnlyList<FeeCharge>> GetOutstandingDuesAsync(Guid? batchId = null, DateTime? asOf = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<PayableRow>> GetPayablesAsync(PayableStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollRow>> GetPayrollAsync(Guid periodId, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollRow>> GetPayrollForPeriodAsync(string periodKey, CancellationToken ct = default);
    Task<IReadOnlyList<StudentCreditTransaction>> GetCreditLedgerAsync(Guid studentId,
        CancellationToken ct = default);
}

/// <summary>
/// Financial statements and reconciliation. Every figure is derived from posted journal lines, so a
/// report can never disagree with the ledger. Statements are accrual-basis: income is recognised when
/// a fee is assessed, not when it is collected, and transfers never appear as income or expense.
/// </summary>
public sealed class ReportingService : IReportingService
{
    private readonly IAccountingRepository _accountingRepository;
    private readonly IAccountingService _accounting;
    private readonly IFeeRepository _fees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ReportingService(IAccountingRepository accountingRepository, IAccountingService accounting,
        IFeeRepository fees, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _accountingRepository = accountingRepository;
        _accounting = accounting;
        _fees = fees;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ProfitAndLossReport> GetProfitAndLossAsync(DateRange range, CancellationToken ct = default)
    {
        _currentUser.Current.Require("reports.view");

        IReadOnlyList<AccountBalanceRow> balances = await _accounting.GetAccountBalancesAsync(range, ct)
            .ConfigureAwait(false);

        var income = balances
            .Where(b => b.Type == AccountType.Income && !b.SignedBalance.IsZero)
            .Select(b => new ProfitAndLossRow("আয়", b.Name, b.SignedBalance))
            .ToList();

        var expenses = balances
            .Where(b => b.Type == AccountType.Expense && !b.SignedBalance.IsZero)
            .Select(b => new ProfitAndLossRow("ব্যয়", b.Name, b.SignedBalance))
            .ToList();

        Money totalIncome = income.Aggregate(Money.Zero, (sum, r) => sum + r.Amount);
        Money totalExpenses = expenses.Aggregate(Money.Zero, (sum, r) => sum + r.Amount);

        return new ProfitAndLossReport(range, income, expenses, totalIncome, totalExpenses,
            totalIncome - totalExpenses);
    }

    public async Task<BalanceSheetReport> GetBalanceSheetAsync(DateTime asOf, CancellationToken ct = default)
    {
        _currentUser.Current.Require("reports.view");

        var range = new DateRange(DateTime.MinValue.Date, asOf);
        IReadOnlyList<AccountBalanceRow> balances = await _accounting.GetAccountBalancesAsync(range, ct)
            .ConfigureAwait(false);

        var assets = balances
            .Where(b => b.Type == AccountType.Asset && !b.SignedBalance.IsZero)
            .Select(b => new BalanceSheetRow("সম্পদ", b.Name, b.SignedBalance))
            .ToList();

        var liabilities = balances
            .Where(b => b.Type == AccountType.Liability && !b.SignedBalance.IsZero)
            .Select(b => new BalanceSheetRow("দায়", b.Name, b.SignedBalance))
            .ToList();

        var equity = balances
            .Where(b => b.Type == AccountType.Equity && !b.SignedBalance.IsZero)
            .Select(b => new BalanceSheetRow("মূলধন", b.Name, b.SignedBalance))
            .ToList();

        ProfitAndLossReport pl = await GetProfitAndLossAsync(range, ct).ConfigureAwait(false);

        Money totalAssets = assets.Aggregate(Money.Zero, (sum, r) => sum + r.Amount);
        Money totalLiabilities = liabilities.Aggregate(Money.Zero, (sum, r) => sum + r.Amount);
        Money totalEquity = equity.Aggregate(Money.Zero, (sum, r) => sum + r.Amount) + pl.NetProfit;

        return new BalanceSheetReport(asOf, assets, liabilities, equity, totalAssets,
            totalLiabilities + totalEquity);
    }

    /// <summary>
    /// Cross-checks the sub-ledgers against the general ledger. A failing check names both sides so a
    /// discrepancy is visible instead of being quietly rounded away.
    /// </summary>
    public async Task<IReadOnlyList<ReconciliationCheck>> RunReconciliationAsync(CancellationToken ct = default)
    {
        _currentUser.Current.Require("reports.view");

        var checks = new List<ReconciliationCheck>();
        var range = new DateRange(new DateTime(2000, 1, 1), DateTime.Today.AddYears(10));

        // 1. Trial balance must balance.
        IReadOnlyList<TrialBalanceRow> trialBalance = await _accounting.GetTrialBalanceAsync(range, ct)
            .ConfigureAwait(false);

        Money debits = trialBalance.Aggregate(Money.Zero, (sum, r) => sum + r.Debit);
        Money credits = trialBalance.Aggregate(Money.Zero, (sum, r) => sum + r.Credit);
        checks.Add(new ReconciliationCheck("ট্রায়াল ব্যালেন্স: ডেবিট = ক্রেডিট", debits, credits));

        // 2. Student receivables: charge sub-ledger vs general ledger.
        AccountMap map = await _accounting.GetAccountMapAsync(ct).ConfigureAwait(false);
        Money ledgerReceivables = await _accounting.GetAccountBalanceAsync(map.StudentReceivables, null, ct)
            .ConfigureAwait(false);
        Money chargeOutstanding = await _unitOfWork.ExecuteAsync(
            token => _fees.GetTotalOutstandingAsync(token), ct).ConfigureAwait(false);
        checks.Add(new ReconciliationCheck("শিক্ষার্থী বকেয়া: সাব-লেজার = জেনারেল লেজার",
            chargeOutstanding, ledgerReceivables));

        // 3. Advance credit liability.
        Money ledgerCredit = await _accounting.GetAccountBalanceAsync(map.StudentAdvanceCredit, null, ct)
            .ConfigureAwait(false);
        Money creditLedger = await _unitOfWork.ExecuteAsync(
            token => _fees.GetTotalCreditLiabilityAsync(token), ct).ConfigureAwait(false);
        checks.Add(new ReconciliationCheck("অগ্রিম ক্রেডিট দায়: ক্রেডিট লেজার = জেনারেল লেজার",
            creditLedger, ledgerCredit));

        // 4. Salary payable vs unpaid payroll.
        Money ledgerSalaryPayable = await _accounting.GetAccountBalanceAsync(map.SalaryPayable, null, ct)
            .ConfigureAwait(false);
        Money unpaidSalary = await _unitOfWork.ExecuteAsync(
            token => _accountingRepository.GetUnpaidPayrollTotalAsync(token), ct).ConfigureAwait(false);
        checks.Add(new ReconciliationCheck("বেতন দায়: বেতন রেকর্ড = জেনারেল লেজার",
            unpaidSalary, ledgerSalaryPayable));

        // 5. Accounts payable vs outstanding supplier bills.
        Money ledgerPayable = await _accounting.GetAccountBalanceAsync(map.AccountsPayable, null, ct)
            .ConfigureAwait(false);
        Money outstandingBills = await _unitOfWork.ExecuteAsync(
            token => _accountingRepository.GetOutstandingPayablesTotalAsync(token), ct).ConfigureAwait(false);
        checks.Add(new ReconciliationCheck("প্রদেয় হিসাব: বিল = জেনারেল লেজার",
            outstandingBills, ledgerPayable));

        // 6. Transfers must never touch income or expense.
        Money transferEffect = await _unitOfWork.ExecuteAsync(
            token => _accountingRepository.SumIncomeExpenseBySourceAsync(
                TransactionSourceKind.Transfer, range, token), ct).ConfigureAwait(false);
        checks.Add(new ReconciliationCheck("স্থানান্তর: আয়/ব্যয়ে প্রভাব = ০", Money.Zero, transferEffect));

        // 7. Payable settlements must never touch income or expense.
        Money settlementEffect = await _unitOfWork.ExecuteAsync(
            token => _accountingRepository.SumIncomeExpenseBySourceAsync(
                TransactionSourceKind.PayableSettlement, range, token), ct).ConfigureAwait(false);
        checks.Add(new ReconciliationCheck("বিল পরিশোধ: ব্যয় পুনরায় স্বীকৃত = ০", Money.Zero, settlementEffect));

        // 8. Salary payments must never recognise salary expense again.
        Money payrollPaymentEffect = await _unitOfWork.ExecuteAsync(
            token => _accountingRepository.SumIncomeExpenseBySourceAsync(
                TransactionSourceKind.PayrollPayment, range, token), ct).ConfigureAwait(false);
        checks.Add(new ReconciliationCheck("বেতন পরিশোধ: ব্যয় পুনরায় স্বীকৃত = ০", Money.Zero,
            payrollPaymentEffect));

        return checks;
    }

    public Task<IReadOnlyList<IncomeRecord>> GetIncomeRegisterAsync(DateRange range,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("reports.view");
        return _unitOfWork.ExecuteAsync(token => _accountingRepository.GetIncomeAsync(range, token), ct);
    }

    public Task<IReadOnlyList<ExpenseRecord>> GetExpenseRegisterAsync(DateRange range,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("reports.view");
        return _unitOfWork.ExecuteAsync(token => _accountingRepository.GetExpensesAsync(range, token), ct);
    }

    public Task<IReadOnlyList<FeeCharge>> GetOutstandingDuesAsync(Guid? batchId = null, DateTime? asOf = null,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.view");
        return _unitOfWork.ExecuteAsync(
            token => _fees.GetOutstandingChargesAsync(batchId, null, asOf, token), ct);
    }

    public Task<IReadOnlyList<PayableRow>> GetPayablesAsync(PayableStatus? status = null,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("accounting.view");
        return _unitOfWork.ExecuteAsync(token => _accountingRepository.GetPayablesAsync(status, token), ct);
    }

    public async Task<IReadOnlyList<PayrollRow>> GetPayrollForPeriodAsync(string periodKey,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("payroll.view");

        return await _unitOfWork.ExecuteAsync(async token =>
        {
            PayrollPeriod? period = await _accountingRepository.GetPayrollPeriodAsync(periodKey, token)
                .ConfigureAwait(false);
            return period is null
                ? Array.Empty<PayrollRow>()
                : await _accountingRepository.GetPayrollRowsAsync(period.Id, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<PayrollRow>> GetPayrollAsync(Guid periodId, CancellationToken ct = default)
    {
        _currentUser.Current.Require("payroll.view");
        return _unitOfWork.ExecuteAsync(token => _accountingRepository.GetPayrollRowsAsync(periodId, token), ct);
    }

    public Task<IReadOnlyList<StudentCreditTransaction>> GetCreditLedgerAsync(Guid studentId,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.view");
        return _unitOfWork.ExecuteAsync(token => _fees.GetCreditTransactionsAsync(studentId, token), ct);
    }
}
