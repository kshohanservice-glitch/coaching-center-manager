namespace ShikkhaDesk.Core;

public enum DigitPreference
{
    Bengali = 0,
    English = 1
}

public enum StudentStatus
{
    Active = 0,
    Archived = 1,
    Withdrawn = 2
}

public enum EnrollmentStatus
{
    Active = 0,
    Transferred = 1,
    Completed = 2,
    Withdrawn = 3
}

public enum StaffStatus
{
    Active = 0,
    Inactive = 1
}

public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    Late = 2,
    Leave = 3,
    Holiday = 4,
    Cancelled = 5
}

public enum FeeChargeKind
{
    MonthlyTuition = 0,
    CourseFee = 1,
    AdmissionFee = 2,
    ExaminationFee = 3,
    RegistrationFee = 4,
    Other = 5
}

public enum PaymentMethod
{
    Cash = 0,
    Bank = 1,
    BKash = 2,
    Nagad = 3,
    Rocket = 4,
    Upay = 5,
    Other = 6
}

public enum PaymentStatus
{
    Posted = 0,
    Reversed = 1
}

public enum CreditTransactionKind
{
    Received = 0,
    Allocated = 1,
    Refunded = 2,
    Adjusted = 3
}

public enum AccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Income = 3,
    Expense = 4
}

public enum FinancialAccountKind
{
    Cash = 0,
    Bank = 1,
    MobileBanking = 2
}

public enum JournalStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2
}

public enum TransactionSourceKind
{
    Manual = 0,
    FeeAssessment = 1,
    FeePayment = 2,
    CreditAllocation = 3,
    Refund = 4,
    Income = 5,
    Expense = 6,
    PayableAccrual = 7,
    PayableSettlement = 8,
    PayrollAccrual = 9,
    PayrollPayment = 10,
    Transfer = 11,
    OpeningBalance = 12,
    Reversal = 13
}

public enum PayableStatus
{
    Open = 0,
    PartiallyPaid = 1,
    Settled = 2,
    Cancelled = 3
}

public enum PayrollStatus
{
    Accrued = 0,
    Paid = 1,
    Cancelled = 2
}

public enum BackupStatus
{
    Succeeded = 0,
    Failed = 1,
    Running = 2
}

public enum DeviceStatus
{
    Registered = 0,
    Revoked = 1
}

public enum LicenseStatus
{
    NotActivated = 0,
    Valid = 1,
    Expired = 2,
    DeviceLimitReached = 3,
    InvalidSignature = 4,
    InstitutionMismatch = 5
}

public enum HostMode
{
    SingleComputer = 0,
    LanHost = 1,
    LanClient = 2
}

public enum ExamStatus
{
    Draft = 0,
    Published = 1
}
