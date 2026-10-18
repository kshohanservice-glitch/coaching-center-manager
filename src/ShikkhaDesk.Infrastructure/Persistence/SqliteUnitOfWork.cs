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

    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await _factory.WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _factory.WriteLock.Release();
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await _factory.WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _factory.WriteLock.Release();
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await _factory.WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_transaction is not null)
            {
                // Nested call: join the ambient transaction instead of starting a new one.
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
        finally
        {
            _factory.WriteLock.Release();
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
