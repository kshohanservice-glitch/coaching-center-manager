using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using Xunit;

namespace ShikkhaDesk.IntegrationTests;

public sealed class AccountingTests
{
    private static readonly DateRange All = new(new DateTime(2000, 1, 1), DateTime.Today.AddYears(5));

    private static async Task<(Guid CashId, Guid BankId)> CreateBankAccountAsync(TestHost host, string name = "সিটি ব্যাংক")
    {
        Account? bankLedger = await host.Get<IAccountingRepository>()
            .GetAccountByCodeAsync(SystemAccountCodes.BankAccounts);

        var account = new FinancialAccount
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = FinancialAccountKind.Bank,
            AccountId = bankLedger!.Id,
            BankName = name,
            AccountNumber = "0011223344",
            OpeningBalance = Money.Zero,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await host.Get<IAccountingRepository>().InsertFinancialAccountAsync(account);
        return (CashId: host.CurrentCashAccountId(), BankId: account.Id);
    }

    [Fact]
    public void Opening_balance_is_recognised_once_and_the_ledger_balances()
    {
        using TestHost host = TestHost.Create().Setup(openingCash: Money.FromDecimal(25000));

        var trial = host.Get<IAccountingService>().GetTrialBalanceAsync(All).GetAwaiter().GetResult();
        Money debits = trial.Aggregate(Money.Zero, (s, r) => s + r.Debit);
        Money credits = trial.Aggregate(Money.Zero, (s, r) => s + r.Credit);

        Assert.Equal(debits, credits);
        Assert.Equal(Money.FromDecimal(25000), debits);

        // The cash sub-account must agree with the ledger, not double count the opening balance.
        IReadOnlyList<FinancialAccountBalance> balances = host.Get<IAccountingService>()
            .GetFinancialAccountBalancesAsync().GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(25000), balances.Single(b => b.IsCash).Balance);
    }

    [Fact]
    public void Transfer_moves_money_without_creating_income_or_expense()
    {
        using TestHost host = TestHost.Create().Setup(openingCash: Money.FromDecimal(20000));
        (Guid cashId, Guid bankId) = CreateBankAccountAsync(host).GetAwaiter().GetResult();

        host.Get<ILedgerOperationsService>().TransferAsync(new TransferRequest(
            DateTime.Today, cashId, bankId, Money.FromDecimal(7500))).GetAwaiter().GetResult();

        IReadOnlyList<FinancialAccountBalance> balances = host.Get<IAccountingService>()
            .GetFinancialAccountBalancesAsync().GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(12500), balances.Single(b => b.FinancialAccountId == cashId).Balance);
        Assert.Equal(Money.FromDecimal(7500), balances.Single(b => b.FinancialAccountId == bankId).Balance);

        ProfitAndLossReport pl = host.Get<IReportingService>().GetProfitAndLossAsync(All).GetAwaiter().GetResult();
        Assert.Equal(Money.Zero, pl.TotalIncome);
        Assert.Equal(Money.Zero, pl.TotalExpenses);
    }

    [Fact]
    public void Accrued_expense_is_counted_once_not_twice_when_settled()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid cashId, _) = CreateBankAccountAsync(host).GetAwaiter().GetResult();

        Account rent = host.Get<IAccountingRepository>()
            .GetAccountByCodeAsync(SystemAccountCodes.OfficeRent).GetAwaiter().GetResult()!;

        Supplier supplier = new()
        {
            Id = Guid.NewGuid(),
            Code = "SUP-001",
            Name = "রাহিম স্টেশনারি",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        host.Get<IAccountingRepository>().InsertSupplierAsync(supplier).GetAwaiter().GetResult();

        Guid payableId = host.Get<ILedgerOperationsService>().CreatePayableAsync(new ExpenseRequest(
            DateTime.Today, rent.Id, Money.FromDecimal(3000), "অফিস ভাড়া", supplier.Id,
            AccrueAsPayable: true, DueDate: DateTime.Today.AddDays(30))).GetAwaiter().GetResult();

        Money expenseAfterAccrual = host.Get<IAccountingService>()
            .GetAccountBalanceAsync(rent.Id, null).GetAwaiter().GetResult();
        Assert.Equal(Money.FromDecimal(3000), expenseAfterAccrual);

        host.Get<ILedgerOperationsService>().SettlePayableAsync(new SettlePayableRequest(
            payableId, DateTime.Today, Money.FromDecimal(3000), cashId, PaymentMethod.Cash))
            .GetAwaiter().GetResult();

        Money expenseAfterSettlement = host.Get<IAccountingService>()
            .GetAccountBalanceAsync(rent.Id, null).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(3000), expenseAfterSettlement);

        IReadOnlyList<PayableRow> payables = host.Get<IReportingService>()
            .GetPayablesAsync().GetAwaiter().GetResult();
        Assert.Equal(Money.Zero, payables.Single(p => p.PayableId == payableId).Outstanding);
    }

    [Fact]
    public void Partial_payable_settlement_keeps_the_remainder_open()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid cashId, _) = CreateBankAccountAsync(host).GetAwaiter().GetResult();

        Account stationery = host.Get<IAccountingRepository>()
            .GetAccountByCodeAsync(SystemAccountCodes.Stationery).GetAwaiter().GetResult()!;

        Supplier supplier = new()
        {
            Id = Guid.NewGuid(), Code = "SUP-002", Name = "নতুন স্টেশনারি", IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        host.Get<IAccountingRepository>().InsertSupplierAsync(supplier).GetAwaiter().GetResult();

        Guid payableId = host.Get<ILedgerOperationsService>().CreatePayableAsync(new ExpenseRequest(
            DateTime.Today, stationery.Id, Money.FromDecimal(5000), "খাতা ও কলম", supplier.Id,
            AccrueAsPayable: true)).GetAwaiter().GetResult();

        host.Get<ILedgerOperationsService>().SettlePayableAsync(new SettlePayableRequest(
            payableId, DateTime.Today, Money.FromDecimal(2000), cashId, PaymentMethod.Cash))
            .GetAwaiter().GetResult();

        PayableRow row = host.Get<IReportingService>().GetPayablesAsync().GetAwaiter().GetResult()
            .Single(p => p.PayableId == payableId);

        Assert.Equal(Money.FromDecimal(3000), row.Outstanding);
        Assert.Equal(PayableStatus.PartiallyPaid, row.Status);
        Assert.Throws<DomainException>(() => host.Get<ILedgerOperationsService>()
            .SettlePayableAsync(new SettlePayableRequest(payableId, DateTime.Today,
                Money.FromDecimal(4000), cashId, PaymentMethod.Cash)).GetAwaiter().GetResult());
    }

    [Fact]
    public void Salary_is_expensed_once_and_the_payable_clears_on_payment()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid cashId, _) = CreateBankAccountAsync(host).GetAwaiter().GetResult();

        IAcademicService academic = host.Get<IAcademicService>();
        Guid staffId = academic.SaveStaffAsync(new Staff
        {
            FullName = "মোঃ করিম শিক্ষক",
            Designation = "শিক্ষক",
            JoiningDate = new DateTime(2026, 1, 1),
            Status = StaffStatus.Active,
            BasicSalary = Money.FromDecimal(20000),
            HouseRentAllowance = Money.FromDecimal(5000),
            MedicalAllowance = Money.FromDecimal(1000)
        }, []).GetAwaiter().GetResult();

        AccountMap map = host.Get<IAccountingService>().GetAccountMapAsync().GetAwaiter().GetResult();

        Guid periodId = host.Get<ILedgerOperationsService>()
            .AccruePayrollAsync("2026-03").GetAwaiter().GetResult();

        Money salaryExpense = host.Get<IAccountingService>()
            .GetAccountBalanceAsync(map.SalaryExpense, null).GetAwaiter().GetResult();
        Money salaryPayable = host.Get<IAccountingService>()
            .GetAccountBalanceAsync(map.SalaryPayable, null).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(26000), salaryExpense);
        Assert.Equal(Money.FromDecimal(26000), salaryPayable);

        // Duplicating the month must be impossible.
        host.Get<ILedgerOperationsService>().AccruePayrollAsync("2026-03").GetAwaiter().GetResult();
        Assert.Equal(Money.FromDecimal(26000), host.Get<IAccountingService>()
            .GetAccountBalanceAsync(map.SalaryExpense, null).GetAwaiter().GetResult());

        Guid recordId = host.Get<IAccountingRepository>().GetPayrollRecordsAsync(periodId)
            .GetAwaiter().GetResult().Single(r => r.StaffId == staffId).Id;

        host.Get<ILedgerOperationsService>().PaySalaryAsync(recordId, DateTime.Today, cashId,
            PaymentMethod.Cash).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(26000), host.Get<IAccountingService>()
            .GetAccountBalanceAsync(map.SalaryExpense, null).GetAwaiter().GetResult());
        Assert.Equal(Money.Zero, host.Get<IAccountingService>()
            .GetAccountBalanceAsync(map.SalaryPayable, null).GetAwaiter().GetResult());
    }

    [Fact]
    public void Income_and_expense_post_to_the_right_accounts()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid cashId, _) = CreateBankAccountAsync(host).GetAwaiter().GetResult();

        Account otherIncome = host.Get<IAccountingRepository>()
            .GetAccountByCodeAsync(SystemAccountCodes.OtherIncome).GetAwaiter().GetResult()!;
        Account electricity = host.Get<IAccountingRepository>()
            .GetAccountByCodeAsync(SystemAccountCodes.Electricity).GetAwaiter().GetResult()!;

        host.Get<ILedgerOperationsService>().RecordIncomeAsync(new IncomeRequest(
            DateTime.Today, otherIncome.Id, cashId, Money.FromDecimal(4000), "বিবিধ আয়"))
            .GetAwaiter().GetResult();

        host.Get<ILedgerOperationsService>().RecordExpenseAsync(new ExpenseRequest(
            DateTime.Today, electricity.Id, Money.FromDecimal(1500), "বিদ্যুৎ বিল",
            FinancialAccountId: cashId)).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(4000), host.Get<IAccountingService>()
            .GetAccountBalanceAsync(otherIncome.Id, null).GetAwaiter().GetResult());
        Assert.Equal(Money.FromDecimal(1500), host.Get<IAccountingService>()
            .GetAccountBalanceAsync(electricity.Id, null).GetAwaiter().GetResult());

        ProfitAndLossReport pl = host.Get<IReportingService>().GetProfitAndLossAsync(All).GetAwaiter().GetResult();
        Assert.Equal(Money.FromDecimal(4000), pl.TotalIncome);
        Assert.Equal(Money.FromDecimal(1500), pl.TotalExpenses);
        Assert.Equal(Money.FromDecimal(2500), pl.NetProfit);
    }

    [Fact]
    public void Cashbook_reconciles_to_the_cash_account_balance()
    {
        using TestHost host = TestHost.Create().Setup(openingCash: Money.FromDecimal(5000));
        (Guid cashId, _) = CreateBankAccountAsync(host).GetAwaiter().GetResult();

        Account otherIncome = host.Get<IAccountingRepository>()
            .GetAccountByCodeAsync(SystemAccountCodes.OtherIncome).GetAwaiter().GetResult()!;

        host.Get<ILedgerOperationsService>().RecordIncomeAsync(new IncomeRequest(
            DateTime.Today, otherIncome.Id, cashId, Money.FromDecimal(2200), "দান"))
            .GetAwaiter().GetResult();

        IReadOnlyList<CashbookRow> cashbook = host.Get<IAccountingService>()
            .GetCashbookAsync(cashId, new DateRange(DateTime.Today.AddDays(-1), DateTime.Today))
            .GetAwaiter().GetResult();

        Money closing = cashbook[^1].RunningBalance;
        Money accountBalance = host.Get<IAccountingRepository>()
            .GetFinancialAccountBalanceAsync(cashId, null).GetAwaiter().GetResult();

        Assert.Equal(accountBalance, closing);
        Assert.Equal(Money.FromDecimal(7200), closing);
    }

    [Fact]
    public void Balance_sheet_balances_after_a_full_set_of_transactions()
    {
        using TestHost host = TestHost.Create().Setup(openingCash: Money.FromDecimal(50000));
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);

        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();
        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(2000), PaymentMethod.Cash)).GetAwaiter().GetResult();

        BalanceSheetReport sheet = host.Get<IReportingService>()
            .GetBalanceSheetAsync(DateTime.Today).GetAwaiter().GetResult();

        Assert.Equal(sheet.TotalAssets, sheet.TotalLiabilitiesAndEquity);
        Assert.Equal(Money.FromDecimal(52000), sheet.TotalAssets);
    }

    [Fact]
    public void Unbalanced_entries_can_never_be_posted_through_the_service()
    {
        using TestHost host = TestHost.Create().Setup();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        var draft = new JournalDraft
        {
            EntryDate = DateTime.Today,
            Description = "ভারসাম্যহীন এন্ট্রি",
            Lines =
            [
                JournalLineDraft.Dr(a, Money.FromDecimal(100)),
                JournalLineDraft.Cr(b, Money.FromDecimal(99))
            ]
        };

        Assert.Throws<InvalidOperationException>(
            () => host.Get<IAccountingService>().PostAsync(draft).GetAwaiter().GetResult());
    }

    [Fact]
    public void All_reconciliation_checks_pass_on_a_realistic_day()
    {
        using TestHost host = TestHost.Create().Setup(openingCash: Money.FromDecimal(30000));
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        (Guid cashId, Guid bankId) = CreateBankAccountAsync(host).GetAwaiter().GetResult();

        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();
        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(3000), PaymentMethod.Cash)).GetAwaiter().GetResult();
        host.Get<ILedgerOperationsService>().TransferAsync(new TransferRequest(
            DateTime.Today, cashId, bankId, Money.FromDecimal(10000))).GetAwaiter().GetResult();

        IReadOnlyList<ReconciliationCheck> checks = host.Get<IReportingService>()
            .RunReconciliationAsync().GetAwaiter().GetResult();

        Assert.Equal(8, checks.Count);
        foreach (ReconciliationCheck check in checks)
        {
            Assert.True(check.Passed,
                $"{check.Name}: expected {check.Expected.ToDecimal():N2}, got {check.Actual.ToDecimal():N2}");
        }
    }

    [Fact]
    public void Dashboard_totals_match_the_reports()
    {
        using TestHost host = TestHost.Create().Setup(openingCash: Money.FromDecimal(10000));
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(1500), batchId);

        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();
        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(1500), PaymentMethod.Cash)).GetAwaiter().GetResult();

        DashboardMetrics metrics = host.Get<IDashboardService>().GetMetricsAsync().GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(1500), metrics.TodaysCollection);
        Assert.Equal(Money.FromDecimal(1500), metrics.MonthCollection);
        Assert.Equal(Money.FromDecimal(500), metrics.OutstandingFees);
        Assert.Equal(Money.FromDecimal(11500), metrics.CashBalance);
        Assert.Equal(1, metrics.ActiveStudents);
        Assert.Equal(1, metrics.ActiveBatches);
    }
}
