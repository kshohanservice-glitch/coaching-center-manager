using System.Globalization;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record AssessMonthlyFeesRequest(
    Guid BatchId,
    string PeriodKey,
    DateTime? DueDate = null,
    Guid? FeePlanId = null,
    bool IncludeOnlyActiveEnrollments = true);

public sealed record AssessmentPreview(
    int StudentCount,
    int ChargeCount,
    Money TotalGross,
    Money TotalDiscount,
    Money TotalNet,
    IReadOnlyList<string> SkippedReasons);

public sealed record FeeAssessmentResult(
    Guid AssessmentId,
    string AssessmentNumber,
    int ChargeCount,
    Money TotalNet,
    Guid? JournalEntryId,
    int SkippedCount);

public interface IFeeService
{
    Task<Guid> CreateFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items, CancellationToken ct = default);
    Task UpdateFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items, CancellationToken ct = default);
    Task<AssessmentPreview> PreviewMonthlyAssessmentAsync(AssessMonthlyFeesRequest request,
        CancellationToken ct = default);
    Task<FeeAssessmentResult> AssessMonthlyFeesAsync(AssessMonthlyFeesRequest request,
        CancellationToken ct = default);
    Task<IReadOnlyList<StudentDuesRow>> GetStudentDuesAsync(Guid studentId, CancellationToken ct = default);
    Task<StudentBalanceSummary> GetStudentBalanceAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<FeeCharge>> GetOutstandingChargesAsync(Guid? batchId = null, Guid? studentId = null,
        DateTime? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<FeePlan>> GetFeePlansAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FeePlanItem>> GetFeePlanItemsAsync(Guid feePlanId, CancellationToken ct = default);
}

/// <summary>
/// Fee assessment. Assessment is deliberately separate from collection, and bulk generation is
/// idempotent: every charge carries a deterministic idempotency key backed by a unique index, so
/// running the same month twice can never create duplicate charges.
/// </summary>
public sealed class FeeService : IFeeService
{
    private readonly IFeeRepository _fees;
    private readonly IAcademicRepository _academic;
    private readonly IAccountingService _accounting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISequenceAllocator _sequences;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public FeeService(IFeeRepository fees, IAcademicRepository academic, IAccountingService accounting,
        IUnitOfWork unitOfWork, ISequenceAllocator sequences, IAuditService audit,
        ICurrentUserService currentUser)
    {
        _fees = fees;
        _academic = academic;
        _accounting = accounting;
        _unitOfWork = unitOfWork;
        _sequences = sequences;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Guid> CreateFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items,
        CancellationToken ct = default)
    {
        Principal principal = _currentUser.Current.Require("fees.assess");
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(items);

        ValidatePlan(plan, items);
        plan.Id = plan.Id == Guid.Empty ? Guid.NewGuid() : plan.Id;
        plan.CreatedAtUtc = DateTime.UtcNow;

        // A plan item is meaningless without its parent, and an empty Guid would be stored as a
        // broken foreign key rather than failing loudly here.
        foreach (FeePlanItem item in items)
        {
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.FeePlanId = plan.Id;
        }

        await _unitOfWork.ExecuteAsync(token => _fees.InsertFeePlanAsync(plan, items, token), ct)
            .ConfigureAwait(false);
        await _audit.RecordAsync("fees.plan.create", nameof(FeePlan), plan.Id, plan.Name, ct: ct)
            .ConfigureAwait(false);
        _ = principal;
        return plan.Id;
    }

    public async Task UpdateFeePlanAsync(FeePlan plan, IReadOnlyList<FeePlanItem> items,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.assess");
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(items);
        ValidatePlan(plan, items);

        foreach (FeePlanItem item in items)
        {
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.FeePlanId = plan.Id;
        }

        // Editing a plan never rewrites historical charges: only future assessments use the new rates.
        await _unitOfWork.ExecuteAsync(token => _fees.UpdateFeePlanAsync(plan, items, token), ct)
            .ConfigureAwait(false);
        await _audit.RecordAsync("fees.plan.update", nameof(FeePlan), plan.Id, plan.Name, ct: ct)
            .ConfigureAwait(false);
    }

    public Task<AssessmentPreview> PreviewMonthlyAssessmentAsync(AssessMonthlyFeesRequest request,
        CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => ComputePreviewAsync(request, token), ct);

    public async Task<FeeAssessmentResult> AssessMonthlyFeesAsync(AssessMonthlyFeesRequest request,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.assess");
        return await _unitOfWork.ExecuteInTransactionAsync(
            token => PostAssessmentAsync(request, token), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes exactly what a bulk assessment would create. The posted run re-uses the same rules so
    /// the operator's preview can never disagree with what actually gets charged.
    /// </summary>
    private async Task<AssessmentPreview> ComputePreviewAsync(AssessMonthlyFeesRequest request,
        CancellationToken ct)
    {
        string periodKey = PeriodKeys.FromDate(PeriodKeys.PeriodStart(request.PeriodKey));
        DateTime dueDate = request.DueDate ?? PeriodKeys.PeriodEnd(periodKey).AddDays(10);
        DateTime chargeDate = PeriodKeys.PeriodStart(periodKey);

        IReadOnlyList<Enrollment> enrollments = await _academic
            .GetActiveEnrollmentsAsync(request.BatchId, ct).ConfigureAwait(false);

        FeePlan? plan = request.FeePlanId.HasValue
            ? await _fees.GetFeePlanAsync(request.FeePlanId.Value, ct).ConfigureAwait(false)
            : await ResolveDefaultPlanAsync(request.BatchId, ct).ConfigureAwait(false);

        if (plan is null)
        {
            throw new DomainException("এই ব্যাচের জন্য কোনো ফি প্ল্যান পাওয়া যায়নি।");
        }

        IReadOnlyList<FeePlanItem> items = await _fees.GetFeePlanItemsAsync(plan.Id, ct).ConfigureAwait(false);
        var recurring = items.Where(i => i.RecurrenceMonths > 0 && ShouldChargeInPeriod(i, periodKey)).ToList();

        var charges = new List<FeeCharge>();
        var skipped = new List<string>();

        foreach (Enrollment enrollment in enrollments)
        {
            Student? student = await _academic.GetStudentAsync(enrollment.StudentId, ct).ConfigureAwait(false);
            if (student is null)
            {
                continue;
            }

            foreach (FeePlanItem item in recurring)
            {
                string key = BuildIdempotencyKey(student.Id, plan.Id, item.Kind, periodKey);
                bool exists = await _fees.AssessmentExistsAsync(plan.Id, periodKey, student.Id, item.Kind, ct)
                    .ConfigureAwait(false);

                if (exists)
                {
                    skipped.Add($"{student.FullName} — {item.Description} ({periodKey}) ইতিমধ্যে ধার্য করা হয়েছে");
                    continue;
                }

                charges.Add(new FeeCharge
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    BatchId = request.BatchId,
                    EnrollmentId = enrollment.Id,
                    Kind = item.Kind,
                    Description = item.Description,
                    PeriodKey = periodKey,
                    ChargeDate = chargeDate,
                    DueDate = dueDate,
                    GrossAmount = item.Amount,
                    DiscountAmount = Money.Zero,
                    NetAmount = item.Amount,
                    PaidAmount = Money.Zero,
                    WaivedAmount = Money.Zero,
                    IdempotencyKey = key,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        var preview = new AssessmentPreview(
            enrollments.Count,
            charges.Count,
            charges.Aggregate(Money.Zero, (sum, c) => sum + c.GrossAmount),
            charges.Aggregate(Money.Zero, (sum, c) => sum + c.DiscountAmount),
            charges.Aggregate(Money.Zero, (sum, c) => sum + c.NetAmount),
            skipped);

        return preview;
    }

    private async Task<FeeAssessmentResult> PostAssessmentAsync(AssessMonthlyFeesRequest request,
        CancellationToken ct)
    {
        // Preview first so the operator's numbers and the posted numbers come from one computation.
        AssessmentPreview preview = await ComputePreviewAsync(request, ct).ConfigureAwait(false);

        if (preview.ChargeCount == 0)
        {
            throw new DomainException(
                preview.SkippedReasons.Count > 0
                    ? "নতুন করে ধার্য করার মতো কোনো ফি নেই — সব ইতিমধ্যে ধার্য করা হয়েছে।"
                    : "এই ব্যাচে কোনো সক্রিয় শিক্ষার্থী পাওয়া যায়নি।");
        }

        string periodKey = PeriodKeys.FromDate(PeriodKeys.PeriodStart(request.PeriodKey));
        DateTime dueDate = request.DueDate ?? PeriodKeys.PeriodEnd(periodKey).AddDays(10);
        DateTime chargeDate = PeriodKeys.PeriodStart(periodKey);

        IReadOnlyList<Enrollment> enrollments = await _academic
            .GetActiveEnrollmentsAsync(request.BatchId, ct).ConfigureAwait(false);

        FeePlan? plan = request.FeePlanId.HasValue
            ? await _fees.GetFeePlanAsync(request.FeePlanId.Value, ct).ConfigureAwait(false)
            : await ResolveDefaultPlanAsync(request.BatchId, ct).ConfigureAwait(false);

        if (plan is null)
        {
            throw new DomainException("এই ব্যাচের জন্য কোনো ফি প্ল্যান পাওয়া যায়নি।");
        }

        IReadOnlyList<FeePlanItem> items = await _fees.GetFeePlanItemsAsync(plan.Id, ct).ConfigureAwait(false);
        var recurring = items.Where(i => i.RecurrenceMonths > 0 && ShouldChargeInPeriod(i, periodKey)).ToList();

        string assessmentNumber = await _sequences.NextFormattedAsync(
            "ASMT", "AS", 5, NumberingPeriod.Yearly, DateTime.Today, ct).ConfigureAwait(false);

        var assessment = new FeeAssessment
        {
            Id = Guid.NewGuid(),
            AssessmentNumber = assessmentNumber,
            PeriodKey = periodKey,
            FeePlanId = plan.Id,
            BatchId = request.BatchId,
            AssessmentDate = DateTime.Today,
            Description = $"মাসিক ফি ধার্য — {periodKey}",
            CreatedByUserId = _currentUser.Current.UserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        var charges = new List<FeeCharge>();
        foreach (Enrollment enrollment in enrollments)
        {
            foreach (FeePlanItem item in recurring)
            {
                bool exists = await _fees.AssessmentExistsAsync(plan.Id, periodKey, enrollment.StudentId,
                    item.Kind, ct).ConfigureAwait(false);

                if (exists)
                {
                    continue;
                }

                charges.Add(new FeeCharge
                {
                    Id = Guid.NewGuid(),
                    StudentId = enrollment.StudentId,
                    BatchId = request.BatchId,
                    EnrollmentId = enrollment.Id,
                    AssessmentId = assessment.Id,
                    Kind = item.Kind,
                    Description = item.Description,
                    PeriodKey = periodKey,
                    ChargeDate = chargeDate,
                    DueDate = dueDate,
                    GrossAmount = item.Amount,
                    NetAmount = item.Amount,
                    IdempotencyKey = BuildIdempotencyKey(enrollment.StudentId, plan.Id, item.Kind, periodKey),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        if (charges.Count == 0)
        {
            throw new DomainException("নতুন করে ধার্য করার মতো কোনো ফি নেই।");
        }

        await _fees.InsertAssessmentAsync(assessment, ct).ConfigureAwait(false);
        await _fees.InsertChargesAsync(charges, ct).ConfigureAwait(false);

        AccountMap map = await _accounting.GetAccountMapAsync(ct).ConfigureAwait(false);
        var lines = charges
            .Select(c => new JournalBuilder.FeeAssessmentLine(c.StudentId, c.Kind, c.NetAmount, c.Description))
            .ToList();

        JournalDraft draft = JournalBuilder.ForFeeAssessment(
            assessment.Id, assessmentNumber, assessment.AssessmentDate, lines, map);

        Guid journalId = await _accounting.PostAsync(draft, ct).ConfigureAwait(false);
        assessment.JournalEntryId = journalId;

        await _audit.RecordAsync("fees.assess", nameof(FeeAssessment), assessment.Id,
            $"{assessmentNumber}: {charges.Count} টি ফি ধার্য, মোট {preview.TotalNet.ToDecimal():N2}", ct: ct)
            .ConfigureAwait(false);

        return new FeeAssessmentResult(assessment.Id, assessmentNumber, charges.Count, preview.TotalNet,
            journalId, preview.SkippedReasons.Count);
    }

    public async Task<IReadOnlyList<StudentDuesRow>> GetStudentDuesAsync(Guid studentId,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.view");
        IReadOnlyList<FeeCharge> charges = await _unitOfWork.ExecuteAsync(
            token => _fees.GetChargesForStudentAsync(studentId, false, token), ct).ConfigureAwait(false);

        IReadOnlyList<Batch> batches = await _unitOfWork.ExecuteAsync(
            token => _academic.GetBatchesAsync(null, token), ct).ConfigureAwait(false);

        var names = batches.ToDictionary(b => b.Id, b => b.Name);

        return charges
            .Where(c => !c.IsVoid && c.Outstanding.IsPositive)
            .Select(c => StudentDuesRow.FromCharge(c, c.BatchId.HasValue && names.ContainsKey(c.BatchId.Value)
                ? names[c.BatchId.Value]
                : null))
            .ToList();
    }

    public async Task<StudentBalanceSummary> GetStudentBalanceAsync(Guid studentId, CancellationToken ct = default)
    {
        IReadOnlyList<FeeCharge> charges = await _unitOfWork.ExecuteAsync(
            token => _fees.GetChargesForStudentAsync(studentId, false, token), ct).ConfigureAwait(false);

        Money credit = await _unitOfWork.ExecuteAsync(
            token => _fees.GetCreditBalanceAsync(studentId, token), ct).ConfigureAwait(false);

        Money charged = charges.Aggregate(Money.Zero, (sum, c) => sum + c.NetAmount);
        Money discount = charges.Aggregate(Money.Zero, (sum, c) => sum + c.DiscountAmount);
        Money paid = charges.Aggregate(Money.Zero, (sum, c) => sum + c.PaidAmount);
        Money waived = charges.Aggregate(Money.Zero, (sum, c) => sum + c.WaivedAmount);
        Money outstanding = charges.Aggregate(Money.Zero, (sum, c) => sum + c.Outstanding);

        return new StudentBalanceSummary(studentId, charged, discount, paid, waived, outstanding, credit);
    }

    public Task<IReadOnlyList<FeeCharge>> GetOutstandingChargesAsync(Guid? batchId = null, Guid? studentId = null,
        DateTime? asOf = null, CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.view");
        return _unitOfWork.ExecuteAsync(
            token => _fees.GetOutstandingChargesAsync(batchId, studentId, asOf, token), ct);
    }

    public Task<IReadOnlyList<FeePlan>> GetFeePlansAsync(CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.view");
        return _unitOfWork.ExecuteAsync(token => _fees.GetFeePlansAsync(token), ct);
    }

    public Task<IReadOnlyList<FeePlanItem>> GetFeePlanItemsAsync(Guid feePlanId, CancellationToken ct = default)
    {
        _currentUser.Current.Require("fees.view");
        return _unitOfWork.ExecuteAsync(token => _fees.GetFeePlanItemsAsync(feePlanId, token), ct);
    }

    private async Task<FeePlan?> ResolveDefaultPlanAsync(Guid batchId, CancellationToken ct)
    {
        IReadOnlyList<FeePlan> plans = await _fees.GetFeePlansAsync(ct).ConfigureAwait(false);
        Batch? batch = await _academic.GetBatchAsync(batchId, ct).ConfigureAwait(false);

        return plans.FirstOrDefault(p => p.IsActive && p.BatchId == batchId)
               ?? plans.FirstOrDefault(p => p.IsActive && batch is not null && p.CourseId == batch.CourseId)
               ?? plans.FirstOrDefault(p => p.IsActive && batch is not null && p.ClassGradeId == batch.ClassGradeId)
               ?? plans.FirstOrDefault(p => p.IsActive);
    }

    private static bool ShouldChargeInPeriod(FeePlanItem item, string periodKey)
    {
        if (item.RecurrenceMonths <= 0)
        {
            return false;
        }

        DateTime periodStart = PeriodKeys.PeriodStart(periodKey);
        return ((periodStart.Year * 12 + periodStart.Month) % item.RecurrenceMonths) == 0;
    }

    private static string BuildIdempotencyKey(Guid studentId, Guid planId, FeeChargeKind kind, string periodKey) =>
        string.Create(CultureInfo.InvariantCulture, $"{studentId:D}:{planId:D}:{(int)kind}:{periodKey}");

    private static void ValidatePlan(FeePlan plan, IReadOnlyList<FeePlanItem> items)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(plan.Name))
        {
            errors.Add("ফি প্ল্যানের নাম প্রয়োজন।");
        }

        if (items.Count == 0)
        {
            errors.Add("ফি প্ল্যানে অন্তত একটি খাত থাকতে হবে।");
        }

        foreach (FeePlanItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                errors.Add("প্রতিটি খাতের বিবরণ প্রয়োজন।");
            }

            if (item.Amount.IsNegative)
            {
                errors.Add("খাতের পরিমাণ ঋণাত্মক হতে পারবে না।");
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
