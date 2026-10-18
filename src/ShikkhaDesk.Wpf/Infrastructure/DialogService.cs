using System.Windows;
using Microsoft.Win32;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>Message boxes and file dialogs, so view models never touch WPF windows directly.</summary>
public interface IDialogService
{
    void Information(string message, string title = "শিক্ষা ডেস্ক");
    bool Confirm(string message, string title = "নিশ্চিত করুন");
    void Error(string message, string title = "সমস্যা");
    string? SaveFile(string defaultName, string filter);
    string? OpenFile(string filter);
}

public sealed class DialogService : IDialogService
{
    public void Information(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Error(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public string? SaveFile(string defaultName, string filter)
    {
        var dialog = new SaveFileDialog { FileName = defaultName, Filter = filter, DefaultExt = ".csv" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? OpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
