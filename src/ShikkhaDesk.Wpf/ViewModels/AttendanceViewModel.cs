using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class AttendanceRow
{
    public Guid StudentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>0 = উপস্থিত, 1 = অনুপস্থিত, 2 = দেরি, 3 = ছুটি. Matches <see cref="AttendanceStatus"/>.</summary>
    public int StatusIndex { get; set; }

    public string? Note { get; set; }

    public static AttendanceStatus ToStatus(int index) => index switch
    {
        1 => AttendanceStatus.Absent,
        2 => AttendanceStatus.Late,
        3 => AttendanceStatus.Leave,
        _ => AttendanceStatus.Present
    };

    public static int FromStatus(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Absent => 1,
        AttendanceStatus.Late => 2,
        AttendanceStatus.Leave => 3,
        _ => 0
    };
}

/// <summary>
/// Daily attendance for one batch. Saving rewrites the day's records through the service, which keeps
/// a link to any record it corrects, so a correction is auditable rather than silently overwritten.
/// </summary>
public sealed class AttendanceViewModel : ViewModelBase
{
    private readonly IAttendanceService _attendance;
    private readonly IStudentService _students;
    private readonly IAcademicService _academic;

    public AttendanceViewModel(IAttendanceService attendance, IStudentService students,
        IAcademicService academic)
    {
        _attendance = attendance;
        _students = students;
        _academic = academic;

        LoadCommand = new AsyncRelayCommand(_ => LoadAsync());
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), () => Rows.Count > 0);
        MarkAllPresentCommand = new RelayCommand(_ => MarkAllPresent());

        _ = LoadBatchesAsync();
    }

    public ObservableCollection<LookupItem> Batches { get; } = [];
    public ObservableCollection<AttendanceRow> Rows { get; } = [];

    private LookupItem? _batch;
    public LookupItem? Batch
    {
        get => _batch;
        set
        {
            if (SetField(ref _batch, value))
            {
                _ = LoadAsync();
            }
        }
    }

    private DateTime _date = DateTime.Today;
    public DateTime Date
    {
        get => _date;
        set
        {
            if (SetField(ref _date, value))
            {
                _ = LoadAsync();
            }
        }
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand MarkAllPresentCommand { get; }

    private async Task LoadBatchesAsync()
    {
        await RunAsync(async () =>
        {
            Batches.Clear();
            foreach (Batch batch in await _academic.GetBatchesAsync(activeOnly: true))
            {
                Batches.Add(new LookupItem { Id = batch.Id, Name = batch.Name });
            }

            Batch = Batches.FirstOrDefault();
        });
    }

    private async Task LoadAsync()
    {
        if (Batch is null || Batch.Id == Guid.Empty)
        {
            return;
        }

        await RunAsync(async () =>
        {
            PagedResult<Student> roster = await _students.SearchAsync(new StudentQuery(
                BatchId: Batch.Id, Status: StudentStatus.Active, PageSize: 500));

            IReadOnlyList<AttendanceRecord> existing = await _attendance.GetForBatchDateAsync(Batch.Id, Date);
            Dictionary<Guid, AttendanceRecord> byStudent = existing.ToDictionary(r => r.StudentId);

            Rows.Clear();
            foreach (Student student in roster.Items)
            {
                byStudent.TryGetValue(student.Id, out AttendanceRecord? record);
                Rows.Add(new AttendanceRow
                {
                    StudentId = student.Id,
                    Code = student.StudentCode,
                    Name = student.FullName,
                    StatusIndex = record is null ? 0 : AttendanceRow.FromStatus(record.Status),
                    Note = record?.Note
                });
            }

            ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();

            AttendanceSummary summary = await _attendance.GetSummaryForBatchDateAsync(Batch.Id, Date);
            Summary = $"উপস্থিত {summary.Present} · অনুপস্থিত {summary.Absent} · দেরি {summary.Late} "
                      + $"· ছুটি {summary.Leave} · মোট {summary.Total} · শতাংশ {summary.PresentPercentage}%";
        }, "উপস্থিতি আনা হচ্ছে…");
    }

    private void MarkAllPresent()
    {
        foreach (AttendanceRow row in Rows)
        {
            row.StatusIndex = 0;
        }

        OnPropertyChanged(nameof(Rows));
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (Batch is null)
            {
                return;
            }

            List<AttendanceEntry> entries = Rows
                .Select(r => new AttendanceEntry(r.StudentId, AttendanceRow.ToStatus(r.StatusIndex), r.Note))
                .ToList();

            await _attendance.SaveAsync(Batch.Id, Date, entries);
            await LoadAsync();
            StatusMessage = "উপস্থিতি সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}
