namespace ShikkhaDesk.Core.Services;

public sealed class JournalLineDraft
{
    public Guid AccountId { get; init; }
    public Money Debit { get; init; }
    public Money Credit { get; init; }
    public Guid? FinancialAccountId { get; init; }
    public Guid? StudentId { get; init; }
    public Guid? SupplierId { get; init; }
    public Guid? StaffId { get; init; }
    public string? Memo { get; init; }

    public static JournalLineDraft Dr(Guid accountId, Money amount, Guid? financialAccountId = null,
        Guid? studentId = null, Guid? supplierId = null, Guid? staffId = null, string? memo = null) =>
        new()
        {
            AccountId = accountId,
            Debit = amount,
            FinancialAccountId = financialAccountId,
            StudentId = studentId,
            SupplierId = supplierId,
            StaffId = staffId,
            Memo = memo
        };

    public static JournalLineDraft Cr(Guid accountId, Money amount, Guid? financialAccountId = null,
        Guid? studentId = null, Guid? supplierId = null, Guid? staffId = null, string? memo = null) =>
        new()
        {
            AccountId = accountId,
            Credit = amount,
            FinancialAccountId = financialAccountId,
            StudentId = studentId,
            SupplierId = supplierId,
            StaffId = staffId,
            Memo = memo
        };
}

/// <summary>
/// An un-posted journal entry. A draft can never be persisted unless <see cref="Validate"/> passes,
/// which is the single place where the "total debits = total credits" invariant is enforced.
/// </summary>
public sealed class JournalDraft
{
    public TransactionSourceKind SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceReference { get; init; }
    public DateTime EntryDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<JournalLineDraft> Lines { get; init; } = Array.Empty<JournalLineDraft>();

    public Money TotalDebit => Lines.Aggregate(Money.Zero, (sum, line) => sum + line.Debit);
    public Money TotalCredit => Lines.Aggregate(Money.Zero, (sum, line) => sum + line.Credit);
    public bool IsBalanced => TotalDebit == TotalCredit;

    public void Validate()
    {
        if (Lines.Count < 2)
        {
            throw new InvalidOperationException(
                "একটি জার্নাল এন্ট্রিতে অন্তত দুইটি লাইন থাকতে হবে। (A journal entry needs at least two lines.)");
        }

        foreach (JournalLineDraft line in Lines)
        {
            if (line.AccountId == Guid.Empty)
            {
                throw new InvalidOperationException("জার্নাল লাইনে হিসাব নির্বাচন করা হয়নি।");
            }

            if (!line.Debit.IsZero && !line.Credit.IsZero)
            {
                throw new InvalidOperationException("একই লাইনে ডেবিট ও ক্রেডিট দুইটিই থাকতে পারবে না।");
            }

            if (line.Debit.IsNegative || line.Credit.IsNegative)
            {
                throw new InvalidOperationException("জার্নাল লাইনের পরিমাণ ঋণাত্মক হতে পারবে না।");
            }

            if (line.Debit.IsZero && line.Credit.IsZero)
            {
                throw new InvalidOperationException("জার্নাল লাইনের পরিমাণ শূন্য হতে পারবে না।");
            }
        }

        if (!IsBalanced)
        {
            throw new InvalidOperationException(
                $"জার্নাল এন্ট্রি ভারসাম্যহীন: ডেবিট {TotalDebit.ToDecimal():N2} বনাম ক্রেডিট {TotalCredit.ToDecimal():N2}।");
        }
    }
}
