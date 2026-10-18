using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ShikkhaDesk.Infrastructure.Logging;

/// <summary>
/// Minimal structured file logger. Personal data, passwords and full financial documents are never
/// logged: callers pass short, non-sensitive summaries only.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly BlockingCollection<string> _queue = new();
    private readonly Task _worker;
    private readonly CancellationTokenSource _cts = new();
    private string _currentFile;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
        _currentFile = Path.Combine(_directory, $"shikkhadesk-{DateTime.Now:yyyyMMdd}.log");
        _worker = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void Enqueue(string line) => _queue.Add(line);

    private async Task WriteLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryTake(out string? line, 500))
                {
                    await File.AppendAllTextAsync(_currentFile, line + Environment.NewLine).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // Logging must never take the application down.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // Ignore shutdown races.
        }

        _cts.Dispose();
        _queue.Dispose();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            string line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} [{logLevel}] {_category}: {message}";

            if (exception is not null)
            {
                line += Environment.NewLine + "    " + exception;
            }

            _provider.Enqueue(line);
        }
    }
}
