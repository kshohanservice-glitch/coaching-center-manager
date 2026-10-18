namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// The complete navigation model. Each item names the permission required to see it; the same
/// permission is re-checked in the application service layer, so hiding a menu entry is a
/// convenience, not the security boundary.
/// </summary>
public static class NavigationCatalogue
{
    public static IReadOnlyList<NavigationItem> All { get; } =
    [
        // Overview
        new NavigationItem { Key = "dashboard", Label = "ড্যাশবোর্ড", Group = "সারসংক্ষেপ", Glyph = "▤" },

        // Academic
        new NavigationItem { Key = "students", Label = "শিক্ষার্থী", Group = "একাডেমিক ব্যবস্থাপনা",
            Glyph = "☺", RequiredPermission = "students.view" },
        new NavigationItem { Key = "admissions", Label = "ভর্তি", Group = "একাডেমিক ব্যবস্থাপনা",
            Glyph = "＋", RequiredPermission = "admissions.view" },
        new NavigationItem { Key = "academics", Label = "ক্লাস ও কোর্স", Group = "একাডেমিক ব্যবস্থাপনা",
            Glyph = "▦", RequiredPermission = "academic.manage" },
        new NavigationItem { Key = "batches", Label = "ব্যাচ", Group = "একাডেমিক ব্যবস্থাপনা",
            Glyph = "▣", RequiredPermission = "academic.manage" },
        new NavigationItem { Key = "staff", Label = "শিক্ষক ও কর্মচারী", Group = "একাডেমিক ব্যবস্থাপনা",
            Glyph = "☻", RequiredPermission = "academic.manage" },
        new NavigationItem { Key = "attendance", Label = "উপস্থিতি", Group = "একাডেমিক ব্যবস্থাপনা",
            Glyph = "✓", RequiredPermission = "attendance.view" },
        new NavigationItem { Key = "exams", Label = "পরীক্ষা ও ফলাফল", Group = "একাডেমিক ব্যবস্থাপনা",
            Glyph = "✎", RequiredPermission = "exams.view" },

        // Fees
        new NavigationItem { Key = "feeplans", Label = "ফি প্ল্যান", Group = "ফি ও পেমেন্ট",
            Glyph = "☰", RequiredPermission = "fees.view" },
        new NavigationItem { Key = "assessment", Label = "ফি ধার্য", Group = "ফি ও পেমেন্ট",
            Glyph = "⇉", RequiredPermission = "fees.assess" },
        new NavigationItem { Key = "dues", Label = "বকেয়া", Group = "ফি ও পেমেন্ট",
            Glyph = "◷", RequiredPermission = "fees.view" },
        new NavigationItem { Key = "collect", Label = "ফি আদায়", Group = "ফি ও পেমেন্ট",
            Glyph = "💰", RequiredPermission = "fees.collect" },
        new NavigationItem { Key = "payments", Label = "পেমেন্ট ইতিহাস", Group = "ফি ও পেমেন্ট",
            Glyph = "⟳", RequiredPermission = "fees.view" },
        new NavigationItem { Key = "receipts", Label = "রশিদ", Group = "ফি ও পেমেন্ট",
            Glyph = "⎙", RequiredPermission = "fees.print" },
        new NavigationItem { Key = "credit", Label = "অগ্রিম ক্রেডিট", Group = "ফি ও পেমেন্ট",
            Glyph = "◈", RequiredPermission = "fees.view" },
        new NavigationItem { Key = "refunds", Label = "ফেরত ও বাতিল", Group = "ফি ও পেমেন্ট",
            Glyph = "↩", RequiredPermission = "fees.view" },

        // Accounting
        new NavigationItem { Key = "accounts", Label = "হিসাবের তালিকা", Group = "হিসাব",
            Glyph = "▤", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "income", Label = "আয়", Group = "হিসাব",
            Glyph = "↓", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "expenses", Label = "ব্যয়", Group = "হিসাব",
            Glyph = "↑", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "journal", Label = "জার্নাল এন্ট্রি", Group = "হিসাব",
            Glyph = "≡", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "cashbook", Label = "নগদ বহি", Group = "হিসাব",
            Glyph = "▤", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "bankbook", Label = "ব্যাংক বহি", Group = "হিসাব",
            Glyph = "▥", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "transfers", Label = "স্থানান্তর", Group = "হিসাব",
            Glyph = "⇄", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "receivables", Label = "প্রাপ্য", Group = "হিসাব",
            Glyph = "◑", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "suppliers", Label = "সরবরাহকারী", Group = "হিসাব",
            Glyph = "⚑", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "payables", Label = "প্রদেয়", Group = "হিসাব",
            Glyph = "◐", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "payroll", Label = "বেতন", Group = "হিসাব",
            Glyph = "☗", RequiredPermission = "payroll.view" },
        new NavigationItem { Key = "ledger", Label = "খতিয়ান", Group = "হিসাব",
            Glyph = "▦", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "trialbalance", Label = "ট্রায়াল ব্যালেন্স", Group = "হিসাব",
            Glyph = "=", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "profitloss", Label = "লাভ-ক্ষতি", Group = "হিসাব",
            Glyph = "±", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "balancesheet", Label = "আর্থিক অবস্থা", Group = "হিসাব",
            Glyph = "▣", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "cashflow", Label = "নগদ প্রবাহ", Group = "হিসাব",
            Glyph = "⇉", RequiredPermission = "accounting.view" },
        new NavigationItem { Key = "reconciliation", Label = "হিসাব মিল", Group = "হিসাব",
            Glyph = "✓", RequiredPermission = "accounting.view" },

        // Reports
        new NavigationItem { Key = "reports", Label = "প্রতিবেদন কেন্দ্র", Group = "প্রতিবেদন",
            Glyph = "▤", RequiredPermission = "reports.view" },

        // Administration
        new NavigationItem { Key = "users", Label = "ব্যবহারকারী ও অনুমতি", Group = "প্রশাসন",
            Glyph = "☻", RequiredPermission = "users.manage" },
        new NavigationItem { Key = "audit", Label = "নিরীক্ষা লগ", Group = "প্রশাসন",
            Glyph = "☰", RequiredPermission = "audit.view" },
        new NavigationItem { Key = "backup", Label = "ব্যাকআপ ও পুনরুদ্ধার", Group = "প্রশাসন",
            Glyph = "⛁", RequiredPermission = "backup.manage" },
        new NavigationItem { Key = "lan", Label = "হোস্ট ও নেটওয়ার্ক", Group = "প্রশাসন",
            Glyph = "⇅", RequiredPermission = "settings.manage" },
        new NavigationItem { Key = "license", Label = "লাইসেন্স ও ডিভাইস", Group = "প্রশাসন",
            Glyph = "⚿", RequiredPermission = "license.manage" },
        new NavigationItem { Key = "institution", Label = "প্রতিষ্ঠান প্রোফাইল", Group = "প্রশাসন",
            Glyph = "⌂", RequiredPermission = "settings.manage" },
        new NavigationItem { Key = "settings", Label = "সাধারণ সেটিংস", Group = "প্রশাসন",
            Glyph = "⚙", RequiredPermission = "settings.manage" },
        new NavigationItem { Key = "about", Label = "সহায়তা ও সংস্করণ", Group = "প্রশাসন", Glyph = "?" }
    ];

    /// <summary>Every key that must be backed by a working page before release.</summary>
    public static IReadOnlyList<string> RequiredKeys => All.Select(i => i.Key).ToList();
}
