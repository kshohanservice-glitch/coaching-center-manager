using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>
/// Sessions, classes, courses and subjects. This is the reference data every other module points at,
/// so records are added here once and then only deactivated, never deleted, while they are in use.
/// </summary>
public sealed class AcademicsViewModel : ViewModelBase
{
    private readonly IAcademicService _academic;
    private readonly AppState _state;

    public AcademicsViewModel(IAcademicService academic, AppState state)
    {
        _academic = academic;
        _state = state;

        AddSessionCommand = new AsyncRelayCommand(_ => AddSessionAsync());
        AddClassCommand = new AsyncRelayCommand(_ => AddClassAsync());
        AddCourseCommand = new AsyncRelayCommand(_ => AddCourseAsync());
        AddSubjectCommand = new AsyncRelayCommand(_ => AddSubjectAsync());

        _ = LoadAsync();
    }

    public ObservableCollection<AcademicSession> Sessions { get; } = [];
    public ObservableCollection<ClassGrade> Classes { get; } = [];
    public ObservableCollection<Course> Courses { get; } = [];
    public ObservableCollection<Subject> Subjects { get; } = [];

    public string SessionName { get; set; } = string.Empty;
    public DateTime SessionStart { get; set; } = new(DateTime.Now.Year, 1, 1);
    public DateTime SessionEnd { get; set; } = new(DateTime.Now.Year, 12, 31);
    public bool SessionIsActive { get; set; }

    public string ClassName { get; set; } = string.Empty;
    public int ClassSortOrder { get; set; }

    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int CourseDurationMonths { get; set; } = 12;

    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;

    public ICommand AddSessionCommand { get; }
    public ICommand AddClassCommand { get; }
    public ICommand AddCourseCommand { get; }
    public ICommand AddSubjectCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            await Fill(Sessions, await _academic.GetSessionsAsync());
            await Fill(Classes, await _academic.GetClassGradesAsync());
            await Fill(Courses, await _academic.GetCoursesAsync());
            await Fill(Subjects, await _academic.GetSubjectsAsync());
        }, "তালিকা আনা হচ্ছে…");
    }

    private static Task Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (T item in items)
        {
            target.Add(item);
        }

        return Task.CompletedTask;
    }

    private async Task AddSessionAsync()
    {
        await RunAsync(async () =>
        {
            if (SessionName.Trim().Length == 0)
            {
                throw new ValidationException("শিক্ষাবর্ষের নাম দিন।");
            }

            if (SessionEnd <= SessionStart)
            {
                throw new ValidationException("শেষ তারিখ শুরুর পরে হতে হবে।");
            }

            await _academic.CreateSessionAsync(new AcademicSession
            {
                Name = SessionName.Trim(),
                StartDate = SessionStart.Date,
                EndDate = SessionEnd.Date,
                IsActive = SessionIsActive
            });

            SessionName = string.Empty;
            OnPropertyChanged(nameof(SessionName));
            await LoadAsync();
            StatusMessage = "শিক্ষাবর্ষ যোগ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task AddClassAsync()
    {
        await RunAsync(async () =>
        {
            if (ClassName.Trim().Length == 0)
            {
                throw new ValidationException("ক্লাসের নাম দিন।");
            }

            await _academic.SaveClassGradeAsync(new ClassGrade
            {
                Name = ClassName.Trim(),
                SortOrder = ClassSortOrder,
                IsActive = true
            });

            ClassName = string.Empty;
            OnPropertyChanged(nameof(ClassName));
            await LoadAsync();
            StatusMessage = "ক্লাস যোগ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task AddCourseAsync()
    {
        await RunAsync(async () =>
        {
            if (CourseName.Trim().Length == 0)
            {
                throw new ValidationException("কোর্সের নাম দিন।");
            }

            await _academic.SaveCourseAsync(new Course
            {
                Name = CourseName.Trim(),
                Code = CourseCode.Trim().Length == 0 ? null : CourseCode.Trim(),
                DurationMonths = CourseDurationMonths,
                IsActive = true
            });

            CourseName = CourseCode = string.Empty;
            OnPropertyChanged(nameof(CourseName));
            OnPropertyChanged(nameof(CourseCode));
            await LoadAsync();
            StatusMessage = "কোর্স যোগ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task AddSubjectAsync()
    {
        await RunAsync(async () =>
        {
            if (SubjectName.Trim().Length == 0)
            {
                throw new ValidationException("বিষয়ের নাম দিন।");
            }

            await _academic.CreateSubjectAsync(new Subject
            {
                Name = SubjectName.Trim(),
                Code = SubjectCode.Trim().Length == 0 ? null : SubjectCode.Trim(),
                IsActive = true
            });

            SubjectName = SubjectCode = string.Empty;
            OnPropertyChanged(nameof(SubjectName));
            OnPropertyChanged(nameof(SubjectCode));
            await LoadAsync();
            StatusMessage = "বিষয় যোগ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}
