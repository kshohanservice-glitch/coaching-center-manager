using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public interface IAccountingService
{
    Task<AccountMap> GetAccountMapAsync(CancellationToken ct = default);
    Task<Guid> PostAsync(JournalDraft draft, CancellationToken ct = default);
    Task<Guid> ReverseJournalAsync(Guid journalEntryId, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateRange range, CancellationToken ct = default);
    Task<IReadOnlyList<AccountBalanceRow>> GetAccountBalancesAsync(DateRange range, CancellationToken ct = default);
    Task<PagedResult<LedgerEntryRow>> GetLedgerAsync(LedgerQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<CashbookRow>> GetCashbookAsync(Guid financialAccountId, DateRange range,
        CancellationToken ct = default);
    Task<IReadOnlyList<FinancialAccountBalance>> GetFinancialAccountBalancesAsync(CancellationToken ct = default);
    Task<Money> GetAccountBalanceAsync(Guid accountId, DateTime? asOf, CancellationToken ct = default);
}

/// <summary>
/// Owns the ledger. Every posting goes through <see cref="PostAsync"/>, which re-validates the draft
/// (debits must equal credits) immediately before the insert, inside the caller's transaction.
/// </summary>
public sealed class AccountingService : IAccountingService
{
    private readonly IAccountingRepository _accounting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISequenceAllocator _sequences;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public AccountingService(IAccountingRepository accounting, IUnitOfWork unitOfWork,
        ISequenceAllocator sequences, IAuditService audit, ICurrentUserService currentUser)
    {
        _accounting = accounting;
        _unitOfWork = unitOfWork;
        _sequences = sequences;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<AccountMap> GetAccountMapAsync(CancellationToken ct = default)
    {
        var map = new AccountMap
        {
            StudentReceivables = await ResolveRequiredAsync(SystemAccountCodes.StudentReceivables, ct),
            StudentAdvanceCredit = await ResolveRequiredAsync(SystemAccountCodes.StudentAdvanceCredit, ct),
            TuitionIncome = await ResolveRequiredAsync(SystemAccountCodes.TuitionIncome, ct),
            AdmissionIncome = await ResolveRequiredAsync(SystemAccountCodes.AdmissionIncome, ct),
            CourseIncome = await ResolveRequiredAsync(SystemAccountCodes.CourseIncome, ct),
            ExamIncome = await ResolveRequiredAsync(SystemAccountCodes.ExamIncome, ct),
            OtherIncome = await ResolveRequiredAsync(SystemAccountCodes.OtherIncome, ct),
            AccountsPayable = await ResolveRequiredAsync(SystemAccountCodes.AccountsPayable, ct),
            SalaryPayable = await ResolveRequiredAsync(SystemAccountCodes.SalaryPayable, ct),
            SalaryExpense = await ResolveRequiredAsync(SystemAccountCodes.TeacherSalaries, ct),
            BankChargeExpense = await ResolveRequiredAsync(SystemAccountCodes.BankCharges, ct),
            OpeningBalanceEquity = await ResolveRequiredAsync(SystemAccountCodes.OpeningCapital, ct)
        };

        map.Validate();
        return map;
    }

    public Task<Guid> PostAsync(JournalDraft draft, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => PostInternalAsync(draft, token), ct);

    private async Task<Guid> PostInternalAsync(JournalDraft draft, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // The balancing invariant is checked here as well as at build time: nothing reaches the
        // database unless total debits equal total credits.
        draft.Validate();

        Guid userId = _currentUser.Current.UserId;
        string journalNumber = await _sequences.NextFormattedAsync(
            "JRNL", "JV", 6, NumberingPeriod.Yearly, draft.EntryDate, ct);

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            JournalNumber = journalNumber,
            EntryDate = draft.EntryDate,
            Description = draft.Description,
            SourceKind = draft.SourceKind,
            SourceId = draft.SourceId,
            SourceReference = draft.SourceReference,
            Status = JournalStatus.Posted,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        var lines = new List<JournalLine>(draft.Lines.Count);
        int order = 0;
        foreach (JournalLineDraft line in draft.Lines)
        {
            lines.Add(new JournalLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = line.AccountId,
                Debit = line.Debit,
                Credit = line.Credit,
                FinancialAccountId = line.FinancialAccountId,
                StudentId = line.StudentId,
                SupplierId = line.SupplierId,
                StaffId = line.StaffId,
                Memo = line.Memo,
                LineOrder = order++
            });
        }

        await _accounting.InsertJournalEntryAsync(entry, lines, ct).ConfigureAwait(false);
        return entry.Id;
    }

    /// <summary>
    /// Reverses a posted journal by mirroring it. The original entry is marked reversed but is never
    /// edited or deleted, so historical reports remain reproducible.
    /// </summary>
    public Task<Guid> ReverseJournalAsync(Guid journalEntryId, string reason, CancellationToken ct = default) =>
        _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            _currentUser.Current.Require(PermissionCodes.Reverse);
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ValidationException("বাতিলের কারণ উল্লেখ করা আবশ্যক।");
            }

            JournalEntryDto? original = await _accounting.GetJournalEntryAsync(journalEntryId, token)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("মূল জার্নাল এন্ট্রিটি পাওয়া যায়নি।");

            if (original.Entry.Status == JournalStatus.Reversed)
            {
                throw new DomainException("এই জার্নাল এন্ট্রিটি ইতিমধ্যে বাতিল করা হয়েছে।");
            }

            var originalLines = original.Lines
                .Select(l => (l.AccountId, l.Debit, l.Credit, l.FinancialAccountId, l.StudentId, l.SupplierId, l.StaffId))
                .ToList();

            JournalDraft reversal = JournalBuilder.ForReversal(
                original.Entry.Id, original.Entry.JournalNumber, DateTime.Today, originalLines, reason);

            Guid reversalId = await PostInternalAsync(reversal, token).ConfigureAwait(false);

            original.Entry.Status = JournalStatus.Reversed;
            original.Entry.ReversedByJournalId = reversalId;
            await _accounting.UpdateJournalEntryAsync(original.Entry, token).ConfigureAwait(false);

            await _audit.RecordAsync("accounting.reverse", nameof(JournalEntry), original.Entry.Id,
                $"{original.Entry.JournalNumber} বাতিল: {reason}", ct: token).ConfigureAwait(false);

            return reversalId;
        }, ct);

    public Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateRange range,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require(PermissionCodes.View);
        return _unitOfWork.ExecuteAsync(token => _accounting.GetTrialBalanceAsync(range, token), ct);
    }

    public Task<IReadOnlyList<AccountBalanceRow>> GetAccountBalancesAsync(DateRange range,
        CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _accounting.GetAccountBalancesAsync(range, token), ct);

    public Task<PagedResult<LedgerEntryRow>> GetLedgerAsync(LedgerQuery query, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _accounting.GetAccountLedgerAsync(query, token), ct);

    public Task<IReadOnlyList<CashbookRow>> GetCashbookAsync(Guid financialAccountId, DateRange range,
        CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _accounting.GetCashbookAsync(financialAccountId, range, token), ct);

    public async Task<IReadOnlyList<FinancialAccountBalance>> GetFinancialAccountBalancesAsync(
        CancellationToken ct = default)
    {
        IReadOnlyList<FinancialAccount> accounts = await _unitOfWork.ExecuteAsync(
            token => _accounting.GetFinancialAccountsAsync(true, token), ct).ConfigureAwait(false);

        var results = new List<FinancialAccountBalance>(accounts.Count);
        foreach (FinancialAccount account in accounts)
        {
            Money balance = await GetAccountBalanceForFinancialAccountAsync(account.Id, null, ct)
                .ConfigureAwait(false);
            results.Add(new FinancialAccountBalance(account.Id, account.Name, account.Kind,
                account.OpeningBalance, balance));
        }

        return results;
    }

    public Task<Money> GetAccountBalanceAsync(Guid accountId, DateTime? asOf, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _accounting.GetAccountBalanceAsync(accountId, asOf, token), ct);

    /// <summary>
    /// Balance of a cash/bank sub-account: its opening balance plus every posted movement tagged with
    /// that account. The shared control account is never used for this, so balances cannot leak
    /// between bank accounts.
    /// </summary>
    public Task<Money> GetAccountBalanceForFinancialAccountAsync(Guid financialAccountId, DateTime? asOf,
        CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(
            token => _accounting.GetFinancialAccountBalanceAsync(financialAccountId, asOf, token), ct);

    private async Task<Guid> ResolveRequiredAsync(string code, CancellationToken ct)
    {
        Account? account = await _unitOfWork.ExecuteAsync(token => _accounting.GetAccountByCodeAsync(code, token), ct)
            .ConfigureAwait(false);

        return account?.Id ?? throw new DomainException(
            $"হিসাব কোড {code} পাওয়া যায়নি। অনুগ্রহ করে হিসাবের তালিকা যাচাই করুন।");
    }
}
