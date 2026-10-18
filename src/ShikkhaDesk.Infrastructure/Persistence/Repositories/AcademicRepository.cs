using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core.Entities;
using Dapper;

namespace ShikkhaDesk.Infrastructure.Persistence.Repositories;

public sealed class AcademicRepository : RepositoryBase, IAcademicRepository
{
    public AcademicRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

    // ------------------------------------------------------------------ students

    private const string StudentColumns = @"
        id, student_code, full_name, guardian_name, guardian_phone, student_phone, address,
        date_of_birth, gender, school_or_college, school_class, photo_path, notes, status,
        created_at_utc, updated_at_utc";

    public Task<Student?> GetStudentAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Student>($"SELECT {StudentColumns} FROM students WHERE id = @Id;",
            new { Id = id }, ct);

    public Task<Student?> GetStudentByCodeAsync(string code, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Student>($"SELECT {StudentColumns} FROM students WHERE student_code = @Code;",
            new { Code = code }, ct);

    public async Task<PagedResult<Student>> SearchStudentsAsync(StudentQuery query, CancellationToken ct = default)
    {
        string term = Like(query.Search);
        string where = @"
            WHERE (@Status IS NULL OR status = @Status)
              AND (@BatchId IS NULL OR id IN (SELECT student_id FROM enrollments
                                              WHERE batch_id = @BatchId AND status = 0))
              AND (@ClassGradeId IS NULL OR id IN (
                    SELECT e.student_id FROM enrollments e
                      JOIN batches b ON b.id = e.batch_id
                     WHERE b.class_grade_id = @ClassGradeId))
              AND (@Search IS NULL OR full_name LIKE @Term ESCAPE '\'
                        OR student_code LIKE @Term ESCAPE '\'
                        OR IFNULL(guardian_phone, '') LIKE @Term ESCAPE '\'
                        OR IFNULL(student_phone, '') LIKE @Term ESCAPE '\')";

        int total = await ExecuteScalarAsync<int?>(
            $"SELECT COUNT(*) FROM students {where};",
            new { query.Status, query.BatchId, query.ClassGradeId, Search = query.Search, Term = term }, ct)
            .ConfigureAwait(false) ?? 0;

        int pageSize = Math.Clamp(query.PageSize, 1, 500);
        int page = Math.Max(1, query.Page);
        var items = (await QueryAsync<Student>($@"
            SELECT {StudentColumns} FROM students {where}
             ORDER BY full_name
             LIMIT @Limit OFFSET @Offset;",
            new
            {
                query.Status,
                query.BatchId,
                query.ClassGradeId,
                Search = query.Search,
                Term = term,
                Limit = pageSize,
                Offset = (page - 1) * pageSize
            }, ct).ConfigureAwait(false)).ToList();

        return new PagedResult<Student>(items, total, page, pageSize);
    }

    public Task<int> CountStudentsByStatusAsync(StudentStatus status, CancellationToken ct = default) =>
        ExecuteScalarAsync<int>("SELECT COUNT(*) FROM students WHERE status = @Status;", new { Status = status }, ct)!;

    public Task<int> CountNewAdmissionsAsync(DateRange range, CancellationToken ct = default) =>
        ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM admissions WHERE admission_date BETWEEN @From AND @To;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct)!;

    public Task InsertStudentAsync(Student student, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO students (id, student_code, full_name, guardian_name, guardian_phone, student_phone,
                                  address, date_of_birth, gender, school_or_college, school_class, photo_path,
                                  notes, status, created_at_utc, updated_at_utc)
            VALUES (@Id, @StudentCode, @FullName, @GuardianName, @GuardianPhone, @StudentPhone, @Address,
                    @DateOfBirth, @Gender, @SchoolOrCollege, @SchoolClass, @PhotoPath, @Notes, @Status,
                    @CreatedAtUtc, @UpdatedAtUtc);", student, ct);

    public Task UpdateStudentAsync(Student student, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE students SET student_code = @StudentCode, full_name = @FullName,
                   guardian_name = @GuardianName, guardian_phone = @GuardianPhone,
                   student_phone = @StudentPhone, address = @Address, date_of_birth = @DateOfBirth,
                   gender = @Gender, school_or_college = @SchoolOrCollege, school_class = @SchoolClass,
                   photo_path = @PhotoPath, notes = @Notes, status = @Status, updated_at_utc = @UpdatedAtUtc
             WHERE id = @Id;", student, ct);

    public async Task<IReadOnlyList<Student>> FindPossibleDuplicatesAsync(string fullName, string? guardianPhone,
        DateTime? dateOfBirth, CancellationToken ct = default)
    {
        var results = new List<Student>();

        if (!string.IsNullOrWhiteSpace(guardianPhone))
        {
            results.AddRange(await QueryAsync<Student>(
                $"SELECT {StudentColumns} FROM students WHERE guardian_phone = @Phone;",
                new { Phone = guardianPhone }, ct).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            results.AddRange(await QueryAsync<Student>(
                $"SELECT {StudentColumns} FROM students WHERE full_name = @Name;",
                new { Name = fullName.Trim() }, ct).ConfigureAwait(false));
        }

        return results
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .Where(s => dateOfBirth is null || s.DateOfBirth is null || s.DateOfBirth.Value.Date == dateOfBirth.Value.Date)
            .ToList();
    }

    public async Task<string> NextStudentCodeAsync(CancellationToken ct = default)
    {
        string? max = await ExecuteScalarAsync<string>(
            "SELECT student_code FROM students ORDER BY LENGTH(student_code) DESC, student_code DESC LIMIT 1;",
            null, ct).ConfigureAwait(false);

        long next = 1;
        if (!string.IsNullOrEmpty(max))
        {
            string digits = new(max.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && long.TryParse(digits, out long parsed))
            {
                next = parsed + 1;
            }
        }

        return "S-" + next.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------ sessions / classes / courses

    public async Task<IReadOnlyList<AcademicSession>> GetSessionsAsync(CancellationToken ct = default) =>
        (await QueryAsync<AcademicSession>(
            "SELECT id, name, start_date, end_date, is_active, created_at_utc FROM academic_sessions ORDER BY start_date DESC;",
            null, ct).ConfigureAwait(false)).ToList();

    public Task<AcademicSession?> GetActiveSessionAsync(CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<AcademicSession>(
            "SELECT id, name, start_date, end_date, is_active, created_at_utc FROM academic_sessions WHERE is_active = 1 LIMIT 1;",
            null, ct);

    public Task InsertSessionAsync(AcademicSession session, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO academic_sessions (id, name, start_date, end_date, is_active, created_at_utc)
            VALUES (@Id, @Name, @StartDate, @EndDate, @IsActive, @CreatedAtUtc);", session, ct);

    public async Task<IReadOnlyList<ClassGrade>> GetClassGradesAsync(CancellationToken ct = default) =>
        (await QueryAsync<ClassGrade>(
            "SELECT id, name, sort_order, is_active, created_at_utc FROM class_grades ORDER BY sort_order, name;",
            null, ct).ConfigureAwait(false)).ToList();

    public Task InsertClassGradeAsync(ClassGrade grade, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO class_grades (id, name, sort_order, is_active, created_at_utc)
            VALUES (@Id, @Name, @SortOrder, @IsActive, @CreatedAtUtc);", grade, ct);

    public Task UpdateClassGradeAsync(ClassGrade grade, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE class_grades SET name = @Name, sort_order = @SortOrder, is_active = @IsActive WHERE id = @Id;",
            grade, ct);

    public async Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken ct = default) =>
        (await QueryAsync<Course>(
            @"SELECT id, name, code, class_grade_id, duration_months, description, is_active, created_at_utc
                FROM courses ORDER BY name;", null, ct).ConfigureAwait(false)).ToList();

    public Task InsertCourseAsync(Course course, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO courses (id, name, code, class_grade_id, duration_months, description, is_active, created_at_utc)
            VALUES (@Id, @Name, @Code, @ClassGradeId, @DurationMonths, @Description, @IsActive, @CreatedAtUtc);",
            course, ct);

    public Task UpdateCourseAsync(Course course, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE courses SET name = @Name, code = @Code, class_grade_id = @ClassGradeId,
                   duration_months = @DurationMonths, description = @Description, is_active = @IsActive
             WHERE id = @Id;", course, ct);

    public async Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken ct = default) =>
        (await QueryAsync<Subject>(
            "SELECT id, name, code, course_id, is_active, created_at_utc FROM subjects ORDER BY name;",
            null, ct).ConfigureAwait(false)).ToList();

    public Task InsertSubjectAsync(Subject subject, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO subjects (id, name, code, course_id, is_active, created_at_utc)
            VALUES (@Id, @Name, @Code, @CourseId, @IsActive, @CreatedAtUtc);", subject, ct);

    // ------------------------------------------------------------------ batches

    public async Task<IReadOnlyList<Batch>> GetBatchesAsync(bool? activeOnly = null, CancellationToken ct = default) =>
        (await QueryAsync<Batch>(
            @"SELECT id, name, session_id, course_id, class_grade_id, teacher_id, start_date, end_date,
                     capacity, schedule_note, is_active, created_at_utc
                FROM batches
               WHERE (@ActiveOnly IS NULL OR is_active = @ActiveOnly)
               ORDER BY is_active DESC, name;",
            new { ActiveOnly = activeOnly }, ct).ConfigureAwait(false)).ToList();

    public Task<Batch?> GetBatchAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Batch>(
            @"SELECT id, name, session_id, course_id, class_grade_id, teacher_id, start_date, end_date,
                     capacity, schedule_note, is_active, created_at_utc
                FROM batches WHERE id = @Id;", new { Id = id }, ct);

    public Task InsertBatchAsync(Batch batch, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO batches (id, name, session_id, course_id, class_grade_id, teacher_id, start_date,
                                 end_date, capacity, schedule_note, is_active, created_at_utc)
            VALUES (@Id, @Name, @SessionId, @CourseId, @ClassGradeId, @TeacherId, @StartDate, @EndDate,
                    @Capacity, @ScheduleNote, @IsActive, @CreatedAtUtc);", batch, ct);

    public Task UpdateBatchAsync(Batch batch, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE batches SET name = @Name, session_id = @SessionId, course_id = @CourseId,
                   class_grade_id = @ClassGradeId, teacher_id = @TeacherId, start_date = @StartDate,
                   end_date = @EndDate, capacity = @Capacity, schedule_note = @ScheduleNote,
                   is_active = @IsActive
             WHERE id = @Id;", batch, ct);

    public Task<int> CountActiveBatchesAsync(CancellationToken ct = default) =>
        ExecuteScalarAsync<int>("SELECT COUNT(*) FROM batches WHERE is_active = 1;", null, ct)!;

    public async Task<IReadOnlyList<BatchSchedule>> GetBatchSchedulesAsync(Guid batchId, CancellationToken ct = default) =>
        (await QueryAsync<BatchSchedule>(
            @"SELECT id, batch_id, day_of_week, start_time, end_time, room
                FROM batch_schedules WHERE batch_id = @BatchId ORDER BY day_of_week, start_time;",
            new { BatchId = batchId }, ct).ConfigureAwait(false)).ToList();

    public async Task ReplaceBatchSchedulesAsync(Guid batchId, IReadOnlyList<BatchSchedule> schedules,
        CancellationToken ct = default)
    {
        await ExecuteAsync("DELETE FROM batch_schedules WHERE batch_id = @BatchId;", new { BatchId = batchId }, ct)
            .ConfigureAwait(false);
        foreach (BatchSchedule schedule in schedules)
        {
            await ExecuteAsync(@"
                INSERT INTO batch_schedules (id, batch_id, day_of_week, start_time, end_time, room)
                VALUES (@Id, @BatchId, @DayOfWeek, @StartTime, @EndTime, @Room);", schedule, ct)
                .ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------ admissions & enrollments

    public async Task<string> NextAdmissionNumberAsync(CancellationToken ct = default)
    {
        string? max = await ExecuteScalarAsync<string>(
            "SELECT admission_number FROM admissions ORDER BY LENGTH(admission_number) DESC, admission_number DESC LIMIT 1;",
            null, ct).ConfigureAwait(false);

        long next = 1;
        if (!string.IsNullOrEmpty(max))
        {
            string digits = new(max.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && long.TryParse(digits, out long parsed))
            {
                next = parsed + 1;
            }
        }

        return "ADM-" + DateTime.UtcNow.Year + "-" + next.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
    }

    public Task InsertAdmissionAsync(Admission admission, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO admissions (id, admission_number, student_id, session_id, admission_date, course_id,
                                    class_grade_id, batch_id, admission_fee, fee_plan_id, receipt_id, notes,
                                    created_by_user_id, created_at_utc)
            VALUES (@Id, @AdmissionNumber, @StudentId, @SessionId, @AdmissionDate, @CourseId, @ClassGradeId,
                    @BatchId, @AdmissionFee, @FeePlanId, @ReceiptId, @Notes, @CreatedByUserId, @CreatedAtUtc);",
            admission, ct);

    public Task<Admission?> GetAdmissionAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Admission>(
            @"SELECT id, admission_number, student_id, session_id, admission_date, course_id, class_grade_id,
                     batch_id, admission_fee, fee_plan_id, receipt_id, notes, created_by_user_id, created_at_utc
                FROM admissions WHERE id = @Id;", new { Id = id }, ct);

    public async Task<IReadOnlyList<Admission>> GetAdmissionsAsync(DateRange range, CancellationToken ct = default) =>
        (await QueryAsync<Admission>(
            @"SELECT id, admission_number, student_id, session_id, admission_date, course_id, class_grade_id,
                     batch_id, admission_fee, fee_plan_id, receipt_id, notes, created_by_user_id, created_at_utc
                FROM admissions WHERE admission_date BETWEEN @From AND @To ORDER BY admission_date DESC;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<Enrollment>> GetEnrollmentsForStudentAsync(Guid studentId,
        CancellationToken ct = default) =>
        (await QueryAsync<Enrollment>(
            @"SELECT id, student_id, batch_id, session_id, enrolled_on, left_on, status, fee_plan_id, notes, created_at_utc
                FROM enrollments WHERE student_id = @StudentId ORDER BY enrolled_on DESC;",
            new { StudentId = studentId }, ct).ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<Enrollment>> GetActiveEnrollmentsAsync(Guid batchId,
        CancellationToken ct = default) =>
        (await QueryAsync<Enrollment>(
            @"SELECT id, student_id, batch_id, session_id, enrolled_on, left_on, status, fee_plan_id, notes, created_at_utc
                FROM enrollments WHERE batch_id = @BatchId AND status = 0 ORDER BY enrolled_on;",
            new { BatchId = batchId }, ct).ConfigureAwait(false)).ToList();

    public Task<Enrollment?> GetActiveEnrollmentAsync(Guid studentId, Guid batchId, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Enrollment>(
            @"SELECT id, student_id, batch_id, session_id, enrolled_on, left_on, status, fee_plan_id, notes, created_at_utc
                FROM enrollments WHERE student_id = @StudentId AND batch_id = @BatchId AND status = 0;",
            new { StudentId = studentId, BatchId = batchId }, ct);

    public Task InsertEnrollmentAsync(Enrollment enrollment, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO enrollments (id, student_id, batch_id, session_id, enrolled_on, left_on, status,
                                     fee_plan_id, notes, created_at_utc)
            VALUES (@Id, @StudentId, @BatchId, @SessionId, @EnrolledOn, @LeftOn, @Status, @FeePlanId, @Notes, @CreatedAtUtc);",
            enrollment, ct);

    public Task UpdateEnrollmentAsync(Enrollment enrollment, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE enrollments SET student_id = @StudentId, batch_id = @BatchId, session_id = @SessionId,
                   enrolled_on = @EnrolledOn, left_on = @LeftOn, status = @Status, fee_plan_id = @FeePlanId,
                   notes = @Notes
             WHERE id = @Id;", enrollment, ct);

    // ------------------------------------------------------------------ staff

    private const string StaffColumns = @"
        id, staff_code, full_name, designation, phone, email, address, joining_date, leaving_date, status,
        basic_salary, house_rent_allowance, medical_allowance, transport_allowance, other_allowance,
        provident_fund_deduction, tax_deduction, other_deduction, bank_account_name, bank_account_number,
        notes, created_at_utc";

    public async Task<IReadOnlyList<Staff>> GetStaffAsync(StaffStatus? status = null, CancellationToken ct = default) =>
        (await QueryAsync<Staff>(
            $"SELECT {StaffColumns} FROM staff WHERE (@Status IS NULL OR status = @Status) ORDER BY full_name;",
            new { Status = status }, ct).ConfigureAwait(false)).ToList();

    public Task<Staff?> GetStaffAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Staff>($"SELECT {StaffColumns} FROM staff WHERE id = @Id;", new { Id = id }, ct);

    public Task InsertStaffAsync(Staff staff, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO staff (id, staff_code, full_name, designation, phone, email, address, joining_date,
                               leaving_date, status, basic_salary, house_rent_allowance, medical_allowance,
                               transport_allowance, other_allowance, provident_fund_deduction, tax_deduction,
                               other_deduction, bank_account_name, bank_account_number, notes, created_at_utc)
            VALUES (@Id, @StaffCode, @FullName, @Designation, @Phone, @Email, @Address, @JoiningDate,
                    @LeavingDate, @Status, @BasicSalary, @HouseRentAllowance, @MedicalAllowance,
                    @TransportAllowance, @OtherAllowance, @ProvidentFundDeduction, @TaxDeduction,
                    @OtherDeduction, @BankAccountName, @BankAccountNumber, @Notes, @CreatedAtUtc);", staff, ct);

    public Task UpdateStaffAsync(Staff staff, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE staff SET staff_code = @StaffCode, full_name = @FullName, designation = @Designation,
                   phone = @Phone, email = @Email, address = @Address, joining_date = @JoiningDate,
                   leaving_date = @LeavingDate, status = @Status, basic_salary = @BasicSalary,
                   house_rent_allowance = @HouseRentAllowance, medical_allowance = @MedicalAllowance,
                   transport_allowance = @TransportAllowance, other_allowance = @OtherAllowance,
                   provident_fund_deduction = @ProvidentFundDeduction, tax_deduction = @TaxDeduction,
                   other_deduction = @OtherDeduction, bank_account_name = @BankAccountName,
                   bank_account_number = @BankAccountNumber, notes = @Notes
             WHERE id = @Id;", staff, ct);

    public async Task<string> NextStaffCodeAsync(CancellationToken ct = default)
    {
        string? max = await ExecuteScalarAsync<string>(
            "SELECT staff_code FROM staff ORDER BY LENGTH(staff_code) DESC, staff_code DESC LIMIT 1;", null, ct)
            .ConfigureAwait(false);

        long next = 1;
        if (!string.IsNullOrEmpty(max))
        {
            string digits = new(max.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && long.TryParse(digits, out long parsed))
            {
                next = parsed + 1;
            }
        }

        return "T-" + next.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<StaffAssignment>> GetStaffAssignmentsAsync(Guid staffId,
        CancellationToken ct = default) =>
        (await QueryAsync<StaffAssignment>(
            @"SELECT id, staff_id, batch_id, subject_id, role, assigned_on, is_active
                FROM staff_assignments WHERE staff_id = @StaffId;", new { StaffId = staffId }, ct)
            .ConfigureAwait(false)).ToList();

    public async Task ReplaceStaffAssignmentsAsync(Guid staffId, IReadOnlyList<StaffAssignment> assignments,
        CancellationToken ct = default)
    {
        await ExecuteAsync("DELETE FROM staff_assignments WHERE staff_id = @StaffId;", new { StaffId = staffId }, ct)
            .ConfigureAwait(false);
        foreach (StaffAssignment assignment in assignments)
        {
            await ExecuteAsync(@"
                INSERT INTO staff_assignments (id, staff_id, batch_id, subject_id, role, assigned_on, is_active)
                VALUES (@Id, @StaffId, @BatchId, @SubjectId, @Role, @AssignedOn, @IsActive);", assignment, ct)
                .ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------ attendance

    private const string AttendanceColumns = @"
        id, batch_id, student_id, attendance_date, status, note, recorded_by_user_id, recorded_at_utc,
        corrected_from_record_id";

    public Task<AttendanceRecord?> GetAttendanceAsync(Guid batchId, Guid studentId, DateTime date,
        CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<AttendanceRecord>(
            $"SELECT {AttendanceColumns} FROM attendance_records WHERE batch_id = @BatchId AND student_id = @StudentId AND attendance_date = @Date;",
            new { BatchId = batchId, StudentId = studentId, Date = StartOfDay(date) }, ct);

    public async Task<IReadOnlyList<AttendanceRecord>> GetAttendanceForBatchDateAsync(Guid batchId, DateTime date,
        CancellationToken ct = default) =>
        (await QueryAsync<AttendanceRecord>(
            $"SELECT {AttendanceColumns} FROM attendance_records WHERE batch_id = @BatchId AND attendance_date = @Date ORDER BY student_id;",
            new { BatchId = batchId, Date = StartOfDay(date) }, ct).ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<AttendanceRecord>> GetAttendanceForStudentAsync(Guid studentId, DateRange range,
        CancellationToken ct = default) =>
        (await QueryAsync<AttendanceRecord>(
            $"SELECT {AttendanceColumns} FROM attendance_records WHERE student_id = @StudentId AND attendance_date BETWEEN @From AND @To ORDER BY attendance_date;",
            new { StudentId = studentId, From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct)
            .ConfigureAwait(false)).ToList();

    public Task InsertAttendanceAsync(AttendanceRecord record, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO attendance_records (id, batch_id, student_id, attendance_date, status, note,
                                            recorded_by_user_id, recorded_at_utc, corrected_from_record_id)
            VALUES (@Id, @BatchId, @StudentId, @AttendanceDate, @Status, @Note, @RecordedByUserId,
                    @RecordedAtUtc, @CorrectedFromRecordId);", record, ct);

    public Task UpdateAttendanceAsync(AttendanceRecord record, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE attendance_records SET status = @Status, note = @Note, recorded_by_user_id = @RecordedByUserId,
                   recorded_at_utc = @RecordedAtUtc, corrected_from_record_id = @CorrectedFromRecordId
             WHERE id = @Id;", record, ct);

    // ------------------------------------------------------------------ exams & marks

    public async Task<IReadOnlyList<Exam>> GetExamsAsync(Guid? sessionId = null, CancellationToken ct = default) =>
        (await QueryAsync<Exam>(
            @"SELECT id, name, exam_type, session_id, exam_date, batch_id, status, created_at_utc
                FROM exams WHERE (@SessionId IS NULL OR session_id = @SessionId) ORDER BY exam_date DESC;",
            new { SessionId = sessionId }, ct).ConfigureAwait(false)).ToList();

    public Task<Exam?> GetExamAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Exam>(
            @"SELECT id, name, exam_type, session_id, exam_date, batch_id, status, created_at_utc
                FROM exams WHERE id = @Id;", new { Id = id }, ct);

    public Task InsertExamAsync(Exam exam, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO exams (id, name, exam_type, session_id, exam_date, batch_id, status, created_at_utc)
            VALUES (@Id, @Name, @ExamType, @SessionId, @ExamDate, @BatchId, @Status, @CreatedAtUtc);", exam, ct);

    public Task UpdateExamAsync(Exam exam, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE exams SET name = @Name, exam_type = @ExamType, session_id = @SessionId, exam_date = @ExamDate,
                   batch_id = @BatchId, status = @Status
             WHERE id = @Id;", exam, ct);

    public async Task<IReadOnlyList<ExamSubject>> GetExamSubjectsAsync(Guid examId, CancellationToken ct = default) =>
        (await QueryAsync<ExamSubject>(
            @"SELECT id, exam_id, subject_id, subject_name, maximum_marks, pass_marks, exam_date, sort_order
                FROM exam_subjects WHERE exam_id = @ExamId ORDER BY sort_order, subject_name;",
            new { ExamId = examId }, ct).ConfigureAwait(false)).ToList();

    public Task<ExamSubject?> GetExamSubjectAsync(Guid examSubjectId, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<ExamSubject>(@"
            SELECT id, exam_id, subject_id, subject_name, maximum_marks, pass_marks, exam_date, sort_order
              FROM exam_subjects WHERE id = @Id;", new { Id = examSubjectId }, ct);

    public async Task ReplaceExamSubjectsAsync(Guid examId, IReadOnlyList<ExamSubject> subjects,
        CancellationToken ct = default)
    {
        await ExecuteAsync("DELETE FROM exam_subjects WHERE exam_id = @ExamId;", new { ExamId = examId }, ct)
            .ConfigureAwait(false);
        foreach (ExamSubject subject in subjects)
        {
            await ExecuteAsync(@"
                INSERT INTO exam_subjects (id, exam_id, subject_id, subject_name, maximum_marks, pass_marks,
                                           exam_date, sort_order)
                VALUES (@Id, @ExamId, @SubjectId, @SubjectName, @MaximumMarks, @PassMarks, @ExamDate, @SortOrder);",
                subject, ct).ConfigureAwait(false);
        }
    }

    private const string MarkColumns = @"
        id, exam_subject_id, student_id, marks_obtained, is_absent, grade, grade_point, remarks,
        entered_by_user_id, entered_at_utc, corrected_from_mark_id";

    public async Task<IReadOnlyList<Mark>> GetMarksAsync(Guid examId, CancellationToken ct = default) =>
        (await QueryAsync<Mark>(
            $@"SELECT {MarkColumns} FROM marks
                WHERE exam_subject_id IN (SELECT id FROM exam_subjects WHERE exam_id = @ExamId);",
            new { ExamId = examId }, ct).ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<Mark>> GetMarksForStudentAsync(Guid studentId, CancellationToken ct = default) =>
        (await QueryAsync<Mark>($"SELECT {MarkColumns} FROM marks WHERE student_id = @StudentId;",
            new { StudentId = studentId }, ct).ConfigureAwait(false)).ToList();

    public async Task UpsertMarksAsync(IReadOnlyList<Mark> marks, CancellationToken ct = default)
    {
        foreach (Mark mark in marks)
        {
            await ExecuteAsync(@"
                INSERT INTO marks (id, exam_subject_id, student_id, marks_obtained, is_absent, grade,
                                   grade_point, remarks, entered_by_user_id, entered_at_utc, corrected_from_mark_id)
                VALUES (@Id, @ExamSubjectId, @StudentId, @MarksObtained, @IsAbsent, @Grade, @GradePoint,
                        @Remarks, @EnteredByUserId, @EnteredAtUtc, @CorrectedFromMarkId)
                ON CONFLICT (exam_subject_id, student_id)
                DO UPDATE SET marks_obtained = @MarksObtained, is_absent = @IsAbsent, grade = @Grade,
                              grade_point = @GradePoint, remarks = @Remarks,
                              entered_by_user_id = @EnteredByUserId, entered_at_utc = @EnteredAtUtc,
                              corrected_from_mark_id = marks.id;", mark, ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<GradeScaleItem>> GetGradeScaleAsync(CancellationToken ct = default) =>
        (await QueryAsync<GradeScaleItem>(
            @"SELECT id, grade, minimum_percentage, grade_point, sort_order FROM grade_scale ORDER BY minimum_percentage DESC;",
            null, ct).ConfigureAwait(false)).ToList();

    public async Task ReplaceGradeScaleAsync(IReadOnlyList<GradeScaleItem> items, CancellationToken ct = default)
    {
        await ExecuteAsync("DELETE FROM grade_scale;", null, ct).ConfigureAwait(false);
        foreach (GradeScaleItem item in items)
        {
            await ExecuteAsync(@"
                INSERT INTO grade_scale (id, grade, minimum_percentage, grade_point, sort_order)
                VALUES (@Id, @Grade, @MinimumPercentage, @GradePoint, @SortOrder);", item, ct)
                .ConfigureAwait(false);
        }
    }
}
