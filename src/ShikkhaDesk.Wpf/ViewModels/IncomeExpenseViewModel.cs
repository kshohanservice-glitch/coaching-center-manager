using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class IncomeExpenseEntry
{
    public string Date { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

/// <summary>
/// Income other than fees. Fee income is recognised by the fee assessment itself; recording it again
/// here would double count, so this screen deliberately excludes fee sources.
/// </summary>
public sealed class IncomeViewModel : ViewModelBase
{
    private readonly ILedgerOperationsService _ledger;
    private readonly IReportingService _reporting;
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public IncomeViewModel(ILedgerOperationsService ledger, IReportingService reporting,
        IAccountingService accounting, AppState state)
    {
        _ledger = ledger;
        _reporting = reporting;
        _accounting = accounting;
        _state = state;

        RecordCommand = new AsyncRelayCommand(_ => RecordAsync());
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ExportCommand = new RelayCommand(_ => Export());

        From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To = DateTime.Today;

        _ = LoadLookupsAsync();
    }

    public ObservableCollection<IncomeExpenseEntry> Rows { get; } = [];
    public ObservableCollection<LookupItem> IncomeAccounts { get; } = [];
    public ObservableCollection<LookupItem> Accounts { get; } = [];

    public DateTime IncomeDate { get; set; } = DateTime.Today;
    public string AmountText { get; set; } = "0";
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    private LookupItem? _incomeAccount;
    public LookupItem? IncomeAccount
    {
        get => _incomeAccount;
        set => SetField(ref _incomeAccount, value);
    }

    private LookupItem? _account;
    public LookupItem? Account
    {
        get => _account;
        set => SetField(ref _account, value);
    }

    private string _totalText = string.Empty;
    public string TotalText
    {
        get => _totalText;
        set => SetField(ref _totalText, value);
    }

    public ICommand RecordCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadLookupsAsync()
    {
        await RunAsync(async () =>
        {
            IncomeAccounts.Clear();
            Accounts.Clear();

            foreach (AccountBalanceRow row in await _accounting.GetAccountBalancesAsync(
                         DateRange.MonthOf(DateTime.Today)))
            {
                if (row.Type == AccountType.Income)
                {
                    IncomeAccounts.Add(new LookupItem { Id = row.AccountId, Name = $"{row.Code} · {row.Name}" });
                }
            }

            foreach (FinancialAccountBalance account in await _accounting.GetFinancialAccountBalancesAsync())
            {
                Accounts.Add(new LookupItem
                {
                    Id = account.FinancialAccountId,
                    Name = $"{account.Name} · {_state.Money(account.Balance)}"
                });
            }

            IncomeAccount = IncomeAccounts.FirstOrDefault();
            Account = Accounts.FirstOrDefault();
            await LoadAsync();
        }, "হিসাব আনা হচ্ছে…");
    }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Rows.Clear();
            Core.Money total = Core.Money.Zero;

            foreach (IncomeRecord record in await _reporting.GetIncomeRegisterAsync(new DateRange(From.Date, To.Date)))
            {
                total += record.Amount;
                Rows.Add(new IncomeExpenseEntry
                {
                    Date = Core.Digits.Format(record.IncomeDate, _state.UseBengaliDigits),
                    Number = record.IncomeNumber,
                    Description = record.Description,
                    Category = record.Source ?? "—",
                    Amount = _state.Money(record.Amount),
                    Reference = record.Reference ?? "—"
                });
            }

            TotalText = $"মোট আয়: {_state.Money(total)} · এন্ট্রি: {_state.Number(Rows.Count)}";
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task RecordAsync()
    {
        await RunAsync(async () =>
        {
            if (IncomeAccount is null || Account is null)
            {
                throw new ValidationException("আয়ের খাত ও জমার হিসাব নির্বাচন করুন।");
            }

            if (Description.Trim().Length == 0)
            {
                throw new ValidationException("বিবরণ দিন।");
            }

            if (!decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture,
                    out decimal amount)
                && !decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture,
                    out amount))
            {
                throw new ValidationException("টাকার পরিমাণ সঠিকভাবে লিখুন।");
            }

            if (amount <= 0)
            {
                throw new ValidationException("টাকার পরিমাণ শূন্যের বেশি হতে হবে।");
            }

            await _ledger.RecordIncomeAsync(new IncomeRequest(
                IncomeDate: IncomeDate.Date,
                IncomeAccountId: IncomeAccount.Id,
                FinancialAccountId: Account.Id,
                Amount: Money.FromDecimal(amount),
                Description: Description.Trim(),
                Source: Source.Trim().Length == 0 ? null : Source.Trim(),
                Reference: Reference.Trim().Length == 0 ? null : Reference.Trim()));

            Description = string.Empty;
            AmountText = "0";
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(AmountText));
            await LoadAsync();
            StatusMessage = "আয় রেকর্ড করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private void Export() => ExportRows("aay.csv");

    protected void ExportRows(string fileName)
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile(fileName, "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("তারিখ", r => r.Date),
            ("নম্বর", r => r.Number),
            ("বিবরণ", r => r.Description),
            ("খাত", r => r.Category),
            ("পরিমাণ", r => r.Amount),
            ("রেফারেন্স", r => r.Reference)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

/// <summary>
/// Expenses. An expense can be recorded as a settled payment or accrued as a payable to a supplier;
/// the accrual is recognised once and settling it later never creates a second expense.
/// </summary>
public sealed class ExpensesViewModel : ViewModelBase
{
    private readonly ILedgerOperationsService _ledger;
    private readonly IReportingService _reporting;
    private readonly IAccountingService _accounting;
    private readonly ISupplierService _suppliers;
    private readonly AppState _state;

    public ExpensesViewModel(ILedgerOperationsService ledger, IReportingService reporting,
        IAccountingService accounting, ISupplierService suppliers, AppState state)
    {
        _ledger = ledger;
        _reporting = reporting;
        _accounting = accounting;
        _suppliers = suppliers;
        _state = state;

        RecordCommand = new AsyncRelayCommand(_ => RecordAsync());
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ExportCommand = new RelayCommand(_ => Export());

        From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To = DateTime.Today;

        _ = LoadLookupsAsync();
    }

    public ObservableCollection<IncomeExpenseEntry> Rows { get; } = [];
    public ObservableCollection<LookupItem> ExpenseAccounts { get; } = [];
    public ObservableCollection<LookupItem> Accounts { get; } = [];
    public ObservableCollection<LookupItem> Suppliers { get; } = [];

    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public DateTime? DueDate { get; set; }
    public string AmountText { get; set; } = "0";
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string BillNumber { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool AccrueAsPayable { get; set; }

    private LookupItem? _expenseAccount;
    public LookupItem? ExpenseAccount
    {
        get => _expenseAccount;
        set => SetField(ref _expenseAccount, value);
    }

    private LookupItem? _account;
    public LookupItem? Account
    {
        get => _account;
        set => SetField(ref _account, value);
    }

    private LookupItem? _supplier;
    public LookupItem? Supplier
    {
        get => _supplier;
        set => SetField(ref _supplier, value);
    }

    private string _totalText = string.Empty;
    public string TotalText
    {
        get => _totalText;
        set => SetField(ref _totalText, value);
    }

    public ICommand RecordCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadLookupsAsync()
    {
        await RunAsync(async () =>
        {
            ExpenseAccounts.Clear();
            Accounts.Clear();

            foreach (AccountBalanceRow row in await _accounting.GetAccountBalancesAsync(
                         DateRange.MonthOf(DateTime.Today)))
            {
                if (row.Type == AccountType.Expense)
                {
                    ExpenseAccounts.Add(new LookupItem { Id = row.AccountId, Name = $"{row.Code} · {row.Name}" });
                }
            }

            foreach (FinancialAccountBalance account in await _accounting.GetFinancialAccountBalancesAsync())
            {
                Accounts.Add(new LookupItem
                {
                    Id = account.FinancialAccountId,
                    Name = $"{account.Name} · {_state.Money(account.Balance)}"
                });
            }

            Suppliers.Clear();
            Suppliers.Add(new LookupItem { Id = Guid.Empty, Name = "—" });
            foreach (Supplier supplier in await _suppliers.GetSuppliersAsync())
            {
                Suppliers.Add(new LookupItem { Id = supplier.Id, Name = supplier.Name });
            }

            ExpenseAccount = ExpenseAccounts.FirstOrDefault();
            Account = Accounts.FirstOrDefault();
            Supplier = Suppliers.FirstOrDefault();
            await LoadAsync();
        }, "হিসাব আনা হচ্ছে…");
    }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Rows.Clear();
            Core.Money total = Core.Money.Zero;

            foreach (ExpenseRecord record in await _reporting.GetExpenseRegisterAsync(
                         new DateRange(From.Date, To.Date)))
            {
                total += record.Amount;
                Rows.Add(new IncomeExpenseEntry
                {
                    Date = Core.Digits.Format(record.ExpenseDate, _state.UseBengaliDigits),
                    Number = record.ExpenseNumber,
                    Description = record.Description,
                    Category = record.IsAccrued ? "বকেয়া (পেয়েবল)" : "পরিশোধিত",
                    Amount = _state.Money(record.Amount),
                    Reference = record.Reference ?? "—"
                });
            }

            TotalText = $"মোট ব্যয়: {_state.Money(total)} · এন্ট্রি: {_state.Number(Rows.Count)}";
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task RecordAsync()
    {
        await RunAsync(async () =>
        {
            if (ExpenseAccount is null)
            {
                throw new ValidationException("ব্যয়ের খাত নির্বাচন করুন।");
            }

            if (!AccrueAsPayable && Account is null)
            {
                throw new ValidationException("পরিশোধের হিসাব নির্বাচন করুন।");
            }

            if (Description.Trim().Length == 0)
            {
                throw new ValidationException("বিবরণ দিন।");
            }

            if (!decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture,
                    out decimal amount)
                && !decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture,
                    out amount))
            {
                throw new ValidationException("টাকার পরিমাণ সঠিকভাবে লিখুন।");
            }

            if (amount <= 0)
            {
                throw new ValidationException("টাকার পরিমাণ শূন্যের বেশি হতে হবে।");
            }

            var request = new ExpenseRequest(
                ExpenseDate: ExpenseDate.Date,
                ExpenseAccountId: ExpenseAccount.Id,
                Amount: Money.FromDecimal(amount),
                Description: Description.Trim(),
                SupplierId: Supplier is { Id: var supplierId } && supplierId != Guid.Empty ? supplierId : null,
                Reference: Reference.Trim().Length == 0 ? null : Reference.Trim(),
                AccrueAsPayable: AccrueAsPayable,
                FinancialAccountId: Account?.Id,
                DueDate: DueDate,
                BillNumber: BillNumber.Trim().Length == 0 ? null : BillNumber.Trim());

            await (AccrueAsPayable
                ? _ledger.CreatePayableAsync(request)
                : _ledger.RecordExpenseAsync(request));

            Description = string.Empty;
            AmountText = "0";
            BillNumber = string.Empty;
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(AmountText));
            OnPropertyChanged(nameof(BillNumber));
            await LoadAsync();
            StatusMessage = AccrueAsPayable
                ? "ব্যয় প্রদেয় হিসেবে রেকর্ড করা হয়েছে।"
                : "ব্যয় রেকর্ড করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("beya.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("তারিখ", r => r.Date),
            ("নম্বর", r => r.Number),
            ("বিবরণ", r => r.Description),
            ("খাত", r => r.Category),
            ("পরিমাণ", r => r.Amount),
            ("রেফারেন্স", r => r.Reference)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}
