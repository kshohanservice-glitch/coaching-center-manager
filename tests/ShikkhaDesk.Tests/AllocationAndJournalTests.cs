using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Services;
using Xunit;

namespace ShikkhaDesk.Tests;

public sealed class AllocationAndJournalTests
{
    private static readonly DateTime Today = new(2026, 3, 10);

    private static AccountMap Map() => new()
    {
        StudentReceivables = Guid.Parse("00000000-0000-4000-8000-000000001400"),
        StudentAdvanceCredit = Guid.Parse("00000000-0000-4000-8000-000000002100"),
        TuitionIncome = Guid.Parse("00000000-0000-4000-8000-000000004100"),
        AdmissionIncome = Guid.Parse("00000000-0000-4000-8000-000000004200"),
        CourseIncome = Guid.Parse("00000000-0000-4000-8000-000000004300"),
        ExamIncome = Guid.Parse("00000000-0000-4000-8000-000000004400"),
        OtherIncome = Guid.Parse("00000000-0000-4000-8000-000000004900"),
        AccountsPayable = Guid.Parse("00000000-0000-4000-8000-000000002200"),
        SalaryPayable = Guid.Parse("00000000-0000-4000-8000-000000002300"),
        SalaryExpense = Guid.Parse("00000000-0000-4000-8000-000000005100"),
        BankChargeExpense = Guid.Parse("00000000-0000-4000-8000-000000005900"),
        OpeningBalanceEquity = Guid.Parse("00000000-0000-4000-8000-000000003100")
    };

    [Fact]
    public void Oldest_first_allocation_pays_the_oldest_due_first()
    {
        var targets = new List<AllocationTarget>
        {
            new(Guid.NewGuid(), Money.FromDecimal(500), Today.AddDays(-10), Today.AddDays(-40), 0),
            new(Guid.NewGuid(), Money.FromDecimal(500), Today.AddDays(-2), Today.AddDays(-10), 1)
        };

        AllocationResult result = PaymentAllocator.Allocate(Money.FromDecimal(300), targets,
            AllocationStrategy.OldestFirst);

        Assert.Single(result.Allocations);
        Assert.Equal(targets[0].ChargeId, result.Allocations[0].ChargeId);
        Assert.Equal(Money.FromDecimal(300), result.Allocations[0].Amount);
        Assert.True(result.Unallocated.IsZero);
    }

    [Fact]
    public void Allocation_never_exceeds_what_is_available()
    {
        var targets = new List<AllocationTarget>
        {
            new(Guid.NewGuid(), Money.FromDecimal(500), Today, Today, 0)
        };

        AllocationResult result = PaymentAllocator.Allocate(Money.FromDecimal(900), targets,
            AllocationStrategy.OldestFirst);

        Assert.Equal(Money.FromDecimal(500), result.Allocations[0].Amount);
        Assert.Equal(Money.FromDecimal(400), result.Unallocated);
        Assert.Equal(Money.FromDecimal(900), result.Allocations[0].Amount + result.Unallocated);
    }

    [Fact]
    public void Manual_allocation_rejects_more_than_the_charge_owes()
    {
        Guid charge = Guid.NewGuid();
        var targets = new List<AllocationTarget> { new(charge, Money.FromDecimal(200), Today, Today, 0) };

        Assert.Throws<InvalidOperationException>(() => PaymentAllocator.Allocate(
            Money.FromDecimal(1000), targets, AllocationStrategy.Manual, [(charge, Money.FromDecimal(500))]));
    }

    [Fact]
    public void Manual_allocation_rejects_more_than_the_money_received()
    {
        Guid charge = Guid.NewGuid();
        var targets = new List<AllocationTarget> { new(charge, Money.FromDecimal(2000), Today, Today, 0) };

        Assert.Throws<InvalidOperationException>(() => PaymentAllocator.Allocate(
            Money.FromDecimal(100), targets, AllocationStrategy.Manual, [(charge, Money.FromDecimal(200))]));
    }

    [Fact]
    public void Pro_rata_allocation_spreads_proportionally_and_keeps_the_total()
    {
        var targets = new List<AllocationTarget>
        {
            new(Guid.NewGuid(), Money.FromDecimal(1000), Today, Today, 0),
            new(Guid.NewGuid(), Money.FromDecimal(1000), Today, Today, 1)
        };

        AllocationResult result = PaymentAllocator.Allocate(Money.FromDecimal(500), targets,
            AllocationStrategy.ProRata);

        Assert.Equal(2, result.Allocations.Count);
        Assert.Equal(Money.FromDecimal(500), result.Allocations[0].Amount + result.Allocations[1].Amount);
        Assert.True(result.Unallocated.IsZero);
    }

    [Fact]
    public void Fee_assessment_recognises_receivable_and_income_together()
    {
        AccountMap map = Map();
        Guid student = Guid.NewGuid();

        JournalDraft draft = JournalBuilder.ForFeeAssessment(Guid.NewGuid(), "AS-0001", Today,
        [
            new JournalBuilder.FeeAssessmentLine(student, FeeChargeKind.MonthlyTuition, Money.FromDecimal(2000),
                "মাসিক বেতন")
        ], map);

        draft.Validate();
        Assert.True(draft.IsBalanced);
        Assert.Equal(Money.FromDecimal(2000), draft.TotalDebit);
        Assert.Equal(Money.FromDecimal(2000), draft.TotalCredit);
        Assert.Contains(draft.Lines, l => l.AccountId == map.StudentReceivables && l.Debit == Money.FromDecimal(2000));
        Assert.Contains(draft.Lines, l => l.AccountId == map.TuitionIncome && l.Credit == Money.FromDecimal(2000));
    }

    [Fact]
    public void Payment_splits_between_receivable_and_advance_credit()
    {
        AccountMap map = Map();
        Guid cashAccount = Guid.NewGuid();
        Guid cashFinancial = Guid.NewGuid();
        Guid student = Guid.NewGuid();

        JournalDraft draft = JournalBuilder.ForFeePayment(Guid.NewGuid(), "RCP-1", Today, student,
            Money.FromDecimal(2500), Money.FromDecimal(2000), cashAccount, cashFinancial, map);

        draft.Validate();
        Assert.Equal(Money.FromDecimal(2500), draft.TotalDebit);
        Assert.Equal(Money.FromDecimal(2500), draft.TotalCredit);
        // Allocated part clears the receivable; the rest becomes a liability, never income.
        Assert.Contains(draft.Lines, l => l.AccountId == map.StudentReceivables && l.Credit == Money.FromDecimal(2000));
        Assert.Contains(draft.Lines,
            l => l.AccountId == map.StudentAdvanceCredit && l.Credit == Money.FromDecimal(500));
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == map.TuitionIncome);
    }

    [Fact]
    public void Credit_allocation_settles_receivable_without_moving_cash()
    {
        AccountMap map = Map();
        Guid cashAccount = Guid.NewGuid();

        JournalDraft draft = JournalBuilder.ForCreditAllocation(Guid.NewGuid(), Today, Guid.NewGuid(),
            Money.FromDecimal(700), map);

        draft.Validate();
        Assert.Equal(Money.FromDecimal(700), draft.TotalDebit);
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == cashAccount);
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == map.TuitionIncome);
    }

    [Fact]
    public void Transfer_never_touches_income_or_expense()
    {
        AccountMap map = Map();
        Guid cash = Guid.NewGuid();
        Guid bank = Guid.NewGuid();

        JournalDraft draft = JournalBuilder.ForTransfer(Guid.NewGuid(), "TRF-1", Today, Money.FromDecimal(5000),
            Money.FromDecimal(50), cash, Guid.NewGuid(), bank, Guid.NewGuid(), map);

        draft.Validate();
        Assert.True(draft.IsBalanced);
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == map.TuitionIncome);
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == map.OtherIncome);
        // Bank charge is the only non-transfer component and is posted as an expense.
        Assert.Contains(draft.Lines, l => l.AccountId == map.BankChargeExpense && l.Debit == Money.FromDecimal(50));
    }

    [Fact]
    public void Payable_settlement_does_not_recognise_expense_again()
    {
        AccountMap map = Map();
        Guid expenseAccount = Guid.NewGuid();
        Guid cash = Guid.NewGuid();

        JournalDraft accrual = JournalBuilder.ForExpenseAccrual(Guid.NewGuid(), "EXP-1", Today,
            Money.FromDecimal(3000), expenseAccount, null, "ভাড়া", map);
        JournalDraft settlement = JournalBuilder.ForPayableSettlement(Guid.NewGuid(), "PAY-1", Today,
            Money.FromDecimal(3000), cash, Guid.NewGuid(), null, map);

        accrual.Validate();
        settlement.Validate();

        Assert.Contains(accrual.Lines, l => l.AccountId == expenseAccount && l.Debit == Money.FromDecimal(3000));
        Assert.DoesNotContain(settlement.Lines, l => l.AccountId == expenseAccount);
        Assert.Contains(settlement.Lines, l => l.AccountId == map.AccountsPayable && l.Debit == Money.FromDecimal(3000));
    }

    [Fact]
    public void Salary_payment_does_not_recognise_salary_expense_again()
    {
        AccountMap map = Map();
        Guid staff = Guid.NewGuid();
        Guid cash = Guid.NewGuid();

        JournalDraft accrual = JournalBuilder.ForPayrollAccrual(Guid.NewGuid(), Today, staff,
            Money.FromDecimal(25000), "করিম", map);
        JournalDraft payment = JournalBuilder.ForPayrollPayment(Guid.NewGuid(), Today, staff,
            Money.FromDecimal(25000), cash, Guid.NewGuid(), "করিম", map);

        accrual.Validate();
        payment.Validate();

        Assert.Contains(accrual.Lines, l => l.AccountId == map.SalaryExpense && l.Debit == Money.FromDecimal(25000));
        Assert.DoesNotContain(payment.Lines, l => l.AccountId == map.SalaryExpense);
        Assert.Contains(payment.Lines, l => l.AccountId == map.SalaryPayable && l.Debit == Money.FromDecimal(25000));
    }

    [Fact]
    public void Refund_of_allocated_money_restores_the_receivable_and_is_never_an_expense()
    {
        AccountMap map = Map();
        Guid cash = Guid.NewGuid();

        JournalDraft draft = JournalBuilder.ForRefund(Guid.NewGuid(), "RFN-1", Today, Guid.NewGuid(),
            Money.FromDecimal(1000), Money.Zero, Money.FromDecimal(1000), cash, Guid.NewGuid(),
            map.StudentReceivables, map);

        draft.Validate();
        Assert.Contains(draft.Lines,
            l => l.AccountId == map.StudentReceivables && l.Debit == Money.FromDecimal(1000));
        // A refund never creates an expense and never touches income: the fee is simply owed again.
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == map.SalaryExpense);
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == map.TuitionIncome);
    }

    [Fact]
    public void Refund_can_split_between_credit_and_allocated_money()
    {
        AccountMap map = Map();
        Guid cash = Guid.NewGuid();

        JournalDraft draft = JournalBuilder.ForRefund(Guid.NewGuid(), "RFN-3", Today, Guid.NewGuid(),
            Money.FromDecimal(1500), Money.FromDecimal(600), Money.FromDecimal(900), cash, Guid.NewGuid(),
            map.StudentReceivables, map);

        draft.Validate();
        Assert.Equal(Money.FromDecimal(1500), draft.TotalDebit);
        Assert.Equal(Money.FromDecimal(1500), draft.TotalCredit);
        Assert.Contains(draft.Lines,
            l => l.AccountId == map.StudentAdvanceCredit && l.Debit == Money.FromDecimal(600));
        Assert.Contains(draft.Lines,
            l => l.AccountId == map.StudentReceivables && l.Debit == Money.FromDecimal(900));
    }

    [Fact]
    public void Refund_of_unallocated_credit_releases_the_liability()
    {
        AccountMap map = Map();
        Guid cash = Guid.NewGuid();

        JournalDraft draft = JournalBuilder.ForRefund(Guid.NewGuid(), "RFN-2", Today, Guid.NewGuid(),
            Money.FromDecimal(1000), Money.FromDecimal(1000), Money.Zero, cash, Guid.NewGuid(),
            map.StudentReceivables, map);

        draft.Validate();
        Assert.Contains(draft.Lines,
            l => l.AccountId == map.StudentAdvanceCredit && l.Debit == Money.FromDecimal(1000));
        Assert.DoesNotContain(draft.Lines, l => l.AccountId == map.TuitionIncome);
    }

    [Fact]
    public void Unbalanced_drafts_can_never_be_validated()
    {
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        var unbalanced = new JournalDraft
        {
            EntryDate = Today,
            Description = "ভারসাম্যহীন",
            Lines =
            [
                JournalLineDraft.Dr(a, Money.FromDecimal(100)),
                JournalLineDraft.Cr(b, Money.FromDecimal(90))
            ]
        };

        Assert.False(unbalanced.IsBalanced);
        Assert.Throws<InvalidOperationException>(() => unbalanced.Validate());
    }

    [Fact]
    public void Single_line_draft_is_rejected()
    {
        var draft = new JournalDraft
        {
            EntryDate = Today,
            Description = "এক লাইন",
            Lines = [JournalLineDraft.Dr(Guid.NewGuid(), Money.FromDecimal(100))]
        };

        Assert.Throws<InvalidOperationException>(() => draft.Validate());
    }

    [Fact]
    public void Reversal_mirrors_the_original_entry_and_stays_balanced()
    {
        Guid receivable = Guid.NewGuid();
        Guid income = Guid.NewGuid();

        JournalDraft original = new()
        {
            EntryDate = Today,
            Description = "মূল",
            Lines =
            [
                JournalLineDraft.Dr(receivable, Money.FromDecimal(1500)),
                JournalLineDraft.Cr(income, Money.FromDecimal(1500))
            ]
        };

        JournalDraft reversal = JournalBuilder.ForReversal(Guid.NewGuid(), "JV-1", Today,
            original.Lines.Select(l => (l.AccountId, l.Debit, l.Credit, l.FinancialAccountId, l.StudentId,
                l.SupplierId, l.StaffId)).ToList(), "ভুল এন্ট্রি");

        reversal.Validate();
        Assert.Contains(reversal.Lines, l => l.AccountId == receivable && l.Credit == Money.FromDecimal(1500));
        Assert.Contains(reversal.Lines, l => l.AccountId == income && l.Debit == Money.FromDecimal(1500));
    }
}
