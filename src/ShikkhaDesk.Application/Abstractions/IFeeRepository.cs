using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Abstractions;

public interface IFeeRepository
{
    // Fee plans
    Task<IReadOnlyList<FeePlan>> GetFeePlansAsync(CancellationToken ct = default);
    Task<FeePlan?> GetFeePlanAsync(Guid id, CancellationToken ct = default);
    Task InsertFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items, CancellationToken ct = default);
    Task UpdateFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items, CancellationToken ct = default);
    Task<IReadOnlyList<FeePlanItem>> GetFeePlanItemsAsync(Guid planId, CancellationToken ct = default);

    // Assessments & charges
    Task InsertAssessmentAsync(FeeAssessment assessment, CancellationToken ct = default);
    Task<IReadOnlyList<FeeAssessment>> GetAssessmentsAsync(DateRange range, CancellationToken ct = default);
    Task<FeeCharge?> GetChargeAsync(Guid chargeId, CancellationToken ct = default);
    Task<IReadOnlyList<FeeCharge>> GetChargesForStudentAsync(Guid studentId, bool includeVoid = false,
        CancellationToken ct = default);
    Task<IReadOnlyList<FeeCharge>> GetChargesForBatchPeriodAsync(Guid batchId, string periodKey,
        CancellationToken ct = default);
    Task InsertChargesAsync(IReadOnlyList<FeeCharge> charges, CancellationToken ct = default);
    Task UpdateChargeAsync(FeeCharge charge, CancellationToken ct = default);
    Task UpdateChargesAsync(IReadOnlyList<FeeCharge> charges, CancellationToken ct = default);
    Task<IReadOnlyList<FeeCharge>> GetOutstandingChargesAsync(Guid? batchId = null, Guid? studentId = null,
        DateTime? asOf = null, CancellationToken ct = default);
    Task<bool> AssessmentExistsAsync(Guid? feePlanId, string? periodKey, Guid studentId, FeeChargeKind kind,
        CancellationToken ct = default);

    // Payments
    Task<Payment?> GetPaymentAsync(Guid id, CancellationToken ct = default);
    Task<Payment?> GetPaymentByReceiptNumberAsync(string receiptNumber, CancellationToken ct = default);
    Task<Payment?> GetPaymentByIdempotencyKeyAsync(Guid studentId, string idempotencyKey,
        CancellationToken ct = default);
    Task InsertPaymentAsync(Payment payment, CancellationToken ct = default);
    Task UpdatePaymentAsync(Payment payment, CancellationToken ct = default);
    Task InsertAllocationsAsync(IReadOnlyList<PaymentAllocation> allocations, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentAllocation>> GetAllocationsForPaymentAsync(Guid paymentId,
        CancellationToken ct = default);
    Task<IReadOnlyList<PaymentAllocation>> GetAllocationsForChargeAsync(Guid chargeId,
        CancellationToken ct = default);
    Task<PagedResult<PaymentListItem>> SearchPaymentsAsync(PaymentQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetPaymentsForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<Money> SumCollectionsAsync(DateRange range, CancellationToken ct = default);

    // Credit
    Task InsertCreditTransactionAsync(StudentCreditTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<StudentCreditTransaction>> GetCreditTransactionsAsync(Guid studentId,
        CancellationToken ct = default);
    Task LinkCreditTransactionJournalAsync(Guid creditTransactionId, Guid journalEntryId,
        CancellationToken ct = default);
    Task<Money> GetCreditBalanceAsync(Guid studentId, CancellationToken ct = default);
    Task<Money> GetTotalCreditLiabilityAsync(CancellationToken ct = default);

    // Refunds
    Task InsertRefundAsync(Refund refund, CancellationToken ct = default);
    Task<IReadOnlyList<Refund>> GetRefundsAsync(DateRange range, CancellationToken ct = default);

    // Receipts
    Task<Receipt?> GetReceiptAsync(Guid id, CancellationToken ct = default);
    Task<Receipt?> GetReceiptByNumberAsync(string receiptNumber, CancellationToken ct = default);
    Task InsertReceiptAsync(Receipt receipt, CancellationToken ct = default);
    Task UpdateReceiptAsync(Receipt receipt, CancellationToken ct = default);
}
