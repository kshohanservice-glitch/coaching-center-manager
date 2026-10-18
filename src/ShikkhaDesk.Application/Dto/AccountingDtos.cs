using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Dto;

public sealed record AccountBalanceRow(Guid AccountId, string Code, string Name, AccountType Type,
    Money DebitTotal, Money CreditTotal)
{
    /// <summary>Natural-sign balance: debit-normal accounts (assets, expenses) show debit minus credit.</summary>
    public Money SignedBalance => Type is AccountType.Asset or AccountType.Expense
        ? DebitTotal - CreditTotal
        : CreditTotal - DebitTotal;
}

public sealed record TrialBalanceRow(string Code, string Name, AccountType Type, Money Debit, Money Credit);

public sealed record LedgerEntryRow(
    Guid JournalEntryId,
    string JournalNumber,
    DateTime EntryDate,
    string Description,
    TransactionSourceKind SourceKind,
    Money Debit,
    Money Credit,
    Money RunningBalance,
    Guid? SourceId,
    string? SourceReference);

public sealed record JournalEntryDto(JournalEntry Entry, IReadOnlyList<JournalLine> Lines);

public sealed record CashbookRow(
    DateTime EntryDate,
    string VoucherNumber,
    string Description,
    Money Receipt,
    Money Payment,
    Money RunningBalance,
    string? Reference);

public sealed record FinancialAccountBalance(Guid FinancialAccountId, string Name, FinancialAccountKind Kind,
    Money OpeningBalance, Money Balance)
{
    public bool IsCash => Kind == FinancialAccountKind.Cash;
}

public sealed record IncomeExpenseRow(DateTime Date, string Number, string Description, string Category,
    Money Amount, string? Reference);

public sealed record PayableRow(Guid PayableId, string BillNumber, string SupplierName, DateTime BillDate,
    DateTime? DueDate, Money Amount, Money PaidAmount, Money Outstanding, PayableStatus Status);

public sealed record PayrollRow(Guid PayrollRecordId, string StaffCode, string StaffName, string Designation,
    Money BasicSalary, Money Allowances, Money Deductions, Money NetSalary, PayrollStatus Status,
    DateTime? PaymentDate);

public sealed record ProfitAndLossRow(string Group, string AccountName, Money Amount);

public sealed record BalanceSheetRow(string Group, string AccountName, Money Amount);

/// <summary>Result of a reconciliation check. Every check reports the two sides so a mismatch is visible.</summary>
public sealed record ReconciliationCheck(string Name, Money Expected, Money Actual)
{
    public Money Difference => Expected - Actual;
    public bool Passed => Difference.IsZero;
}
