using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using ShikkhaDesk.Infrastructure;
using ShikkhaDesk.Lan.Client;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>Institution profile. Everything printed on receipts and reports comes from here.</summary>
public sealed class InstitutionViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly AppState _state;

    public InstitutionViewModel(ISettingsService settings, AppState state)
    {
        _settings = settings;
        _state = state;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        _ = LoadAsync();
    }

    public string Name { get; set; } = string.Empty;
    public string NameBn { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = "৳";
    public string ReceiptPrefix { get; set; } = "SD";
    public int DigitIndex { get; set; }
    public DateTime? EstablishedOn { get; set; }

    public ICommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Institution? institution = await _settings.GetInstitutionAsync();
            if (institution is null)
            {
                return;
            }

            Name = institution.Name;
            NameBn = institution.NameBn ?? string.Empty;
            Address = institution.Address ?? string.Empty;
            Phone = institution.Phone ?? string.Empty;
            Email = institution.Email ?? string.Empty;
            CurrencySymbol = institution.CurrencySymbol;
            ReceiptPrefix = institution.ReceiptPrefix;
            DigitIndex = institution.DigitPreference == DigitPreference.English ? 1 : 0;
            EstablishedOn = institution.EstablishedOn;

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(NameBn));
            OnPropertyChanged(nameof(Address));
            OnPropertyChanged(nameof(Phone));
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(CurrencySymbol));
            OnPropertyChanged(nameof(ReceiptPrefix));
            OnPropertyChanged(nameof(DigitIndex));
            OnPropertyChanged(nameof(EstablishedOn));
        }, "প্রোফাইল আনা হচ্ছে…");
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (Name.Trim().Length == 0)
            {
                throw new ValidationException("প্রতিষ্ঠানের নাম দিন।");
            }

            Institution? existing = await _settings.GetInstitutionAsync();
            var institution = existing ?? new Institution();

            institution.Name = Name.Trim();
            institution.NameBn = NameBn.Trim().Length == 0 ? null : NameBn.Trim();
            institution.Address = Address.Trim().Length == 0 ? null : Address.Trim();
            institution.Phone = Phone.Trim().Length == 0 ? null : Phone.Trim();
            institution.Email = Email.Trim().Length == 0 ? null : Email.Trim();
            institution.CurrencySymbol = string.IsNullOrWhiteSpace(CurrencySymbol) ? "৳" : CurrencySymbol.Trim();
            institution.ReceiptPrefix = string.IsNullOrWhiteSpace(ReceiptPrefix) ? "SD" : ReceiptPrefix.Trim();
            institution.DigitPreference = DigitIndex == 1 ? DigitPreference.English : DigitPreference.Bengali;
            institution.EstablishedOn = EstablishedOn;

            await _settings.SaveInstitutionAsync(institution);
            _state.Begin(_state.CurrentUser, institution);
            StatusMessage = "প্রোফাইল সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}

/// <summary>General settings stored as key/value settings, plus the receipt configuration.</summary>
public sealed class GeneralSettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ShikkhaDeskEnvironment _environment;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public GeneralSettingsViewModel(ISettingsService settings, ShikkhaDeskEnvironment environment,
        IDialogService dialogs, AppState state)
    {
        _settings = settings;
        _environment = environment;
        _dialogs = dialogs;
        _state = state;

        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        BrowseBackupCommand = new RelayCommand(_ => BrowseBackup());
        _ = LoadAsync();
    }

    public string BackupLocation { get; set; } = string.Empty;
    public string ReceiptPrefix { get; set; } = "SD";
    public string ReceiptFooter { get; set; } = string.Empty;
    public bool ShowLogoOnReceipt { get; set; } = true;
    public int AutoBackupDays { get; set; } = 7;
    public string LateFeeAmount { get; set; } = "0";
    public int LateFeeGraceDays { get; set; } = 5;

    public ICommand SaveCommand { get; }
    public ICommand BrowseBackupCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            BackupLocation = await _settings.GetAsync("backup.location", _environment.BackupDirectory);
            ReceiptPrefix = await _settings.GetAsync("receipt.prefix", "SD");
            ReceiptFooter = await _settings.GetAsync("receipt.footer", "ধন্যবাদ।");
            ShowLogoOnReceipt = await _settings.GetBoolAsync("receipt.showLogo", true);
            AutoBackupDays = await _settings.GetIntAsync("backup.autoEveryDays", 7);
            LateFeeGraceDays = await _settings.GetIntAsync("fees.graceDays", 5);
            LateFeeAmount = await _settings.GetAsync("fees.lateFeeAmount", "0");

            OnPropertyChanged(nameof(BackupLocation));
            OnPropertyChanged(nameof(ReceiptPrefix));
            OnPropertyChanged(nameof(ReceiptFooter));
            OnPropertyChanged(nameof(ShowLogoOnReceipt));
            OnPropertyChanged(nameof(AutoBackupDays));
            OnPropertyChanged(nameof(LateFeeGraceDays));
            OnPropertyChanged(nameof(LateFeeAmount));
        }, "সেটিংস আনা হচ্ছে…");
    }

    private void BrowseBackup()
    {
        string? path = _dialogs.SaveFile("ব্যাকআপ স্থান", "ফোল্ডার|*.*");
        if (path is null)
        {
            return;
        }

        BackupLocation = Path.GetDirectoryName(path) ?? BackupLocation;
        OnPropertyChanged(nameof(BackupLocation));
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (!decimal.TryParse(LateFeeAmount.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture,
                    out decimal lateFee)
                && !decimal.TryParse(LateFeeAmount.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture,
                    out lateFee))
            {
                throw new ValidationException("বিলম্ব ফি সঠিকভাবে লিখুন।");
            }

            if (lateFee < 0)
            {
                throw new ValidationException("বিলম্ব ফি ঋণাত্মক হতে পারে না।");
            }

            await _settings.SetAsync("backup.location", BackupLocation.Trim());
            await _settings.SetAsync("receipt.prefix", ReceiptPrefix.Trim());
            await _settings.SetAsync("receipt.footer", ReceiptFooter.Trim());
            await _settings.SetAsync("receipt.showLogo", ShowLogoOnReceipt ? "true" : "false");
            await _settings.SetAsync("backup.autoEveryDays", AutoBackupDays.ToString(CultureInfo.InvariantCulture));
            await _settings.SetAsync("fees.graceDays", LateFeeGraceDays.ToString(CultureInfo.InvariantCulture));
            await _settings.SetAsync("fees.lateFeeAmount",
                lateFee.ToString("0.##", CultureInfo.InvariantCulture));

            StatusMessage = "সেটিংস সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}

/// <summary>
/// Network settings. In host mode the desktop starts the LAN host process; in client mode it points
/// at a host. All application logic still runs on the machine that owns the database.
/// </summary>
public sealed class LanSettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IDeviceFingerprint _device;
    private readonly AppState _state;

    public LanSettingsViewModel(ISettingsService settings, IDeviceFingerprint device, AppState state)
    {
        _settings = settings;
        _device = device;
        _state = state;

        StartHostCommand = new AsyncRelayCommand(_ => StartHostAsync());
        TestCommand = new AsyncRelayCommand(_ => TestAsync());
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        _ = LoadAsync();
    }

    public ObservableCollection<string> Addresses { get; } = [];

    public int ModeIndex { get; set; }
    public string HostAddress { get; set; } = string.Empty;
    public string PortText { get; set; } = "8787";

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string DeviceId => _device.DeviceId;

    public ICommand StartHostCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            string mode = await _settings.GetAsync("host.mode", "Single");
            ModeIndex = mode switch
            {
                "LanHost" => 1,
                "Client" => 2,
                _ => 0
            };

            HostAddress = await _settings.GetAsync("lan.hostAddress", string.Empty);
            PortText = await _settings.GetAsync("lan.port", "8787");

            Addresses.Clear();
            foreach (string address in LocalAddresses())
            {
                Addresses.Add(address);
            }

            OnPropertyChanged(nameof(ModeIndex));
            OnPropertyChanged(nameof(HostAddress));
            OnPropertyChanged(nameof(PortText));
            OnPropertyChanged(nameof(Addresses));
            StatusText = "এই কম্পিউটারের ঠিকানা: " + string.Join(", ", Addresses);
        }, "সেটিংস আনা হচ্ছে…");
    }

    internal static IEnumerable<string> LocalAddresses()
    {
        foreach (System.Net.NetworkInformation.NetworkInterface nic in
                 System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
            {
                continue;
            }

            foreach (System.Net.NetworkInformation.UnicastIPAddressInformation info in
                     nic.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(info.Address))
                {
                    yield return info.Address.ToString();
                }
            }
        }
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (!int.TryParse(PortText.Trim(), out int port) || port is < 1 or > 65535)
            {
                throw new ValidationException("পোর্ট ১ থেকে ৬৫৫৩৫-এর মধ্যে হতে হবে।");
            }

            string mode = ModeIndex switch
            {
                1 => "LanHost",
                2 => "Client",
                _ => "Single"
            };

            await _settings.SetAsync("host.mode", mode);
            await _settings.SetAsync("lan.hostAddress", HostAddress.Trim());
            await _settings.SetAsync("lan.port", port.ToString(CultureInfo.InvariantCulture));

            StatusMessage = "নেটওয়ার্ক সেটিংস সংরক্ষণ করা হয়েছে। পরিবর্তন কার্যকর করতে অ্যাপটি পুনরায় চালু করুন।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task StartHostAsync()
    {
        await RunAsync(async () =>
        {
            string directory = AppContext.BaseDirectory;
            string candidate = Path.Combine(directory, "ShikkhaDesk.Lan.Host.exe");

            if (!File.Exists(candidate))
            {
                throw new NotFoundException(
                    "হোস্ট সার্ভিস (ShikkhaDesk.Lan.Host.exe) এই ইনস্টলেশনে পাওয়া যায়নি। "
                    + "ইনস্টলারের 'LAN Host' উপাংশটি নির্বাচন করে পুনরায় ইনস্টল করুন।");
            }

            if (!int.TryParse(PortText.Trim(), out int port))
            {
                port = 8787;
            }

            var start = new System.Diagnostics.ProcessStartInfo(candidate)
            {
                UseShellExecute = true,
                Arguments = $"--console --port {port}"
            };

            System.Diagnostics.Process.Start(start);
            StatusMessage = $"হোস্ট চালু করা হয়েছে (পোর্ট {port})। একটি কনসোল উইন্ডো খোলা থাকবে; "
                            + "সেটি বন্ধ করলে হোস্ট বন্ধ হয়ে যাবে।";
            await LoadAsync();
        }, "হোস্ট চালু করা হচ্ছে…");
    }

    private async Task TestAsync()
    {
        await RunAsync(async () =>
        {
            if (HostAddress.Trim().Length == 0)
            {
                throw new ValidationException("হোস্টের আইপি ঠিকানা দিন।");
            }

            var options = new LanClientOptions
            {
                HostAddress = HostAddress.Trim(),
                Port = int.TryParse(PortText.Trim(), out int port) ? port : 8787
            };

            using var connection = new LanConnection(options, _device.DeviceId);
            HostInfo info = await connection.GetInfoAsync();

            StatusText = $"সংযোগ সফল: {info.InstitutionName} · হোস্ট সংস্করণ {info.HostVersion} · "
                         + $"সংযুক্ত ক্লায়েন্ট {_state.Number(info.ConnectedClients)}";
        }, "সংযোগ পরীক্ষা করা হচ্ছে…");
    }
}

/// <summary>About, help and version information.</summary>
public sealed class AboutViewModel : ViewModelBase
{
    private readonly ISetupService _setup;
    private readonly ILicenseService _licenses;
    private readonly ShikkhaDeskEnvironment _environment;
    private readonly AppState _state;

    public AboutViewModel(ISetupService setup, ILicenseService licenses,
        ShikkhaDeskEnvironment environment, AppState state)
    {
        _setup = setup;
        _licenses = licenses;
        _environment = environment;
        _state = state;
        _ = LoadAsync();
    }

    private string _details = string.Empty;
    public string Details
    {
        get => _details;
        set => SetField(ref _details, value);
    }

    public string Version => "শিক্ষা ডেস্ক " + App.ApplicationVersion;

    public string DataPath => _environment.DataDirectory;

    public string LogPath => _environment.LogDirectory;

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            LicenseStatusInfo licence = await _licenses.GetStatusAsync();

            Details = string.Join(Environment.NewLine,
                "কোচিং সেন্টারের সম্পূর্ণ ব্যবস্থাপনা, এক জায়গায়।",
                string.Empty,
                "সংস্করণ: " + App.ApplicationVersion,
                "লাইসেন্স: " + licence.Status + (licence.ExpiresAtUtc is null
                    ? " (মেয়াদ সীমাহীন)"
                    : " (মেয়াদ: " + Core.Digits.Format(licence.ExpiresAtUtc.Value, _state.UseBengaliDigits) + ")"),
                "ডিভাইস: " + (licence.CurrentDeviceId ?? "—"),
                string.Empty,
                "ডেটা: " + _environment.DataDirectory,
                "ব্যাকআপ: " + _environment.BackupDirectory,
                "লগ: " + _environment.LogDirectory);
        });
    }
}
