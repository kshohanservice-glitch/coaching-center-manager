using Microsoft.Extensions.Logging;

namespace ShikkhaDesk.Lan.Server;

/// <summary>Bridges the host's ASP.NET Core logging into the ShikkhaDesk file logger.</summary>
internal sealed class ShikkhaDeskLanLoggerProvider : ILoggerProvider
{
    private readonly ILoggerFactory _inner;

    public ShikkhaDeskLanLoggerProvider(ILoggerFactory inner) => _inner = inner;

    public ILogger CreateLogger(string categoryName) => new Bridge(_inner.CreateLogger(categoryName));

    public void Dispose()
    {
    }

    private sealed class Bridge : ILogger
    {
        private readonly ILogger _inner;

        public Bridge(ILogger inner) => _inner = inner;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
