namespace ShikkhaDesk.Core.Services;

/// <summary>
/// Reserved chart-of-accounts codes used by automatically generated journal entries. The accounting
/// engine resolves accounts by code rather than by name or primary key, so renaming an account in the
/// UI can never break posting, and an upgraded database resolves to the same accounts.
/// </summary>
public static class SystemAccountCodes
{
    public const string CashInHand = "1100";
    public const string BankAccounts = "1200";
    public const string MobileBanking = "1300";
    public const string StudentReceivables = "1400";
    public const string OtherReceivables = "1500";
    public const string FixedAssets = "1600";

    public const string StudentAdvanceCredit = "2100";
    public const string AccountsPayable = "2200";
    public const string SalaryPayable = "2300";
    public const string OtherLiabilities = "2400";

    public const string OpeningCapital = "3100";
    public const string RetainedEarnings = "3200";

    public const string TuitionIncome = "4100";
    public const string AdmissionIncome = "4200";
    public const string CourseIncome = "4300";
    public const string ExamIncome = "4400";
    public const string OtherIncome = "4900";

    public const string TeacherSalaries = "5100";
    public const string StaffSalaries = "5110";
    public const string OfficeRent = "5200";
    public const string Electricity = "5300";
    public const string Internet = "5400";
    public const string Stationery = "5500";
    public const string Marketing = "5600";
    public const string Maintenance = "5700";
    public const string Equipment = "5800";
    public const string Transport = "5850";
    public const string BankCharges = "5900";
    public const string OtherExpenses = "5990";
}
