using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using ShikkhaDesk.Infrastructure;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class AuditRow
{
    public string When { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Entity { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

/// <summary>Audit log. Read-only and append-only: entries can never be edited or removed from here.</summary>
public sealed class AuditViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly AppState _state;

    public AuditViewModel(IUserService users, AppState state)
    {
        _users = users;
        _state = state;
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        ExportCommand = new RelayCommand(_ => Export());

        From = DateTime.Today.AddDays(-30);
        To = DateTime.Today;
        _ = SearchAsync();
    }

    public ObservableCollection<AuditRow> Rows { get; } = [];

    public string? Search { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    private string _totalText = string.Empty;
    public string TotalText
    {
        get => _totalText;
        set => SetField(ref _totalText, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task SearchAsync()
    {
        await RunAsync(async () =>
        {
            PagedResult<AuditLog> page = await _users.GetAuditLogsAsync(
                string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
                new DateRange(From.Date, To.Date),
                page: 1,
                pageSize: 500);

            Rows.Clear();
            foreach (AuditLog log in page.Items)
            {
                Rows.Add(new AuditRow
                {
                    When = Core.Digits.Format(log.OccurredAtUtc, _state.UseBengaliDigits, "dd-MM-yyyy HH:mm"),
                    User = log.UserName ?? "(সিস্টেম)",
                    Action = log.Action,
                    Entity = log.EntityType ?? "—",
                    Summary = log.Summary ?? string.Empty
                });
            }

            TotalText = "মোট এন্ট্রি: " + _state.Number(page.TotalCount);
        }, "নিরীক্ষা লগ আনা হচ্ছে…");
    }

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("nirokha.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Rows,
            ("সময়", r => r.When),
            ("ব্যবহারকারী", r => r.User),
            ("কাজ", r => r.Action),
            ("বিষয়", r => r.Entity),
            ("বিবরণ", r => r.Summary)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}

public sealed class BackupRowItem
{
    public string FileName { get; init; } = string.Empty;
    public string Created { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
}

/// <summary>
/// Backup and restore. Backups use SQLite's online backup API so the database does not have to be
/// closed, and a restore is only accepted after the candidate file passes an integrity check.
/// </summary>
public sealed class BackupViewModel : ViewModelBase
{
    private readonly IBackupService _backups;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;
    private readonly ShikkhaDeskEnvironment _environment;

    public BackupViewModel(IBackupService backups, IDialogService dialogs, AppState state,
        ShikkhaDeskEnvironment environment)
    {
        _backups = backups;
        _environment = environment;
        _dialogs = dialogs;
        _state = state;

        CreateCommand = new AsyncRelayCommand(_ => CreateAsync());
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        RestoreCommand = new AsyncRelayCommand(_ => RestoreAsync(), () => Selected is not null);
        VerifyCommand = new AsyncRelayCommand(_ => VerifyAsync(), () => Selected is not null);

        _ = LoadAsync();
    }

    public ObservableCollection<BackupRowItem> Rows { get; } = [];

    private BackupRowItem? _selected;
    public BackupRowItem? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                ((AsyncRelayCommand)RestoreCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)VerifyCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private string _locationText = string.Empty;
    public string LocationText
    {
        get => _locationText;
        set => SetField(ref _locationText, value);
    }

    public ICommand CreateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand VerifyCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Rows.Clear();
            foreach (BackupRecord record in await _backups.GetHistoryAsync(100))
            {
                Rows.Add(new BackupRowItem
                {
                    FileName = Path.GetFileName(record.FilePath),
                    Created = Core.Digits.Format(record.StartedAtUtc, _state.UseBengaliDigits, "dd-MM-yyyy HH:mm"),
                    Size = $"{record.FileSizeBytes / 1024.0 / 1024.0:0.00} MB",
                    FullPath = record.FilePath,
                    Status = record.Status == BackupStatus.Completed ? "সম্পন্ন" : record.Status.ToString()
                });
            }

            LocationText = "ব্যাকআপ স্থান: " + _environment.BackupDirectory;
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task CreateAsync()
    {
        await RunAsync(async () =>
        {
            BackupRecord record = await _backups.CreateBackupAsync();
            await LoadAsync();
            StatusMessage = "ব্যাকআপ তৈরি: " + Path.GetFileName(record.FilePath);
        }, "ব্যাকআপ তৈরি করা হচ্ছে…");
    }

    private async Task VerifyAsync()
    {
        if (Selected is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            bool ok = await _backups.VerifyBackupAsync(Selected.FullPath);
            StatusMessage = ok
                ? Path.GetFileName(Selected.FullPath) + " — অখণ্ডতা যাচাই সফল।"
                : Path.GetFileName(Selected.FullPath) + " — ফাইলটি ক্ষতিগ্রস্ত বা পঠনযোগ্য নয়।";
        }, "যাচাই করা হচ্ছে…");
    }

    private async Task RestoreAsync()
    {
        if (Selected is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                "সতর্কতা: বর্তমান ডেটা সম্পূর্ণরূপে '" + Selected.FileName + "' দ্বারা প্রতিস্থাপিত হবে।\n\n"
                + "চালিয়ে যাওয়ার আগে একটি নতুন ব্যাকআপ নেওয়া হবে। অ্যাপ্লিকেশন পুনরায় চালু করতে হবে।\n\n"
                + "চালিয়ে যাবেন?", "ব্যাকআপ পুনরুদ্ধার"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            RestoreResult result = await _backups.RestoreAsync(Selected.FullPath);
            StatusMessage = result.Succeeded
                ? "পুনরুদ্ধার সম্পন্ন। অনুগ্রহ করে শিক্ষা ডেস্ক পুনরায় চালু করুন।"
                : "পুনরুদ্ধার ব্যর্থ: " + result.Message;
        }, "পুনরুদ্ধার করা হচ্ছে…");
    }
}

public sealed class DeviceRow
{
    public string DeviceId { get; init; } = string.Empty;
    public string Registered { get; init; } = string.Empty;
    public string LastSeen { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Licence and device management. Verification is entirely offline against the embedded vendor public
/// key; no internet connection is used or required, and an expired licence never locks data away.
/// </summary>
public sealed class LicenseViewModel : ViewModelBase
{
    private readonly ILicenseService _licenses;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public LicenseViewModel(ILicenseService licenses, IDialogService dialogs, AppState state)
    {
        _licenses = licenses;
        _dialogs = dialogs;
        _state = state;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ActivateCommand = new AsyncRelayCommand(_ => ActivateAsync());
        RegisterDeviceCommand = new AsyncRelayCommand(_ => RegisterDeviceAsync());
        RevokeDeviceCommand = new AsyncRelayCommand(_ => RevokeDeviceAsync(), () => SelectedDevice is not null);
        ReplaceDeviceCommand = new AsyncRelayCommand(_ => ReplaceDeviceAsync(), () => SelectedDevice is not null);

        _ = LoadAsync();
    }

    public ObservableCollection<DeviceRow> Devices { get; } = [];

    public string LicenseKey { get; set; } = string.Empty;

    private DeviceRow? _selectedDevice;
    public DeviceRow? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetField(ref _selectedDevice, value))
            {
                ((AsyncRelayCommand)RevokeDeviceCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ReplaceDeviceCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ActivateCommand { get; }
    public ICommand RegisterDeviceCommand { get; }
    public ICommand RevokeDeviceCommand { get; }
    public ICommand ReplaceDeviceCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            LicenseStatusInfo status = await _licenses.GetStatusAsync();

            StatusText = string.Join(Environment.NewLine,
                "অবস্থা: " + Translate(status.Status),
                status.Message,
                $"ডিভাইস: {_state.Number(status.ActiveDeviceCount)} / {_state.Number(status.DeviceLimit)}",
                status.CurrentDeviceRegistered
                    ? "এই কম্পিউটার নিবন্ধিত।"
                    : "এই কম্পিউটারটি এখনো নিবন্ধিত নয় — 'ডিভাইস নিবন্ধন' চাপুন।",
                status.ExpiresAtUtc is null
                    ? "মেয়াদ: সীমাহীন"
                    : "মেয়াদ শেষ: " + Core.Digits.Format(status.ExpiresAtUtc.Value, _state.UseBengaliDigits),
                "বর্তমান ডিভাইস আইডি: " + (status.CurrentDeviceId ?? "—"));

            Devices.Clear();
            foreach (LicensedDevice device in await _licenses.GetDevicesAsync())
            {
                Devices.Add(new DeviceRow
                {
                    DeviceId = device.DeviceId,
                    Registered = Core.Digits.Format(device.RegisteredAtUtc, _state.UseBengaliDigits),
                    LastSeen = device.LastSeenAtUtc is null
                        ? "—"
                        : Core.Digits.Format(device.LastSeenAtUtc.Value, _state.UseBengaliDigits),
                    Status = device.Status.ToString()
                });
            }
        }, "লাইসেন্স যাচাই করা হচ্ছে…");
    }

    private static string Translate(LicenseStatus status) => status switch
    {
        LicenseStatus.Valid => "বৈধ ও সক্রিয়",
        LicenseStatus.Expired => "মেয়াদ শেষ",
        LicenseStatus.DeviceLimitReached => "ডিভাইস সীমা অতিক্রম",
        LicenseStatus.InvalidSignature => "স্বাক্ষর যাচাই ব্যর্থ",
        LicenseStatus.InstitutionMismatch => "প্রতিষ্ঠানের সাথে মেলেনি",
        LicenseStatus.NotActivated => "নিবন্ধিত নয়",
        _ => status.ToString()
    };

    private async Task ActivateAsync()
    {
        await RunAsync(async () =>
        {
            if (LicenseKey.Trim().Length == 0)
            {
                throw new ValidationException("লাইসেন্স কী পেস্ট করুন।");
            }

            LicensePayload payload = LicensePayload.Parse(LicenseKey.Trim());
            await _licenses.ActivateAsync(payload);
            LicenseKey = string.Empty;
            OnPropertyChanged(nameof(LicenseKey));
            await LoadAsync();
            StatusMessage = "লাইসেন্স সক্রিয় করা হয়েছে।";
        }, "যাচাই করা হচ্ছে…");
    }

    private async Task RegisterDeviceAsync()
    {
        await RunAsync(async () =>
        {
            bool registered = await _licenses.RegisterCurrentDeviceAsync();
            await LoadAsync();
            StatusMessage = registered
                ? "এই কম্পিউটার নিবন্ধন করা হয়েছে।"
                : "ডিভাইস সীমা পূর্ণ — পুরনো ডিভাইস সরিয়ে নিন বা প্রতিস্থাপন করুন।";
        }, "নিবন্ধন করা হচ্ছে…");
    }

    private async Task RevokeDeviceAsync()
    {
        if (SelectedDevice is null
            || !_dialogs.Confirm($"{SelectedDevice.DeviceId} ডিভাইসটির নিবন্ধন সরানো হবে।", "নিশ্চিত করুন"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _licenses.RevokeDeviceAsync(SelectedDevice.DeviceId);
            await LoadAsync();
            StatusMessage = "ডিভাইস সরানো হয়েছে।";
        });
    }

    private async Task ReplaceDeviceAsync()
    {
        if (SelectedDevice is null
            || !_dialogs.Confirm($"{SelectedDevice.DeviceId}-এর জায়গায় এই কম্পিউটারটি বসানো হবে।",
                "নিশ্চিত করুন"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            bool replaced = await _licenses.ReplaceDeviceAsync(SelectedDevice.DeviceId);
            await LoadAsync();
            StatusMessage = replaced ? "ডিভাইস প্রতিস্থাপন করা হয়েছে।" : "প্রতিস্থাপন করা যায়নি।";
        }, "প্রতিস্থাপন করা হচ্ছে…");
    }
}
