using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>One grid row. Values are rendered here, in Bengali, so the grid stays a plain table.</summary>
public sealed class StudentRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Guardian { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Admitted { get; init; } = string.Empty;
}

public sealed class LookupItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;

    public override string ToString() => Name;
}

public sealed class StudentsViewModel : ViewModelBase
{
    private readonly IStudentService _students;
    private readonly IAcademicService _academic;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public StudentsViewModel(IStudentService students, IAcademicService academic,
        IDialogService dialogs, AppState state)
    {
        _students = students;
        _academic = academic;
        _dialogs = dialogs;
        _state = state;

        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        NewCommand = new AsyncRelayCommand(_ => CreateAsync());
        EditCommand = new AsyncRelayCommand(_ => EditAsync(), _ => Selected is not null);
        ArchiveCommand = new AsyncRelayCommand(_ => ArchiveAsync(), _ => Selected is not null);
        ReactivateCommand = new AsyncRelayCommand(_ => ReactivateAsync(), _ => Selected is not null);
        ExportCommand = new RelayCommand(_ => Export());

        _ = LoadLookupsAsync();
        _ = SearchAsync();
    }

    public ObservableCollection<StudentRow> Rows { get; } = [];
    public ObservableCollection<LookupItem> Batches { get; } = [];

    private string? _search;
    public string? Search
    {
        get => _search;
        set => SetField(ref _search, value);
    }

    private LookupItem? _selectedBatch;
    public LookupItem? SelectedBatch
    {
        get => _selectedBatch;
        set => SetField(ref _selectedBatch, value);
    }

    private int _statusFilterIndex;
    public int StatusFilterIndex
    {
        get => _statusFilterIndex;
        set => SetField(ref _statusFilterIndex, value);
    }

    private StudentRow? _selected;
    public StudentRow? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                RaiseCommands();
            }
        }
    }

    private int _total;
    public string TotalText => "মোট: " + _state.Number(_total);

    public ICommand SearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand ReactivateCommand { get; }
    public ICommand ExportCommand { get; }

    private void RaiseCommands()
    {
        ((AsyncRelayCommand)EditCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ArchiveCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ReactivateCommand).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(TotalText));
    }

    private async Task LoadLookupsAsync()
    {
        await RunAsync(async () =>
        {
            Batches.Clear();
            Batches.Add(new LookupItem { Id = Guid.Empty, Name = "সব ব্যাচ" });

            foreach (Batch batch in await _academic.GetBatchesAsync(activeOnly: true))
            {
                Batches.Add(new LookupItem { Id = batch.Id, Name = batch.Name });
            }

            SelectedBatch = Batches[0];
        });
    }

    private async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            var query = new StudentQuery(
                Search: string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
                BatchId: SelectedBatch is not null && SelectedBatch.Id != Guid.Empty
                    ? SelectedBatch.Id
                    : null,
                Status: StatusFilterIndex switch
                {
                    1 => StudentStatus.Active,
                    2 => StudentStatus.Withdrawn,
                    3 => StudentStatus.Archived,
                    _ => null
                },
                Page: 1,
                PageSize: 500);

            PagedResult<Student> page = await _students.SearchAsync(query);

            Rows.Clear();
            foreach (Student student in page.Items)
            {
                Rows.Add(new StudentRow
                {
                    Id = student.Id,
                    Code = student.StudentCode,
                    Name = student.FullName,
                    Guardian = student.GuardianName ?? string.Empty,
                    Phone = student.GuardianPhone ?? student.StudentPhone ?? string.Empty,
                    Status = Translate(student.Status),
                    Admitted = Core.Digits.Format(student.CreatedAtUtc, _state.UseBengaliDigits)
                });
            }

            _total = page.TotalCount;
            RaiseCommands();
        }, "খোঁজা হচ্ছে…");
    }

    private static string Translate(StudentStatus status) => status switch
    {
        StudentStatus.Active => "সক্রিয়",
        StudentStatus.Inactive => "নিষ্ক্রিয়",
        StudentStatus.Archived => "সংরক্ষিত",
        _ => status.ToString()
    };

    private async Task CreateAsync()
    {
        var editor = new StudentEditorWindow();
        if (editor.ShowDialog() != true)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _students.CreateAsync(editor.Student);
            await SearchAsync();
            StatusMessage = "শিক্ষার্থী যোগ করা হয়েছে: " + editor.Student.StudentCode;
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task EditAsync()
    {
        if (Selected is null)
        {
            return;
        }

        Student? existing = await _students.GetAsync(Selected.Id);
        if (existing is null)
        {
            ErrorMessage = "শিক্ষার্থী খুঁজে পাওয়া যায়নি।";
            return;
        }

        var editor = new StudentEditorWindow(existing);
        if (editor.ShowDialog() != true)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _students.UpdateAsync(editor.Student);
            await SearchAsync();
            StatusMessage = "তথ্য হালনাগাদ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task ArchiveAsync()
    {
        if (Selected is null || !_dialogs.Confirm(
                $"{Selected.Name}-কে সংরক্ষিত (archived) করা হবে। এটি মুছে ফেলা নয়; পরে পুনরায় সক্রিয় করা যাবে।",
                "নিশ্চিত করুন"))
        {
            return;
        }

        await RunAsync(async () => { await _students.ArchiveAsync(Selected.Id); await SearchAsync(); });
    }

    private async Task ReactivateAsync()
    {
        if (Selected is null)
        {
            return;
        }

        await RunAsync(async () => { await _students.ReactivateAsync(Selected.Id); await SearchAsync(); });
    }

    private void Export()
    {
        string? path = _dialogs.SaveFile("shikkharthi.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("কোড", r => r.Code),
            ("নাম", r => r.Name),
            ("অভিভাবক", r => r.Guardian),
            ("ফোন", r => r.Phone),
            ("অবস্থা", r => r.Status),
            ("ভর্তি", r => r.Admitted)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}
