using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class ExamRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class SubjectDraft
{
    public string Name { get; set; } = string.Empty;
    public string MaximumMarks { get; set; } = "100";
    public string PassMarks { get; set; } = "33";
}

public sealed class MarkRow
{
    public Guid StudentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Marks { get; set; } = string.Empty;
    public bool IsAbsent { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public sealed class ExamsViewModel : ViewModelBase
{
    private readonly IExamService _exams;
    private readonly IStudentService _students;
    private readonly IAcademicService _academic;
    private readonly AppState _state;

    public ExamsViewModel(IExamService exams, IStudentService students, IAcademicService academic,
        AppState state)
    {
        _exams = exams;
        _students = students;
        _academic = academic;
        _state = state;

        CreateExamCommand = new AsyncRelayCommand(_ => CreateExamAsync());
        AddSubjectCommand = new RelayCommand(_ => SubjectDrafts.Add(new SubjectDraft()));
        LoadMarksCommand = new AsyncRelayCommand(_ => LoadMarksAsync());
        SaveMarksCommand = new AsyncRelayCommand(_ => SaveMarksAsync());
        PublishCommand = new AsyncRelayCommand(_ => PublishAsync());
        SaveGradeScaleCommand = new AsyncRelayCommand(_ => SaveGradeScaleAsync());

        _ = LoadAsync();
    }

    public ObservableCollection<ExamRow> Exams { get; } = [];
    public ObservableCollection<SubjectDraft> SubjectDrafts { get; } = [];
    public ObservableCollection<LookupItem> Sessions { get; } = [];
    public ObservableCollection<LookupItem> Batches { get; } = [];
    public ObservableCollection<LookupItem> ExamSubjects { get; } = [];
    public ObservableCollection<MarkRow> Marks { get; } = [];
    public ObservableCollection<GradeScaleItem> GradeScale { get; } = [];

    private ExamRow? _selected;
    public ExamRow? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                _ = LoadSubjectsAsync();
            }
        }
    }

    private LookupItem? _selectedSubject;
    public LookupItem? SelectedSubject
    {
        get => _selectedSubject;
        set => SetField(ref _selectedSubject, value);
    }

    public string ExamName { get; set; } = string.Empty;
    public string ExamType { get; set; } = "মাসিক";
    public DateTime ExamDate { get; set; } = DateTime.Today;
    public LookupItem? Session { get; set; }
    public LookupItem? Batch { get; set; }

    public ICommand CreateExamCommand { get; }
    public ICommand AddSubjectCommand { get; }
    public ICommand LoadMarksCommand { get; }
    public ICommand SaveMarksCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand SaveGradeScaleCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Exams.Clear();
            foreach (Exam exam in await _exams.GetExamsAsync())
            {
                Exams.Add(new ExamRow
                {
                    Id = exam.Id,
                    Name = exam.Name,
                    Type = exam.ExamType ?? "—",
                    Date = Core.Digits.Format(exam.ExamDate, _state.UseBengaliDigits),
                    Status = exam.Status switch
                    {
                        ExamStatus.Published => "প্রকাশিত",
                        _ => "খসড়া"
                    }
                });
            }

            Fill(Sessions, (await _academic.GetSessionsAsync())
                .Select(s => new LookupItem { Id = s.Id, Name = s.Name }));
            Fill(Batches, (await _academic.GetBatchesAsync(activeOnly: true))
                .Select(b => new LookupItem { Id = b.Id, Name = b.Name }));
            Session ??= Sessions.FirstOrDefault();

            GradeScale.Clear();
            foreach (GradeScaleItem item in await _exams.GetGradeScaleAsync())
            {
                GradeScale.Add(item);
            }
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

    private async Task CreateExamAsync()
    {
        await RunAsync(async () =>
        {
            if (ExamName.Trim().Length == 0 || Session is null || Session.Id == Guid.Empty)
            {
                throw new ValidationException("পরীক্ষার নাম ও শিক্ষাবর্ষ দিন।");
            }

            if (SubjectDrafts.Count == 0)
            {
                throw new ValidationException("কমপক্ষে একটি বিষয় যোগ করুন।");
            }

            var subjects = SubjectDrafts.Select((draft, index) => new ExamSubject
            {
                SubjectName = draft.Name.Trim(),
                MaximumMarks = decimal.TryParse(draft.MaximumMarks, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal max) ? max : 100m,
                PassMarks = decimal.TryParse(draft.PassMarks, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal pass) ? pass : 33m,
                SortOrder = index
            }).ToList();

            if (subjects.Any(s => s.SubjectName.Length == 0))
            {
                throw new ValidationException("প্রতিটি বিষয়ের নাম দিন।");
            }

            await _exams.SaveExamAsync(new Exam
            {
                Name = ExamName.Trim(),
                ExamType = ExamType.Trim().Length == 0 ? null : ExamType.Trim(),
                SessionId = Session.Id,
                ExamDate = ExamDate.Date,
                BatchId = Batch is { Id: var batchId } && batchId != Guid.Empty ? batchId : null,
                Status = ExamStatus.Draft
            }, subjects);

            ExamName = string.Empty;
            OnPropertyChanged(nameof(ExamName));
            SubjectDrafts.Clear();
            await LoadAsync();
            StatusMessage = "পরীক্ষা তৈরি করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task LoadSubjectsAsync()
    {
        if (Selected is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Fill(ExamSubjects, (await _exams.GetExamSubjectsAsync(Selected.Id))
                .Select(s => new LookupItem { Id = s.Id, Name = $"{s.SubjectName} (পূর্ণমান {s.MaximumMarks})" }));
            SelectedSubject = ExamSubjects.FirstOrDefault(i => i.Id != Guid.Empty);
            Marks.Clear();
        });
    }

    private async Task LoadMarksAsync()
    {
        if (Selected is null || SelectedSubject is null || SelectedSubject.Id == Guid.Empty)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Exam? exam = (await _exams.GetExamsAsync()).FirstOrDefault(e => e.Id == Selected.Id);

            PagedResult<Student> roster = await _students.SearchAsync(new StudentQuery(
                BatchId: exam?.BatchId, Status: StudentStatus.Active, PageSize: 500));

            IReadOnlyList<Mark> existing = await _exams.GetMarksAsync(Selected.Id);
            Dictionary<Guid, Mark> byStudent = existing
                .Where(m => m.ExamSubjectId == SelectedSubject.Id)
                .GroupBy(m => m.StudentId)
                .ToDictionary(g => g.Key, g => g.First());

            Marks.Clear();
            foreach (Student student in roster.Items)
            {
                byStudent.TryGetValue(student.Id, out Mark? mark);
                Marks.Add(new MarkRow
                {
                    StudentId = student.Id,
                    Code = student.StudentCode,
                    Name = student.FullName,
                    Marks = mark?.MarksObtained?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    IsAbsent = mark?.IsAbsent ?? false,
                    Remarks = mark?.Remarks ?? string.Empty
                });
            }
        }, "নম্বর আনা হচ্ছে…");
    }

    private async Task SaveMarksAsync()
    {
        await RunAsync(async () =>
        {
            if (SelectedSubject is null || SelectedSubject.Id == Guid.Empty)
            {
                throw new ValidationException("বিষয় নির্বাচন করুন।");
            }

            List<ExamMarkEntry> entries = Marks.Select(row => new ExamMarkEntry(
                row.StudentId,
                row.IsAbsent || !decimal.TryParse(row.Marks?.Trim(), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal obtained)
                    ? null
                    : obtained,
                row.IsAbsent,
                row.Remarks.Trim().Length == 0 ? null : row.Remarks.Trim())).ToList();

            await _exams.SaveMarksAsync(SelectedSubject.Id, entries);
            StatusMessage = "নম্বর সংরক্ষণ করা হয়েছে।";
            await LoadMarksAsync();
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task PublishAsync()
    {
        if (Selected is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _exams.PublishAsync(Selected.Id);
            await LoadAsync();
            StatusMessage = "ফলাফল প্রকাশ করা হয়েছে।";
        }, "প্রকাশ করা হচ্ছে…");
    }

    private async Task SaveGradeScaleAsync()
    {
        await RunAsync(async () =>
        {
            await _exams.SaveGradeScaleAsync(GradeScale.ToList());
            StatusMessage = "গ্রেড স্কেল সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}
