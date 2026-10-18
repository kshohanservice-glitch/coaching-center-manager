using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Services;

public sealed record AttendanceEntry(Guid StudentId, AttendanceStatus Status, string? Note = null);

public sealed record AttendanceSummary(int Present, int Absent, int Late, int Leave, int Total)
{
    public decimal PresentPercentage => Total == 0 ? 0m : Math.Round(Present * 100m / Total, 2);
}

public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceRecord>> GetForBatchDateAsync(Guid batchId, DateTime date,
        CancellationToken ct = default);
    Task SaveAsync(Guid batchId, DateTime date, IReadOnlyList<AttendanceEntry> entries,
        CancellationToken ct = default);
    Task<AttendanceSummary> GetSummaryForBatchDateAsync(Guid batchId, DateTime date, CancellationToken ct = default);
    Task<AttendanceSummary> GetSummaryForStudentAsync(Guid studentId, DateRange range, CancellationToken ct = default);
}

/// <summary>
/// Daily attendance. One record per student, per batch, per day — enforced by a unique index as well as
/// by this service — and corrections keep a link to the record they replace so the audit trail survives.
/// </summary>
public sealed class AttendanceService : IAttendanceService
{
    private readonly IAcademicRepository _academic;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public AttendanceService(IAcademicRepository academic, IUnitOfWork unitOfWork, IAuditService audit,
        ICurrentUserService currentUser)
    {
        _academic = academic;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<AttendanceRecord>> GetForBatchDateAsync(Guid batchId, DateTime date,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("attendance.view");
        return _unitOfWork.ExecuteAsync(token => _academic.GetAttendanceForBatchDateAsync(batchId, date, token), ct);
    }

    public async Task SaveAsync(Guid batchId, DateTime date, IReadOnlyList<AttendanceEntry> entries,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("attendance.edit");
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new ValidationException("উপস্থিতির তথ্য খালি।");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            DateTime day = date.Date;
            Guid userId = _currentUser.Current.UserId;

            foreach (AttendanceEntry entry in entries)
            {
                Enrollment? enrollment = await _academic.GetActiveEnrollmentAsync(entry.StudentId, batchId, token)
                    .ConfigureAwait(false);

                if (enrollment is null)
                {
                    throw new ValidationException(
                        "এই শিক্ষার্থী নির্বাচিত তারিখে ব্যাচে ভর্তি ছিল না, তাই উপস্থিতি দেওয়া যাবে না।");
                }

                if (enrollment.EnrolledOn.Date > day)
                {
                    throw new ValidationException(
                        "ভর্তির তারিখের আগের উপস্থিতি দেওয়া যাবে না।");
                }

                AttendanceRecord? existing = await _academic
                    .GetAttendanceAsync(batchId, entry.StudentId, day, token).ConfigureAwait(false);

                if (existing is null)
                {
                    await _academic.InsertAttendanceAsync(new AttendanceRecord
                    {
                        Id = Guid.NewGuid(),
                        BatchId = batchId,
                        StudentId = entry.StudentId,
                        AttendanceDate = day,
                        Status = entry.Status,
                        Note = entry.Note,
                        RecordedByUserId = userId,
                        RecordedAtUtc = DateTime.UtcNow
                    }, token).ConfigureAwait(false);

                    continue;
                }

                if (existing.Status == entry.Status
                    && string.Equals(existing.Note, entry.Note, StringComparison.Ordinal))
                {
                    continue;
                }

                // Correction: keep the previous record and point the new one back at it.
                await _academic.InsertAttendanceAsync(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    StudentId = entry.StudentId,
                    AttendanceDate = day,
                    Status = entry.Status,
                    Note = entry.Note,
                    RecordedByUserId = userId,
                    RecordedAtUtc = DateTime.UtcNow,
                    CorrectedFromRecordId = existing.Id
                }, token).ConfigureAwait(false);

                existing.Status = entry.Status;
                existing.Note = entry.Note;
                existing.RecordedByUserId = userId;
                existing.RecordedAtUtc = DateTime.UtcNow;
                await _academic.UpdateAttendanceAsync(existing, token).ConfigureAwait(false);
            }

            await _audit.RecordAsync("attendance.save", nameof(AttendanceRecord), batchId,
                $"{day:yyyy-MM-dd}: {entries.Count} জন", ct: token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task<AttendanceSummary> GetSummaryForBatchDateAsync(Guid batchId, DateTime date,
        CancellationToken ct = default)
    {
        IReadOnlyList<AttendanceRecord> records = await GetForBatchDateAsync(batchId, date, ct)
            .ConfigureAwait(false);
        return Summarise(records);
    }

    public async Task<AttendanceSummary> GetSummaryForStudentAsync(Guid studentId, DateRange range,
        CancellationToken ct = default)
    {
        IReadOnlyList<AttendanceRecord> records = await _unitOfWork.ExecuteAsync(
            token => _academic.GetAttendanceForStudentAsync(studentId, range, token), ct).ConfigureAwait(false);
        return Summarise(records);
    }

    private static AttendanceSummary Summarise(IReadOnlyList<AttendanceRecord> records)
    {
        int present = records.Count(r => r.Status == AttendanceStatus.Present);
        int absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        int late = records.Count(r => r.Status == AttendanceStatus.Late);
        int leave = records.Count(r => r.Status == AttendanceStatus.Leave);

        // Cancelled classes and holidays are not counted against the student.
        int total = present + absent + late + leave;
        return new AttendanceSummary(present, absent, late, leave, total);
    }
}
