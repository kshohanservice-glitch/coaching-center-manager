using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>
/// New admission. Runs the real <see cref="IStudentService.AdmitAsync"/>: student, enrollment,
/// optional admission fee charge and the duplicate check, all in one transaction on the server.
/// </summary>
public sealed class AdmissionsViewModel : ViewModelBase
{
    private readonly IStudentService _students;
    private readonly IAcademicService _academic;
    private readonly IFeeService _fees;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public AdmissionsViewModel(IStudentService students, IAcademicService academic, IFeeService fees,
        IDialogService dialogs, AppState state)
    {
        _students = students;
        _academic = academic;
        _fees = fees;
        _dialogs = dialogs;
        _state = state;

        AdmitCommand = new AsyncRelayCommand(_ => AdmitAsync());
        ClearCommand = new RelayCommand(_ => Clear());
        _ = LoadLookupsAsync();
    }

    public ObservableCollection<LookupItem> Sessions { get; } = [];
    public ObservableCollection<LookupItem> Batches { get; } = [];
    public ObservableCollection<LookupItem> Courses { get; } = [];
    public ObservableCollection<LookupItem> ClassGrades { get; } = [];
    public ObservableCollection<LookupItem> FeePlans { get; } = [];

    public string FullName { get; set; } = string.Empty;
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public string StudentPhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string SchoolOrCollege { get; set; } = string.Empty;
    public string SchoolClass { get; set; } = string.Empty;
    public string AdmissionFeeText { get; set; } = "0";
    public string Notes { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }
    public DateTime AdmissionDate { get; set; } = DateTime.Today;

    public LookupItem? Session { get; set; }
    public LookupItem? Batch { get; set; }
    public LookupItem? Course { get; set; }
    public LookupItem? ClassGrade { get; set; }
    public LookupItem? FeePlan { get; set; }

    private string? _resultText;
    public string? ResultText
    {
        get => _resultText;
        set => SetField(ref _resultText, value);
    }

    public ICommand AdmitCommand { get; }
    public ICommand ClearCommand { get; }

    private async Task LoadLookupsAsync()
    {
        await RunAsync(async () =>
        {
            await FillAsync(Sessions, (await _academic.GetSessionsAsync()).Select(s =>
                new LookupItem { Id = s.Id, Name = s.Name + (s.IsActive ? " (চলতি)" : string.Empty) }));

            await FillAsync(Batches, (await _academic.GetBatchesAsync(activeOnly: true))
                .Select(b => new LookupItem { Id = b.Id, Name = b.Name }));

            await FillAsync(Courses, (await _academic.GetCoursesAsync())
                .Select(c => new LookupItem { Id = c.Id, Name = c.Name }));

            await FillAsync(ClassGrades, (await _academic.GetClassGradesAsync())
                .Select(g => new LookupItem { Id = g.Id, Name = g.Name }));

            await FillAsync(FeePlans, (await _fees.GetFeePlansAsync())
                .Select(p => new LookupItem { Id = p.Id, Name = p.Name }));

            Session = Sessions.FirstOrDefault();
            Batch = Batches.FirstOrDefault();
        }, "তালিকা আনা হচ্ছে…");
    }

    private static Task FillAsync(ObservableCollection<LookupItem> target, IEnumerable<LookupItem> items)
    {
        target.Clear();
        target.Add(new LookupItem { Id = Guid.Empty, Name = "— নির্বাচন করুন —" });
        foreach (LookupItem item in items)
        {
            target.Add(item);
        }

        return Task.CompletedTask;
    }

    private async Task AdmitAsync()
    {
        await RunAsync(async () =>
        {
            if (FullName.Trim().Length == 0)
            {
                throw new ValidationException("শিক্ষার্থীর নাম দিন।");
            }

            if (Session is null || Session.Id == Guid.Empty)
            {
                throw new ValidationException("শিক্ষাবর্ষ নির্বাচন করুন।");
            }

            if (!decimal.TryParse(AdmissionFeeText.Trim(), out decimal admissionFee) || admissionFee < 0)
            {
                throw new ValidationException("ভর্তি ফি সঠিকভাবে লিখুন।");
            }

            var request = new AdmissionRequest(
                FullName: FullName.Trim(),
                GuardianName: Text(GuardianName),
                GuardianPhone: Text(GuardianPhone),
                StudentPhone: Text(StudentPhone),
                Address: Text(Address),
                DateOfBirth: DateOfBirth,
                Gender: Text(Gender),
                SchoolOrCollege: Text(SchoolOrCollege),
                SchoolClass: Text(SchoolClass),
                SessionId: Session.Id,
                BatchId: Batch is { Id: var batchId } && batchId != Guid.Empty ? batchId : null,
                CourseId: Course is { Id: var courseId } && courseId != Guid.Empty ? courseId : null,
                ClassGradeId: ClassGrade is { Id: var gradeId } && gradeId != Guid.Empty ? gradeId : null,
                AdmissionFee: Money.FromDecimal(admissionFee),
                FeePlanId: FeePlan is { Id: var planId } && planId != Guid.Empty ? planId : null,
                AdmissionDate: AdmissionDate,
                Notes: Text(Notes));

            AdmissionResult result = await _students.AdmitAsync(request);

            string duplicates = result.PossibleDuplicates.Count == 0
                ? string.Empty
                : "\n\nসতর্কতা: একই নাম/ফোনের সাথে মিল আছে — "
                  + string.Join(", ", result.PossibleDuplicates.Select(s => $"{s.StudentCode} {s.FullName}"));

            ResultText = $"ভর্তি সম্পন্ন: {result.AdmissionNumber}"
                         + (result.AdmissionChargeId is null
                             ? string.Empty
                             : "\nভর্তি ফি ধার্য করা হয়েছে: " + _state.Money(Money.FromDecimal(admissionFee)))
                         + duplicates;

            StatusMessage = "ভর্তি সফল হয়েছে।";
            Clear();
        }, "ভর্তি করা হচ্ছে…");
    }

    private void Clear()
    {
        FullName = GuardianName = GuardianPhone = StudentPhone = Address = Gender = string.Empty;
        SchoolOrCollege = SchoolClass = Notes = string.Empty;
        AdmissionFeeText = "0";
        DateOfBirth = null;
        AdmissionDate = DateTime.Today;
        ResultText = null;
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(GuardianName));
        OnPropertyChanged(nameof(GuardianPhone));
        OnPropertyChanged(nameof(StudentPhone));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(Gender));
        OnPropertyChanged(nameof(SchoolOrCollege));
        OnPropertyChanged(nameof(SchoolClass));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(AdmissionFeeText));
        OnPropertyChanged(nameof(DateOfBirth));
        OnPropertyChanged(nameof(AdmissionDate));
    }

    private static string? Text(string value)
    {
        string text = value.Trim();
        return text.Length == 0 ? null : text;
    }
}
