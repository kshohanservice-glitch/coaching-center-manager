using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class StudentSearchRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

public sealed class DueLine
{
    public Guid ChargeId { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string Net { get; init; } = string.Empty;
    public string Paid { get; init; } = string.Empty;
    public string Outstanding { get; init; } = string.Empty;
    public string DueDate { get; init; } = string.Empty;
}

/// <summary>
/// Fee collection. Money is only ever taken through <see cref="IPaymentService.CollectAsync"/>, which
/// posts the ledger entry and allocates the payment in one transaction; anything received but not yet
/// allocated becomes advance credit (a liability), never income.
/// </summary>
public sealed class CollectFeeViewModel : ViewModelBase
{
    private readonly IPaymentService _payments;
    private readonly IFeeService _fees;
    private readonly IStudentService _students;
    private readonly IAccountingService _accounting;
    private readonly IPrintService _printing;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public CollectFeeViewModel(IPaymentService payments, IFeeService fees, IStudentService students,
        IAccountingService accounting, IPrintService printing, IDialogService dialogs, AppState state)
    {
        _payments = payments;
        _fees = fees;
        _students = students;
        _accounting = accounting;
        _printing = printing;
        _dialogs = dialogs;
        _state = state;

        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        SelectCommand = new AsyncRelayCommand(_ => SelectAsync(), () => SelectedSearch is not null);
        CollectCommand = new AsyncRelayCommand(_ => CollectAsync(), () => Student is not null);
        PrintCommand = new RelayCommand(_ => PrintReceipt(), () => LastReceipt is not null);
        ApplyCreditCommand = new AsyncRelayCommand(_ => ApplyCreditAsync(), () => Student is not null);

        _ = LoadAccountsAsync();
    }

    public ObservableCollection<StudentSearchRow> SearchResults { get; } = [];
    public ObservableCollection<DueLine> Dues { get; } = [];
    public ObservableCollection<LookupItem> Accounts { get; } = [];

    private string? _search;
    public string? Search
    {
        get => _search;
        set => SetField(ref _search, value);
    }

    private StudentSearchRow? _selectedSearch;
    public StudentSearchRow? SelectedSearch
    {
        get => _selectedSearch;
        set
        {
            if (SetField(ref _selectedSearch, value))
            {
                ((AsyncRelayCommand)SelectCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public Student? Student { get; private set; }

    private StudentBalanceSummary? _balance;
    public StudentBalanceSummary? Balance
    {
        get => _balance;
        set => SetField(ref _balance, value);
    }

    public string StudentText => Student is null
        ? "কোনো শিক্ষার্থী নির্বাচন করা হয়নি।"
        : $"{Student.FullName} ({Student.StudentCode})";

    public string BalanceText => Balance is null
        ? string.Empty
        : $"মোট ধার্য: {_state.Money(Balance.TotalCharged)} · ছাড়: {_state.Money(Balance.TotalDiscount)} "
          + $"· পরিশোধ: {_state.Money(Balance.TotalPaid)} · বকেয়া: {_state.Money(Balance.Outstanding)} "
          + $"· ক্রেডিট: {_state.Money(Balance.CreditBalance)}";

    public string AmountText { get; set; } = "0";
    public int MethodIndex { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    private LookupItem? _account;
    public LookupItem? Account
    {
        get => _account;
        set => SetField(ref _account, value);
    }

    private string _receiptText = string.Empty;
    public string ReceiptText
    {
        get => _receiptText;
        set => SetField(ref _receiptText, value);
    }

    public ReceiptData? LastReceipt { get; private set; }

    public ICommand SearchCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CollectCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ApplyCreditCommand { get; }

    private async Task LoadAccountsAsync()
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

            Account = Accounts.FirstOrDefault();
        });
    }

    private async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Search))
            {
                throw new ValidationException("নাম, কোড বা ফোন দিন।");
            }

            PagedResult<Student> page = await _students.SearchAsync(
                new StudentQuery(Search: Search.Trim(), PageSize: 50));

            SearchResults.Clear();
            foreach (Student student in page.Items)
            {
                SearchResults.Add(new StudentSearchRow
                {
                    Id = student.Id,
                    Code = student.StudentCode,
                    Name = student.FullName,
                    Phone = student.GuardianPhone ?? student.StudentPhone ?? "—"
                });
            }
        }, "খোঁজা হচ্ছে…");
    }

    private async Task SelectAsync()
    {
        if (SelectedSearch is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Student = await _students.GetAsync(SelectedSearch.Id);
            Balance = await _fees.GetStudentBalanceAsync(SelectedSearch.Id);

            Dues.Clear();
            foreach (StudentDuesRow row in await _fees.GetStudentDuesAsync(SelectedSearch.Id))
            {
                Dues.Add(new DueLine
                {
                    ChargeId = row.ChargeId,
                    Description = row.Description,
                    Period = row.PeriodKey ?? "—",
                    Net = _state.Money(row.NetAmount),
                    Paid = _state.Money(row.PaidAmount),
                    Outstanding = _state.Money(row.Outstanding),
                    DueDate = Core.Digits.Format(row.DueDate, _state.UseBengaliDigits)
                });
            }

            AmountText = Balance.PayableAfterCredit.ToDecimal().ToString("0.##", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(AmountText));
            OnPropertyChanged(nameof(StudentText));
            OnPropertyChanged(nameof(BalanceText));
            ((AsyncRelayCommand)CollectCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ApplyCreditCommand).RaiseCanExecuteChanged();
        }, "বিবরণ আনা হচ্ছে…");
    }

    private async Task CollectAsync()
    {
        await RunAsync(async () =>
        {
            if (Student is null)
            {
                return;
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

            var request = new CollectPaymentRequest(
                StudentId: Student.Id,
                Amount: Money.FromDecimal(amount),
                Method: ToMethod(MethodIndex),
                FinancialAccountId: Account?.Id,
                TransactionReference: Reference.Trim().Length == 0 ? null : Reference.Trim(),
                PaymentDate: PaymentDate,
                Notes: Notes.Trim().Length == 0 ? null : Notes.Trim());

            PaymentResult result = await _payments.CollectAsync(request);
            LastReceipt = result.Receipt;

            ReceiptText = BuildReceiptText(result);
            StatusMessage = result.WasDuplicate
                ? "এই পেমেন্টটি ইতিমধ্যে নেওয়া হয়েছে — নকল প্রতিরোধ করা হয়েছে।"
                : "পেমেন্ট গ্রহণ করা হয়েছে: " + result.ReceiptNumber;

            ((RelayCommand)PrintCommand).RaiseCanExecuteChanged();

            await SelectAsync();
        }, "পেমেন্ট নেওয়া হচ্ছে…");
    }

    private string BuildReceiptText(PaymentResult result)
    {
        ReceiptData receipt = result.Receipt;
        IEnumerable<string> lines = receipt.Lines.Select(l =>
            $"   {l.Description}{(l.PeriodKey is null ? string.Empty : " (" + l.PeriodKey + ")")}: "
            + _state.Money(l.Amount));

        return string.Join(Environment.NewLine,
            $"রশিদ নং: {receipt.ReceiptNumber}",
            $"তারিখ: {Core.Digits.Format(receipt.PaymentDate, _state.UseBengaliDigits, "dd-MM-yyyy HH:mm")}",
            $"শিক্ষার্থী: {receipt.StudentName} ({receipt.StudentCode})",
            string.Empty,
            string.Join(Environment.NewLine, lines),
            string.Empty,
            $"মোট প্রদান: {_state.Money(receipt.AmountReceived)}",
            $"ক্রেডিটে গিয়েছে: {_state.Money(receipt.AdvanceCredit)}",
            $"অবশিষ্ট বকেয়া: {_state.Money(receipt.RemainingOutstanding)}");
    }

    private void PrintReceipt()
    {
        if (LastReceipt is null)
        {
            return;
        }

        try
        {
            _printing.PrintReceipt(LastReceipt);
        }
        catch (Exception ex)
        {
            _dialogs.Error("মুদ্রণ করা যায়নি: " + ex.Message);
        }
    }

    private async Task ApplyCreditAsync()
    {
        await RunAsync(async () =>
        {
            if (Student is null)
            {
                return;
            }

            Core.Money applied = await _payments.ApplyCreditAsync(Student.Id);
            StatusMessage = "ক্রেডিট বরাদ্দ করা হয়েছে: " + _state.Money(applied);
            await SelectAsync();
        }, "ক্রেডিট বরাদ্দ করা হচ্ছে…");
    }

    private static PaymentMethod ToMethod(int index) => index switch
    {
        1 => PaymentMethod.Bank,
        2 => PaymentMethod.MobileBanking,
        3 => PaymentMethod.Card,
        _ => PaymentMethod.Cash
    };
}
