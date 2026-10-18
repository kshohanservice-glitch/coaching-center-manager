namespace ShikkhaDesk.Core.Entities;

public sealed class FeePlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CourseId { get; set; }
    public Guid? ClassGradeId { get; set; }
    public Guid? BatchId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class FeePlanItem
{
    public Guid Id { get; set; }
    public Guid FeePlanId { get; set; }
    public FeeChargeKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public Money Amount { get; set; }
    /// <summary>For recurring monthly fees: 1 = monthly, 3 = quarterly, 12 = yearly, 0 = one-off.</summary>
    public int RecurrenceMonths { get; set; } = 1;
    public int? InstallmentCount { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// A billing run. Assessments are idempotent: the (FeePlan, PeriodKey, Student) triple is unique so
/// re-running the same assessment can never create duplicate charges.
/// </summary>
public sealed class FeeAssessment
{
    public Guid Id { get; set; }
    public string AssessmentNumber { get; set; } = string.Empty;
    public string? PeriodKey { get; set; }
    public Guid? FeePlanId { get; set; }
    public Guid? BatchId { get; set; }
    public DateTime AssessmentDate { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class FeeCharge
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? EnrollmentId { get; set; }
    public Guid? AssessmentId { get; set; }
    public FeeChargeKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>YYYY-MM for recurring charges; null for one-off charges.</summary>
    public string? PeriodKey { get; set; }
    public DateTime ChargeDate { get; set; }
    public DateTime DueDate { get; set; }
    public Money GrossAmount { get; set; }
    public Money DiscountAmount { get; set; }
    public Money NetAmount { get; set; }
    public Money PaidAmount { get; set; }
    public Money WaivedAmount { get; set; }
    public bool IsVoid { get; set; }
    public Guid? VoidedByChargeId { get; set; }

    /// <summary>
    /// Deterministic key (student + plan + kind + period) that makes bulk assessment idempotent.
    /// Backed by a unique index so a repeated run can never create a duplicate charge.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Outstanding balance: net amount less allocated payments and waivers.</summary>
    public Money Outstanding => IsVoid ? Money.Zero : NetAmount - PaidAmount - WaivedAmount;
}

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public Money AmountReceived { get; set; }
    public Money AmountAllocated { get; set; }
    public Money AmountRefunded { get; set; }
    public PaymentMethod Method { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Posted;
    public Guid CollectedByUserId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? ReversedByPaymentId { get; set; }
    /// <summary>Client-supplied idempotency key; unique per student so retries cannot double-charge.</summary>
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Money received but not yet allocated to any charge — available advance credit.</summary>
    public Money Unallocated => AmountReceived - AmountAllocated - AmountRefunded;
}

public sealed class PaymentAllocation
{
    public Guid Id { get; set; }

    /// <summary>Null when the row records an advance-credit application rather than a cash payment.</summary>
    public Guid? PaymentId { get; set; }
    public Guid FeeChargeId { get; set; }
    public Money Amount { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
    public Guid? CreditTransactionId { get; set; }
}

/// <summary>Immutable movement in the student advance-credit ledger.</summary>
public sealed class StudentCreditTransaction
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public CreditTransactionKind Kind { get; set; }
    public Money Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? AllocationId { get; set; }
    public Guid? RefundId { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Signed effect on the student's available credit balance.</summary>
    public Money SignedAmount => Kind switch
    {
        CreditTransactionKind.Received => Amount,
        CreditTransactionKind.Adjusted => Amount,
        CreditTransactionKind.Allocated => -Amount,
        CreditTransactionKind.Refunded => -Amount,
        _ => Money.Zero
    };
}

public sealed class Refund
{
    public Guid Id { get; set; }
    public string RefundNumber { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTime RefundDate { get; set; }
    public Money Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? JournalEntryId { get; set; }
    public Guid ProcessedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Receipt numbering sequence. Rows are created per (prefix, period) and advanced inside the same
/// database transaction as the payment, which makes numbering concurrency-safe.
/// </summary>
public sealed class ReceiptSequence
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string PeriodKey { get; set; } = string.Empty;
    public long LastNumber { get; set; }
}

public sealed class Receipt
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string DocumentKind { get; set; } = "Fee";
    public Guid? StudentId { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTime IssuedAt { get; set; }
    public int ReprintCount { get; set; }
    public DateTime? LastReprintedAt { get; set; }
    public string? PayloadJson { get; set; }
}
