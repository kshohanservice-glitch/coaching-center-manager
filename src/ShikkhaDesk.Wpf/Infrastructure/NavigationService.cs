using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ShikkhaDesk.Wpf.Infrastructure;

public sealed class NavigationGroup
{
    public string Name { get; init; } = string.Empty;
    public ObservableCollection<NavigationItem> Items { get; } = new();
}

public sealed class NavigationService : ViewModelBase
{
    private readonly AppState _state;
    private readonly Func<string, ViewModelBase> _factory;
    private NavigationItem? _selected;
    private ViewModelBase? _currentPage;

    public NavigationService(AppState state, Func<string, ViewModelBase> factory)
    {
        _state = state;
        _factory = factory;
        Groups = new ObservableCollection<NavigationGroup>();
        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is string key)
            {
                Navigate(key);
            }
        });
    }

    public ObservableCollection<NavigationGroup> Groups { get; }

    public ICommand NavigateCommand { get; }

    public NavigationItem? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value) && value is not null)
            {
                Navigate(value.Key);
            }
        }
    }

    public ViewModelBase? CurrentPage
    {
        get => _currentPage;
        private set => SetField(ref _currentPage, value);
    }

    private string? _pageTitle;
    public string? PageTitle
    {
        get => _pageTitle;
        private set => SetField(ref _pageTitle, value);
    }

    /// <summary>Builds the menu after sign-in, showing only what the user is allowed to open.</summary>
    public void BuildMenu()
    {
        Groups.Clear();

        foreach (IGrouping<string, NavigationItem> group in NavigationCatalogue.All
                     .Where(i => i.RequiredPermission is null || _state.Has(i.RequiredPermission))
                     .GroupBy(i => i.Group))
        {
            var container = new NavigationGroup { Name = group.Key };
            foreach (NavigationItem item in group)
            {
                container.Items.Add(item);
            }

            Groups.Add(container);
        }
    }

    public void Navigate(string key)
    {
        NavigationItem? item = NavigationCatalogue.All.FirstOrDefault(i => i.Key == key);
        if (item is null)
        {
            return;
        }

        if (item.RequiredPermission is not null && !_state.Has(item.RequiredPermission))
        {
            throw new AuthorizationException(
                $"এই পৃষ্ঠাটি দেখার অনুমতি আপনার নেই ({item.RequiredPermission})।");
        }

        foreach (NavigationItem candidate in NavigationCatalogue.All)
        {
            candidate.IsActive = candidate.Key == key;
        }

        try
        {
            CurrentPage = _factory(key);
            PageTitle = item.Label;
            _selected = item;
            OnPropertyChanged(nameof(Selected));
        }
        catch (Exception ex)
        {
            CurrentPage = new ErrorPageViewModel(item.Label, ex);
            PageTitle = item.Label;
        }
    }
}

/// <summary>Shown when a page cannot be created, so a failure is visible instead of a blank screen.</summary>
public sealed class ErrorPageViewModel : ViewModelBase
{
    public ErrorPageViewModel(string page, Exception exception)
    {
        Page = page;
        Detail = exception.Message;
    }

    public string Page { get; }
    public string Detail { get; }
}
