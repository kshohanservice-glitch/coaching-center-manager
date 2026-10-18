using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record ExamMarkEntry(Guid StudentId, decimal? MarksObtained, bool IsAbsent, string? Remarks);

public interface IExamService
{
    Task<IReadOnlyList<Exam>> GetExamsAsync(Guid? sessionId = null, CancellationToken ct = default);
    Task<Guid> SaveExamAsync(Exam exam, IReadOnlyList<ExamSubject> subjects, CancellationToken ct = default);
    Task<IReadOnlyList<ExamSubject>> GetExamSubjectsAsync(Guid examId, CancellationToken ct = default);
    Task<IReadOnlyList<Mark>> GetMarksAsync(Guid examId, CancellationToken ct = default);
    Task SaveMarksAsync(Guid examSubjectId, IReadOnlyList<ExamMarkEntry> entries, CancellationToken ct = default);
    Task PublishAsync(Guid examId, CancellationToken ct = default);
    Task<IReadOnlyList<GradeScaleItem>> GetGradeScaleAsync(CancellationToken ct = default);
    Task SaveGradeScaleAsync(IReadOnlyList<GradeScaleItem> items, CancellationToken ct = default);
}

/// <summary>
/// Examinations and results. Marks are validated against the subject maximum, absences are recorded
/// explicitly rather than as zero, and any change to a published result keeps a link to the mark it
/// replaces so the correction is auditable.
/// </summary>
public sealed class ExamService : IExamService
{
    private readonly IAcademicRepository _academic;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public ExamService(IAcademicRepository academic, IUnitOfWork unitOfWork, IAuditService audit,
        ICurrentUserService currentUser)
    {
        _academic = academic;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<Exam>> GetExamsAsync(Guid? sessionId = null, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetExamsAsync(sessionId, token), ct);

    public async Task<Guid> SaveExamAsync(Exam exam, IReadOnlyList<ExamSubject> subjects,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("exams.manage");
        ArgumentNullException.ThrowIfNull(exam);

        if (string.IsNullOrWhiteSpace(exam.Name))
        {
            throw new ValidationException("পরীক্ষার নাম প্রয়োজন।");
        }

        foreach (ExamSubject subject in subjects ?? [])
        {
            if (subject.MaximumMarks <= 0)
            {
                throw new ValidationException($"{subject.SubjectName}: পূর্ণমান শূন্যের বেশি হতে হবে।");
            }

            if (subject.PassMarks < 0 || subject.PassMarks > subject.MaximumMarks)
            {
                throw new ValidationException($"{subject.SubjectName}: পাস নম্বর ০ থেকে পূর্ণমানের মধ্যে হতে হবে।");
            }
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            bool isNew = exam.Id == Guid.Empty;
            exam.Id = isNew ? Guid.NewGuid() : exam.Id;
            exam.CreatedAtUtc = isNew ? DateTime.UtcNow : exam.CreatedAtUtc;

            if (isNew)
            {
                await _academic.InsertExamAsync(exam, token).ConfigureAwait(false);
            }
            else
            {
                Exam? existing = await _academic.GetExamAsync(exam.Id, token).ConfigureAwait(false);
                if (existing is { Status: ExamStatus.Published })
                {
                    throw new DomainException(
                        "প্রকাশিত পরীক্ষার বিষয় পরিবর্তন করা যাবে না। প্রথমে ফলাফল অপ্রকাশিত করুন।");
                }

                await _academic.UpdateExamAsync(exam, token).ConfigureAwait(false);
            }

            if (subjects is { Count: > 0 })
            {
                await _academic.ReplaceExamSubjectsAsync(exam.Id, subjects, token).ConfigureAwait(false);
            }

            await _audit.RecordAsync(isNew ? "exams.create" : "exams.update", nameof(Exam), exam.Id, exam.Name,
                ct: token).ConfigureAwait(false);

            return exam.Id;
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ExamSubject>> GetExamSubjectsAsync(Guid examId, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetExamSubjectsAsync(examId, token), ct);

    public Task<IReadOnlyList<Mark>> GetMarksAsync(Guid examId, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetMarksAsync(examId, token), ct);

    public async Task SaveMarksAsync(Guid examSubjectId, IReadOnlyList<ExamMarkEntry> entries,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("exams.manage");
        ArgumentNullException.ThrowIfNull(entries);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            ExamSubject? subject = await _academic.GetExamSubjectAsync(examSubjectId, token)
                .ConfigureAwait(false);

            if (subject is null)
            {
                throw new NotFoundException("পরীক্ষার বিষয় পাওয়া যায়নি।");
            }

            IReadOnlyList<GradeScaleItem> scale = await _academic.GetGradeScaleAsync(token).ConfigureAwait(false);
            Guid userId = _currentUser.Current.UserId;

            var marks = new List<Mark>(entries.Count);
            foreach (ExamMarkEntry entry in entries)
            {
                if (entry.IsAbsent)
                {
                    marks.Add(new Mark
                    {
                        Id = Guid.NewGuid(),
                        ExamSubjectId = examSubjectId,
                        StudentId = entry.StudentId,
                        MarksObtained = null,
                        IsAbsent = true,
                        Grade = "F",
                        GradePoint = 0,
                        Remarks = string.IsNullOrWhiteSpace(entry.Remarks) ? "অনুপস্থিত" : entry.Remarks,
                        EnteredByUserId = userId,
                        EnteredAtUtc = DateTime.UtcNow
                    });

                    continue;
                }

                if (entry.MarksObtained is null)
                {
                    throw new ValidationException("নম্বর দিন অথবা অনুপস্থিত হিসেবে চিহ্নিত করুন।");
                }

                if (entry.MarksObtained.Value < 0 || entry.MarksObtained.Value > subject.MaximumMarks)
                {
                    throw new ValidationException(
                        $"নম্বর ০ থেকে {subject.MaximumMarks} এর মধ্যে হতে হবে।");
                }

                decimal percentage = GradeCalculator.Percentage(entry.MarksObtained.Value, subject.MaximumMarks);
                string? grade = GradeCalculator.GradeFor(percentage, scale);
                decimal? gradePoint = GradeCalculator.GradePointFor(grade, scale);

                marks.Add(new Mark
                {
                    Id = Guid.NewGuid(),
                    ExamSubjectId = examSubjectId,
                    StudentId = entry.StudentId,
                    MarksObtained = entry.MarksObtained.Value,
                    IsAbsent = false,
                    Grade = grade,
                    GradePoint = gradePoint,
                    Remarks = entry.Remarks,
                    EnteredByUserId = userId,
                    EnteredAtUtc = DateTime.UtcNow
                });
            }

            await _academic.UpsertMarksAsync(marks, token).ConfigureAwait(false);
            await _audit.RecordAsync("exams.marks", nameof(Mark), examSubjectId,
                $"{subject.SubjectName}: {marks.Count} জন", ct: token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task PublishAsync(Guid examId, CancellationToken ct = default)
    {
        _currentUser.Current.Require("exams.publish");

        await _unitOfWork.ExecuteAsync(async token =>
        {
            Exam? exam = await _academic.GetExamAsync(examId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("পরীক্ষা পাওয়া যায়নি।");

            exam.Status = ExamStatus.Published;
            await _academic.UpdateExamAsync(exam, token).ConfigureAwait(false);
            await _audit.RecordAsync("exams.publish", nameof(Exam), examId, exam.Name, ct: token)
                .ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<GradeScaleItem>> GetGradeScaleAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _academic.GetGradeScaleAsync(token), ct);

    public async Task SaveGradeScaleAsync(IReadOnlyList<GradeScaleItem> items, CancellationToken ct = default)
    {
        _currentUser.Current.Require("exams.manage");
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ValidationException("গ্রেড স্কেল খালি হতে পারবে না।");
        }

        await _unitOfWork.ExecuteAsync(token => _academic.ReplaceGradeScaleAsync(items, token), ct)
            .ConfigureAwait(false);
    }
}
