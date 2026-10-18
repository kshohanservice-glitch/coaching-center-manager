using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Abstractions;

public interface IAcademicRepository
{
    // Students
    Task<Student?> GetStudentAsync(Guid id, CancellationToken ct = default);
    Task<Student?> GetStudentByCodeAsync(string code, CancellationToken ct = default);
    Task<PagedResult<Student>> SearchStudentsAsync(StudentQuery query, CancellationToken ct = default);
    Task<int> CountStudentsByStatusAsync(StudentStatus status, CancellationToken ct = default);
    Task<int> CountNewAdmissionsAsync(DateRange range, CancellationToken ct = default);
    Task InsertStudentAsync(Student student, CancellationToken ct = default);
    Task UpdateStudentAsync(Student student, CancellationToken ct = default);
    Task<IReadOnlyList<Student>> FindPossibleDuplicatesAsync(string fullName, string? guardianPhone,
        DateTime? dateOfBirth, CancellationToken ct = default);
    Task<string> NextStudentCodeAsync(CancellationToken ct = default);

    // Sessions, classes, courses, subjects
    Task<IReadOnlyList<AcademicSession>> GetSessionsAsync(CancellationToken ct = default);
    Task<AcademicSession?> GetActiveSessionAsync(CancellationToken ct = default);
    Task InsertSessionAsync(AcademicSession session, CancellationToken ct = default);
    Task<IReadOnlyList<ClassGrade>> GetClassGradesAsync(CancellationToken ct = default);
    Task InsertClassGradeAsync(ClassGrade grade, CancellationToken ct = default);
    Task UpdateClassGradeAsync(ClassGrade grade, CancellationToken ct = default);
    Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken ct = default);
    Task InsertCourseAsync(Course course, CancellationToken ct = default);
    Task UpdateCourseAsync(Course course, CancellationToken ct = default);
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken ct = default);
    Task InsertSubjectAsync(Subject subject, CancellationToken ct = default);

    // Batches
    Task<IReadOnlyList<Batch>> GetBatchesAsync(bool? activeOnly = null, CancellationToken ct = default);
    Task<Batch?> GetBatchAsync(Guid id, CancellationToken ct = default);
    Task InsertBatchAsync(Batch batch, CancellationToken ct = default);
    Task UpdateBatchAsync(Batch batch, CancellationToken ct = default);
    Task<int> CountActiveBatchesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BatchSchedule>> GetBatchSchedulesAsync(Guid batchId, CancellationToken ct = default);
    Task ReplaceBatchSchedulesAsync(Guid batchId, IReadOnlyList<BatchSchedule> schedules,
        CancellationToken ct = default);

    // Admissions & enrollments
    Task<string> NextAdmissionNumberAsync(CancellationToken ct = default);
    Task InsertAdmissionAsync(Admission admission, CancellationToken ct = default);
    Task<Admission?> GetAdmissionAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Admission>> GetAdmissionsAsync(DateRange range, CancellationToken ct = default);
    Task<IReadOnlyList<Enrollment>> GetEnrollmentsForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<Enrollment>> GetActiveEnrollmentsAsync(Guid batchId, CancellationToken ct = default);
    Task<Enrollment?> GetActiveEnrollmentAsync(Guid studentId, Guid batchId, CancellationToken ct = default);
    Task InsertEnrollmentAsync(Enrollment enrollment, CancellationToken ct = default);
    Task UpdateEnrollmentAsync(Enrollment enrollment, CancellationToken ct = default);

    // Staff
    Task<IReadOnlyList<Staff>> GetStaffAsync(StaffStatus? status = null, CancellationToken ct = default);
    Task<Staff?> GetStaffAsync(Guid id, CancellationToken ct = default);
    Task InsertStaffAsync(Staff staff, CancellationToken ct = default);
    Task UpdateStaffAsync(Staff staff, CancellationToken ct = default);
    Task<string> NextStaffCodeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StaffAssignment>> GetStaffAssignmentsAsync(Guid staffId, CancellationToken ct = default);
    Task ReplaceStaffAssignmentsAsync(Guid staffId, IReadOnlyList<StaffAssignment> assignments,
        CancellationToken ct = default);

    // Attendance
    Task<AttendanceRecord?> GetAttendanceAsync(Guid batchId, Guid studentId, DateTime date,
        CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> GetAttendanceForBatchDateAsync(Guid batchId, DateTime date,
        CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> GetAttendanceForStudentAsync(Guid studentId, DateRange range,
        CancellationToken ct = default);
    Task InsertAttendanceAsync(AttendanceRecord record, CancellationToken ct = default);
    Task UpdateAttendanceAsync(AttendanceRecord record, CancellationToken ct = default);

    // Exams & marks
    Task<IReadOnlyList<Exam>> GetExamsAsync(Guid? sessionId = null, CancellationToken ct = default);
    Task<Exam?> GetExamAsync(Guid id, CancellationToken ct = default);
    Task InsertExamAsync(Exam exam, CancellationToken ct = default);
    Task UpdateExamAsync(Exam exam, CancellationToken ct = default);
    Task<IReadOnlyList<ExamSubject>> GetExamSubjectsAsync(Guid examId, CancellationToken ct = default);
    Task ReplaceExamSubjectsAsync(Guid examId, IReadOnlyList<ExamSubject> subjects, CancellationToken ct = default);
    Task<IReadOnlyList<Mark>> GetMarksAsync(Guid examId, CancellationToken ct = default);
    Task<IReadOnlyList<Mark>> GetMarksForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task UpsertMarksAsync(IReadOnlyList<Mark> marks, CancellationToken ct = default);
    Task<IReadOnlyList<GradeScaleItem>> GetGradeScaleAsync(CancellationToken ct = default);
    Task ReplaceGradeScaleAsync(IReadOnlyList<GradeScaleItem> items, CancellationToken ct = default);
}
