using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Services;

public interface IAcademicService
{
    Task<IReadOnlyList<AcademicSession>> GetSessionsAsync(CancellationToken ct = default);
    Task<Guid> CreateSessionAsync(AcademicSession session, CancellationToken ct = default);
    Task<IReadOnlyList<ClassGrade>> GetClassGradesAsync(CancellationToken ct = default);
    Task<Guid> SaveClassGradeAsync(ClassGrade grade, CancellationToken ct = default);
    Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken ct = default);
    Task<Guid> SaveCourseAsync(Course course, CancellationToken ct = default);
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken ct = default);
    Task<Guid> CreateSubjectAsync(Subject subject, CancellationToken ct = default);
    Task<IReadOnlyList<Batch>> GetBatchesAsync(bool? activeOnly = null, CancellationToken ct = default);
    Task<Batch?> GetBatchAsync(Guid id, CancellationToken ct = default);
    Task<Guid> SaveBatchAsync(Batch batch, IReadOnlyList<BatchSchedule> schedules, CancellationToken ct = default);
    Task TransferStudentAsync(Guid studentId, Guid fromBatchId, Guid toBatchId, DateTime transferDate,
        string? notes, CancellationToken ct = default);
    Task<IReadOnlyList<Staff>> GetStaffAsync(StaffStatus? status = null, CancellationToken ct = default);
    Task<Staff?> GetStaffAsync(Guid id, CancellationToken ct = default);
    Task<Guid> SaveStaffAsync(Staff staff, IReadOnlyList<StaffAssignment> assignments,
        CancellationToken ct = default);
}

public sealed class AcademicService : IAcademicService
{
    private readonly IAcademicRepository _academic;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public AcademicService(IAcademicRepository academic, IUnitOfWork unitOfWork, IAuditService audit,
        ICurrentUserService currentUser)
    {
        _academic = academic;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<AcademicSession>> GetSessionsAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetSessionsAsync(token), ct);

    public async Task<Guid> CreateSessionAsync(AcademicSession session, CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(session.Name))
        {
            throw new ValidationException("শিক্ষাবর্ষের নাম প্রয়োজন।");
        }

        if (session.EndDate <= session.StartDate)
        {
            throw new ValidationException("শেষ তারিখ শুরুর তারিখের পরে হতে হবে।");
        }

        session.Id = session.Id == Guid.Empty ? Guid.NewGuid() : session.Id;
        session.CreatedAtUtc = DateTime.UtcNow;

        await _unitOfWork.ExecuteAsync(token => _academic.InsertSessionAsync(session, token), ct)
            .ConfigureAwait(false);
        await _audit.RecordAsync("academic.session.create", nameof(AcademicSession), session.Id, session.Name,
            ct: ct).ConfigureAwait(false);
        return session.Id;
    }

    public Task<IReadOnlyList<ClassGrade>> GetClassGradesAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetClassGradesAsync(token), ct);

    public async Task<Guid> SaveClassGradeAsync(ClassGrade grade, CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");
        ArgumentNullException.ThrowIfNull(grade);

        if (string.IsNullOrWhiteSpace(grade.Name))
        {
            throw new ValidationException("শ্রেণির নাম প্রয়োজন।");
        }

        if (grade.Id == Guid.Empty)
        {
            grade.Id = Guid.NewGuid();
            grade.CreatedAtUtc = DateTime.UtcNow;
            await _unitOfWork.ExecuteAsync(token => _academic.InsertClassGradeAsync(grade, token), ct)
                .ConfigureAwait(false);
        }
        else
        {
            await _unitOfWork.ExecuteAsync(token => _academic.UpdateClassGradeAsync(grade, token), ct)
                .ConfigureAwait(false);
        }

        return grade.Id;
    }

    public Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetCoursesAsync(token), ct);

    public async Task<Guid> SaveCourseAsync(Course course, CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");
        ArgumentNullException.ThrowIfNull(course);

        if (string.IsNullOrWhiteSpace(course.Name))
        {
            throw new ValidationException("কোর্সের নাম প্রয়োজন।");
        }

        if (course.Id == Guid.Empty)
        {
            course.Id = Guid.NewGuid();
            course.CreatedAtUtc = DateTime.UtcNow;
            await _unitOfWork.ExecuteAsync(token => _academic.InsertCourseAsync(course, token), ct)
                .ConfigureAwait(false);
        }
        else
        {
            await _unitOfWork.ExecuteAsync(token => _academic.UpdateCourseAsync(course, token), ct)
                .ConfigureAwait(false);
        }

        return course.Id;
    }

    public Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetSubjectsAsync(token), ct);

    public async Task<Guid> CreateSubjectAsync(Subject subject, CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");
        ArgumentNullException.ThrowIfNull(subject);

        if (string.IsNullOrWhiteSpace(subject.Name))
        {
            throw new ValidationException("বিষয়ের নাম প্রয়োজন।");
        }

        subject.Id = subject.Id == Guid.Empty ? Guid.NewGuid() : subject.Id;
        subject.CreatedAtUtc = DateTime.UtcNow;

        await _unitOfWork.ExecuteAsync(token => _academic.InsertSubjectAsync(subject, token), ct)
            .ConfigureAwait(false);
        return subject.Id;
    }

    public Task<IReadOnlyList<Batch>> GetBatchesAsync(bool? activeOnly = null, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetBatchesAsync(activeOnly, token), ct);

    public Task<Batch?> GetBatchAsync(Guid id, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetBatchAsync(id, token), ct);

    public async Task<Guid> SaveBatchAsync(Batch batch, IReadOnlyList<BatchSchedule> schedules,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");
        ArgumentNullException.ThrowIfNull(batch);

        if (string.IsNullOrWhiteSpace(batch.Name))
        {
            throw new ValidationException("ব্যাচের নাম প্রয়োজন।");
        }

        if (batch.EndDate.HasValue && batch.EndDate.Value < batch.StartDate)
        {
            throw new ValidationException("ব্যাচের শেষ তারিখ শুরুর তারিখের আগে হতে পারবে না।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            bool isNew = batch.Id == Guid.Empty;
            batch.Id = isNew ? Guid.NewGuid() : batch.Id;
            batch.CreatedAtUtc = isNew ? DateTime.UtcNow : batch.CreatedAtUtc;

            if (isNew)
            {
                await _academic.InsertBatchAsync(batch, token).ConfigureAwait(false);
            }
            else
            {
                await _academic.UpdateBatchAsync(batch, token).ConfigureAwait(false);
            }

            await _academic.ReplaceBatchSchedulesAsync(batch.Id, schedules ?? [], token).ConfigureAwait(false);
            await _audit.RecordAsync(isNew ? "academic.batch.create" : "academic.batch.update",
                nameof(Batch), batch.Id, batch.Name, ct: token).ConfigureAwait(false);

            return batch.Id;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Transfers a student between batches. The old enrollment is closed and a new one is created, so
    /// historical attendance, results and fees stay attached to the batch the student was actually in.
    /// </summary>
    public async Task TransferStudentAsync(Guid studentId, Guid fromBatchId, Guid toBatchId, DateTime transferDate,
        string? notes, CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");

        if (fromBatchId == toBatchId)
        {
            throw new ValidationException("উৎস ও গন্তব্য ব্যাচ একই হতে পারবে না।");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            Enrollment? current = await _academic.GetActiveEnrollmentAsync(studentId, fromBatchId, token)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("শিক্ষার্থীর এই ব্যাচে কোনো সক্রিয় ভর্তি নেই।");

            Batch? target = await _academic.GetBatchAsync(toBatchId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("গন্তব্য ব্যাচ পাওয়া যায়নি।");

            if (!target.IsActive)
            {
                throw new ValidationException("গন্তব্য ব্যাচটি সক্রিয় নয়।");
            }

            if (target.Capacity.HasValue)
            {
                IReadOnlyList<Enrollment> enrolled = await _academic
                    .GetActiveEnrollmentsAsync(toBatchId, token).ConfigureAwait(false);

                if (enrolled.Count >= target.Capacity.Value)
                {
                    throw new ValidationException("গন্তব্য ব্যাচের আসন পূর্ণ।");
                }
            }

            current.Status = EnrollmentStatus.Transferred;
            current.LeftOn = transferDate.Date;
            current.Notes = notes;
            await _academic.UpdateEnrollmentAsync(current, token).ConfigureAwait(false);

            await _academic.InsertEnrollmentAsync(new Enrollment
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                BatchId = toBatchId,
                SessionId = target.SessionId,
                EnrolledOn = transferDate.Date,
                Status = EnrollmentStatus.Active,
                FeePlanId = current.FeePlanId,
                Notes = notes,
                CreatedAtUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false);

            await _audit.RecordAsync("academic.transfer", nameof(Enrollment), current.Id,
                $"ব্যাচ স্থানান্তর: {fromBatchId} → {toBatchId}", ct: token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Staff>> GetStaffAsync(StaffStatus? status = null, CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");
        return _unitOfWork.ExecuteAsync(token => _academic.GetStaffAsync(status, token), ct);
    }

    public Task<Staff?> GetStaffAsync(Guid id, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetStaffAsync(id, token), ct);

    public async Task<Guid> SaveStaffAsync(Staff staff, IReadOnlyList<StaffAssignment> assignments,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("academic.manage");
        ArgumentNullException.ThrowIfNull(staff);

        if (string.IsNullOrWhiteSpace(staff.FullName))
        {
            throw new ValidationException("কর্মচারীর নাম প্রয়োজন।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            bool isNew = staff.Id == Guid.Empty;
            staff.Id = isNew ? Guid.NewGuid() : staff.Id;
            staff.CreatedAtUtc = isNew ? DateTime.UtcNow : staff.CreatedAtUtc;

            if (isNew)
            {
                if (string.IsNullOrWhiteSpace(staff.StaffCode))
                {
                    staff.StaffCode = await _academic.NextStaffCodeAsync(token).ConfigureAwait(false);
                }

                await _academic.InsertStaffAsync(staff, token).ConfigureAwait(false);
            }
            else
            {
                await _academic.UpdateStaffAsync(staff, token).ConfigureAwait(false);
            }

            if (assignments is { Count: > 0 })
            {
                await _academic.ReplaceStaffAssignmentsAsync(staff.Id, assignments, token).ConfigureAwait(false);
            }

            await _audit.RecordAsync(isNew ? "staff.create" : "staff.update", nameof(Staff), staff.Id,
                $"{staff.StaffCode} — {staff.FullName}", ct: token).ConfigureAwait(false);

            return staff.Id;
        }, ct).ConfigureAwait(false);
    }
}
