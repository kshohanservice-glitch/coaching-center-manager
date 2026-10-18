using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class TrialBalanceRowItem
{
    public string Code { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Debit { get; init; } = string.Empty;
    public string Credit { get; init; } = string.Empty;
}

/// <summary>Trial balance. Totals must agree; if they do not, the page says so instead of hiding it.</summary>
public sealed class TrialBalanceViewModel : ViewModelBase
{
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public TrialBalanceViewModel(IAccountingService accounting, AppState state)
    {
        _accounting = accounting;
        _state = state;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ExportCommand = new RelayCommand(_ => Export());

        From = new DateTime(DateTime.Today.Year, 1, 1);
        To = DateTime.Today;
        _ = LoadAsync();
    }

    public ObservableCollection<TrialBalanceRowItem> Rows { get; } = [];

    public DateTime From { get; set; }
    public DateTime To { get; set; }

    private string _totals = string.Empty;
    public string Totals
    {
        get => _totals;
        set => SetField(ref _totals, value);
    }

    private bool _balanced;
    public bool Balanced
    {
        get => _balanced;
        set => SetField(ref _balanced, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<TrialBalanceRow> rows = await _accounting.GetTrialBalanceAsync(
                new DateRange(From.Date, To.Date));

            Rows.Clear();
            Core.Money debit = Core.Money.Zero;
            Core.Money credit = Core.Money.Zero;

            foreach (TrialBalanceRow row in rows)
            {
                debit += row.Debit;
                credit += row.Credit;
                Rows.Add(new TrialBalanceRowItem
                {
                    Code = row.Code,
                    Account = row.Name,
                    Type = row.Type.ToString(),
                    Debit = _state.Money(row.Debit),
                    Credit = _state.Money(row.Credit)
                });
            }

            Balanced = debit == credit;
            Totals = $"মোট ডেবিট {_state.Money(debit)} · মোট ক্রেডিট {_state.Money(credit)} · "
                     + (Balanced ? "হিসাব মিলেছে" : "হিসাব মেলেনি — পার্থক্য " + _state.Money(debit - credit));
        }, "ট্রায়াল ব্যালেন্স তৈরি করা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("trial-balance.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("কোড", r => r.Code),
            ("হিসাব", r => r.Account),
            ("ধরন", r => r.Type),
            ("ডেবিট", r => r.Debit),
            ("ক্রেডিট", r => r.Credit)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class ProfitLossRowItem
{
    public string Group { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
}

public sealed class ProfitLossViewModel : ViewModelBase
{
    private readonly IReportingService _reporting;
    private readonly IPrintService _printing;
    private readonly AppState _state;

    public ProfitLossViewModel(IReportingService reporting, IPrintService printing, AppState state)
    {
        _reporting = reporting;
        _printing = printing;
        _state = state;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        PrintCommand = new RelayCommand(_ => Print());
        ExportCommand = new RelayCommand(_ => Export());

        From = new DateTime(DateTime.Today.Year, 1, 1);
        To = DateTime.Today;
        _ = LoadAsync();
    }

    public ObservableCollection<ProfitLossRowItem> Rows { get; } = [];

    public DateTime From { get; set; }
    public DateTime To { get; set; }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            ProfitAndLossReport report = await _reporting.GetProfitAndLossAsync(new DateRange(From.Date, To.Date));

            Rows.Clear();
            foreach (ProfitAndLossRow row in report.Income)
            {
                Rows.Add(new ProfitLossRowItem
                {
                    Group = "আয়",
                    Account = row.AccountName,
                    Amount = _state.Money(row.Amount)
                });
            }

            foreach (ProfitAndLossRow row in report.Expenses)
            {
                Rows.Add(new ProfitLossRowItem
                {
                    Group = "ব্যয়",
                    Account = row.AccountName,
                    Amount = _state.Money(row.Amount)
                });
            }

            Summary = $"মোট আয় {_state.Money(report.TotalIncome)} · মোট ব্যয় {_state.Money(report.TotalExpenses)} · "
                      + (report.NetProfit.IsNegative ? "নিট ক্ষতি " : "নিট লাভ ")
                      + _state.Money(report.NetProfit.Abs());
        }, "লাভ-ক্ষতি তৈরি করা হচ্ছে…");
    }

    private void Print() => _printing.PrintReport("লাভ-ক্ষতি বিবরণী",
        $"{Core.Digits.Format(From, _state.UseBengaliDigits)} থেকে {Core.Digits.Format(To, _state.UseBengaliDigits)}",
        ["ধরন", "হিসাব", "পরিমাণ"],
        Rows.Select(r => (IReadOnlyList<string>)[r.Group, r.Account, r.Amount]).ToList());

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("labh-khoti.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("ধরন", r => r.Group),
            ("হিসাব", r => r.Account),
            ("পরিমাণ", r => r.Amount)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class BalanceSheetRowItem
{
    public string Group { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
}

public sealed class BalanceSheetViewModel : ViewModelBase
{
    private readonly IReportingService _reporting;
    private readonly IPrintService _printing;
    private readonly AppState _state;

    public BalanceSheetViewModel(IReportingService reporting, IPrintService printing, AppState state)
    {
        _reporting = reporting;
        _printing = printing;
        _state = state;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        PrintCommand = new RelayCommand(_ => Print());
        ExportCommand = new RelayCommand(_ => Export());
        AsOf = DateTime.Today;
        _ = LoadAsync();
    }

    public ObservableCollection<BalanceSheetRowItem> Rows { get; } = [];

    public DateTime AsOf { get; set; }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    private bool _balanced;
    public bool Balanced
    {
        get => _balanced;
        set => SetField(ref _balanced, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            BalanceSheetReport report = await _reporting.GetBalanceSheetAsync(AsOf.Date);

            Rows.Clear();
            Add("সম্পদ", report.Assets);
            Add("দায়", report.Liabilities);
            Add("মালিকানা", report.Equity);

            Balanced = report.TotalAssets == report.TotalLiabilitiesAndEquity;
            Summary = $"মোট সম্পদ {_state.Money(report.TotalAssets)} · দায় + মালিকানা "
                      + $"{_state.Money(report.TotalLiabilitiesAndEquity)} · "
                      + (Balanced ? "হিসাব মিলেছে" : "হিসাব মেলেনি — পর্যালোচনা প্রয়োজন");
        }, "আর্থিক অবস্থা তৈরি করা হচ্ছে…");
    }

    private void Add(string group, IEnumerable<BalanceSheetRow> rows)
    {
        foreach (BalanceSheetRow row in rows)
        {
            Rows.Add(new BalanceSheetRowItem
            {
                Group = group,
                Account = row.AccountName,
                Amount = _state.Money(row.Amount)
            });
        }
    }

    private void Print() => _printing.PrintReport("আর্থিক অবস্থার বিবরণী",
        Core.Digits.Format(AsOf, _state.UseBengaliDigits) + " অনুযায়ী",
        ["ধরন", "হিসাব", "পরিমাণ"],
        Rows.Select(r => (IReadOnlyList<string>)[r.Group, r.Account, r.Amount]).ToList());

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("arthik-obostha.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("ধরন", r => r.Group),
            ("হিসাব", r => r.Account),
            ("পরিমাণ", r => r.Amount)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class CashFlowRowItem
{
    public string Section { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
}

/// <summary>
/// Direct-method cash flow. Receipts and payments are grouped by the source document that produced
/// them, taken straight from the ledger lines of every cash and bank account, so the statement
/// reconciles to the cash and bank balances on the balance sheet.
/// </summary>
public sealed class CashFlowViewModel : ViewModelBase
{
    private readonly IAccountingService _accounting;
    private readonly IPrintService _printing;
    private readonly AppState _state;

    public CashFlowViewModel(IAccountingService accounting, IPrintService printing, AppState state)
    {
        _accounting = accounting;
        _printing = printing;
        _state = state;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        PrintCommand = new RelayCommand(_ => Print());
        ExportCommand = new RelayCommand(_ => Export());

        From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To = DateTime.Today;
        _ = LoadAsync();
    }

    public ObservableCollection<CashFlowRowItem> Rows { get; } = [];

    public DateTime From { get; set; }
    public DateTime To { get; set; }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<FinancialAccountBalance> accounts = await _accounting.GetFinancialAccountBalancesAsync();
            var range = new DateRange(From.Date, To.Date);

            var bySource = new Dictionary<TransactionSourceKind, Core.Money>();
            Core.Money opening = Core.Money.Zero;
            Core.Money closing = Core.Money.Zero;

            foreach (FinancialAccountBalance account in accounts)
            {
                // The cash book's own opening and closing rows are authoritative for the period, so
                // the statement ties back to the same numbers the cash book screen shows.
                IReadOnlyList<CashbookRow> book = await _accounting.GetCashbookAsync(
                    account.FinancialAccountId, range);

                if (book.Count > 0)
                {
                    opening += book[0].RunningBalance;
                    closing += book[^1].RunningBalance;
                }

                // Movements are grouped by source document, which the cash book rows do not carry.
                PagedResult<LedgerEntryRow> page = await _accounting.GetLedgerAsync(new LedgerQuery(
                    FinancialAccountId: account.FinancialAccountId,
                    From: range.From,
                    To: range.To,
                    PageSize: 2000));

                foreach (LedgerEntryRow row in page.Items)
                {
                    Core.Money movement = row.Debit - row.Credit;
                    if (movement.IsZero)
                    {
                        continue;
                    }

                    bySource.TryGetValue(row.SourceKind, out Core.Money current);
                    bySource[row.SourceKind] = current + movement;
                }
            }

            Rows.Clear();
            Core.Money net = Core.Money.Zero;

            foreach (KeyValuePair<TransactionSourceKind, Core.Money> entry in bySource
                         .OrderByDescending(e => e.Value.MinorUnits))
            {
                net += entry.Value;
                Rows.Add(new CashFlowRowItem
                {
                    Section = entry.Value.IsNegative ? "প্রদান" : "প্রাপ্তি",
                    Description = Describe(entry.Key),
                    Amount = _state.Money(entry.Value.Abs())
                });
            }

            Summary = $"প্রারম্ভিক স্থিতি {_state.Money(opening)} · নিট পরিবর্তন {_state.Money(net)} "
                      + $"· সমাপনী স্থিতি {_state.Money(closing)}"
                      + (opening + net == closing
                          ? " · হিসাব মিলেছে"
                          : $" · পার্থক্য {_state.Money(opening + net - closing)}");
        }, "নগদ প্রবাহ তৈরি করা হচ্ছে…");
    }

    private static string Describe(TransactionSourceKind kind) => kind switch
    {
        TransactionSourceKind.FeePayment => "ফি আদায়",
        TransactionSourceKind.FeeAssessment => "ফি ধার্য (নগদ নয়)",
        TransactionSourceKind.CreditAllocation => "ক্রেডিট বরাদ্দ",
        TransactionSourceKind.Refund => "ফেরত",
        TransactionSourceKind.Income => "অন্যান্য আয়",
        TransactionSourceKind.Expense => "ব্যয় প্রদান",
        TransactionSourceKind.PayableAccrual => "প্রদেয় সৃষ্টি",
        TransactionSourceKind.PayableSettlement => "প্রদেয় পরিশোধ",
        TransactionSourceKind.PayrollAccrual => "বেতন সৃষ্টি",
        TransactionSourceKind.PayrollPayment => "বেতন পরিশোধ",
        TransactionSourceKind.Transfer => "হিসাব স্থানান্তর",
        TransactionSourceKind.OpeningBalance => "প্রারম্ভিক স্থিতি",
        TransactionSourceKind.Reversal => "বিপরীত এন্ট্রি",
        _ => "অন্যান্য"
    };

    private void Print() => _printing.PrintReport("নগদ প্রবাহ বিবরণী",
        $"{Core.Digits.Format(From, _state.UseBengaliDigits)} থেকে {Core.Digits.Format(To, _state.UseBengaliDigits)}",
        ["ধরন", "বিবরণ", "পরিমাণ"],
        Rows.Select(r => (IReadOnlyList<string>)[r.Section, r.Description, r.Amount]).ToList());

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("nogod-probah.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("ধরন", r => r.Section),
            ("বিবরণ", r => r.Description),
            ("পরিমাণ", r => r.Amount)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class ReconciliationRowItem
{
    public string Name { get; init; } = string.Empty;
    public string Expected { get; init; } = string.Empty;
    public string Actual { get; init; } = string.Empty;
    public string Difference { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
}

/// <summary>
/// The accounting self-check. Every rule that must hold between the ledger and the source documents
/// is listed with both sides, so a mismatch is visible instead of being averaged away.
/// </summary>
public sealed class ReconciliationViewModel : ViewModelBase
{
    private readonly IReportingService _reporting;
    private readonly AppState _state;

    public ReconciliationViewModel(IReportingService reporting, AppState state)
    {
        _reporting = reporting;
        _state = state;
        RunCommand = new AsyncRelayCommand(_ => RunChecksAsync());
        _ = RunChecksAsync();
    }

    public ObservableCollection<ReconciliationRowItem> Rows { get; } = [];

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    private bool _allPassed;
    public bool AllPassed
    {
        get => _allPassed;
        set => SetField(ref _allPassed, value);
    }

    public ICommand RunCommand { get; }

    private async Task RunChecksAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<ReconciliationCheck> checks = await _reporting.RunReconciliationAsync();

            Rows.Clear();
            foreach (ReconciliationCheck check in checks)
            {
                Rows.Add(new ReconciliationRowItem
                {
                    Name = check.Name,
                    Expected = _state.Money(check.Expected),
                    Actual = _state.Money(check.Actual),
                    Difference = _state.Money(check.Difference),
                    Result = check.Passed ? "মিলেছে" : "মেলেনি"
                });
            }

            AllPassed = checks.All(c => c.Passed);
            Summary = $"{_state.Number(checks.Count(c => c.Passed))}/{_state.Number(checks.Count)} "
                      + "পরীক্ষা সফল"
                      + (AllPassed
                          ? " — হিসাব সামঞ্জস্যপূর্ণ।"
                          : " — মেলেনি এমন বিষয়গুলো পর্যালোচনা করুন।");
        }, "হিসাব মিলাচ্ছি…");
    }
}
