namespace ShikkhaDesk.Core.Services;

/// <summary>
/// Resolves the configurable ledger accounts used by automatically generated journal entries.
/// The chart of accounts is user-editable, so system accounts are matched by a reserved code range
/// (1000-5999) rather than by hard-coded primary keys.
/// </summary>
public sealed class AccountMap
{
    public Guid StudentReceivables { get; init; }
    public Guid StudentAdvanceCredit { get; init; }
    public Guid TuitionIncome { get; init; }
    public Guid AdmissionIncome { get; init; }
    public Guid CourseIncome { get; init; }
    public Guid ExamIncome { get; init; }
    public Guid OtherIncome { get; init; }
    public Guid AccountsPayable { get; init; }
    public Guid SalaryPayable { get; init; }
    public Guid SalaryExpense { get; init; }
    public Guid BankChargeExpense { get; init; }
    public Guid OpeningBalanceEquity { get; init; }

    public Guid IncomeAccountFor(FeeChargeKind kind) => kind switch
    {
        FeeChargeKind.MonthlyTuition => TuitionIncome,
        FeeChargeKind.CourseFee => CourseIncome,
        FeeChargeKind.AdmissionFee => AdmissionIncome,
        FeeChargeKind.ExaminationFee => ExamIncome,
        FeeChargeKind.RegistrationFee => OtherIncome,
        FeeChargeKind.Other => OtherIncome,
        _ => OtherIncome
    };

    public void Validate()
    {
        var required = new (string Name, Guid Value)[]
        {
            (nameof(StudentReceivables), StudentReceivables),
            (nameof(StudentAdvanceCredit), StudentAdvanceCredit),
            (nameof(TuitionIncome), TuitionIncome),
            (nameof(AdmissionIncome), AdmissionIncome),
            (nameof(CourseIncome), CourseIncome),
            (nameof(ExamIncome), ExamIncome),
            (nameof(OtherIncome), OtherIncome),
            (nameof(AccountsPayable), AccountsPayable),
            (nameof(SalaryPayable), SalaryPayable),
            (nameof(SalaryExpense), SalaryExpense),
            (nameof(BankChargeExpense), BankChargeExpense),
            (nameof(OpeningBalanceEquity), OpeningBalanceEquity)
        };

        foreach ((string name, Guid value) in required)
        {
            if (value == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"হিসাব মানচিত্রে '{name}' হিসাব পাওয়া যায়নি। অনুগ্রহ করে হিসাবের তালিকা যাচাই করুন।");
            }
        }
    }
}
