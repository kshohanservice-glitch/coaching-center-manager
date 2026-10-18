using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>
/// Refunds and reversals. A refund returns money the student had already paid and never touches the
/// income accounts; cancelling or waiving a fee is a separate, explicit action that reverses income.
/// </summary>
public sealed class RefundsViewModel : ViewModelBase
{
    private readonly IPaymentService _payments;
    private readonly IFeeService _fees;
    private readonly IStudentService _students;
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public RefundsViewModel(IPaymentService payments, IFeeService fees, IStudentService students,
        IAccountingService accounting, AppState state)
    {
        _payments = payments;
        _fees = fees;
        _students = students;
        _accounting = accounting;
        _state = state;

        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        SelectCommand = new AsyncRelayCommand(_ => SelectAsync(), () => SelectedSearch is not null);
        RefundCommand = new AsyncRelayCommand(_ => RefundAsync(), () => Student is not null);
        ReverseCommand = new AsyncRelayCommand(_ => ReverseAsync());

        _ = LoadAccountsAsync();
    }

    public ObservableCollection<StudentSearchRow> SearchResults { get; } = [];
    public ObservableCollection<LookupItem> Accounts { get; } = [];

    public string? Search { get; set; }

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

    private string _summary = "কোনো শিক্ষার্থী নির্বাচন করা হয়নি।";
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public string AmountText { get; set; } = "0";
    public string Reason { get; set; } = string.Empty;
    public int MethodIndex { get; set; }
    public string ReceiptToReverse { get; set; } = string.Empty;
    public string ReverseReason { get; set; } = string.Empty;

    private LookupItem? _account;
    public LookupItem? Account
    {
        get => _account;
        set => SetField(ref _account, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand RefundCommand { get; }
    public ICommand ReverseCommand { get; }

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
            StudentBalanceSummary balance = await _fees.GetStudentBalanceAsync(Student!.Id)
                ?? throw new NotFoundException("ব্যালেন্স পাওয়া যায়নি।");

            Summary = $"{Student.FullName} ({Student.StudentCode}) · বকেয়া {_state.Money(balance.Outstanding)} "
                      + $"· ক্রেডিট {_state.Money(balance.CreditBalance)}";

            ((AsyncRelayCommand)RefundCommand).RaiseCanExecuteChanged();
        }, "বিবরণ আনা হচ্ছে…");
    }

    private async Task RefundAsync()
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

            if (Reason.Trim().Length < 3)
            {
                throw new ValidationException("ফেরতের কারণ লিখুন।");
            }

            await _payments.RefundAsync(new RefundRequest(
                StudentId: Student.Id,
                Amount: Money.FromDecimal(amount),
                Reason: Reason.Trim(),
                Method: ToMethod(MethodIndex),
                FinancialAccountId: Account?.Id));

            StatusMessage = "ফেরত সম্পন্ন: " + _state.Money(Money.FromDecimal(amount));
            Reason = string.Empty;
            AmountText = "0";
            OnPropertyChanged(nameof(Reason));
            OnPropertyChanged(nameof(AmountText));
            await SelectAsync();
        }, "ফেরত করা হচ্ছে…");
    }

    private async Task ReverseAsync()
    {
        await RunAsync(async () =>
        {
            if (ReceiptToReverse.Trim().Length == 0 || ReverseReason.Trim().Length < 3)
            {
                throw new ValidationException("রশিদ নম্বর ও বাতিলের কারণ দিন।");
            }

            ReceiptData receipt = await _payments.GetReceiptAsync(ReceiptToReverse.Trim(), markReprint: false);
            await _payments.ReversePaymentAsync(
                await FindPaymentIdAsync(receipt.ReceiptNumber), ReverseReason.Trim());

            StatusMessage = "পেমেন্ট বাতিল করা হয়েছে: " + receipt.ReceiptNumber;
        }, "বাতিল করা হচ্ছে…");
    }

    private async Task<Guid> FindPaymentIdAsync(string receiptNumber)
    {
        PagedResult<PaymentListItem> page = await _payments.SearchAsync(new PaymentQuery(PageSize: 1000));
        PaymentListItem? match = page.Items.FirstOrDefault(
            p => string.Equals(p.ReceiptNumber, receiptNumber, StringComparison.OrdinalIgnoreCase));

        return match?.PaymentId
               ?? throw new NotFoundException($"রশিদ {receiptNumber} খুঁজে পাওয়া যায়নি।");
    }

    private static PaymentMethod ToMethod(int index) => Labels.MethodAt(index);
}
