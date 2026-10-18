using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using ShikkhaDesk.Core;
using ShikkhaDesk.Application.Abstractions;

namespace ShikkhaDesk.Infrastructure.Persistence;

/// <summary>
/// Unit of work over the single SQLite connection. Writes are serialised through the factory's
/// write lock, so two concurrent payments can never interleave their receipt numbering or
/// their ledger postings.
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteConnectionFactory _factory;
    private readonly AsyncLocal<int> _lockDepth = new();
    private SqliteTransaction? _transaction;
    private int _depth;
    private bool _disposed;

    public SqliteUnitOfWork(SqliteConnectionFactory factory)
    {
        _factory = factory;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public IDbConnection Connection => _factory.Connection;

    public IDbTransaction? Transaction => _transaction;

    public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return WithWriteLockAsync(work, cancellationToken);
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await WithWriteLockAsync<object?>(async ct =>
        {
            await work(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serialises access to the single connection, without deadlocking a nested service call.
    /// </summary>
    /// <remarks>
    /// The unit of work is a singleton shared by every service, and a service routinely calls
    /// another service inside its own unit of work. Taking the semaphore again on the same async
    /// flow would therefore wait for a lock this very call already holds — a deadlock, not a delay.
    /// The depth counter is per async flow, so two genuinely concurrent callers still queue.
    /// </remarks>
    private async Task<TResult> WithWriteLockAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        if (_lockDepth.Value > 0)
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }

        await _factory.WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        _lockDepth.Value = 1;

        try
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lockDepth.Value = 0;
            _factory.WriteLock.Release();
        }
    }

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return WithWriteLockAsync(ct => RunInTransactionAsync(work, ct), cancellationToken);
    }

    private async Task<TResult> RunInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            // Nested call: join the ambient transaction instead of starting a new one, and let the
            // outermost frame commit or roll the whole thing back as one unit.
            _depth++;
            try
            {
                return await work(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _depth--;
            }
        }

        _transaction = (SqliteTransaction)_factory.Connection.BeginTransaction(IsolationLevel.Serializable);
        _depth = 1;

        try
        {
            TResult result = await work(cancellationToken).ConfigureAwait(false);
            if (_depth == 1)
            {
                _transaction.Commit();
            }

            return result;
        }
        catch (Exception exception)
        {
            try
            {
                _transaction.Rollback();
            }
            catch (SqliteException)
            {
                // Rolling back an already-aborted transaction must not mask the original error.
            }

            throw DescribeConstraintFailure(exception);
        }
        finally
        {
            _depth--;
            if (_depth <= 0)
            {
                await _transaction.DisposeAsync().ConfigureAwait(false);
                _transaction = null;
                _depth = 0;
            }
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync<object?>(async ct =>
        {
            await work(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// SQLite reports a foreign-key violation without naming the table or the row, which leaves an
    /// operator staring at "FOREIGN KEY constraint failed" with nothing to act on. Naming the
    /// offending row turns that into something a person can actually fix.
    /// </summary>
    private Exception DescribeConstraintFailure(Exception exception)
    {
        if (exception is not SqliteException sqlite)
        {
            return exception;
        }

        bool isForeignKey = sqlite.SqliteErrorCode == 19
            && (sqlite.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                || sqlite.SqliteExtendedErrorCode == 787);

        if (!isForeignKey)
        {
            return sqlite;
        }

        List<string> violations;
        try
        {
            violations = _factory.Connection.Query<string>(@"
                SELECT DISTINCT [table] || ' সারি ' || [rowid] || ' -> ' || IFNULL([parent], '(অজানা)')
                  FROM pragma_foreign_key_check;").ToList();
        }
        catch (Exception)
        {
            // Diagnosing the failure must never replace it.
            return sqlite;
        }

        return violations.Count == 0
            ? sqlite
            : new DomainException("তথ্যের সংযোগে সমস্যা (foreign key): " + string.Join(", ", violations)
                                  + " — " + sqlite.Message, sqlite);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _transaction?.Dispose();
        _transaction = null;
    }
}
