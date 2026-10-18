using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class StaffRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Designation { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Gross { get; init; } = string.Empty;
    public string Net { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class StaffViewModel : ViewModelBase
{
    private readonly IAcademicService _academic;
    private readonly AppState _state;

    public StaffViewModel(IAcademicService academic, AppState state)
    {
        _academic = academic;
        _state = state;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        ClearCommand = new RelayCommand(_ => Clear());
        _ = LoadAsync();
    }

    public ObservableCollection<StaffRow> Rows { get; } = [];

    private StaffRow? _selected;
    public StaffRow? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public string FullName { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;

    public DateTime JoiningDate { get; set; } = DateTime.Today;

    public string BasicSalary { get; set; } = "0";
    public string HouseRent { get; set; } = "0";
    public string Medical { get; set; } = "0";
    public string Transport { get; set; } = "0";
    public string OtherAllowance { get; set; } = "0";
    public string ProvidentFund { get; set; } = "0";
    public string Tax { get; set; } = "0";
    public string OtherDeduction { get; set; } = "0";

    public ICommand SaveCommand { get; }
    public ICommand ClearCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Rows.Clear();
            foreach (Staff staff in await _academic.GetStaffAsync())
            {
                Rows.Add(new StaffRow
                {
                    Id = staff.Id,
                    Code = staff.StaffCode,
                    Name = staff.FullName,
                    Designation = staff.Designation ?? "—",
                    Phone = staff.Phone ?? "—",
                    Gross = _state.Money(staff.GrossSalary),
                    Net = _state.Money(staff.NetSalary),
                    Status = staff.Status == StaffStatus.Active ? "কর্মরত" : "বিদায়ী"
                });
            }
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (FullName.Trim().Length == 0)
            {
                throw new ValidationException("নাম দিন।");
            }

            var staff = new Staff
            {
                FullName = FullName.Trim(),
                Designation = Nullable(Designation),
                Phone = Nullable(Phone),
                Email = Nullable(Email),
                Address = Nullable(Address),
                JoiningDate = JoiningDate.Date,
                Status = StaffStatus.Active,
                BasicSalary = Parse(BasicSalary),
                HouseRentAllowance = Parse(HouseRent),
                MedicalAllowance = Parse(Medical),
                TransportAllowance = Parse(Transport),
                OtherAllowance = Parse(OtherAllowance),
                ProvidentFundDeduction = Parse(ProvidentFund),
                TaxDeduction = Parse(Tax),
                OtherDeduction = Parse(OtherDeduction),
                BankAccountName = Nullable(BankAccountName),
                BankAccountNumber = Nullable(BankAccountNumber)
            };

            await _academic.SaveStaffAsync(staff, Array.Empty<StaffAssignment>());
            Clear();
            await LoadAsync();
            StatusMessage = "কর্মচারী সংরক্ষণ করা হয়েছে: " + staff.StaffCode;
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private void Clear()
    {
        FullName = Designation = Phone = Email = Address = string.Empty;
        BankAccountName = BankAccountNumber = string.Empty;
        BasicSalary = HouseRent = Medical = Transport = OtherAllowance = "0";
        ProvidentFund = Tax = OtherDeduction = "0";
        JoiningDate = DateTime.Today;

        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(Designation));
        OnPropertyChanged(nameof(Phone));
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(BankAccountName));
        OnPropertyChanged(nameof(BankAccountNumber));
        OnPropertyChanged(nameof(BasicSalary));
        OnPropertyChanged(nameof(HouseRent));
        OnPropertyChanged(nameof(Medical));
        OnPropertyChanged(nameof(Transport));
        OnPropertyChanged(nameof(OtherAllowance));
        OnPropertyChanged(nameof(ProvidentFund));
        OnPropertyChanged(nameof(Tax));
        OnPropertyChanged(nameof(OtherDeduction));
        OnPropertyChanged(nameof(JoiningDate));
    }

    private static Money Parse(string text) =>
        Money.FromDecimal(decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture,
            out decimal value) || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture,
            out value)
            ? value
            : 0m);

    private static string? Nullable(string value)
    {
        string text = value.Trim();
        return text.Length == 0 ? null : text;
    }
}
