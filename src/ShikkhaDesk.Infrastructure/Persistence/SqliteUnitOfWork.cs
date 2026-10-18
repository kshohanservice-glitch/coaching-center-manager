using System.Data;
using Microsoft.Data.Sqlite;
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
        catch
        {
            try
            {
                _transaction.Rollback();
            }
            catch (SqliteException)
            {
                // Rolling back an already-aborted transaction must not mask the original error.
            }

            throw;
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
