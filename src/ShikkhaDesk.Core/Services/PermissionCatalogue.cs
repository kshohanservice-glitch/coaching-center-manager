namespace ShikkhaDesk.Core.Services;

/// <summary>
/// The permission catalogue. Permissions are enforced in the application service layer, not by hiding
/// UI elements, so a direct API call cannot bypass them.
/// </summary>
public static class PermissionCodes
{
    public const string View = "view";
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Approve = "approve";
    public const string Reverse = "reverse";
    public const string Print = "print";
    public const string Export = "export";
    public const string Delete = "delete";

    public static readonly (string Code, string Group, string NameBn)[] All =
    [
        ("students.view", "শিক্ষার্থী", "শিক্ষার্থী দেখা"),
        ("students.create", "শিক্ষার্থী", "শিক্ষার্থী যোগ করা"),
        ("students.edit", "শিক্ষার্থী", "শিক্ষার্থী সম্পাদনা"),
        ("students.archive", "শিক্ষার্থী", "শিক্ষার্থী সংরক্ষণ"),
        ("students.print", "শিক্ষার্থী", "শিক্ষার্থী প্রতিবেদন প্রিন্ট"),
        ("students.export", "শিক্ষার্থী", "শিক্ষার্থী তথ্য রপ্তানি"),

        ("admissions.view", "ভর্তি", "ভর্তি দেখা"),
        ("admissions.create", "ভর্তি", "নতুন ভর্তি"),

        ("academic.manage", "একাডেমিক", "ক্লাস, কোর্স ও ব্যাচ ব্যবস্থাপনা"),
        ("attendance.view", "উপস্থিতি", "উপস্থিতি দেখা"),
        ("attendance.edit", "উপস্থিতি", "উপস্থিতি এন্ট্রি ও সংশোধন"),
        ("exams.view", "পরীক্ষা", "পরীক্ষা ও ফলাফল দেখা"),
        ("exams.manage", "পরীক্ষা", "পরীক্ষা তৈরি ও নম্বর এন্ট্রি"),
        ("exams.publish", "পরীক্ষা", "ফলাফল প্রকাশ"),

        ("fees.view", "ফি", "ফি ও বকেয়া দেখা"),
        ("fees.assess", "ফি", "ফি ধার্য করা"),
        ("fees.collect", "ফি", "ফি আদায়"),
        ("fees.allocate", "ফি", "পেমেন্ট ও ক্রেডিট বরাদ্দ"),
        ("fees.reverse", "ফি", "পেমেন্ট বাতিল"),
        ("fees.refund", "ফি", "ফেরত প্রদান"),
        ("fees.print", "ফি", "রশিদ প্রিন্ট"),
        ("fees.export", "ফি", "ফি প্রতিবেদন রপ্তানি"),

        ("accounting.view", "হিসাব", "হিসাব দেখা"),
        ("accounting.manage", "হিসাব", "হিসাবের তালিকা ব্যবস্থাপনা"),
        ("accounting.post", "হিসাব", "আয়, ব্যয় ও জার্নাল এন্ট্রি"),
        ("accounting.reverse", "হিসাব", "জার্নাল বাতিল"),
        ("payroll.manage", "বেতন", "বেতন ও বেতন পরিশোধ"),
        ("payroll.view", "বেতন", "বেতন দেখা"),

        ("reports.view", "প্রতিবেদন", "প্রতিবেদন দেখা"),
        ("reports.export", "প্রতিবেদন", "প্রতিবেদন রপ্তানি"),

        ("users.manage", "ব্যবহারকারী", "ব্যবহারকারী ব্যবস্থাপনা"),
        ("roles.manage", "ব্যবহারকারী", "ভূমিকা ও অনুমতি"),
        ("audit.view", "নিরীক্ষা", "নিরীক্ষা লগ দেখা"),

        ("settings.manage", "সেটিংস", "প্রতিষ্ঠান ও সাধারণ সেটিংস"),
        ("backup.manage", "ব্যাকআপ", "ব্যাকআপ ব্যবস্থাপনা"),
        ("restore.perform", "ব্যাকআপ", "ব্যাকআপ পুনরুদ্ধার"),
        ("license.manage", "লাইসেন্স", "লাইসেন্স ও ডিভাইস ব্যবস্থাপনা")
    ];

    public static bool IsKnown(string code) => All.Any(p => p.Code == code);

    /// <summary>Default permission set for the built-in Admin role.</summary>
    public static string[] AdministratorPermissions => All.Select(p => p.Code).ToArray();

    public static string[] AccountantPermissions =>
    [
        "students.view", "admissions.view", "attendance.view",
        "fees.view", "fees.assess", "fees.collect", "fees.allocate", "fees.print", "fees.export",
        "accounting.view", "accounting.manage", "accounting.post",
        "payroll.view", "payroll.manage",
        "reports.view", "reports.export"
    ];

    public static string[] TeacherPermissions =>
    [
        "students.view", "attendance.view", "attendance.edit",
        "exams.view", "exams.manage",
        "reports.view"
    ];
}
