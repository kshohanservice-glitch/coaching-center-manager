using System.Globalization;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record CollectPaymentRequest(
    Guid StudentId,
    Money Amount,
    PaymentMethod Method,
    Guid? FinancialAccountId = null,
    string? TransactionReference = null,
    DateTime? PaymentDate = null,
    string? Notes = null,
    IReadOnlyList<(Guid ChargeId, Money Amount)>? ManualAllocations = null,
    AllocationStrategy Strategy = AllocationStrategy.OldestFirst,
    string? IdempotencyKey = null);

public sealed record PaymentResult(Guid PaymentId, string ReceiptNumber, ReceiptData Receipt, bool WasDuplicate);

public sealed record RefundRequest(
    Guid StudentId,
    Money Amount,
    string Reason,
    PaymentMethod Method,
    Guid? FinancialAccountId = null,
    Guid? PaymentId = null,
    string? Reference = null);

public interface IPaymentService
{
    Task<PaymentResult> CollectAsync(CollectPaymentRequest request, CancellationToken ct = default);
    Task<ReceiptData> GetReceiptAsync(string receiptNumber, bool markReprint, CancellationToken ct = default);
    Task<ReceiptData> ReprintAsync(string receiptNumber, CancellationToken ct = default);
    Task<Guid> ReversePaymentAsync(Guid paymentId, string reason, CancellationToken ct = default);
    Task<Guid> RefundAsync(RefundRequest request, CancellationToken ct = default);
    Task<Money> ApplyCreditAsync(Guid studentId, IReadOnlyList<(Guid ChargeId, Money Amount)>? targets = null,
        Money? amount = null, CancellationToken ct = default);
    Task<PagedResult<PaymentListItem>> SearchAsync(PaymentQuery query, CancellationToken ct = default);
}

/// <summary>
/// Fee collection, allocation, advance credit, refunds and reversals.
///
/// Money rules enforced here:
/// * a payment can never allocate more than it received;
/// * a charge can never be paid beyond what it owes;
/// * unallocated money becomes an advance-credit liability, never income;
/// * the same money can never be both settled fees and available credit;
/// * a retry with the same idempotency key returns the original payment instead of charging twice.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IFeeRepository _fees;
    private readonly IAcademicRepository _academic;
    private readonly IAccountingRepository _accountingRepository;
    private readonly IAccountingService _accounting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISequenceAllocator _sequences;
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public PaymentService(IFeeRepository fees, IAcademicRepository academic,
        IAccountingRepository accountingRepository, IAccountingService accounting, IUnitOfWork unitOfWork,
        ISequenceAllocator sequences, ISettingsService settings, IAuditService audit,
        ICurrentUserService currentUser)
    {
        _fees = fees;
        _academic = academic;
        _accountingRepository = accountingRepository;
        _accounting = accounting;
        _unitOfWork = unitOfWork;
        _sequences = sequences;
        _settings = settings;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<PaymentResult> CollectAsync(CollectPaymentRequest request, CancellationToken ct = default)
    {
        Principal principal = _currentUser.Current.Require("fees.collect");
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= Money.Zero)
        {
            throw new ValidationException("পেমেন্টের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(token => CollectInternalAsync(request, principal, token), ct)
            .ConfigureAwait(false);
    }

    private async Task<PaymentResult> CollectInternalAsync(CollectPaymentRequest request, Principal principal,
        CancellationToken ct)
    {
        // Idempotency first: a retry or a double-click must never create a second payment.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            Payment? existing = await _fees.GetPaymentByIdempotencyKeyAsync(request.StudentId,
                request.IdempotencyKey!, ct).ConfigureAwait(false);

            if (existing is not null)
            {
                ReceiptData duplicateReceipt = await BuildReceiptDataAsync(existing, ct).ConfigureAwait(false);
                return new PaymentResult(existing.Id, existing.ReceiptNumber, duplicateReceipt, true);
            }
        }

        Student? student = await _academic.GetStudentAsync(request.StudentId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("শিক্ষার্থী পাওয়া যায়নি।");

        DateTime paymentDate = (request.PaymentDate ?? DateTime.Today).Date;

        FinancialAccount deposit = await ResolveDepositAccountAsync(request.FinancialAccountId, request.Method, ct)
            .ConfigureAwait(false);

        IReadOnlyList<FeeCharge> openCharges = await _fees
            .GetOutstandingChargesAsync(studentId: student.Id, asOf: null, ct: ct).ConfigureAwait(false);

        var targets = openCharges
            .Select((charge, index) => new AllocationTarget(charge.Id, charge.Outstanding, charge.DueDate,
                charge.ChargeDate, index))
            .ToList();

        AllocationResult allocation = PaymentAllocator.Allocate(
            request.Amount, targets, request.Strategy, request.ManualAllocations);

        ReceiptSettings receiptSettings = await _settings.GetReceiptSettingsAsync(ct).ConfigureAwait(false);
        string receiptNumber = await _sequences.NextFormattedAsync(
            "RCPT",
            receiptSettings.Prefix,
            receiptSettings.PeriodKeyFor(paymentDate),
            receiptSettings.NumberWidth,
            receiptSettings.IncludeYear,
            paymentDate, ct).ConfigureAwait(false);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ReceiptNumber = receiptNumber,
            PaymentDate = paymentDate,
            AmountReceived = request.Amount,
            AmountAllocated = allocation.Allocations.Aggregate(Money.Zero, (sum, a) => sum + a.Amount),
            AmountRefunded = Money.Zero,
            Method = request.Method,
            FinancialAccountId = deposit.Id,
            TransactionReference = string.IsNullOrWhiteSpace(request.TransactionReference)
                ? null
                : request.TransactionReference.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = PaymentStatus.Posted,
            CollectedByUserId = principal.UserId,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? null
                : request.IdempotencyKey.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _fees.InsertPaymentAsync(payment, ct).ConfigureAwait(false);

        var chargeById = openCharges.ToDictionary(c => c.Id);
        var allocations = new List<PaymentAllocation>(allocation.Allocations.Count);
        var updatedCharges = new List<FeeCharge>();

        foreach ((Guid chargeId, Money amount) in allocation.Allocations)
        {
            if (!chargeById.TryGetValue(chargeId, out FeeCharge? charge))
            {
                throw new DomainException("বরাদ্দযোগ্য ফি খাত পাওয়া যায়নি।");
            }

            if (amount > charge.Outstanding)
            {
                throw new DomainException(
                    $"একটি খাতে বকেয়ার ({charge.Outstanding.ToDecimal():N2}) চেয়ে বেশি ({amount.ToDecimal():N2}) বরাদ্দ করা যাবে না।");
            }

            var allocationRow = new PaymentAllocation
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                FeeChargeId = chargeId,
                Amount = amount,
                AllocatedAtUtc = DateTime.UtcNow
            };

            allocations.Add(allocationRow);
            charge.PaidAmount += amount;
            updatedCharges.Add(charge);
        }

        await _fees.InsertAllocationsAsync(allocations, ct).ConfigureAwait(false);
        await _fees.UpdateChargesAsync(updatedCharges, ct).ConfigureAwait(false);

        // Anything not allocated stays a liability: it is advance credit, not income.
        if (allocation.Unallocated.IsPositive)
        {
            await _fees.InsertCreditTransactionAsync(new StudentCreditTransaction
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                Kind = CreditTransactionKind.Received,
                Amount = allocation.Unallocated,
                TransactionDate = paymentDate,
                PaymentId = payment.Id,
                Description = $"অগ্রিম জমা — {receiptNumber}",
                CreatedAtUtc = DateTime.UtcNow
            }, ct).ConfigureAwait(false);
        }

        AccountMap map = await _accounting.GetAccountMapAsync(ct).ConfigureAwait(false);
        JournalDraft draft = JournalBuilder.ForFeePayment(
            payment.Id,
            receiptNumber,
            paymentDate,
            student.Id,
            payment.AmountReceived,
            payment.AmountAllocated,
            deposit.AccountId,
            deposit.Id,
            map);

        Guid journalId = await _accounting.PostAsync(draft, ct).ConfigureAwait(false);
        payment.JournalEntryId = journalId;
        await _fees.UpdatePaymentAsync(payment, ct).ConfigureAwait(false);

        await _fees.InsertReceiptAsync(new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            DocumentKind = "Fee",
            StudentId = student.Id,
            PaymentId = payment.Id,
            IssuedAt = DateTime.UtcNow,
            ReprintCount = 0
        }, ct).ConfigureAwait(false);

        await _audit.RecordAsync("fees.collect", nameof(Payment), payment.Id,
            $"{receiptNumber}: {payment.AmountReceived.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)} " +
            $"আদায়, বরাদ্দ {payment.AmountAllocated.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)}",
            ct: ct).ConfigureAwait(false);

        ReceiptData receipt = await BuildReceiptDataAsync(payment, ct).ConfigureAwait(false);
        return new PaymentResult(payment.Id, receiptNumber, receipt, false);
    }

    public Task<ReceiptData> GetReceiptAsync(string receiptNumber, bool markReprint,
        CancellationToken ct = default) =>
        markReprint ? ReprintAsync(receiptNumber, ct) : LoadReceiptAsync(receiptNumber, false, ct);

    /// <summary>
    /// A reprint reproduces the original receipt and records the reprint count. It never creates a
    /// payment, a journal entry or a second receipt number.
    /// </summary>
    public Task<ReceiptData> ReprintAsync(string receiptNumber, CancellationToken ct = default) =>
        LoadReceiptAsync(receiptNumber, true, ct);

    private async Task<ReceiptData> LoadReceiptAsync(string receiptNumber, bool markReprint, CancellationToken ct)
    {
        _currentUser.Current.Require("fees.print");

        Payment? payment = await _unitOfWork.ExecuteAsync(
            token => _fees.GetPaymentByReceiptNumberAsync(receiptNumber, token), ct).ConfigureAwait(false)
            ?? throw new NotFoundException("এই নম্বরের কোনো রশিদ পাওয়া যায়নি।");

        Receipt? receipt = await _unitOfWork.ExecuteAsync(
            token => _fees.GetReceiptByNumberAsync(receiptNumber, token), ct).ConfigureAwait(false);

        if (markReprint && receipt is not null)
        {
            receipt.ReprintCount++;
            receipt.LastReprintedAt = DateTime.UtcNow;
            await _unitOfWork.ExecuteAsync(token => _fees.UpdateReceiptAsync(receipt, token), ct)
                .ConfigureAwait(false);
        }

        return await BuildReceiptDataAsync(payment, ct, receipt?.ReprintCount ?? 0,
            (receipt?.ReprintCount ?? 0) > 0).ConfigureAwait(false);
    }

    /// <summary>
    /// Reverses a posted payment: unwinds the allocations, removes any advance credit it created and
    /// posts the mirror journal entry. The original payment is marked reversed, never deleted.
    /// </summary>
    public Task<Guid> ReversePaymentAsync(Guid paymentId, string reason, CancellationToken ct = default) =>
        _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            Principal principal = _currentUser.Current.Require("fees.reverse");
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ValidationException("বাতিলের কারণ উল্লেখ করা আবশ্যক।");
            }

            Payment payment = await _fees.GetPaymentAsync(paymentId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("পেমেন্ট পাওয়া যায়নি।");

            if (payment.Status == PaymentStatus.Reversed)
            {
                throw new DomainException("এই পেমেন্টটি ইতিমধ্যে বাতিল করা হয়েছে।");
            }

            IReadOnlyList<PaymentAllocation> allocations = await _fees
                .GetAllocationsForPaymentAsync(payment.Id, token).ConfigureAwait(false);

            var restoredCharges = new List<FeeCharge>();
            foreach (PaymentAllocation allocation in allocations)
            {
                FeeCharge? charge = await _fees.GetChargeAsync(allocation.FeeChargeId, token).ConfigureAwait(false);
                if (charge is null)
                {
                    continue;
                }

                charge.PaidAmount -= allocation.Amount;
                if (charge.PaidAmount.IsNegative)
                {
                    throw new DomainException("পরিশোধিত অর্থ বাতিলের ফলে ঋণাত্মক হয়ে যাবে; প্রশাসকের সাথে যোগাযোগ করুন।");
                }

                restoredCharges.Add(charge);
            }

            await _fees.UpdateChargesAsync(restoredCharges, token).ConfigureAwait(false);

            // Credit created by this payment must be removed as well, or it could be spent twice.
            Money unallocated = payment.Unallocated;
            if (unallocated.IsPositive)
            {
                await _fees.InsertCreditTransactionAsync(new StudentCreditTransaction
                {
                    Id = Guid.NewGuid(),
                    StudentId = payment.StudentId,
                    Kind = CreditTransactionKind.Refunded,
                    Amount = unallocated,
                    TransactionDate = DateTime.Today,
                    PaymentId = payment.Id,
                    Description = $"পেমেন্ট বাতিল — {payment.ReceiptNumber}",
                    CreatedAtUtc = DateTime.UtcNow
                }, token).ConfigureAwait(false);
            }

            Guid reversalJournalId = payment.JournalEntryId.HasValue
                ? await _accounting.ReverseJournalAsync(payment.JournalEntryId.Value, reason, token)
                    .ConfigureAwait(false)
                : Guid.Empty;

            payment.Status = PaymentStatus.Reversed;
            payment.ReversedByPaymentId = reversalJournalId;
            payment.Notes = string.IsNullOrWhiteSpace(payment.Notes)
                ? "বাতিল: " + reason
                : payment.Notes + " | বাতিল: " + reason;

            await _fees.UpdatePaymentAsync(payment, token).ConfigureAwait(false);

            await _audit.RecordAsync("fees.reverse", nameof(Payment), payment.Id,
                $"{payment.ReceiptNumber} বাতিল: {reason}", ct: token).ConfigureAwait(false);

            _ = principal;
            return reversalJournalId;
        }, ct);

    /// <summary>
    /// Refunds money to a student. Credit that was never allocated is simply released from the
    /// liability; money already allocated to a fee reverses that income rather than creating a new
    /// expense, and the fee becomes payable again.
    /// </summary>
    public Task<Guid> RefundAsync(RefundRequest request, CancellationToken ct = default) =>
        _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            Principal principal = _currentUser.Current.Require("fees.refund");
            ArgumentNullException.ThrowIfNull(request);

            if (request.Amount <= Money.Zero)
            {
                throw new ValidationException("ফেরতের পরিমাণ শূন্যের বেশি হতে হবে।");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ValidationException("ফেরতের কারণ উল্লেখ করা আবশ্যক।");
            }

            Money creditBalance = await _fees.GetCreditBalanceAsync(request.StudentId, token)
                .ConfigureAwait(false);

            Money fromCredit = Money.Min(request.Amount, creditBalance);
            Money fromAllocated = request.Amount - fromCredit;

            if (fromAllocated.IsPositive)
            {
                await UnwindAllocationsAsync(request.StudentId, fromAllocated, token).ConfigureAwait(false);
            }

            FinancialAccount payFrom = await ResolveDepositAccountAsync(request.FinancialAccountId,
                request.Method, token).ConfigureAwait(false);

            string refundNumber = await _sequences.NextFormattedAsync(
                "RFND", "RFN", DateTime.Today.ToString("yyyy-MM"), 5, true, DateTime.Today, token)
                .ConfigureAwait(false);

            var refund = new Refund
            {
                Id = Guid.NewGuid(),
                RefundNumber = refundNumber,
                StudentId = request.StudentId,
                PaymentId = request.PaymentId,
                RefundDate = DateTime.Today,
                Amount = request.Amount,
                Method = request.Method,
                FinancialAccountId = payFrom.Id,
                Reason = request.Reason.Trim(),
                ProcessedByUserId = principal.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _fees.InsertRefundAsync(refund, token).ConfigureAwait(false);

            if (fromCredit.IsPositive)
            {
                await _fees.InsertCreditTransactionAsync(new StudentCreditTransaction
                {
                    Id = Guid.NewGuid(),
                    StudentId = request.StudentId,
                    Kind = CreditTransactionKind.Refunded,
                    Amount = fromCredit,
                    TransactionDate = DateTime.Today,
                    RefundId = refund.Id,
                    Description = $"অগ্রিম ক্রেডিট ফেরত — {refundNumber}",
                    CreatedAtUtc = DateTime.UtcNow
                }, token).ConfigureAwait(false);
            }

            if (request.PaymentId.HasValue)
            {
                Payment? source = await _fees.GetPaymentAsync(request.PaymentId.Value, token).ConfigureAwait(false);
                if (source is not null)
                {
                    source.AmountRefunded += request.Amount;
                    await _fees.UpdatePaymentAsync(source, token).ConfigureAwait(false);
                }
            }

            AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);
            JournalDraft draft = JournalBuilder.ForRefund(
                refund.Id,
                refundNumber,
                refund.RefundDate,
                request.StudentId,
                request.Amount,
                fromCredit,
                fromAllocated,
                payFrom.AccountId,
                payFrom.Id,
                map.TuitionIncome,
                map);

            Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
            refund.JournalEntryId = journalId;

            await _audit.RecordAsync("fees.refund", nameof(Refund), refund.Id,
                $"{refundNumber}: {request.Amount.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)} ফেরত — {request.Reason}",
                ct: token).ConfigureAwait(false);

            return refund.Id;
        }, ct);

    /// <summary>
    /// Applies a student's advance credit to open charges. This settles receivables and releases the
    /// liability; it does not move cash and does not recognise income.
    /// </summary>
    public Task<Money> ApplyCreditAsync(Guid studentId, IReadOnlyList<(Guid ChargeId, Money Amount)>? targets = null,
        Money? amount = null, CancellationToken ct = default) =>
        _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            _currentUser.Current.Require("fees.allocate");

            Money available = await _fees.GetCreditBalanceAsync(studentId, token).ConfigureAwait(false);
            if (available <= Money.Zero)
            {
                throw new DomainException("এই শিক্ষার্থীর কোনো অগ্রিম ক্রেডিট নেই।");
            }

            Money toApply = Money.Min(amount ?? available, available);

            IReadOnlyList<FeeCharge> openCharges = await _fees
                .GetOutstandingChargesAsync(studentId: studentId, asOf: null, ct: token).ConfigureAwait(false);

            var allocationTargets = openCharges
                .Select((charge, index) => new AllocationTarget(charge.Id, charge.Outstanding, charge.DueDate,
                    charge.ChargeDate, index))
                .ToList();

            AllocationResult result = PaymentAllocator.Allocate(
                toApply, allocationTargets,
                targets is { Count: > 0 } ? AllocationStrategy.Manual : AllocationStrategy.OldestFirst,
                targets);

            if (result.Allocations.Count == 0 || result.Allocations.All(a => a.Amount.IsZero))
            {
                throw new DomainException("ক্রেডিট বরাদ্দ করার মতো কোনো বকেয়া নেই।");
            }

            var chargeById = openCharges.ToDictionary(c => c.Id);
            var updated = new List<FeeCharge>();
            var allocations = new List<PaymentAllocation>();
            Money applied = Money.Zero;

            foreach ((Guid chargeId, Money allocationAmount) in result.Allocations)
            {
                if (!chargeById.TryGetValue(chargeId, out FeeCharge? charge))
                {
                    continue;
                }

                if (allocationAmount > charge.Outstanding)
                {
                    throw new DomainException("বকেয়ার চেয়ে বেশি ক্রেডিট বরাদ্দ করা যাবে না।");
                }

                var creditTransactionId = Guid.NewGuid();
                await _fees.InsertCreditTransactionAsync(new StudentCreditTransaction
                {
                    Id = creditTransactionId,
                    StudentId = studentId,
                    Kind = CreditTransactionKind.Allocated,
                    Amount = allocationAmount,
                    TransactionDate = DateTime.Today,
                    Description = "অগ্রিম ক্রেডিট বরাদ্দ",
                    CreatedAtUtc = DateTime.UtcNow
                }, token).ConfigureAwait(false);

                var allocationRow = new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    PaymentId = null,
                    FeeChargeId = chargeId,
                    Amount = allocationAmount,
                    AllocatedAtUtc = DateTime.UtcNow,
                    CreditTransactionId = creditTransactionId
                };

                allocations.Add(allocationRow);
                charge.PaidAmount += allocationAmount;
                updated.Add(charge);
                applied += allocationAmount;

                AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);
                JournalDraft draft = JournalBuilder.ForCreditAllocation(
                    creditTransactionId, DateTime.Today, studentId, allocationAmount, map);

                Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
                await _fees.LinkCreditTransactionJournalAsync(creditTransactionId, journalId, token)
                    .ConfigureAwait(false);
            }

            await _fees.InsertAllocationsAsync(allocations, token).ConfigureAwait(false);
            await _fees.UpdateChargesAsync(updated, token).ConfigureAwait(false);

            await _audit.RecordAsync("fees.credit.apply", nameof(FeeCharge), studentId,
                $"অগ্রিম ক্রেডিট বরাদ্দ: {applied.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)}",
                ct: token).ConfigureAwait(false);

            return applied;
        }, ct);

    public Task<PagedResult<PaymentListItem>> SearchAsync(PaymentQuery query, CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.view");
        return _unitOfWork.ExecuteAsync(token => _fees.SearchPaymentsAsync(query, token), ct);
    }

    private async Task UnwindAllocationsAsync(Guid studentId, Money amount, CancellationToken ct)
    {
        Money remaining = amount;
        IReadOnlyList<FeeCharge> charges = await _fees.GetChargesForStudentAsync(studentId, false, ct)
            .ConfigureAwait(false);

        foreach (FeeCharge charge in charges.Where(c => c.PaidAmount.IsPositive)
                     .OrderByDescending(c => c.DueDate))
        {
            if (remaining.IsZero)
            {
                break;
            }

            IReadOnlyList<PaymentAllocation> allocations = await _fees
                .GetAllocationsForChargeAsync(charge.Id, ct).ConfigureAwait(false);

            foreach (PaymentAllocation allocation in allocations
                         .Where(a => a.Amount.IsPositive)
                         .OrderByDescending(a => a.AllocatedAtUtc))
            {
                if (remaining.IsZero)
                {
                    break;
                }

                Money take = Money.Min(remaining, allocation.Amount);
                charge.PaidAmount -= take;
                remaining -= take;

                await _fees.InsertAllocationsAsync(new[]
                {
                    new PaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        PaymentId = allocation.PaymentId,
                        FeeChargeId = charge.Id,
                        Amount = -take,
                        AllocatedAtUtc = DateTime.UtcNow
                    }
                }, ct).ConfigureAwait(false);
            }

            await _fees.UpdateChargeAsync(charge, ct).ConfigureAwait(false);
        }

        if (remaining.IsPositive)
        {
            throw new DomainException("ফেরতযোগ্য পরিশোধিত অর্থ পর্যাপ্ত নয়।");
        }
    }

    private async Task<FinancialAccount> ResolveDepositAccountAsync(Guid? financialAccountId, PaymentMethod method,
        CancellationToken ct)
    {
        if (financialAccountId.HasValue)
        {
            FinancialAccount? account = await _accountingRepository
                .GetFinancialAccountAsync(financialAccountId.Value, ct).ConfigureAwait(false);

            if (account is null)
            {
                throw new NotFoundException("নির্বাচিত নগদ/ব্যাংক হিসাব পাওয়া যায়নি।");
            }

            if (!account.IsActive)
            {
                throw new DomainException("নির্বাচিত হিসাবটি নিষ্ক্রিয়।");
            }

            return account;
        }

        IReadOnlyList<FinancialAccount> accounts = await _accountingRepository
            .GetFinancialAccountsAsync(true, ct).ConfigureAwait(false);

        FinancialAccountKind preferred = method == PaymentMethod.Cash
            ? FinancialAccountKind.Cash
            : method is PaymentMethod.BKash or PaymentMethod.Nagad or PaymentMethod.Rocket or PaymentMethod.Upay
                ? FinancialAccountKind.MobileBanking
                : FinancialAccountKind.Bank;

        FinancialAccount? match = accounts.FirstOrDefault(a => a.Kind == preferred)
                                  ?? accounts.FirstOrDefault(a => a.Kind == FinancialAccountKind.Cash)
                                  ?? accounts.FirstOrDefault();

        return match ?? throw new DomainException(
            "কোনো নগদ বা ব্যাংক হিসাব কনফিগার করা নেই। অনুগ্রহ করে হিসাব সেটিংস থেকে একটি হিসাব যোগ করুন।");
    }

    private async Task<ReceiptData> BuildReceiptDataAsync(Payment payment, CancellationToken ct,
        int reprintCount = 0, bool isReprint = false)
    {
        Student? student = await _academic.GetStudentAsync(payment.StudentId, ct).ConfigureAwait(false);
        Institution? institution = await _settings.GetInstitutionAsync(ct).ConfigureAwait(false);

        IReadOnlyList<PaymentAllocation> allocations = await _fees
            .GetAllocationsForPaymentAsync(payment.Id, ct).ConfigureAwait(false);

        IReadOnlyList<FeeCharge> charges = await _fees
            .GetChargesForStudentAsync(payment.StudentId, true, ct).ConfigureAwait(false);

        var chargeById = charges.ToDictionary(c => c.Id);
        var lines = new List<ReceiptLine>(allocations.Count);
        foreach (PaymentAllocation allocation in allocations)
        {
            if (!chargeById.TryGetValue(allocation.FeeChargeId, out FeeCharge? charge))
            {
                continue;
            }

            lines.Add(new ReceiptLine(charge.Description, charge.PeriodKey, allocation.Amount));
        }

        StudentBalanceSummary balance = await ComputeBalanceAsync(payment.StudentId, charges, ct)
            .ConfigureAwait(false);

        IReadOnlyList<Enrollment> enrollments = await _academic
            .GetEnrollmentsForStudentAsync(payment.StudentId, ct).ConfigureAwait(false);

        IReadOnlyList<Batch> batches = await _academic.GetBatchesAsync(null, ct).ConfigureAwait(false);
        Enrollment? current = enrollments.FirstOrDefault(e => e.Status == EnrollmentStatus.Active);
        string? batchName = current is null
            ? null
            : batches.FirstOrDefault(b => b.Id == current.BatchId)?.Name;

        return new ReceiptData
        {
            ReceiptNumber = payment.ReceiptNumber,
            PaymentDate = payment.PaymentDate,
            StudentCode = student?.StudentCode ?? string.Empty,
            StudentName = student?.FullName ?? string.Empty,
            GuardianName = student?.GuardianName,
            ClassOrBatch = batchName,
            AmountReceived = payment.AmountReceived,
            AmountAllocated = payment.AmountAllocated,
            AdvanceCredit = payment.Unallocated,
            PreviousOutstanding = balance.Outstanding + payment.AmountAllocated,
            RemainingOutstanding = balance.Outstanding,
            CreditBalance = balance.CreditBalance,
            PaymentMethod = DescribeMethod(payment.Method),
            TransactionReference = payment.TransactionReference,
            CollectedBy = _currentUser.Current.FullName,
            Notes = payment.Notes,
            Lines = lines,
            ReprintCount = reprintCount,
            IsReprint = isReprint,
            InstitutionName = institution?.Name ?? "ShikkhaDesk",
            InstitutionAddress = institution?.Address,
            InstitutionPhone = institution?.Phone,
            InstitutionEmail = institution?.Email,
            CurrencySymbol = institution?.CurrencySymbol ?? "৳"
        };
    }

    private async Task<StudentBalanceSummary> ComputeBalanceAsync(Guid studentId,
        IReadOnlyList<FeeCharge> charges, CancellationToken ct)
    {
        Money credit = await _fees.GetCreditBalanceAsync(studentId, ct).ConfigureAwait(false);
        Money outstanding = charges.Aggregate(Money.Zero, (sum, c) => sum + c.Outstanding);
        Money charged = charges.Aggregate(Money.Zero, (sum, c) => sum + c.NetAmount);
        Money discount = charges.Aggregate(Money.Zero, (sum, c) => sum + c.DiscountAmount);
        Money paid = charges.Aggregate(Money.Zero, (sum, c) => sum + c.PaidAmount);
        Money waived = charges.Aggregate(Money.Zero, (sum, c) => sum + c.WaivedAmount);
        return new StudentBalanceSummary(studentId, charged, discount, paid, waived, outstanding, credit);
    }

    private static string DescribeMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "নগদ",
        PaymentMethod.Bank => "ব্যাংক",
        PaymentMethod.BKash => "বিকাশ",
        PaymentMethod.Nagad => "নগদ (মোবাইল)",
        PaymentMethod.Rocket => "রকেট",
        PaymentMethod.Upay => "উপায়",
        _ => "অন্যান্য"
    };
}
