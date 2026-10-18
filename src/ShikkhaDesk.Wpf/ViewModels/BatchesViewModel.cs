using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class BatcheRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Course { get; init; } = string.Empty;
    public string Starts { get; init; } = string.Empty;
    public string Capacity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class BatchesViewModel : ViewModelBase
{
    private readonly IAcademicService _academic;
    private readonly AppState _state;

    public BatchesViewModel(IAcademicService academic, AppState state)
    {
        _academic = academic;
        _state = state;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        _ = LoadAsync();
    }

    public ObservableCollection<BatcheRow> Rows { get; } = [];
    public ObservableCollection<LookupItem> Sessions { get; } = [];
    public ObservableCollection<LookupItem> Courses { get; } = [];
    public ObservableCollection<LookupItem> Classes { get; } = [];

    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public string CapacityText { get; set; } = "40";
    public string ScheduleNote { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string StartTime { get; set; } = "16:00";
    public string EndTime { get; set; } = "17:30";
    public int DayOfWeekIndex { get; set; }

    public LookupItem? Session { get; set; }
    public LookupItem? Course { get; set; }
    public LookupItem? ClassGrade { get; set; }

    public ICommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<Batch> batches = await _academic.GetBatchesAsync();
            IReadOnlyList<Course> courses = await _academic.GetCoursesAsync();

            Rows.Clear();
            foreach (Batch batch in batches)
            {
                Rows.Add(new BatcheRow
                {
                    Id = batch.Id,
                    Name = batch.Name,
                    Course = courses.FirstOrDefault(c => c.Id == batch.CourseId)?.Name ?? "—",
                    Starts = Core.Digits.Format(batch.StartDate, _state.UseBengaliDigits),
                    Capacity = batch.Capacity?.ToString() ?? "—",
                    Status = batch.IsActive ? "চলমান" : "বন্ধ"
                });
            }

            Fill(Sessions, (await _academic.GetSessionsAsync())
                .Select(s => new LookupItem { Id = s.Id, Name = s.Name }));
            Fill(Courses, courses.Select(c => new LookupItem { Id = c.Id, Name = c.Name }));
            Fill(Classes, (await _academic.GetClassGradesAsync())
                .Select(g => new LookupItem { Id = g.Id, Name = g.Name }));

            Session ??= Sessions.FirstOrDefault();
        }, "তালিকা আনা হচ্ছে…");
    }

    private static void Fill(ObservableCollection<LookupItem> target, IEnumerable<LookupItem> items)
    {
        target.Clear();
        target.Add(new LookupItem { Id = Guid.Empty, Name = "—" });
        foreach (LookupItem item in items)
        {
            target.Add(item);
        }
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (Name.Trim().Length == 0)
            {
                throw new ValidationException("ব্যাচের নাম দিন।");
            }

            if (Session is null || Session.Id == Guid.Empty)
            {
                throw new ValidationException("শিক্ষাবর্ষ নির্বাচন করুন।");
            }

            int? capacity = int.TryParse(CapacityText.Trim(), out int parsed) ? parsed : null;

            var schedule = new List<BatchSchedule>
            {
                new()
                {
                    DayOfWeek = DayOfWeekIndex,
                    StartTime = StartTime.Trim(),
                    EndTime = EndTime.Trim(),
                    Room = Room.Trim().Length == 0 ? null : Room.Trim()
                }
            };

            await _academic.SaveBatchAsync(new Batch
            {
                Name = Name.Trim(),
                SessionId = Session.Id,
                CourseId = Course is { Id: var courseId } && courseId != Guid.Empty ? courseId : null,
                ClassGradeId = ClassGrade is { Id: var gradeId } && gradeId != Guid.Empty ? gradeId : null,
                StartDate = StartDate.Date,
                Capacity = capacity,
                ScheduleNote = ScheduleNote.Trim().Length == 0 ? null : ScheduleNote.Trim(),
                IsActive = true
            }, schedule);

            Name = string.Empty;
            OnPropertyChanged(nameof(Name));
            await LoadAsync();
            StatusMessage = "ব্যাচ সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}
