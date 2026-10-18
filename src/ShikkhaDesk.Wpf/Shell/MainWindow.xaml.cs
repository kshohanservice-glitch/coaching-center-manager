using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.Shell;

public partial class MainWindow : Window
{
    private readonly NavigationService _navigation;

    public MainWindow(NavigationService navigation, AppState state)
    {
        InitializeComponent();

        _navigation = navigation;
        DataContext = navigation;

        InstitutionName.Text = string.IsNullOrWhiteSpace(state.Institution?.NameBn)
            ? state.Institution?.Name ?? "শিক্ষা ডেস্ক"
            : state.Institution.NameBn;
        CurrentUserName.Text = $"{state.CurrentUser.FullName} ({state.CurrentUser.RoleName})";
        VersionText.Text = $"সংস্করণ {App.ApplicationVersion} · অফলাইন-প্রথম · ডেটা: {App.DataLocation}";

        _navigation.BuildMenu();

        if (_navigation.CurrentPage is null)
        {
            _navigation.Navigate("dashboard");
        }
    }

    public NavigationService Navigation => _navigation;

    private void Logout_Click(object sender, RoutedEventArgs e) => App.Restart();
}
