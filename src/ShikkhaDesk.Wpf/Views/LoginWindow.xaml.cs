using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;

namespace ShikkhaDesk.Wpf.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        VersionText.Text = $"সংস্করণ {App.ApplicationVersion} · অফলাইন চালিত";

        try
        {
            DeviceText.Text = "ডিভাইস: " + App.Services!.GetRequiredService<IDeviceFingerprint>().DeviceId;
        }
        catch (Exception)
        {
            DeviceText.Text = string.Empty;
        }

        UserTextBox.Focus();
    }

    public string UserName => UserTextBox.Text.Trim();

    public string Password => PasswordBox.Password;

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        if (UserName.Length == 0 || Password.Length == 0)
        {
            MessageText.Text = "ব্যবহারকারীর নাম ও পাসওয়ার্ড উভয়ই দিন।";
            return;
        }

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// There is no self-service password reset: an offline application cannot prove identity by
    /// email. The message tells the operator exactly who can help, which is honest and actionable.
    /// </summary>
    private void ForgotPassword_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(
            "পাসওয়ার্ড রিসেটের জন্য অ্যাডমিন-স্তরের ব্যবহারকারীকে লগ ইন করে\n" +
            "'ব্যবহারকারী ও অনুমতি' পৃষ্ঠা থেকে নতুন পাসওয়ার্ড সেট করতে হবে।\n\n" +
            "নিরাপত্তার কারণে পাসওয়ার্ড পুনরুদ্ধারের কোনো স্বয়ংক্রিয় পথ রাখা হয়নি।",
            "পাসওয়ার্ড রিসেট", MessageBoxButton.OK, MessageBoxImage.Information);
}
