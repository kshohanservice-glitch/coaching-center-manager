using System.Windows.Input;

namespace ShikkhaDesk.Wpf.Infrastructure;

internal static class CommandExtensions
{
    /// <summary>Awaits an <see cref="AsyncRelayCommand"/> so a view can trigger one from an event handler.</summary>
    public static Task ExecuteAsync(this ICommand command, object? parameter)
    {
        if (command is AsyncRelayCommand async)
        {
            return async.ExecuteTaskAsync(parameter);
        }

        command.Execute(parameter);
        return Task.CompletedTask;
    }
}
