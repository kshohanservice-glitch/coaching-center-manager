using System.Data;
using Microsoft.Data.Sqlite;

namespace ShikkhaDesk.Infrastructure.Persistence;

public sealed class SqliteConnectionOptions
{
    public string DatabasePath { get; init; } = string.Empty;
    public int BusyTimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Owns the single long-lived SQLite connection for the process. Write-Ahead Logging is enabled so
/// readers never block, a busy timeout is set so a transient lock waits instead of failing, and
/// foreign keys are enforced on every connection.
/// </summary>
public sealed class SqliteConnectionFactory : IDisposable
{
    static SqliteConnectionFactory() => DapperConfiguration.EnsureConfigured();

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;

    public SqliteConnectionFactory(SqliteConnectionOptions options)
    {
        Options = options;
    }

    public SqliteConnectionOptions Options { get; }

    public SqliteConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection ??= Open();
        }
    }

    /// <summary>
    /// Serialises writers. SQLite allows a single writer at a time; taking this lock around a
    /// transaction is what makes concurrent receipt numbering and concurrent payments safe.
    /// </summary>
    public SemaphoreSlim WriteLock => _writeLock;

    private SqliteConnection Open()
    {
        string? directory = Path.GetDirectoryName(Options.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = Options.BusyTimeoutSeconds
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        using (IDbCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA synchronous = FULL;";
            pragma.ExecuteNonQuery();
        }

        return connection;
    }

    /// <summary>
    /// Closes and discards the pooled connection so the database file can be replaced (restore) or
    /// moved. Callers must not use any open transaction or repository handle afterwards.
    /// </summary>
    public void Recycle()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            // Dispose alone returns the handle to Microsoft.Data.Sqlite's pool, which keeps the
            // database file open. A restore that then replaces the file reopens through that same
            // pooled handle and reads the old content back, so the restore silently does nothing.
            SqliteConnection.ClearPool(_connection);
        }
        finally
        {
            try
            {
                _connection.Dispose();
            }
            finally
            {
                _connection = null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection?.Dispose();
        _connection = null;
        _writeLock.Dispose();
    }
}
