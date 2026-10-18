using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class JournalRow
{
    public string Journal { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Debit { get; init; } = string.Empty;
    public string Credit { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

public sealed class LedgerRowItem
{
    public string Date { get; init; } = string.Empty;
    public string Journal { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Debit { get; init; } = string.Empty;
    public string Credit { get; init; } = string.Empty;
    public string Balance { get; init; } = string.Empty;
}

/// <summary>General ledger and journal enquiry. Read-only: entries are made by the modules that own them.</summary>
public sealed class JournalViewModel : ViewModelBase
{
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public JournalViewModel(IAccountingService accounting, AppState state)
    {
        _accounting = accounting;
        _state = state;
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        ExportCommand = new RelayCommand(_ => Export());
        _ = SearchAsync();
    }

    public ObservableCollection<JournalRow> Rows { get; } = [];

    public DateTime From { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime To { get; set; } = DateTime.Today;

    private string _totals = string.Empty;
    public string Totals
    {
        get => _totals;
        set => SetField(ref _totals, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            PagedResult<LedgerEntryRow> page = await _accounting.GetLedgerAsync(new LedgerQuery(
                From: From.Date, To: To.Date, PageSize: 1000));

            Rows.Clear();
            Core.Money debit = Core.Money.Zero;
            Core.Money credit = Core.Money.Zero;

            foreach (LedgerEntryRow row in page.Items)
            {
                debit += row.Debit;
                credit += row.Credit;
                Rows.Add(new JournalRow
                {
                    Journal = row.JournalNumber,
                    Date = Core.Digits.Format(row.EntryDate, _state.UseBengaliDigits),
                    Description = row.Description,
                    Source = Translate(row.SourceKind),
                    Debit = _state.Money(row.Debit),
                    Credit = _state.Money(row.Credit),
                    Reference = row.SourceReference ?? "—"
                });
            }

            Totals = $"ডেবিট: {_state.Money(debit)} · ক্রেডিট: {_state.Money(credit)}"
                     + (debit == credit ? " · মিলেছে" : $" · পার্থক্য {_state.Money(debit - credit)}");
        }, "এন্ট্রি আনা হচ্ছে…");
    }

    private static string Translate(TransactionSourceKind kind) => kind switch
    {
        TransactionSourceKind.FeeAssessment => "ফি ধার্য",
        TransactionSourceKind.FeePayment => "ফি আদায়",
        TransactionSourceKind.CreditAllocation => "ক্রেডিট বরাদ্দ",
        TransactionSourceKind.Refund => "ফেরত",
        TransactionSourceKind.Income => "আয়",
        TransactionSourceKind.Expense => "ব্যয়",
        TransactionSourceKind.PayableAccrual => "প্রদেয় সৃষ্টি",
        TransactionSourceKind.PayableSettlement => "প্রদেয় পরিশোধ",
        TransactionSourceKind.PayrollAccrual => "বেতন সৃষ্টি",
        TransactionSourceKind.PayrollPayment => "বেতন পরিশোধ",
        TransactionSourceKind.Transfer => "স্থানান্তর",
        TransactionSourceKind.OpeningBalance => "প্রারম্ভিক স্থিতি",
        TransactionSourceKind.Reversal => "বিপরীত এন্ট্রি",
        TransactionSourceKind.Manual => "ম্যানুয়াল জার্নাল",
        _ => kind.ToString()
    };

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("journal.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("জার্নাল", r => r.Journal),
            ("তারিখ", r => r.Date),
            ("বিবরণ", r => r.Description),
            ("উৎস", r => r.Source),
            ("ডেবিট", r => r.Debit),
            ("ক্রেডিট", r => r.Credit),
            ("রেফারেন্স", r => r.Reference)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

/// <summary>Account ledger: every posted line touching one account, with a running balance.</summary>
public sealed class LedgerViewModel : ViewModelBase
{
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public LedgerViewModel(IAccountingService accounting, AppState state)
    {
        _accounting = accounting;
        _state = state;
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        ExportCommand = new RelayCommand(_ => Export());
        _ = LoadAccountsAsync();
    }

    public ObservableCollection<LookupItem> Accounts { get; } = [];
    public ObservableCollection<LedgerRowItem> Rows { get; } = [];

    public DateTime From { get; set; } = new DateTime(DateTime.Today.Year, 1, 1);
    public DateTime To { get; set; } = DateTime.Today;

    private LookupItem? _account;
    public LookupItem? Account
    {
        get => _account;
        set => SetField(ref _account, value);
    }

    private string _closingText = string.Empty;
    public string ClosingText
    {
        get => _closingText;
        set => SetField(ref _closingText, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAccountsAsync()
    {
        await RunAsync(async () =>
        {
            Accounts.Clear();
            foreach (AccountBalanceRow row in await _accounting.GetAccountBalancesAsync(
                         DateRange.MonthOf(DateTime.Today)))
            {
                Accounts.Add(new LookupItem { Id = row.AccountId, Name = $"{row.Code} · {row.Name}" });
            }

            Account = Accounts.FirstOrDefault();
            await SearchAsync();
        }, "হিসাব আনা হচ্ছে…");
    }

    private async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            if (Account is null)
            {
                return;
            }

            PagedResult<LedgerEntryRow> page = await _accounting.GetLedgerAsync(new LedgerQuery(
                AccountId: Account.Id, From: From.Date, To: To.Date, PageSize: 1000));

            Rows.Clear();
            foreach (LedgerEntryRow row in page.Items)
            {
                Rows.Add(new LedgerRowItem
                {
                    Date = Core.Digits.Format(row.EntryDate, _state.UseBengaliDigits),
                    Journal = row.JournalNumber,
                    Description = row.Description,
                    Debit = _state.Money(row.Debit),
                    Credit = _state.Money(row.Credit),
                    Balance = _state.Money(row.RunningBalance)
                });
            }

            Core.Money balance = await _accounting.GetAccountBalanceAsync(Account.Id, To.Date);
            ClosingText = "সমাপনী স্থিতি: " + _state.Money(balance);
        }, "খতিয়ান আনা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("khotian.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("তারিখ", r => r.Date),
            ("জার্নাল", r => r.Journal),
            ("বিবরণ", r => r.Description),
            ("ডেবিট", r => r.Debit),
            ("ক্রেডিট", r => r.Credit),
            ("স্থিতি", r => r.Balance)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class CashbookRowItem
{
    public string Date { get; init; } = string.Empty;
    public string Voucher { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Receipt { get; init; } = string.Empty;
    public string Payment { get; init; } = string.Empty;
    public string Balance { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

/// <summary>
/// Cash book and bank book. Both read the same service with a different account filter, so the two
/// screens can never drift apart in how they compute a running balance.
/// </summary>
public class CashbookViewModelBase : ViewModelBase
{
    protected readonly IAccountingService Accounting;
    protected readonly AppState State;
    private readonly bool _cashOnly;

    protected CashbookViewModelBase(IAccountingService accounting, AppState state, bool cashOnly)
    {
        Accounting = accounting;
        State = state;
        _cashOnly = cashOnly;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ExportCommand = new RelayCommand(_ => Export());
        _ = LoadAsync();
    }

    public ObservableCollection<LookupItem> Accounts { get; } = [];
    public ObservableCollection<CashbookRowItem> Rows { get; } = [];

    public DateTime From { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime To { get; set; } = DateTime.Today;

    private LookupItem? _account;
    public LookupItem? Account
    {
        get => _account;
        set => SetField(ref _account, value);
    }

    private string _totals = string.Empty;
    public string Totals
    {
        get => _totals;
        set => SetField(ref _totals, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<FinancialAccountBalance> accounts = await Accounting.GetFinancialAccountBalancesAsync();

            Accounts.Clear();
            foreach (FinancialAccountBalance account in accounts.Where(a => a.IsCash == _cashOnly))
            {
                Accounts.Add(new LookupItem
                {
                    Id = account.FinancialAccountId,
                    Name = $"{account.Name} · {State.Money(account.Balance)}"
                });
            }

            Account ??= Accounts.FirstOrDefault();

            Rows.Clear();
            if (Account is null)
            {
                Totals = _cashOnly ? "কোনো নগদ হিসাব নেই।" : "কোনো ব্যাংক হিসাব নেই।";
                return;
            }

            Core.Money receipt = Core.Money.Zero;
            Core.Money payment = Core.Money.Zero;
            Core.Money closing = Core.Money.Zero;

            foreach (CashbookRow row in await Accounting.GetCashbookAsync(Account.Id,
                         new DateRange(From.Date, To.Date)))
            {
                receipt += row.Receipt;
                payment += row.Payment;
                closing = row.RunningBalance;

                Rows.Add(new CashbookRowItem
                {
                    Date = Core.Digits.Format(row.EntryDate, State.UseBengaliDigits),
                    Voucher = row.VoucherNumber,
                    Description = row.Description,
                    Receipt = row.Receipt.IsZero ? "—" : State.Money(row.Receipt),
                    Payment = row.Payment.IsZero ? "—" : State.Money(row.Payment),
                    Balance = State.Money(row.RunningBalance),
                    Reference = row.Reference ?? "—"
                });
            }

            Totals = $"মোট প্রাপ্তি: {State.Money(receipt)} · মোট প্রদান: {State.Money(payment)} "
                     + $"· সমাপনী স্থিতি: {State.Money(closing)}";
        }, "বহি আনা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile(_cashOnly ? "nogod-bohi.csv" : "bank-bohi.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("তারিখ", r => r.Date),
            ("ভাউচার", r => r.Voucher),
            ("বিবরণ", r => r.Description),
            ("প্রাপ্তি", r => r.Receipt),
            ("প্রদান", r => r.Payment),
            ("স্থিতি", r => r.Balance),
            ("রেফারেন্স", r => r.Reference)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class CashbookViewModel : CashbookViewModelBase
{
    public CashbookViewModel(IAccountingService accounting, AppState state)
        : base(accounting, state, cashOnly: true)
    {
    }
}

public sealed class BankbookViewModel : CashbookViewModelBase
{
    public BankbookViewModel(IAccountingService accounting, AppState state)
        : base(accounting, state, cashOnly: false)
    {
    }
}
