using Dapper;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Infrastructure.Persistence;

/// <summary>
/// Allocates receipt, voucher and journal numbers. Must be called inside a unit-of-work transaction:
/// the increment and the row it numbers commit together, which is what guarantees no number is
/// reused and none is skipped under concurrent writers.
/// </summary>
public sealed class SequenceAllocator : ISequenceAllocator
{
    private const string UpdateSql = @"
        UPDATE receipt_sequences
           SET last_number = last_number + 1
         WHERE scope = @Scope AND prefix = @Prefix AND period_key = @PeriodKey;";

    private const string InsertSql = @"
        INSERT INTO receipt_sequences (id, scope, prefix, period_key, last_number)
        VALUES (@Id, @Scope, @Prefix, @PeriodKey, 1)
        ON CONFLICT (scope, prefix, period_key)
        DO UPDATE SET last_number = last_number + 1;";

    private const string SelectSql = @"
        SELECT last_number FROM receipt_sequences
         WHERE scope = @Scope AND prefix = @Prefix AND period_key = @PeriodKey;";

    private readonly IUnitOfWork _unitOfWork;

    public SequenceAllocator(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<long> NextAsync(string scope, string prefix, string periodKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var parameters = new { Scope = scope, Prefix = prefix, PeriodKey = periodKey, Id = Guid.NewGuid() };
        int updated = await _unitOfWork.Connection.ExecuteAsync(
            new CommandDefinition(UpdateSql, parameters, _unitOfWork.Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (updated == 0)
        {
            await _unitOfWork.Connection.ExecuteAsync(
                new CommandDefinition(InsertSql, parameters, _unitOfWork.Transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        return await _unitOfWork.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(SelectSql, parameters, _unitOfWork.Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<string> NextFormattedAsync(string kind, string prefix, int width,
        NumberingPeriod period, DateTime date, CancellationToken cancellationToken = default)
    {
        string scope = ReceiptNumbering.Scope(kind, prefix, period, date);
        long number = await NextAsync(scope, prefix, ReceiptNumbering.PeriodToken(period, date),
            cancellationToken).ConfigureAwait(false);
        return ReceiptNumbering.Format(prefix, number, width, period, date);
    }

    /// <summary>Peeks at the next number without consuming it, for preview purposes only.</summary>
    public async Task<string> PeekFormattedAsync(string kind, string prefix, int width,
        NumberingPeriod period, DateTime date, CancellationToken cancellationToken = default)
    {
        string scope = ReceiptNumbering.Scope(kind, prefix, period, date);
        long current = await _unitOfWork.Connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(SelectSql, new { Scope = scope, Prefix = prefix,
                PeriodKey = ReceiptNumbering.PeriodToken(period, date) },
                _unitOfWork.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false) ?? 0;
        return ReceiptNumbering.Format(prefix, current + 1, width, period, date);
    }
}
