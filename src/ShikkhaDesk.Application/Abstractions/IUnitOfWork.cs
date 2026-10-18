using System.Data;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Abstractions;

/// <summary>Exposes the connection and the ambient transaction to the persistence layer.</summary>
public interface IDbSession
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }
}

/// <summary>
/// A single SQLite transaction. Financial operations are all-or-nothing: receipt numbering, payment
/// rows, allocations, credit movements and journal lines commit together or not at all.
/// </summary>
public interface IUnitOfWork : IDbSession, IDisposable
{
    Guid Id { get; }

    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);
}

/// <summary>Allocates the next number in a named sequence, safe under concurrent writers.</summary>
public interface ISequenceAllocator
{
    Task<long> NextAsync(string scope, string prefix, string periodKey,
        CancellationToken cancellationToken = default);

    Task<string> NextFormattedAsync(string kind, string prefix, int width, NumberingPeriod period,
        DateTime date, CancellationToken cancellationToken = default);
}
