using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Security;

namespace ShikkhaDesk.Wpf.Views;

/// <summary>
/// First-run wizard. It writes real records through <see cref="ISetupService"/>: institution, admin
/// user, permission catalogue, opening cash account, first academic session and the opening balance
/// journal. Nothing about it is a mock-up.
/// </summary>
public partial class SetupWizardWindow : Window
{
    private readonly Panel[] _steps;
    private int _index;

    public SetupWizardWindow()
    {
        InitializeComponent();

        _steps = [StepWelcome, StepInstitution, StepSession, StepAdmin, StepNetwork, StepFinish];

        SessionStartPicker.SelectedDate = new DateTime(DateTime.Now.Year, 1, 1);
        SessionEndPicker.SelectedDate = new DateTime(DateTime.Now.Year, 12, 31);
        SessionNameBox.Text = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);

        ModeSingle.Checked += (_, _) => UpdateModeFields();
        ModeLanHost.Checked += (_, _) => UpdateModeFields();
        ModeLanClient.Checked += (_, _) => UpdateModeFields();

        ShowStep(0);
    }

    private void UpdateModeFields()
    {
        bool client = ModeLanClient.IsChecked == true;
        HostAddressBox.IsEnabled = client;
        HostPortBox.IsEnabled = client;
    }

    private void ShowStep(int index)
    {
        _index = index;
        for (int i = 0; i < _steps.Length; i++)
        {
            _steps[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }

        ErrorText.Text = string.Empty;
        BackButton.IsEnabled = index > 0 && index < _steps.Length - 1;
        NextButton.Visibility = index == _steps.Length - 1 ? Visibility.Collapsed : Visibility.Visible;
        FinishButton.Visibility = index == _steps.Length - 1 ? Visibility.Visible : Visibility.Collapsed;

        (string title, string hint) = index switch
        {
            0 => ("স্বাগতম", "ধাপ ১ / ৬"),
            1 => ("প্রতিষ্ঠানের তথ্য", "ধাপ ২ / ৬ — এগুলো রশিদ ও প্রতিবেদনে ছাপা হবে"),
            2 => ("শিক্ষাবর্ষ ও সূচনাকালীন স্থিতি", "ধাপ ৩ / ৬"),
            3 => ("অ্যাডমিন ব্যবহারকারী", "ধাপ ৪ / ৬"),
            4 => ("নেটওয়ার্ক মোড", "ধাপ ৫ / ৬ — পরে পরিবর্তন করা যাবে"),
            _ => ("সব প্রস্তুত", "ধাপ ৬ / ৬")
        };

        StepTitle.Text = title;
        StepHint.Text = hint;

        if (index == _steps.Length - 1)
        {
            BuildSummary();
        }
    }

    private void BuildSummary() => SummaryText.Text = string.Join(Environment.NewLine,
        $"প্রতিষ্ঠান: {NameBox.Text.Trim()}",
        $"শিক্ষাবর্ষ: {SessionNameBox.Text.Trim()} ({SessionStartPicker.SelectedDate:yyyy-MM-dd} → {SessionEndPicker.SelectedDate:yyyy-MM-dd})",
        $"সূচনাকালীন নগদ স্থিতি: {CurrencyBox.Text.Trim()} {OpeningCashBox.Text.Trim()}",
        $"অ্যাডমিন: {AdminUserBox.Text.Trim()} ({AdminNameBox.Text.Trim()})",
        $"মোড: {CurrentModeLabel()}",
        $"রশিদ প্রিফিক্স: {ReceiptPrefixBox.Text.Trim()}");

    private string CurrentModeLabel() => ModeLanClient.IsChecked == true
        ? $"ল্যান ক্লায়েন্ট ({HostAddressBox.Text.Trim()}:{HostPortBox.Text.Trim()})"
        : ModeLanHost.IsChecked == true ? "ল্যান হোস্ট" : "একক কম্পিউটার";

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_index > 0)
        {
            ShowStep(_index - 1);
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateStep(_index))
        {
            return;
        }

        ShowStep(_index + 1);
    }

    private bool ValidateStep(int index) => index switch
    {
        0 => true,
        1 => ValidateInstitution(),
        2 => ValidateSession(),
        3 => ValidateAdmin(),
        4 => ValidateNetwork(),
        _ => true
    };

    private bool ValidateInstitution()
    {
        if (NameBox.Text.Trim().Length == 0)
        {
            ErrorText.Text = "প্রতিষ্ঠানের নাম দিন।";
            return false;
        }

        if (ReceiptPrefixBox.Text.Trim().Length == 0)
        {
            ErrorText.Text = "রশিদ প্রিফিক্স দিন (যেমন SD)।";
            return false;
        }

        return true;
    }

    private bool ValidateSession()
    {
        if (SessionNameBox.Text.Trim().Length == 0)
        {
            ErrorText.Text = "শিক্ষাবর্ষের নাম দিন।";
            return false;
        }

        if (SessionStartPicker.SelectedDate is null || SessionEndPicker.SelectedDate is null)
        {
            ErrorText.Text = "শিক্ষাবর্ষের শুরু ও শেষ তারিখ দিন।";
            return false;
        }

        if (SessionEndPicker.SelectedDate <= SessionStartPicker.SelectedDate)
        {
            ErrorText.Text = "শেষ তারিখ শুরুর তারিখের পরে হতে হবে।";
            return false;
        }

        if (!decimal.TryParse(OpeningCashBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture,
                out decimal opening) && !decimal.TryParse(OpeningCashBox.Text.Trim(), NumberStyles.Number,
                CultureInfo.CurrentCulture, out opening))
        {
            ErrorText.Text = "সূচনাকালীন নগদ স্থিতি সঠিকভাবে লিখুন (যেমন ২৫০০০ বা 25000)।";
            return false;
        }

        if (opening < 0)
        {
            ErrorText.Text = "সূচনাকালীন স্থিতি ঋণাত্মক হতে পারে না।";
            return false;
        }

        return true;
    }

    private bool ValidateAdmin()
    {
        string password = AdminPasswordBox.Password;

        if (AdminUserBox.Text.Trim().Length < 3)
        {
            ErrorText.Text = "ব্যবহারকারীর নাম কমপক্ষে ৩ অক্ষরের হতে হবে।";
            return false;
        }

        if (AdminNameBox.Text.Trim().Length == 0)
        {
            ErrorText.Text = "পুরো নাম দিন।";
            return false;
        }

        if (password != AdminConfirmBox.Password)
        {
            ErrorText.Text = "দুইবার দেওয়া পাসওয়ার্ড মিলছে না।";
            return false;
        }

        PasswordStrengthText.Text = PasswordHasher.ValidateStrength(password, out string detail)
            ? string.Empty
            : detail;

        if (!PasswordHasher.ValidateStrength(password, out string message))
        {
            ErrorText.Text = message;
            return false;
        }

        return true;
    }

    private bool ValidateNetwork()
    {
        if (ModeLanClient.IsChecked != true)
        {
            return true;
        }

        if (HostAddressBox.Text.Trim().Length == 0)
        {
            ErrorText.Text = "ক্লায়েন্ট মোডে হোস্টের আইপি ঠিকানা দিতে হবে।";
            return false;
        }

        if (!int.TryParse(HostPortBox.Text.Trim(), out int port) || port is < 1 or > 65535)
        {
            ErrorText.Text = "পোর্ট ১ থেকে ৬৫৫৩৫-এর মধ্যে হতে হবে।";
            return false;
        }

        return true;
    }

    private void BrowseBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "ব্যাকআপ কোথায় রাখা হবে?",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            BackupBox.Text = dialog.SelectedPath;
        }
    }

    private async void Finish_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        FinishButton.IsEnabled = false;

        try
        {
            string openingText = OpeningCashBox.Text.Trim();
            if (!decimal.TryParse(openingText, NumberStyles.Number, CultureInfo.InvariantCulture,
                    out decimal opening) && !decimal.TryParse(openingText, NumberStyles.Number,
                    CultureInfo.CurrentCulture, out opening))
            {
                opening = 0m;
            }

            var request = new SetupRequest(
                InstitutionName: NameBox.Text.Trim(),
                InstitutionAddress: Nullable(AddressBox.Text),
                Phone: Nullable(PhoneBox.Text),
                Email: Nullable(EmailBox.Text),
                CurrencyCode: "BDT",
                CurrencySymbol: string.IsNullOrWhiteSpace(CurrencyBox.Text) ? "৳" : CurrencyBox.Text.Trim(),
                DigitPreference: (DigitBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "English"
                    ? DigitPreference.English
                    : DigitPreference.Bengali,
                ReceiptPrefix: ReceiptPrefixBox.Text.Trim(),
                BackupLocation: Nullable(BackupBox.Text),
                AdminUserName: AdminUserBox.Text.Trim(),
                AdminFullName: AdminNameBox.Text.Trim(),
                AdminPassword: AdminPasswordBox.Password,
                HostMode: ModeLanClient.IsChecked == true ? HostMode.Client
                    : ModeLanHost.IsChecked == true ? HostMode.LanHost
                    : HostMode.Single,
                SessionName: SessionNameBox.Text.Trim(),
                SessionStart: SessionStartPicker.SelectedDate!.Value.Date,
                SessionEnd: SessionEndPicker.SelectedDate!.Value.Date,
                OpeningCashBalance: Money.FromDecimal(opening),
                LanHostAddress: ModeLanClient.IsChecked == true ? HostAddressBox.Text.Trim() : null,
                LanPort: ModeLanClient.IsChecked == true ? int.Parse(HostPortBox.Text.Trim()) : null);

            await App.Services!.GetRequiredService<ISetupService>().InitializeAsync(request);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = "সেটআপ সম্পন্ন করা যায়নি: " + ex.Message;
            FinishButton.IsEnabled = true;
        }
    }

    private static string? Nullable(string value)
    {
        string text = value.Trim();
        return text.Length == 0 ? null : text;
    }
}
