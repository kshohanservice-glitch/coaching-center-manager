using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Wpf.Views;

/// <summary>
/// Student capture and edit. The duplicate check is advisory: a guardian re-enrolling a second child
/// must not be blocked, so a possible match is shown and the operator decides.
/// </summary>
public partial class StudentEditorWindow : Window
{
    private readonly Student _student;

    public StudentEditorWindow()
        : this(new Student { Status = StudentStatus.Active, CreatedAtUtc = DateTime.UtcNow })
    {
    }

    public StudentEditorWindow(Student student)
    {
        InitializeComponent();
        _student = student;

        FullNameBox.Text = student.FullName;
        GuardianNameBox.Text = student.GuardianName ?? string.Empty;
        GuardianPhoneBox.Text = student.GuardianPhone ?? string.Empty;
        StudentPhoneBox.Text = student.StudentPhone ?? string.Empty;
        GenderBox.Text = student.Gender ?? string.Empty;
        DateOfBirthPicker.SelectedDate = student.DateOfBirth;
        StudentCodeBox.Text = student.StudentCode;
        StudentCodeBox.IsEnabled = student.Id == Guid.Empty;
        AddressBox.Text = student.Address ?? string.Empty;
        SchoolBox.Text = student.SchoolOrCollege ?? string.Empty;
        SchoolClassBox.Text = student.SchoolClass ?? string.Empty;
        NotesBox.Text = student.Notes ?? string.Empty;

        if (student.Id == Guid.Empty)
        {
            StudentCodeBox.Text = "স্বয়ংক্রিয়ভাবে তৈরি হবে";
            StudentCodeBox.IsEnabled = false;
        }

        FullNameBox.Focus();
    }

    public Student Student => _student;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (FullNameBox.Text.Trim().Length == 0)
        {
            ErrorText.Text = "শিক্ষার্থীর নাম দিন।";
            return;
        }

        _student.FullName = FullNameBox.Text.Trim();
        _student.GuardianName = Nullable(GuardianNameBox.Text);
        _student.GuardianPhone = Nullable(GuardianPhoneBox.Text);
        _student.StudentPhone = Nullable(StudentPhoneBox.Text);
        _student.Gender = GenderBox.Text.Trim().Length == 0 ? null : GenderBox.Text.Trim();
        _student.DateOfBirth = DateOfBirthPicker.SelectedDate;
        _student.Address = Nullable(AddressBox.Text);
        _student.SchoolOrCollege = Nullable(SchoolBox.Text);
        _student.SchoolClass = Nullable(SchoolClassBox.Text);
        _student.Notes = Nullable(NotesBox.Text);
        _student.UpdatedAtUtc = DateTime.UtcNow;

        if (_student.Id != Guid.Empty)
        {
            StudentCodeBox.Text = _student.StudentCode;
        }

        // A warning here prevents two records for the same child; it never silently overwrites.
        if (_student.Id == Guid.Empty)
        {
            try
            {
                IReadOnlyList<Student> duplicates = await App.Services!
                    .GetRequiredService<IStudentService>()
                    .FindDuplicatesAsync(_student.FullName, _student.GuardianPhone, _student.DateOfBirth);

                if (duplicates.Count > 0 && DuplicateText.Text.Length == 0)
                {
                    DuplicateText.Text = "সতর্কতা: এই নাম ও ফোনের সাথে মিল আছে "
                        + string.Join(", ", duplicates.Select(s => $"{s.StudentCode} {s.FullName}"))
                        + "। নতুন করে তৈরি করতে আবার 'সংরক্ষণ' চাপুন; ভুল হলে বাতিল করুন।";
                    return;
                }
            }
            catch (Exception)
            {
                // A duplicate check that cannot run must not block a legitimate admission.
            }
        }

        DialogResult = true;
        Close();
    }

    private static string? Nullable(string value)
    {
        string text = value.Trim();
        return text.Length == 0 ? null : text;
    }
}
