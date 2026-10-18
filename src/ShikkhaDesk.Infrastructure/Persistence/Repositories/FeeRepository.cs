using Dapper;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Infrastructure.Persistence.Repositories;

public sealed class FeeRepository : RepositoryBase, IFeeRepository
{
    public FeeRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

    // ------------------------------------------------------------------ fee plans

    public async Task<IReadOnlyList<FeePlan>> GetFeePlansAsync(CancellationToken ct = default) =>
        (await QueryAsync<FeePlan>(
            @"SELECT id, name, course_id, class_grade_id, batch_id, effective_from, effective_to, is_active, created_at_utc
                FROM fee_plans ORDER BY is_active DESC, name;", null, ct).ConfigureAwait(false)).ToList();

    public Task<FeePlan?> GetFeePlanAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<FeePlan>(
            @"SELECT id, name, course_id, class_grade_id, batch_id, effective_from, effective_to, is_active, created_at_utc
                FROM fee_plans WHERE id = @Id;", new { Id = id }, ct);

    public async Task InsertFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items, CancellationToken ct = default)
    {
        await ExecuteAsync(@"
            INSERT INTO fee_plans (id, name, course_id, class_grade_id, batch_id, effective_from, effective_to,
                                   is_active, created_at_utc)
            VALUES (@Id, @Name, @CourseId, @ClassGradeId, @BatchId, @EffectiveFrom, @EffectiveTo, @IsActive, @CreatedAtUtc);",
            plan, ct).ConfigureAwait(false);

        foreach (FeePlanItem item in items)
        {
            await ExecuteAsync(@"
                INSERT INTO fee_plan_items (id, fee_plan_id, kind, description, amount, recurrence_months,
                                            installment_count, sort_order)
                VALUES (@Id, @FeePlanId, @Kind, @Description, @Amount, @RecurrenceMonths, @InstallmentCount, @SortOrder);",
                item, ct).ConfigureAwait(false);
        }
    }

    public async Task UpdateFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items, CancellationToken ct = default)
    {
        await ExecuteAsync(@"
            UPDATE fee_plans SET name = @Name, course_id = @CourseId, class_grade_id = @ClassGradeId,
                   batch_id = @BatchId, effective_from = @EffectiveFrom, effective_to = @EffectiveTo,
                   is_active = @IsActive
             WHERE id = @Id;", plan, ct).ConfigureAwait(false);

        await ExecuteAsync("DELETE FROM fee_plan_items WHERE fee_plan_id = @FeePlanId;",
            new { FeePlanId = plan.Id }, ct).ConfigureAwait(false);

        foreach (FeePlanItem item in items)
        {
            await ExecuteAsync(@"
                INSERT INTO fee_plan_items (id, fee_plan_id, kind, description, amount, recurrence_months,
                                            installment_count, sort_order)
                VALUES (@Id, @FeePlanId, @Kind, @Description, @Amount, @RecurrenceMonths, @InstallmentCount, @SortOrder);",
                item, ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<FeePlanItem>> GetFeePlanItemsAsync(Guid planId, CancellationToken ct = default) =>
        (await QueryAsync<FeePlanItem>(
            @"SELECT id, fee_plan_id, kind, description, amount, recurrence_months, installment_count, sort_order
                FROM fee_plan_items WHERE fee_plan_id = @PlanId ORDER BY sort_order, id;",
            new { PlanId = planId }, ct).ConfigureAwait(false)).ToList();

    // ------------------------------------------------------------------ assessments & charges

    private const string ChargeColumns = @"
        id, student_id, batch_id, enrollment_id, assessment_id, kind, description, period_key, charge_date,
        due_date, gross_amount, discount_amount, net_amount, paid_amount, waived_amount, is_void,
        voided_by_charge_id, idempotency_key, created_at_utc";

    public Task InsertAssessmentAsync(FeeAssessment assessment, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO fee_assessments (id, assessment_number, period_key, fee_plan_id, batch_id,
                                         assessment_date, journal_entry_id, description, created_by_user_id,
                                         created_at_utc)
            VALUES (@Id, @AssessmentNumber, @PeriodKey, @FeePlanId, @BatchId, @AssessmentDate, @JournalEntryId,
                    @Description, @CreatedByUserId, @CreatedAtUtc);", assessment, ct);

    public async Task<IReadOnlyList<FeeAssessment>> GetAssessmentsAsync(DateRange range, CancellationToken ct = default) =>
        (await QueryAsync<FeeAssessment>(
            @"SELECT id, assessment_number, period_key, fee_plan_id, batch_id, assessment_date, journal_entry_id,
                     description, created_by_user_id, created_at_utc
                FROM fee_assessments WHERE assessment_date BETWEEN @From AND @To ORDER BY assessment_date DESC;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false)).ToList();

    public Task<FeeCharge?> GetChargeAsync(Guid chargeId, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<FeeCharge>(
            $"SELECT {ChargeColumns} FROM fee_charges WHERE id = @Id;", new { Id = chargeId }, ct);

    public async Task<IReadOnlyList<FeeCharge>> GetChargesForStudentAsync(Guid studentId, bool includeVoid = false,
        CancellationToken ct = default) =>
        (await QueryAsync<FeeCharge>(
            $"SELECT {ChargeColumns} FROM fee_charges WHERE student_id = @StudentId AND (@IncludeVoid = 1 OR is_void = 0) ORDER BY due_date, created_at_utc;",
            new { StudentId = studentId, IncludeVoid = includeVoid ? 1 : 0 }, ct).ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<FeeCharge>> GetChargesForBatchPeriodAsync(Guid batchId, string periodKey,
        CancellationToken ct = default) =>
        (await QueryAsync<FeeCharge>(
            $"SELECT {ChargeColumns} FROM fee_charges WHERE batch_id = @BatchId AND period_key = @PeriodKey;",
            new { BatchId = batchId, PeriodKey = periodKey }, ct).ConfigureAwait(false)).ToList();

    public async Task InsertChargesAsync(IReadOnlyList<FeeCharge> charges, CancellationToken ct = default)
    {
        foreach (FeeCharge charge in charges)
        {
            await ExecuteAsync(@"
                INSERT INTO fee_charges (id, student_id, batch_id, enrollment_id, assessment_id, kind, description,
                                         period_key, charge_date, due_date, gross_amount, discount_amount,
                                         net_amount, paid_amount, waived_amount, is_void, voided_by_charge_id,
                                         idempotency_key, created_at_utc)
                VALUES (@Id, @StudentId, @BatchId, @EnrollmentId, @AssessmentId, @Kind, @Description, @PeriodKey,
                        @ChargeDate, @DueDate, @GrossAmount, @DiscountAmount, @NetAmount, @PaidAmount,
                        @WaivedAmount, @IsVoid, @VoidedByChargeId, @IdempotencyKey, @CreatedAtUtc);",
                charge, ct).ConfigureAwait(false);
        }
    }

    public Task UpdateChargeAsync(FeeCharge charge, CancellationToken ct = default) =>
        UpdateChargesAsync([charge], ct);

    public async Task UpdateChargesAsync(IReadOnlyList<FeeCharge> charges, CancellationToken ct = default)
    {
        foreach (FeeCharge charge in charges)
        {
            await ExecuteAsync(@"
                UPDATE fee_charges SET gross_amount = @GrossAmount, discount_amount = @DiscountAmount,
                       net_amount = @NetAmount, paid_amount = @PaidAmount, waived_amount = @WaivedAmount,
                       is_void = @IsVoid, voided_by_charge_id = @VoidedByChargeId, due_date = @DueDate,
                       description = @Description
                 WHERE id = @Id;", charge, ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<FeeCharge>> GetOutstandingChargesAsync(Guid? batchId = null,
        Guid? studentId = null, DateTime? asOf = null, CancellationToken ct = default) =>
        (await QueryAsync<FeeCharge>(
            $@"SELECT {ChargeColumns} FROM fee_charges
                WHERE is_void = 0
                  AND (net_amount - paid_amount - waived_amount) > 0
                  AND (@BatchId IS NULL OR batch_id = @BatchId)
                  AND (@StudentId IS NULL OR student_id = @StudentId)
                  AND (@AsOf IS NULL OR due_date <= @AsOf)
                 ORDER BY due_date, created_at_utc;",
            new { BatchId = batchId, StudentId = studentId, AsOf = asOf }, ct).ConfigureAwait(false)).ToList();

    public async Task<bool> AssessmentExistsAsync(Guid? feePlanId, string? periodKey, Guid studentId,
        FeeChargeKind kind, CancellationToken ct = default)
    {
        int count = await ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM fee_charges
               WHERE student_id = @StudentId AND kind = @Kind
                 AND IFNULL(period_key, '') = @PeriodKey
                 AND (@FeePlanId IS NULL OR assessment_id IN (
                        SELECT id FROM fee_assessments WHERE fee_plan_id = @FeePlanId));",
            new { StudentId = studentId, Kind = kind, PeriodKey = periodKey ?? string.Empty, FeePlanId = feePlanId }, ct)
            .ConfigureAwait(false);

        return count > 0;
    }

    // ------------------------------------------------------------------ payments

    private const string PaymentColumns = @"
        id, student_id, receipt_number, payment_date, amount_received, amount_allocated, amount_refunded,
        method, financial_account_id, transaction_reference, notes, status, collected_by_user_id,
        journal_entry_id, reversed_by_payment_id, idempotency_key, created_at_utc";

    public Task<Payment?> GetPaymentAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Payment>(
            $"SELECT {PaymentColumns} FROM payments WHERE id = @Id;", new { Id = id }, ct);

    public Task<Payment?> GetPaymentByReceiptNumberAsync(string receiptNumber, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Payment>(
            $"SELECT {PaymentColumns} FROM payments WHERE receipt_number = @ReceiptNumber;",
            new { ReceiptNumber = receiptNumber }, ct);

    public Task<Payment?> GetPaymentByIdempotencyKeyAsync(Guid studentId, string idempotencyKey,
        CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Payment>(
            $"SELECT {PaymentColumns} FROM payments WHERE student_id = @StudentId AND idempotency_key = @Key;",
            new { StudentId = studentId, Key = idempotencyKey }, ct);

    public Task InsertPaymentAsync(Payment payment, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO payments (id, student_id, receipt_number, payment_date, amount_received, amount_allocated,
                                  amount_refunded, method, financial_account_id, transaction_reference, notes,
                                  status, collected_by_user_id, journal_entry_id, reversed_by_payment_id,
                                  idempotency_key, created_at_utc)
            VALUES (@Id, @StudentId, @ReceiptNumber, @PaymentDate, @AmountReceived, @AmountAllocated,
                    @AmountRefunded, @Method, @FinancialAccountId, @TransactionReference, @Notes, @Status,
                    @CollectedByUserId, @JournalEntryId, @ReversedByPaymentId, @IdempotencyKey, @CreatedAtUtc);",
            payment, ct);

    public Task UpdatePaymentAsync(Payment payment, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE payments SET amount_allocated = @AmountAllocated, amount_refunded = @AmountRefunded,
                   status = @Status, reversed_by_payment_id = @ReversedByPaymentId, notes = @Notes,
                   journal_entry_id = @JournalEntryId
             WHERE id = @Id;", payment, ct);

    public async Task InsertAllocationsAsync(IReadOnlyList<PaymentAllocation> allocations,
        CancellationToken ct = default)
    {
        foreach (PaymentAllocation allocation in allocations)
        {
            await ExecuteAsync(@"
                INSERT INTO payment_allocations (id, payment_id, fee_charge_id, amount, allocated_at_utc,
                                                 credit_transaction_id)
                VALUES (@Id, @PaymentId, @FeeChargeId, @Amount, @AllocatedAtUtc, @CreditTransactionId);",
                allocation, ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<PaymentAllocation>> GetAllocationsForPaymentAsync(Guid paymentId,
        CancellationToken ct = default) =>
        (await QueryAsync<PaymentAllocation>(
            @"SELECT id, payment_id, fee_charge_id, amount, allocated_at_utc, credit_transaction_id
                FROM payment_allocations WHERE payment_id = @PaymentId;", new { PaymentId = paymentId }, ct)
            .ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<PaymentAllocation>> GetAllocationsForChargeAsync(Guid chargeId,
        CancellationToken ct = default) =>
        (await QueryAsync<PaymentAllocation>(
            @"SELECT id, payment_id, fee_charge_id, amount, allocated_at_utc, credit_transaction_id
                FROM payment_allocations WHERE fee_charge_id = @ChargeId;", new { ChargeId = chargeId }, ct)
            .ConfigureAwait(false)).ToList();

    public async Task<PagedResult<PaymentListItem>> SearchPaymentsAsync(PaymentQuery query,
        CancellationToken ct = default)
    {
        string where = @"
            WHERE (@From IS NULL OR payment_date >= @From)
              AND (@To IS NULL OR payment_date <= @To)
              AND (@StudentId IS NULL OR student_id = @StudentId)
              AND (@Method IS NULL OR method = @Method)
              AND (@CollectedByUserId IS NULL OR collected_by_user_id = @CollectedByUserId)
              AND (@Status IS NULL OR status = @Status)
              AND (@BatchId IS NULL OR student_id IN (SELECT student_id FROM enrollments WHERE batch_id = @BatchId))";

        var parameters = new
        {
            From = query.From,
            To = query.To,
            query.StudentId,
            query.Method,
            query.CollectedByUserId,
            query.Status,
            query.BatchId
        };

        int total = await ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM payments {where};", parameters, ct)
            .ConfigureAwait(false) ?? 0;

        int pageSize = Math.Clamp(query.PageSize, 1, 500);
        int page = Math.Max(1, query.Page);

        var items = (await QueryAsync<PaymentListItem>($@"
            SELECT p.id AS PaymentId, p.receipt_number AS ReceiptNumber, p.student_id AS StudentId,
                   s.student_code AS StudentCode, s.full_name AS StudentName, p.payment_date AS PaymentDate,
                   p.amount_received AS AmountReceived, p.amount_allocated AS AmountAllocated,
                   (p.amount_received - p.amount_allocated - p.amount_refunded) AS Unallocated,
                   p.method AS Method, p.status AS Status, p.transaction_reference AS TransactionReference,
                   IFNULL(u.full_name, '') AS CollectedBy
              FROM payments p
              JOIN students s ON s.id = p.student_id
              LEFT JOIN users u ON u.id = p.collected_by_user_id
              {where}
             ORDER BY p.payment_date DESC, p.receipt_number DESC
             LIMIT @Limit OFFSET @Offset;",
            new
            {
                query.From,
                query.To,
                query.StudentId,
                query.Method,
                query.CollectedByUserId,
                query.Status,
                query.BatchId,
                Limit = pageSize,
                Offset = (page - 1) * pageSize
            }, ct).ConfigureAwait(false)).ToList();

        return new PagedResult<PaymentListItem>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsForStudentAsync(Guid studentId,
        CancellationToken ct = default) =>
        (await QueryAsync<Payment>(
            $"SELECT {PaymentColumns} FROM payments WHERE student_id = @StudentId ORDER BY payment_date DESC;",
            new { StudentId = studentId }, ct).ConfigureAwait(false)).ToList();

    public Task<Money> SumCollectionsAsync(DateRange range, CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(
            @"SELECT IFNULL(SUM(amount_received), 0) FROM payments
               WHERE status = 0 AND payment_date BETWEEN @From AND @To;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct)!;

    // ------------------------------------------------------------------ credit

    private const string CreditColumns = @"
        id, student_id, kind, amount, transaction_date, payment_id, allocation_id, refund_id, description,
        journal_entry_id, created_at_utc";

    public Task InsertCreditTransactionAsync(StudentCreditTransaction transaction, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO student_credit_transactions (id, student_id, kind, amount, transaction_date, payment_id,
                                                     allocation_id, refund_id, description, journal_entry_id,
                                                     created_at_utc)
            VALUES (@Id, @StudentId, @Kind, @Amount, @TransactionDate, @PaymentId, @AllocationId, @RefundId,
                    @Description, @JournalEntryId, @CreatedAtUtc);", transaction, ct);

    public async Task<IReadOnlyList<StudentCreditTransaction>> GetCreditTransactionsAsync(Guid studentId,
        CancellationToken ct = default) =>
        (await QueryAsync<StudentCreditTransaction>(
            $"SELECT {CreditColumns} FROM student_credit_transactions WHERE student_id = @StudentId ORDER BY transaction_date, created_at_utc;",
            new { StudentId = studentId }, ct).ConfigureAwait(false)).ToList();

    public Task LinkCreditTransactionJournalAsync(Guid creditTransactionId, Guid journalEntryId,
        CancellationToken ct = default) =>
        ExecuteAsync(
            "UPDATE student_credit_transactions SET journal_entry_id = @JournalEntryId WHERE id = @Id;",
            new { Id = creditTransactionId, JournalEntryId = journalEntryId }, ct);

    public Task<Money> GetCreditBalanceAsync(Guid studentId, CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(@"
            SELECT IFNULL(SUM(CASE kind WHEN 0 THEN amount WHEN 3 THEN amount ELSE -amount END), 0)
              FROM student_credit_transactions WHERE student_id = @StudentId;",
            new { StudentId = studentId }, ct)!;

    public Task<Money> GetTotalCreditLiabilityAsync(CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(@"
            SELECT IFNULL(SUM(CASE kind WHEN 0 THEN amount WHEN 3 THEN amount ELSE -amount END), 0)
              FROM student_credit_transactions;", null, ct)!;

    // ------------------------------------------------------------------ refunds

    public Task InsertRefundAsync(Refund refund, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO refunds (id, refund_number, student_id, payment_id, refund_date, amount, method,
                                 financial_account_id, reason, journal_entry_id, processed_by_user_id, created_at_utc)
            VALUES (@Id, @RefundNumber, @StudentId, @PaymentId, @RefundDate, @Amount, @Method,
                    @FinancialAccountId, @Reason, @JournalEntryId, @ProcessedByUserId, @CreatedAtUtc);",
            refund, ct);

    public async Task<IReadOnlyList<Refund>> GetRefundsAsync(DateRange range, CancellationToken ct = default) =>
        (await QueryAsync<Refund>(
            @"SELECT id, refund_number, student_id, payment_id, refund_date, amount, method, financial_account_id,
                     reason, journal_entry_id, processed_by_user_id, created_at_utc
                FROM refunds WHERE refund_date BETWEEN @From AND @To ORDER BY refund_date DESC;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false)).ToList();

    // ------------------------------------------------------------------ receipts

    public Task<Receipt?> GetReceiptAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Receipt>(
            @"SELECT id, receipt_number, document_kind, student_id, payment_id, issued_at, reprint_count,
                     last_reprinted_at, payload_json
                FROM receipts WHERE id = @Id;", new { Id = id }, ct);

    public Task<Receipt?> GetReceiptByNumberAsync(string receiptNumber, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Receipt>(
            @"SELECT id, receipt_number, document_kind, student_id, payment_id, issued_at, reprint_count,
                     last_reprinted_at, payload_json
                FROM receipts WHERE receipt_number = @ReceiptNumber;", new { ReceiptNumber = receiptNumber }, ct);

    public Task InsertReceiptAsync(Receipt receipt, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO receipts (id, receipt_number, document_kind, student_id, payment_id, issued_at,
                                  reprint_count, last_reprinted_at, payload_json)
            VALUES (@Id, @ReceiptNumber, @DocumentKind, @StudentId, @PaymentId, @IssuedAt, @ReprintCount,
                    @LastReprintedAt, @PayloadJson);", receipt, ct);

    public Task UpdateReceiptAsync(Receipt receipt, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE receipts SET reprint_count = @ReprintCount, last_reprinted_at = @LastReprintedAt,
                   payload_json = @PayloadJson
             WHERE id = @Id;", receipt, ct);
}
