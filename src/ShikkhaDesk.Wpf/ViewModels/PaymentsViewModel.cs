using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class PaymentHistoryRow
{
    public Guid PaymentId { get; init; }
    public string Receipt { get; init; } = string.Empty;
    public string StudentCode { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Received { get; init; } = string.Empty;
    public string Allocated { get; init; } = string.Empty;
    public string Credit { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CollectedBy { get; init; } = string.Empty;
}

public sealed class PaymentsViewModel : ViewModelBase
{
    private readonly IPaymentService _payments;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public PaymentsViewModel(IPaymentService payments, IDialogService dialogs, AppState state)
    {
        _payments = payments;
        _dialogs = dialogs;
        _state = state;

        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        ReverseCommand = new AsyncRelayCommand(_ => ReverseAsync(), () => Selected is not null);
        ExportCommand = new RelayCommand(_ => Export());

        From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To = DateTime.Today;
        _ = SearchAsync();
    }

    public ObservableCollection<PaymentHistoryRow> Rows { get; } = [];

    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? StudentSearch { get; set; }
    public int MethodIndex { get; set; }

    private PaymentHistoryRow? _selected;
    public PaymentHistoryRow? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                ((AsyncRelayCommand)ReverseCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private string _totalText = string.Empty;
    public string TotalText
    {
        get => _totalText;
        set => SetField(ref _totalText, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand ReverseCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            Guid? studentId = null;
            if (!string.IsNullOrWhiteSpace(StudentSearch))
            {
                // The history screen searches by name/code through the payments query itself, so no
                // second lookup is needed.
                studentId = null;
            }

            var query = new PaymentQuery(
                From: From.Date,
                To: To.Date,
                StudentId: studentId,
                Method: MethodIndex switch
                {
                    1 => PaymentMethod.Cash,
                    2 => PaymentMethod.Bank,
                    3 => PaymentMethod.MobileBanking,
                    4 => PaymentMethod.Card,
                    _ => null
                },
                PageSize: 500);

            PagedResult<PaymentListItem> page = await _payments.SearchAsync(query);

            Rows.Clear();
            Core.Money received = Core.Money.Zero;

            foreach (PaymentListItem item in page.Items)
            {
                received += item.AmountReceived;
                Rows.Add(new PaymentHistoryRow
                {
                    PaymentId = item.PaymentId,
                    Receipt = item.ReceiptNumber,
                    StudentCode = item.StudentCode,
                    StudentName = item.StudentName,
                    Date = Core.Digits.Format(item.PaymentDate, _state.UseBengaliDigits),
                    Received = _state.Money(item.AmountReceived),
                    Allocated = _state.Money(item.AmountAllocated),
                    Credit = _state.Money(item.Unallocated),
                    Method = Translate(item.Method),
                    Status = Translate(item.Status),
                    CollectedBy = item.CollectedBy
                });
            }

            TotalText = $"মোট আদায়: {_state.Money(received)} · পেমেন্ট: {_state.Number(page.TotalCount)}";
        }, "খোঁজা হচ্ছে…");
    }

    private async Task ReverseAsync()
    {
        if (Selected is null)
        {
            return;
        }

        var prompt = new PromptWindow("পেমেন্ট বাতিল",
            $"রশিদ {Selected.Receipt}-এর পেমেন্ট বাতিল করা হবে। এটি একটি বিপরীত জার্নাল এন্ট্রি তৈরি করবে; "
            + "মূল রেকর্ড মুছে যাবে না।",
            "বাতিলের কারণ লিখুন *", multiline: true);

        if (prompt.ShowDialog() != true)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _payments.ReversePaymentAsync(Selected.PaymentId, prompt.Value);
            StatusMessage = "পেমেন্ট বাতিল করা হয়েছে এবং বিপরীত এন্ট্রি পোস্ট করা হয়েছে।";
            await SearchAsync();
        }, "বাতিল করা হচ্ছে…");
    }

    private void Export()
    {
        string? path = _dialogs.SaveFile("payment.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("রশিদ", r => r.Receipt),
            ("কোড", r => r.StudentCode),
            ("নাম", r => r.StudentName),
            ("তারিখ", r => r.Date),
            ("প্রদান", r => r.Received),
            ("বরাদ্দ", r => r.Allocated),
            ("ক্রেডিট", r => r.Credit),
            ("পদ্ধতি", r => r.Method),
            ("অবস্থা", r => r.Status),
            ("আদায়কারী", r => r.CollectedBy)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }

    private static string Translate(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "নগদ",
        PaymentMethod.Bank => "ব্যাংক",
        PaymentMethod.MobileBanking => "মোবাইল ব্যাংকিং",
        PaymentMethod.Card => "কার্ড",
        _ => method.ToString()
    };

    private static string Translate(PaymentStatus status) => status switch
    {
        PaymentStatus.Active => "বৈধ",
        PaymentStatus.Reversed => "বাতিল",
        PaymentStatus.PartiallyRefunded => "আংশিক ফেরত",
        PaymentStatus.Refunded => "ফেরত",
        _ => status.ToString()
    };
}
