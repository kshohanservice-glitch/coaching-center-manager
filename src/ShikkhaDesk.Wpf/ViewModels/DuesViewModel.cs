using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class DuesRow
{
    public Guid ChargeId { get; init; }
    public Guid StudentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Student { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string DueDate { get; init; } = string.Empty;
    public string Outstanding { get; init; } = string.Empty;
    public long OutstandingMinor { get; init; }
    public string DaysOverdue { get; init; } = string.Empty;
}

/// <summary>Outstanding receivables. Read from the live charges, never from a cached summary.</summary>
public sealed class DuesViewModel : ViewModelBase
{
    private readonly IFeeService _fees;
    private readonly IStudentService _students;
    private readonly IAcademicService _academic;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public DuesViewModel(IFeeService fees, IStudentService students, IAcademicService academic,
        IDialogService dialogs, AppState state)
    {
        _fees = fees;
        _students = students;
        _academic = academic;
        _dialogs = dialogs;
        _state = state;

        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        ExportCommand = new RelayCommand(_ => Export());
        _ = LoadAsync();
    }

    public ObservableCollection<DuesRow> Rows { get; } = [];
    public ObservableCollection<LookupItem> Batches { get; } = [];

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
            IReadOnlyList<FeeCharge> charges = await _fees.GetOutstandingChargesAsync(
                batchId: Batch is { Id: var id } && id != Guid.Empty ? id : null);

            Dictionary<Guid, Student> students = (await _students.SearchAsync(new StudentQuery(PageSize: 1000)))
                .Items.ToDictionary(s => s.Id);

            Rows.Clear();
            Core.Money total = Core.Money.Zero;

            foreach (FeeCharge charge in charges.OrderBy(c => c.DueDate))
            {
                students.TryGetValue(charge.StudentId, out Student? student);
                int overdue = (DateTime.Today - charge.DueDate.Date).Days;
                total += charge.Outstanding;

                Rows.Add(new DuesRow
                {
                    ChargeId = charge.Id,
                    StudentId = charge.StudentId,
                    Code = student?.StudentCode ?? "—",
                    Student = student?.FullName ?? "—",
                    Phone = student?.GuardianPhone ?? student?.StudentPhone ?? "—",
                    Kind = Translate(charge.Kind),
                    Description = charge.Description,
                    Period = charge.PeriodKey ?? "—",
                    DueDate = Core.Digits.Format(charge.DueDate, _state.UseBengaliDigits),
                    Outstanding = _state.Money(charge.Outstanding),
                    OutstandingMinor = charge.Outstanding.MinorUnits,
                    DaysOverdue = overdue > 0 ? _state.Number(overdue) + " দিন" : "—"
                });
            }

            TotalText = $"মোট বকেয়া: {_state.Money(total)} · খাত: {_state.Number(Rows.Count)}";
        }, "বকেয়া হিসাব করা হচ্ছে…");
    }

    private static string Translate(FeeChargeKind kind) => Labels.For(kind);

    private void Export()
    {
        string? path = _dialogs.SaveFile("boyeka.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("কোড", r => r.Code),
            ("নাম", r => r.Student),
            ("ফোন", r => r.Phone),
            ("খাত", r => r.Kind),
            ("বিবরণ", r => r.Description),
            ("মাস", r => r.Period),
            ("পরিশোধের তারিখ", r => r.DueDate),
            ("বকেয়া", r => r.Outstanding),
            ("মেয়াদোত্তীর্ণ", r => r.DaysOverdue)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}
