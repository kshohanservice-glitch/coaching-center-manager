namespace ShikkhaDesk.Core.Entities;

/// <summary>Institution profile shown on receipts, reports and the login screen.</summary>
public sealed class Institution
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameBn { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoPath { get; set; }
    public string? RegistrationNumber { get; set; }
    public string CurrencyCode { get; set; } = "BDT";
    public string CurrencySymbol { get; set; } = "৳";
    public DigitPreference DigitPreference { get; set; } = DigitPreference.Bengali;

    /// <summary>Prefix for receipt numbers, e.g. "SD" produces SD-000001.</summary>
    public string ReceiptPrefix { get; set; } = "SD";

    public DateTime? EstablishedOn { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AcademicSession
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A class or grade level, e.g. "Class 9" or "HSC 1st year".</summary>
public sealed class ClassGrade
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Course
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Guid? ClassGradeId { get; set; }
    public int DurationMonths { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Subject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Guid? CourseId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Batch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ClassGradeId { get; set; }
    public Guid? TeacherId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Capacity { get; set; }
    public string? ScheduleNote { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class BatchSchedule
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    /// <summary>0 = Sunday ... 6 = Saturday (Gregorian week as used in Bangladesh).</summary>
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = "00:00";
    public string EndTime { get; set; } = "00:00";
    public string? Room { get; set; }
}

public sealed class Staff
{
    public Guid Id { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTime JoiningDate { get; set; }
    public DateTime? LeavingDate { get; set; }
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    public Money BasicSalary { get; set; }
    public Money HouseRentAllowance { get; set; }
    public Money MedicalAllowance { get; set; }
    public Money TransportAllowance { get; set; }
    public Money OtherAllowance { get; set; }
    public Money ProvidentFundDeduction { get; set; }
    public Money TaxDeduction { get; set; }
    public Money OtherDeduction { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gross monthly salary before deductions.</summary>
    public Money GrossSalary => BasicSalary + HouseRentAllowance + MedicalAllowance
                                + TransportAllowance + OtherAllowance;

    public Money TotalDeductions => ProvidentFundDeduction + TaxDeduction + OtherDeduction;

    public Money NetSalary => GrossSalary - TotalDeductions;
}

public sealed class StaffAssignment
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? SubjectId { get; set; }
    public string? Role { get; set; }
    public DateTime AssignedOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Student
{
    public Guid Id { get; set; }
    /// <summary>Stable, human-readable identifier; never derived from the current class or batch.</summary>
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? StudentPhone { get; set; }
    public string? Address { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? SchoolOrCollege { get; set; }
    public string? SchoolClass { get; set; }
    public string? PhotoPath { get; set; }
    public string? Notes { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class Admission
{
    public Guid Id { get; set; }
    public string AdmissionNumber { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Guid SessionId { get; set; }
    public DateTime AdmissionDate { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ClassGradeId { get; set; }
    public Guid? BatchId { get; set; }
    public Money AdmissionFee { get; set; }
    public Guid? FeePlanId { get; set; }
    public Guid? ReceiptId { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Enrollment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid BatchId { get; set; }
    public Guid SessionId { get; set; }
    public DateTime EnrolledOn { get; set; }
    public DateTime? LeftOn { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public Guid? FeePlanId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid RecordedByUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public Guid? CorrectedFromRecordId { get; set; }
}

public sealed class Exam
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExamType { get; set; }
    public Guid SessionId { get; set; }
    public DateTime ExamDate { get; set; }
    public Guid? BatchId { get; set; }
    public ExamStatus Status { get; set; } = ExamStatus.Draft;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ExamSubject
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid? SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public decimal MaximumMarks { get; set; }
    public decimal PassMarks { get; set; }
    public DateTime? ExamDate { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Mark
{
    public Guid Id { get; set; }
    public Guid ExamSubjectId { get; set; }
    public Guid StudentId { get; set; }
    public decimal? MarksObtained { get; set; }
    public bool IsAbsent { get; set; }
    public string? Grade { get; set; }
    public decimal? GradePoint { get; set; }
    public string? Remarks { get; set; }
    public Guid EnteredByUserId { get; set; }
    public DateTime EnteredAtUtc { get; set; }
    public Guid? CorrectedFromMarkId { get; set; }
}

public sealed class GradeScaleItem
{
    public Guid Id { get; set; }
    public string Grade { get; set; } = string.Empty;
    public decimal MinimumPercentage { get; set; }
    public decimal GradePoint { get; set; }
    public int SortOrder { get; set; }
}
