using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// Bengali names for the domain's fixed lists. Kept in one place because a fee kind that reads
/// "টিউশন ফি" on one screen and "মাসিক টিউশন" on the next is a support call waiting to happen.
/// Only values that actually exist in the domain are named here — the UI never invents a category
/// the ledger cannot post.
/// </summary>
public static class Labels
{
    public static string For(FeeChargeKind kind) => kind switch
    {
        FeeChargeKind.MonthlyTuition => "মাসিক টিউশন ফি",
        FeeChargeKind.CourseFee => "কোর্স ফি",
        FeeChargeKind.AdmissionFee => "ভর্তি ফি",
        FeeChargeKind.ExaminationFee => "পরীক্ষা ফি",
        FeeChargeKind.RegistrationFee => "রেজিস্ট্রেশন ফি",
        _ => "অন্যান্য ফি"
    };

    /// <summary>The fee kinds a plan item may use, in combo-box order.</summary>
    public static IReadOnlyList<FeeChargeKind> FeeChargeKinds { get; } =
    [
        FeeChargeKind.MonthlyTuition,
        FeeChargeKind.CourseFee,
        FeeChargeKind.AdmissionFee,
        FeeChargeKind.ExaminationFee,
        FeeChargeKind.RegistrationFee,
        FeeChargeKind.Other
    ];

    public static string For(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "নগদ",
        PaymentMethod.Bank => "ব্যাংক",
        PaymentMethod.BKash => "বিকাশ",
        PaymentMethod.Nagad => "নগদ (নগদ অ্যাপ)",
        PaymentMethod.Rocket => "রকেট",
        PaymentMethod.Upay => "উপায়",
        _ => "অন্যান্য"
    };

    /// <summary>The payment methods offered for collecting money, in combo-box order.</summary>
    public static IReadOnlyList<PaymentMethod> PaymentMethods { get; } =
        [PaymentMethod.Cash, PaymentMethod.Bank, PaymentMethod.BKash, PaymentMethod.Rocket];

    public static PaymentMethod MethodAt(int index) => index switch
    {
        1 => PaymentMethod.Bank,
        2 => PaymentMethod.BKash,
        3 => PaymentMethod.Rocket,
        _ => PaymentMethod.Cash
    };

    public static string For(PaymentStatus status) => status switch
    {
        PaymentStatus.Posted => "বৈধ",
        PaymentStatus.Reversed => "বাতিল",
        _ => status.ToString()
    };

    public static string For(PayrollStatus status) => status switch
    {
        PayrollStatus.Accrued => "খসড়া",
        PayrollStatus.Paid => "পরিশোধিত",
        PayrollStatus.Cancelled => "বাতিল",
        _ => status.ToString()
    };

    public static string For(StudentStatus status) => status switch
    {
        StudentStatus.Active => "চলমান",
        StudentStatus.Archived => "সংরক্ষিত",
        StudentStatus.Withdrawn => "ছাড়পত্র নিয়েছে",
        _ => status.ToString()
    };

    public static string For(ExamStatus status) => status switch
    {
        ExamStatus.Draft => "খসড়া",
        ExamStatus.Published => "প্রকাশিত",
        _ => status.ToString()
    };

    public static string For(BackupStatus status) => status switch
    {
        BackupStatus.Succeeded => "সম্পন্ন",
        BackupStatus.Failed => "ব্যর্থ",
        BackupStatus.Running => "চলছে",
        _ => status.ToString()
    };

    public static string For(HostMode mode) => mode switch
    {
        HostMode.SingleComputer => "একক কম্পিউটার",
        HostMode.LanHost => "ল্যান হোস্ট",
        HostMode.LanClient => "ল্যান ক্লায়েন্ট",
        _ => mode.ToString()
    };
}
