using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class TransferViewModel : ViewModelBase
{
    private readonly ILedgerOperationsService _ledger;
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public TransferViewModel(ILedgerOperationsService ledger, IAccountingService accounting, AppState state)
    {
        _ledger = ledger;
        _accounting = accounting;
        _state = state;
        TransferCommand = new AsyncRelayCommand(_ => TransferAsync());
        _ = LoadAsync();
    }

    public ObservableCollection<LookupItem> Accounts { get; } = [];

    public DateTime TransferDate { get; set; } = DateTime.Today;
    public string AmountText { get; set; } = "0";
    public string FeeText { get; set; } = "0";
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    private LookupItem? _from;
    public LookupItem? From
    {
        get => _from;
        set => SetField(ref _from, value);
    }

    private LookupItem? _to;
    public LookupItem? To
    {
        get => _to;
        set => SetField(ref _to, value);
    }

    public ICommand TransferCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Accounts.Clear();
            foreach (FinancialAccountBalance account in await _accounting.GetFinancialAccountBalancesAsync())
            {
                Accounts.Add(new LookupItem
                {
                    Id = account.FinancialAccountId,
                    Name = $"{account.Name} · {_state.Money(account.Balance)}"
                });
            }

            From = Accounts.FirstOrDefault();
            To = Accounts.Skip(1).FirstOrDefault();
        }, "হিসাব আনা হচ্ছে…");
    }

    private async Task TransferAsync()
    {
        await RunAsync(async () =>
        {
            if (From is null || To is null)
            {
                throw new ValidationException("উভয় হিসাব নির্বাচন করুন।");
            }

            if (From.Id == To.Id)
            {
                throw new ValidationException("একই হিসাবে স্থানান্তর করা যায় না।");
            }

            Money amount = Parse(AmountText);
            if (!amount.IsPositive)
            {
                throw new ValidationException("টাকার পরিমাণ শূন্যের বেশি হতে হবে।");
            }

            await _ledger.TransferAsync(new TransferRequest(
                TransferDate: TransferDate.Date,
                FromFinancialAccountId: From.Id,
                ToFinancialAccountId: To.Id,
                Amount: amount,
                Fee: Parse(FeeText),
                Reference: Reference.Trim().Length == 0 ? null : Reference.Trim(),
                Notes: Notes.Trim().Length == 0 ? null : Notes.Trim()));

            AmountText = FeeText = "0";
            OnPropertyChanged(nameof(AmountText));
            OnPropertyChanged(nameof(FeeText));
            await LoadAsync();
            StatusMessage = "স্থানান্তর সম্পন্ন: " + _state.Money(amount);
        }, "স্থানান্তর করা হচ্ছে…");
    }

    private static Money Parse(string text) => Money.FromDecimal(
        decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
        || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            ? value
            : 0m);
}

public sealed class ReceivablesViewModel : ViewModelBase
{
    private readonly IReportingService _reporting;
    private readonly IStudentService _students;
    private readonly IAcademicService _academic;
    private readonly AppState _state;

    public ReceivablesViewModel(IReportingService reporting, IStudentService students,
        IAcademicService academic, AppState state)
    {
        _reporting = reporting;
        _students = students;
        _academic = academic;
        _state = state;

        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        ExportCommand = new RelayCommand(_ => Export());
        _ = LoadAsync();
    }

    public ObservableCollection<DuesRow> Rows { get; } = [];
    public ObservableCollection<LookupItem> Batches { get; } = [];

    public DateTime? AsOf { get; set; }

    private LookupItem? _batch;
    public LookupItem? Batch
    {
        get => _batch;
        set => SetField(ref _batch, value);
    }

    private string _totalText = string.Empty;
    public string TotalText
    {
        get => _totalText;
        set => SetField(ref _totalText, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Batches.Clear();
            Batches.Add(new LookupItem { Id = Guid.Empty, Name = "সব ব্যাচ" });
            foreach (Batch batch in await _academic.GetBatchesAsync(activeOnly: true))
            {
                Batches.Add(new LookupItem { Id = batch.Id, Name = batch.Name });
            }

            Batch = Batches[0];
            await SearchAsync();
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<FeeCharge> charges = await _reporting.GetOutstandingDuesAsync(
                batchId: Batch is { Id: var id } && id != Guid.Empty ? id : null,
                asOf: AsOf);

            Dictionary<Guid, Student> students = (await _students.SearchAsync(new StudentQuery(PageSize: 1000)))
                .Items.ToDictionary(s => s.Id);

            Rows.Clear();
            Core.Money total = Core.Money.Zero;

            foreach (FeeCharge charge in charges.OrderBy(c => c.DueDate))
            {
                students.TryGetValue(charge.StudentId, out Student? student);
                total += charge.Outstanding;
                int overdue = (DateTime.Today - charge.DueDate.Date).Days;

                Rows.Add(new DuesRow
                {
                    ChargeId = charge.Id,
                    StudentId = charge.StudentId,
                    Code = student?.StudentCode ?? "—",
                    Student = student?.FullName ?? "—",
                    Phone = student?.GuardianPhone ?? student?.StudentPhone ?? "—",
                    Kind = "প্রাপ্য",
                    Description = charge.Description,
                    Period = charge.PeriodKey ?? "—",
                    DueDate = Core.Digits.Format(charge.DueDate, _state.UseBengaliDigits),
                    Outstanding = _state.Money(charge.Outstanding),
                    OutstandingMinor = charge.Outstanding.MinorUnits,
                    DaysOverdue = overdue > 0 ? _state.Number(overdue) + " দিন" : "—"
                });
            }

            TotalText = $"মোট প্রাপ্য: {_state.Money(total)} · খাত: {_state.Number(Rows.Count)}";
        }, "প্রাপ্য হিসাব করা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("prapya.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("কোড", r => r.Code),
            ("নাম", r => r.Student),
            ("ফোন", r => r.Phone),
            ("বিবরণ", r => r.Description),
            ("মাস", r => r.Period),
            ("পরিশোধের তারিখ", r => r.DueDate),
            ("প্রাপ্য", r => r.Outstanding)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class SupplierRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class SuppliersViewModel : ViewModelBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersViewModel(ISupplierService suppliers)
    {
        _suppliers = suppliers;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        _ = LoadAsync();
    }

    public ObservableCollection<SupplierRow> Rows { get; } = [];

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public ICommand SaveCommand { get; }
    public ICommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Rows.Clear();
            foreach (Supplier supplier in await _suppliers.GetSuppliersAsync())
            {
                Rows.Add(new SupplierRow
                {
                    Id = supplier.Id,
                    Name = supplier.Name,
                    Phone = supplier.Phone ?? "—",
                    Email = supplier.Email ?? "—",
                    Status = supplier.IsActive ? "সক্রিয়" : "নিষ্ক্রিয়"
                });
            }
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (Name.Trim().Length == 0)
            {
                throw new ValidationException("সরবরাহকারীর নাম দিন।");
            }

            await _suppliers.SaveSupplierAsync(new Supplier
            {
                Name = Name.Trim(),
                Phone = Phone.Trim().Length == 0 ? null : Phone.Trim(),
                Email = Email.Trim().Length == 0 ? null : Email.Trim(),
                Address = Address.Trim().Length == 0 ? null : Address.Trim(),
                IsActive = true
            });

            Name = Phone = Email = Address = string.Empty;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Phone));
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(Address));
            await LoadAsync();
            StatusMessage = "সরবরাহকারী সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}

public sealed class PayableRowItem
{
    public Guid PayableId { get; init; }
    public string Bill { get; init; } = string.Empty;
    public string Supplier { get; init; } = string.Empty;
    public string BillDate { get; init; } = string.Empty;
    public string DueDate { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
    public string Paid { get; init; } = string.Empty;
    public string Outstanding { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Accounts payable. An accrued expense is recognised once; settling it here clears the liability and
/// never creates a second expense.
/// </summary>
public sealed class PayablesViewModel : ViewModelBase
{
    private readonly IReportingService _reporting;
    private readonly ILedgerOperationsService _ledger;
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public PayablesViewModel(IReportingService reporting, ILedgerOperationsService ledger,
        IAccountingService accounting, AppState state)
    {
        _reporting = reporting;
        _ledger = ledger;
        _accounting = accounting;
        _state = state;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        SettleCommand = new AsyncRelayCommand(_ => SettleAsync(), () => Selected is not null);
        ExportCommand = new RelayCommand(_ => Export());

        From = new DateTime(DateTime.Today.Year, 1, 1);
        To = DateTime.Today;
        _ = LoadAsync();
    }

    public ObservableCollection<PayableRowItem> Rows { get; } = [];
    public ObservableCollection<LookupItem> Accounts { get; } = [];

    public int StatusIndex { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string AmountText { get; set; } = "0";
    public DateTime SettlementDate { get; set; } = DateTime.Today;
    public int MethodIndex { get; set; }
    public string Reference { get; set; } = string.Empty;

    private PayableRowItem? _selected;
    public PayableRowItem? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                ((AsyncRelayCommand)SettleCommand).RaiseCanExecuteChanged();
            }
        }
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

    public ICommand RefreshCommand { get; }
    public ICommand SettleCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<PayableRow> rows = await _reporting.GetPayablesAsync(StatusIndex switch
            {
                1 => PayableStatus.Open,
                2 => PayableStatus.PartiallyPaid,
                3 => PayableStatus.Settled,
                _ => null
            });

            Rows.Clear();
            Core.Money outstanding = Core.Money.Zero;

            foreach (PayableRow row in rows)
            {
                outstanding += row.Outstanding;
                Rows.Add(new PayableRowItem
                {
                    PayableId = row.PayableId,
                    Bill = row.BillNumber,
                    Supplier = row.SupplierName,
                    BillDate = Core.Digits.Format(row.BillDate, _state.UseBengaliDigits),
                    DueDate = row.DueDate is null ? "—" : Core.Digits.Format(row.DueDate.Value, _state.UseBengaliDigits),
                    Amount = _state.Money(row.Amount),
                    Paid = _state.Money(row.PaidAmount),
                    Outstanding = _state.Money(row.Outstanding),
                    Status = row.Status switch
                    {
                        PayableStatus.Open => "খোলা",
                        PayableStatus.PartiallyPaid => "আংশিক পরিশোধিত",
                        PayableStatus.Settled => "পরিশোধিত",
                        _ => "বাতিল"
                    }
                });
            }

            Accounts.Clear();
            foreach (FinancialAccountBalance account in await _accounting.GetFinancialAccountBalancesAsync())
            {
                Accounts.Add(new LookupItem
                {
                    Id = account.FinancialAccountId,
                    Name = $"{account.Name} · {_state.Money(account.Balance)}"
                });
            }

            Account ??= Accounts.FirstOrDefault();
            TotalText = $"মোট প্রদেয়: {_state.Money(outstanding)}";
        }, "প্রদেয় আনা হচ্ছে…");
    }

    private async Task SettleAsync()
    {
        await RunAsync(async () =>
        {
            if (Selected is null || Account is null)
            {
                return;
            }

            Money amount = Money.FromDecimal(decimal.TryParse(AmountText.Trim(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out decimal value)
                || decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                    ? value
                    : 0m);

            if (!amount.IsPositive)
            {
                throw new ValidationException("পরিশোধের পরিমাণ সঠিকভাবে লিখুন।");
            }

            await _ledger.SettlePayableAsync(new SettlePayableRequest(
                PayableId: Selected.PayableId,
                SettlementDate: SettlementDate.Date,
                Amount: amount,
                FinancialAccountId: Account.Id,
                Method: MethodIndex switch
                {
                    1 => PaymentMethod.Bank,
                    2 => PaymentMethod.MobileBanking,
                    _ => PaymentMethod.Cash
                },
                Reference: Reference.Trim().Length == 0 ? null : Reference.Trim()));

            AmountText = "0";
            OnPropertyChanged(nameof(AmountText));
            await LoadAsync();
            StatusMessage = "প্রদেয় পরিশোধ করা হয়েছে।";
        }, "পরিশোধ করা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("prodeya.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("বিল", r => r.Bill),
            ("সরবরাহকারী", r => r.Supplier),
            ("তারিখ", r => r.BillDate),
            ("পরিশোধের তারিখ", r => r.DueDate),
            ("পরিমাণ", r => r.Amount),
            ("পরিশোধিত", r => r.Paid),
            ("অবশিষ্ট", r => r.Outstanding),
            ("অবস্থা", r => r.Status)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}
