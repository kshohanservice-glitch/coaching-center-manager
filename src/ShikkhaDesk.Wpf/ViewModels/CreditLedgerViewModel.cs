using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class CreditRow
{
    public string Date { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
    public string Balance { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

/// <summary>
/// Advance credit for one student. Credit received is a liability until it is allocated to a fee, so
/// this ledger is the audit trail for money taken before it became income.
/// </summary>
public sealed class CreditLedgerViewModel : ViewModelBase
{
    private readonly IPaymentService _payments;
    private readonly IReportingService _reporting;
    private readonly IStudentService _students;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public CreditLedgerViewModel(IPaymentService payments, IReportingService reporting,
        IStudentService students, IDialogService dialogs, AppState state)
    {
        _payments = payments;
        _reporting = reporting;
        _students = students;
        _dialogs = dialogs;
        _state = state;

        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        SelectCommand = new AsyncRelayCommand(_ => SelectAsync(), () => SelectedSearch is not null);
        ApplyCommand = new AsyncRelayCommand(_ => ApplyAsync(), () => Student is not null);
    }

    public ObservableCollection<StudentSearchRow> SearchResults { get; } = [];
    public ObservableCollection<CreditRow> Rows { get; } = [];

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

    public ICommand SearchCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand ApplyCommand { get; }

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
            IReadOnlyList<StudentCreditTransaction> transactions =
                await _reporting.GetCreditLedgerAsync(Student!.Id);

            Rows.Clear();
            Money running = Money.Zero;

            foreach (StudentCreditTransaction transaction in transactions.OrderBy(t => t.TransactionDate))
            {
                running += transaction.SignedAmount;
                Rows.Add(new CreditRow
                {
                    Date = Core.Digits.Format(transaction.TransactionDate, _state.UseBengaliDigits),
                    Kind = Translate(transaction.Kind),
                    Amount = _state.Money(transaction.Amount),
                    Balance = _state.Money(running),
                    Description = transaction.Description ?? "—",
                    Reference = "—"
                });
            }

            Summary = $"{Student.FullName} ({Student.StudentCode}) · বর্তমান ক্রেডিট: {_state.Money(running)}";
            ((AsyncRelayCommand)ApplyCommand).RaiseCanExecuteChanged();
        }, "ক্রেডিট খতিয়ান আনা হচ্ছে…");
    }

    private async Task ApplyAsync()
    {
        if (Student is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Money applied = await _payments.ApplyCreditAsync(Student.Id);
            StatusMessage = "ক্রেডিট বরাদ্দ করা হয়েছে: " + _state.Money(applied);
            await SelectAsync();
        }, "ক্রেডিট বরাদ্দ করা হচ্ছে…");
    }

    private static string Translate(CreditTransactionKind kind) => kind switch
    {
        CreditTransactionKind.Received => "ক্রেডিট পাওয়া",
        CreditTransactionKind.Allocated => "ফিতে বরাদ্দ",
        CreditTransactionKind.Refunded => "ফেরত দেওয়া",
        CreditTransactionKind.Adjusted => "সমন্বয়",
        _ => kind.ToString()
    };
}
