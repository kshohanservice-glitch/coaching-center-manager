using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class PayrollRowItem
{
    public Guid PayrollRecordId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Designation { get; init; } = string.Empty;
    public string Basic { get; init; } = string.Empty;
    public string Allowances { get; init; } = string.Empty;
    public string Deductions { get; init; } = string.Empty;
    public string Net { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Paid { get; init; } = string.Empty;
}

/// <summary>
/// Payroll. Accruing a period recognises the salary expense once; paying it later clears the payable
/// and never creates a second expense.
/// </summary>
public sealed class PayrollViewModel : ViewModelBase
{
    private readonly ILedgerOperationsService _ledger;
    private readonly IReportingService _reporting;
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public PayrollViewModel(ILedgerOperationsService ledger, IReportingService reporting,
        IAccountingService accounting, AppState state)
    {
        _ledger = ledger;
        _reporting = reporting;
        _accounting = accounting;
        _state = state;

        AccrueCommand = new AsyncRelayCommand(_ => AccrueAsync());
        LoadCommand = new AsyncRelayCommand(_ => LoadAsync());
        PayCommand = new AsyncRelayCommand(_ => PayAsync(), () => Selected is not null);
        ExportCommand = new RelayCommand(_ => Export());

        PeriodKey = PeriodKeys.FromDate(DateTime.Today);
        PaymentDate = DateTime.Today;
        _ = LoadAsync();
    }

    public ObservableCollection<PayrollRowItem> Rows { get; } = [];
    public ObservableCollection<LookupItem> Accounts { get; } = [];

    public string PeriodKey { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public int MethodIndex { get; set; }

    private PayrollRowItem? _selected;
    public PayrollRowItem? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                ((AsyncRelayCommand)PayCommand).RaiseCanExecuteChanged();
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

    public ICommand AccrueCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand PayCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<PayrollRow> rows = string.IsNullOrWhiteSpace(PeriodKey)
                ? Array.Empty<PayrollRow>()
                : await _reporting.GetPayrollForPeriodAsync(PeriodKey.Trim());

            Rows.Clear();
            Core.Money net = Core.Money.Zero;
            Core.Money paid = Core.Money.Zero;

            foreach (PayrollRow row in rows)
            {
                net += row.NetSalary;
                if (row.PaymentDate is not null)
                {
                    paid += row.NetSalary;
                }

                Rows.Add(new PayrollRowItem
                {
                    PayrollRecordId = row.PayrollRecordId,
                    Code = row.StaffCode,
                    Name = row.StaffName,
                    Designation = row.Designation,
                    Basic = _state.Money(row.BasicSalary),
                    Allowances = _state.Money(row.Allowances),
                    Deductions = _state.Money(row.Deductions),
                    Net = _state.Money(row.NetSalary),
                    Status = row.Status switch
                    {
                        PayrollStatus.Paid => "পরিশোধিত",
                        PayrollStatus.Cancelled => "বাতিল",
                        _ => "খসড়া"
                    },
                    Paid = row.PaymentDate is null
                        ? "—"
                        : Core.Digits.Format(row.PaymentDate.Value, _state.UseBengaliDigits)
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
            TotalText = $"মোট নিট বেতন: {_state.Money(net)} · পরিশোধিত: {_state.Money(paid)} "
                        + $"· বকেয়া: {_state.Money(net - paid)}";
        }, "বেতন আনা হচ্ছে…");
    }

    private async Task AccrueAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(PeriodKey))
            {
                throw new ValidationException("মাস দিন (YYYY-MM)।");
            }

            await _ledger.AccruePayrollAsync(PeriodKey.Trim());
            StatusMessage = "বেতন তৈরি ও ব্যয় স্বীকৃত করা হয়েছে: " + PeriodKey.Trim();
            await LoadAsync();
        }, "বেতন তৈরি করা হচ্ছে…");
    }

    private async Task PayAsync()
    {
        await RunAsync(async () =>
        {
            if (Selected is null || Account is null)
            {
                return;
            }

            await _ledger.PaySalaryAsync(
                Selected.PayrollRecordId,
                PaymentDate.Date,
                Account.Id,
                Labels.MethodAt(MethodIndex));

            StatusMessage = Selected.Name + "-এর বেতন পরিশোধ করা হয়েছে।";
            await LoadAsync();
        }, "পরিশোধ করা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("beton.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("কোড", r => r.Code),
            ("নাম", r => r.Name),
            ("পদবি", r => r.Designation),
            ("মূল", r => r.Basic),
            ("ভাতা", r => r.Allowances),
            ("কর্তন", r => r.Deductions),
            ("নিট", r => r.Net),
            ("অবস্থা", r => r.Status),
            ("পরিশোধ", r => r.Paid)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}
