using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Dto;

public sealed record StudentDuesRow(
    Guid ChargeId,
    Guid StudentId,
    string? BatchName,
    FeeChargeKind Kind,
    string Description,
    string? PeriodKey,
    DateTime ChargeDate,
    DateTime DueDate,
    Money GrossAmount,
    Money DiscountAmount,
    Money NetAmount,
    Money PaidAmount,
    Money WaivedAmount,
    Money Outstanding)
{
    public static StudentDuesRow FromCharge(FeeCharge charge, string? batchName) => new(
        charge.Id, charge.StudentId, batchName, charge.Kind, charge.Description, charge.PeriodKey,
        charge.ChargeDate, charge.DueDate, charge.GrossAmount, charge.DiscountAmount, charge.NetAmount,
        charge.PaidAmount, charge.WaivedAmount, charge.Outstanding);
}

public sealed record StudentBalanceSummary(
    Guid StudentId,
    Money TotalCharged,
    Money TotalDiscount,
    Money TotalPaid,
    Money TotalWaived,
    Money Outstanding,
    Money CreditBalance)
{
    /// <summary>What the student would need to pay today, after applying available advance credit.</summary>
    public Money PayableAfterCredit => Outstanding - CreditBalance > Money.Zero
        ? Outstanding - CreditBalance
        : Money.Zero;
}

public sealed record PaymentListItem(
    Guid PaymentId,
    string ReceiptNumber,
    Guid StudentId,
    string StudentCode,
    string StudentName,
    DateTime PaymentDate,
    Money AmountReceived,
    Money AmountAllocated,
    Money Unallocated,
    PaymentMethod Method,
    PaymentStatus Status,
    string? TransactionReference,
    string CollectedBy);

public sealed record CreditLedgerRow(
    Guid TransactionId,
    CreditTransactionKind Kind,
    Money Amount,
    Money RunningBalance,
    DateTime TransactionDate,
    string? Description,
    string? Reference);

/// <summary>Everything a fee receipt needs, assembled once and reused for preview, print and reprint.</summary>
public sealed class ReceiptData
{
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime PaymentDate { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string? GuardianName { get; init; }
    public string? ClassOrBatch { get; init; }
    public Money AmountReceived { get; init; }
    public Money AmountAllocated { get; init; }
    public Money AdvanceCredit { get; init; }
    public Money PreviousOutstanding { get; init; }
    public Money RemainingOutstanding { get; init; }
    public Money CreditBalance { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string? TransactionReference { get; init; }
    public string CollectedBy { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public IReadOnlyList<ReceiptLine> Lines { get; init; } = Array.Empty<ReceiptLine>();
    public int ReprintCount { get; init; }
    public bool IsReprint { get; init; }
    public string InstitutionName { get; init; } = string.Empty;
    public string? InstitutionAddress { get; init; }
    public string? InstitutionPhone { get; init; }
    public string? InstitutionEmail { get; init; }
    public string CurrencySymbol { get; init; } = "৳";
}

public sealed record ReceiptLine(string Description, string? PeriodKey, Money Amount);
