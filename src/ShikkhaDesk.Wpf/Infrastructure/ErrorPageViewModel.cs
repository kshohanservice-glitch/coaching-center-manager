using System.Windows.Input;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// Shown when a page cannot be built. The failure is stated plainly instead of leaving the operator
/// staring at an empty frame, which is how a broken page used to look.
/// </summary>
public sealed class ErrorPageViewModel : ViewModelBase
{
    private readonly NavigationService _navigation;

    public ErrorPageViewModel(NavigationService navigation, string page, string detail)
    {
        _navigation = navigation;
        Page = page;
        Detail = detail;
        GoHomeCommand = new RelayCommand(() => _navigation.Navigate("dashboard"));
    }

    public string Page { get; }

    public string Detail { get; }

    public ICommand GoHomeCommand { get; }
}
