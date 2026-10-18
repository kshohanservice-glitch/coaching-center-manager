using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record AdmissionRequest(
    string FullName,
    string? GuardianName,
    string? GuardianPhone,
    string? StudentPhone,
    string? Address,
    DateTime? DateOfBirth,
    string? Gender,
    string? SchoolOrCollege,
    string? SchoolClass,
    Guid SessionId,
    Guid? BatchId,
    Guid? CourseId,
    Guid? ClassGradeId,
    Money AdmissionFee,
    Guid? FeePlanId,
    DateTime? AdmissionDate = null,
    string? Notes = null);

public sealed record AdmissionResult(Guid StudentId, Guid AdmissionId, string AdmissionNumber,
    Guid? AdmissionChargeId, IReadOnlyList<Student> PossibleDuplicates);

public interface IStudentService
{
    Task<PagedResult<Student>> SearchAsync(StudentQuery query, CancellationToken ct = default);
    Task<Student?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(Student student, CancellationToken ct = default);
    Task UpdateAsync(Student student, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task ReactivateAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Student>> FindDuplicatesAsync(string fullName, string? guardianPhone,
        DateTime? dateOfBirth, CancellationToken ct = default);
    Task<AdmissionResult> AdmitAsync(AdmissionRequest request, CancellationToken ct = default);
    Task<StudentBalanceSummary> GetBalanceAsync(Guid studentId, CancellationToken ct = default);
}

public sealed class StudentService : IStudentService
{
    private readonly IAcademicRepository _academic;
    private readonly IFeeRepository _fees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountingService _accounting;

    public StudentService(IAcademicRepository academic, IFeeRepository fees, IUnitOfWork unitOfWork,
        IAuditService audit, ICurrentUserService currentUser, IAccountingService accounting)
    {
        _academic = academic;
        _fees = fees;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _accounting = accounting;
    }

    public Task<PagedResult<Student>> SearchAsync(StudentQuery query, CancellationToken ct = default)
    {
        _currentUser.Current.Require("students.view");
        return _unitOfWork.ExecuteAsync(token => _academic.SearchStudentsAsync(query, token), ct);
    }

    public Task<Student?> GetAsync(Guid id, CancellationToken ct = default)
    {
        _currentUser.Current.Require("students.view");
        return _unitOfWork.ExecuteAsync(token => _academic.GetStudentAsync(id, token), ct);
    }

    public async Task<Guid> CreateAsync(Student student, CancellationToken ct = default)
    {
        _currentUser.Current.Require("students.create");
        ArgumentNullException.ThrowIfNull(student);

        if (string.IsNullOrWhiteSpace(student.FullName))
        {
            throw new ValidationException("শিক্ষার্থীর নাম প্রয়োজন।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (student.Id == Guid.Empty)
            {
                student.Id = Guid.NewGuid();
            }

            if (string.IsNullOrWhiteSpace(student.StudentCode))
            {
                student.StudentCode = await _academic.NextStudentCodeAsync(token).ConfigureAwait(false);
            }

            student.CreatedAtUtc = DateTime.UtcNow;
            student.UpdatedAtUtc = DateTime.UtcNow;
            student.Status = StudentStatus.Active;

            await _academic.InsertStudentAsync(student, token).ConfigureAwait(false);
            await _audit.RecordAsync("students.create", nameof(Student), student.Id,
                $"{student.StudentCode} — {student.FullName}", ct: token).ConfigureAwait(false);
            return student.Id;
        }, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Student student, CancellationToken ct = default)
    {
        _currentUser.Current.Require("students.edit");
        ArgumentNullException.ThrowIfNull(student);

        if (string.IsNullOrWhiteSpace(student.FullName))
        {
            throw new ValidationException("শিক্ষার্থীর নাম প্রয়োজন।");
        }

        await _unitOfWork.ExecuteAsync(async token =>
        {
            student.UpdatedAtUtc = DateTime.UtcNow;
            await _academic.UpdateStudentAsync(student, token).ConfigureAwait(false);
            await _audit.RecordAsync("students.update", nameof(Student), student.Id, student.FullName, ct: token)
                .ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Archiving never deletes anything: attendance, results, fees and payments stay linked to the
    /// student so historical reports remain reproducible.
    /// </summary>
    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        _currentUser.Current.Require("students.archive");
        await SetStatusAsync(id, StudentStatus.Archived, "students.archive", ct).ConfigureAwait(false);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        _currentUser.Current.Require("students.edit");
        await SetStatusAsync(id, StudentStatus.Active, "students.reactivate", ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Student>> FindDuplicatesAsync(string fullName, string? guardianPhone,
        DateTime? dateOfBirth, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(
            token => _academic.FindPossibleDuplicatesAsync(fullName, guardianPhone, dateOfBirth, token), ct);

    public async Task<AdmissionResult> AdmitAsync(AdmissionRequest request, CancellationToken ct = default)
    {
        _currentUser.Current.Require("admissions.create");
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException("শিক্ষার্থীর নাম প্রয়োজন।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var student = new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = await _academic.NextStudentCodeAsync(token).ConfigureAwait(false),
                FullName = request.FullName.Trim(),
                GuardianName = request.GuardianName,
                GuardianPhone = request.GuardianPhone,
                StudentPhone = request.StudentPhone,
                Address = request.Address,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                SchoolOrCollege = request.SchoolOrCollege,
                SchoolClass = request.SchoolClass,
                Status = StudentStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            IReadOnlyList<Student> duplicates = await _academic
                .FindPossibleDuplicatesAsync(student.FullName, student.GuardianPhone, student.DateOfBirth, token)
                .ConfigureAwait(false);

            await _academic.InsertStudentAsync(student, token).ConfigureAwait(false);

            DateTime admissionDate = (request.AdmissionDate ?? DateTime.Today).Date;
            string admissionNumber = await _academic.NextAdmissionNumberAsync(token).ConfigureAwait(false);

            var admission = new Admission
            {
                Id = Guid.NewGuid(),
                AdmissionNumber = admissionNumber,
                StudentId = student.Id,
                SessionId = request.SessionId,
                AdmissionDate = admissionDate,
                CourseId = request.CourseId,
                ClassGradeId = request.ClassGradeId,
                BatchId = request.BatchId,
                AdmissionFee = request.AdmissionFee,
                FeePlanId = request.FeePlanId,
                Notes = request.Notes,
                CreatedByUserId = _currentUser.Current.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _academic.InsertAdmissionAsync(admission, token).ConfigureAwait(false);

            Guid? chargeId = null;
            if (request.AdmissionFee.IsPositive)
            {
                var charge = new FeeCharge
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    BatchId = request.BatchId,
                    Kind = FeeChargeKind.AdmissionFee,
                    Description = "ভর্তি ফি",
                    PeriodKey = "ONEOFF-" + admissionDate.ToString("yyyy-MM-dd"),
                    ChargeDate = admissionDate,
                    DueDate = admissionDate,
                    GrossAmount = request.AdmissionFee,
                    NetAmount = request.AdmissionFee,
                    IdempotencyKey = $"adm:{admission.Id:D}",
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _fees.InsertChargesAsync([charge], token).ConfigureAwait(false);
                chargeId = charge.Id;

                // The admission fee is a due like every other charge, so it has to appear as a
                // receivable in the general ledger too. Without this the fee sub-ledger shows a due
                // the ledger never recognised, and every payment that settles it over-credits the
                // receivable control account - which is exactly what the reconciliation catches.
                AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);
                JournalDraft draft = JournalBuilder.ForFeeAssessment(
                    admission.Id,
                    admissionNumber,
                    admissionDate,
                    [new JournalBuilder.FeeAssessmentLine(student.Id, FeeChargeKind.AdmissionFee,
                        charge.NetAmount, charge.Description)],
                    map);

                await _accounting.PostAsync(draft, token).ConfigureAwait(false);
            }

            if (request.BatchId.HasValue)
            {
                await _academic.InsertEnrollmentAsync(new Enrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    BatchId = request.BatchId.Value,
                    SessionId = request.SessionId,
                    EnrolledOn = admissionDate,
                    Status = EnrollmentStatus.Active,
                    FeePlanId = request.FeePlanId,
                    CreatedAtUtc = DateTime.UtcNow
                }, token).ConfigureAwait(false);
            }

            await _audit.RecordAsync("admissions.create", nameof(Admission), admission.Id,
                $"{admissionNumber} — {student.FullName}", ct: token).ConfigureAwait(false);

            return new AdmissionResult(student.Id, admission.Id, admissionNumber, chargeId, duplicates);
        }, ct).ConfigureAwait(false);
    }

    public async Task<StudentBalanceSummary> GetBalanceAsync(Guid studentId, CancellationToken ct = default)
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

    private async Task SetStatusAsync(Guid id, StudentStatus status, string auditAction, CancellationToken ct)
    {
        await _unitOfWork.ExecuteAsync(async token =>
        {
            Student? student = await _academic.GetStudentAsync(id, token).ConfigureAwait(false)
                ?? throw new NotFoundException("শিক্ষার্থী পাওয়া যায়নি।");

            student.Status = status;
            student.UpdatedAtUtc = DateTime.UtcNow;
            await _academic.UpdateStudentAsync(student, token).ConfigureAwait(false);
            await _audit.RecordAsync(auditAction, nameof(Student), id,
                $"{student.StudentCode} — {status}", ct: token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }
}
