using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>
/// Reports centre. Every report is generated from the same services the individual screens use, so
/// the centre is a convenience, never a second source of numbers.
/// </summary>
public sealed class ReportsViewModel : ViewModelBase
{
    private readonly IStudentService _students;
    private readonly IFeeService _fees;
    private readonly IPaymentService _payments;
    private readonly IReportingService _reporting;
    private readonly IAcademicService _academic;
    private readonly IPrintService _printing;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public ReportsViewModel(IStudentService students, IFeeService fees, IPaymentService payments,
        IReportingService reporting, IAcademicService academic, IPrintService printing,
        IDialogService dialogs, AppState state)
    {
        _students = students;
        _fees = fees;
        _payments = payments;
        _reporting = reporting;
        _academic = academic;
        _printing = printing;
        _dialogs = dialogs;
        _state = state;

        GenerateCommand = new AsyncRelayCommand(_ => GenerateAsync());
        ExportCommand = new RelayCommand(_ => Export());
        PrintCommand = new RelayCommand(_ => Print());

        From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To = DateTime.Today;
        _ = LoadBatchesAsync();
    }

    public ObservableCollection<LookupItem> Batches { get; } = [];
    public ObservableCollection<IDictionary<string, string>> Rows { get; } = [];

    public int ReportIndex { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    private LookupItem? _batch;
    public LookupItem? Batch
    {
        get => _batch;
        set => SetField(ref _batch, value);
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public ICommand GenerateCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand PrintCommand { get; }

    private async Task LoadBatchesAsync()
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
        });
    }

    private (string Title, string[] Headers) Current => ReportIndex switch
    {
        0 => ("শিক্ষার্থী তালিকা", ["কোড", "নাম", "অভিভাবক", "ফোন", "অবস্থা"]),
        1 => ("বকেয়া প্রতিবেদন", ["কোড", "নাম", "খাত", "মাস", "পরিশোধের তারিখ", "বকেয়া"]),
        2 => ("আদায় খতিয়ান", ["রশিদ", "কোড", "নাম", "তারিখ", "প্রদান", "বরাদ্দ", "পদ্ধতি"]),
        3 => ("আয়ের প্রতিবেদন", ["তারিখ", "নম্বর", "বিবরণ", "উৎস", "পরিমাণ"]),
        _ => ("ব্যয়ের প্রতিবেদন", ["তারিখ", "নম্বর", "বিবরণ", "ধরন", "পরিমাণ"])
    };

    private async Task GenerateAsync()
    {
        await RunAsync(async () =>
        {
            Rows.Clear();
            Guid? batchId = Batch is { Id: var id } && id != Guid.Empty ? id : null;

            switch (ReportIndex)
            {
                case 0:
                    await StudentReportAsync(batchId);
                    break;
                case 1:
                    await DuesReportAsync(batchId);
                    break;
                case 2:
                    await CollectionReportAsync(batchId);
                    break;
                case 3:
                    await IncomeReportAsync();
                    break;
                default:
                    await ExpenseReportAsync();
                    break;
            }

            Summary = $"সারি: {_state.Number(Rows.Count)} · {Current.Title}";
        }, "প্রতিবেদন তৈরি করা হচ্ছে…");
    }

    private async Task StudentReportAsync(Guid? batchId)
    {
        PagedResult<Student> page = await _students.SearchAsync(new StudentQuery(
            BatchId: batchId, Status: StudentStatus.Active, PageSize: 1000));

        foreach (Student student in page.Items)
        {
            Rows.Add(new Dictionary<string, string>
            {
                ["কোড"] = student.StudentCode,
                ["নাম"] = student.FullName,
                ["অভিভাবক"] = student.GuardianName ?? "—",
                ["ফোন"] = student.GuardianPhone ?? student.StudentPhone ?? "—",
                ["অবস্থা"] = student.Status.ToString()
            });
        }
    }

    private async Task DuesReportAsync(Guid? batchId)
    {
        Dictionary<Guid, Student> students = (await _students.SearchAsync(new StudentQuery(PageSize: 1000)))
            .Items.ToDictionary(s => s.Id);

        foreach (FeeCharge charge in await _fees.GetOutstandingChargesAsync(batchId: batchId))
        {
            students.TryGetValue(charge.StudentId, out Student? student);
            Rows.Add(new Dictionary<string, string>
            {
                ["কোড"] = student?.StudentCode ?? "—",
                ["নাম"] = student?.FullName ?? "—",
                ["খাত"] = charge.Description,
                ["মাস"] = charge.PeriodKey ?? "—",
                ["পরিশোধের তারিখ"] = Core.Digits.Format(charge.DueDate, _state.UseBengaliDigits),
                ["বকেয়া"] = _state.Money(charge.Outstanding)
            });
        }
    }

    private async Task CollectionReportAsync(Guid? batchId)
    {
        PagedResult<PaymentListItem> page = await _payments.SearchAsync(new PaymentQuery(
            From: From.Date, To: To.Date, BatchId: batchId, PageSize: 1000));

        foreach (PaymentListItem item in page.Items)
        {
            Rows.Add(new Dictionary<string, string>
            {
                ["রশিদ"] = item.ReceiptNumber,
                ["কোড"] = item.StudentCode,
                ["নাম"] = item.StudentName,
                ["তারিখ"] = Core.Digits.Format(item.PaymentDate, _state.UseBengaliDigits),
                ["প্রদান"] = _state.Money(item.AmountReceived),
                ["বরাদ্দ"] = _state.Money(item.AmountAllocated),
                ["পদ্ধতি"] = item.Method.ToString()
            });
        }
    }

    private async Task IncomeReportAsync()
    {
        foreach (IncomeRecord record in await _reporting.GetIncomeRegisterAsync(new DateRange(From.Date, To.Date)))
        {
            Rows.Add(new Dictionary<string, string>
            {
                ["তারিখ"] = Core.Digits.Format(record.IncomeDate, _state.UseBengaliDigits),
                ["নম্বর"] = record.IncomeNumber,
                ["বিবরণ"] = record.Description,
                ["উৎস"] = record.Source ?? "—",
                ["পরিমাণ"] = _state.Money(record.Amount)
            });
        }
    }

    private async Task ExpenseReportAsync()
    {
        foreach (ExpenseRecord record in await _reporting.GetExpenseRegisterAsync(new DateRange(From.Date, To.Date)))
        {
            Rows.Add(new Dictionary<string, string>
            {
                ["তারিখ"] = Core.Digits.Format(record.ExpenseDate, _state.UseBengaliDigits),
                ["নম্বর"] = record.ExpenseNumber,
                ["বিবরণ"] = record.Description,
                ["ধরন"] = record.IsAccrued ? "প্রদেয়" : "নগদ",
                ["পরিমাণ"] = _state.Money(record.Amount)
            });
        }
    }

    /// <summary>Column names for the current report; the grid builds its columns from these.</summary>
    public string[] Headers => Current.Headers;

    private void Export()
    {
        if (Rows.Count == 0)
        {
            _dialogs.Information("প্রথমে প্রতিবেদন তৈরি করুন।");
            return;
        }

        string? path = _dialogs.SaveFile("protibedon.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.Build(Headers,
            Rows.Select(row => Headers.Select(h => row.TryGetValue(h, out string? value) ? value : string.Empty))));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }

    private void Print()
    {
        if (Rows.Count == 0)
        {
            _dialogs.Information("প্রথমে প্রতিবেদন তৈরি করুন।");
            return;
        }

        _printing.PrintReport(Current.Title,
            $"{Core.Digits.Format(From, _state.UseBengaliDigits)} থেকে {Core.Digits.Format(To, _state.UseBengaliDigits)}",
            Headers,
            Rows.Select(row => (IReadOnlyList<string>)Headers
                .Select(h => row.TryGetValue(h, out string? value) ? value : string.Empty).ToArray()).ToList(),
            landscape: true);
    }
}
