using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>One entry in the sidebar. A navigation item only exists when the user may open it.</summary>
public sealed class NavigationItem : INotifyPropertyChanged
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Group { get; init; }
    public string? Glyph { get; init; }

    /// <summary>Permission required for the item to be shown. Enforced here and in the service layer.</summary>
    public string? RequiredPermission { get; init; }

    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
