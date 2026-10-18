namespace ShikkhaDesk.Core.Entities;

public sealed class Account
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>True when the account physically holds money (cash, bank, mobile banking).</summary>
    public bool IsCashEquivalent { get; set; }
}

public sealed class FinancialAccount
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FinancialAccountKind Kind { get; set; }
    public Guid AccountId { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Branch { get; set; }
    public Money OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class JournalEntry
{
    public Guid Id { get; set; }
    public string JournalNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionSourceKind SourceKind { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceReference { get; set; }
    public JournalStatus Status { get; set; } = JournalStatus.Posted;
    public Guid? ReversalOfJournalId { get; set; }
    public Guid? ReversedByJournalId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class JournalLine
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public Money Debit { get; set; }
    public Money Credit { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? StaffId { get; set; }
    public string? Memo { get; set; }
    public int LineOrder { get; set; }
}

public sealed class Transfer
{
    public Guid Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public Guid FromFinancialAccountId { get; set; }
    public Guid ToFinancialAccountId { get; set; }
    public Money Amount { get; set; }
    public Money Fee { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class IncomeRecord
{
    public Guid Id { get; set; }
    public string IncomeNumber { get; set; } = string.Empty;
    public DateTime IncomeDate { get; set; }
    public Guid IncomeAccountId { get; set; }
    public Guid FinancialAccountId { get; set; }
    public Money Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Reference { get; set; }
    public string? AttachmentPath { get; set; }
    public bool IsApproved { get; set; } = true;
    public Guid? JournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ExpenseRecord
{
    public Guid Id { get; set; }
    public string ExpenseNumber { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public Guid ExpenseAccountId { get; set; }
    public Money Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? Reference { get; set; }
    public string? AttachmentPath { get; set; }
    public bool IsApproved { get; set; } = true;
    /// <summary>True when the expense was accrued as a payable rather than paid immediately.</summary>
    public bool IsAccrued { get; set; }
    public Guid? PayableId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Supplier
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public Money OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class Payable
{
    public Guid Id { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public DateTime BillDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Money Amount { get; set; }
    public Money PaidAmount { get; set; }
    public PayableStatus Status { get; set; } = PayableStatus.Open;
    public string? Description { get; set; }
    public Guid? AccrualJournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Money Outstanding => Amount - PaidAmount;
}

public sealed class PayableSettlement
{
    public Guid Id { get; set; }
    public string SettlementNumber { get; set; } = string.Empty;
    public Guid PayableId { get; set; }
    public DateTime SettlementDate { get; set; }
    public Money Amount { get; set; }
    public Guid FinancialAccountId { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PayrollPeriod
{
    public Guid Id { get; set; }
    /// <summary>YYYY-MM.</summary>
    public string PeriodKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? PaymentDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PayrollRecord
{
    public Guid Id { get; set; }
    public Guid PayrollPeriodId { get; set; }
    public Guid StaffId { get; set; }
    public Money BasicSalary { get; set; }
    public Money Allowances { get; set; }
    public Money Deductions { get; set; }
    public Money NetSalary { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Accrued;
    public Guid? AccrualJournalEntryId { get; set; }
    public Guid? PaymentJournalEntryId { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public DateTime? PaymentDate { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
