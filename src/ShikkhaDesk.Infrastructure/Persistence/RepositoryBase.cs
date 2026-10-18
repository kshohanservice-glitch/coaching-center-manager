using Dapper;
using ShikkhaDesk.Application.Abstractions;

namespace ShikkhaDesk.Infrastructure.Persistence;

/// <summary>Shared plumbing for the Dapper-backed repositories.</summary>
public abstract class RepositoryBase
{
    protected RepositoryBase(IUnitOfWork unitOfWork) => UnitOfWork = unitOfWork;

    protected IUnitOfWork UnitOfWork { get; }

    protected Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null,
        CancellationToken ct = default) =>
        UnitOfWork.Connection.QueryAsync<T>(new CommandDefinition(sql, parameters, UnitOfWork.Transaction,
            cancellationToken: ct));

    protected Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null,
        CancellationToken ct = default) =>
        UnitOfWork.Connection.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, parameters,
            UnitOfWork.Transaction, cancellationToken: ct));

    protected Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        UnitOfWork.Connection.QuerySingleAsync<T>(new CommandDefinition(sql, parameters, UnitOfWork.Transaction,
            cancellationToken: ct));

    protected Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default) =>
        UnitOfWork.Connection.ExecuteAsync(new CommandDefinition(sql, parameters, UnitOfWork.Transaction,
            cancellationToken: ct));

    protected Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        UnitOfWork.Connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, UnitOfWork.Transaction,
            cancellationToken: ct));

    /// <summary>Dates are stored as TEXT; normalising the bounds keeps range filters inclusive.</summary>
    protected static DateTime StartOfDay(DateTime date) => date.Date;

    /// <summary>Inclusive end bound: the last second of the day.</summary>
    protected static DateTime EndOfDay(DateTime date) => date.Date.AddDays(1).AddSeconds(-1);

    /// <summary>Safe LIKE pattern: escapes wildcards so a search term cannot change the query shape.</summary>
    protected static string Like(string? term) =>
        string.IsNullOrWhiteSpace(term)
            ? "%"
            : "%" + term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
