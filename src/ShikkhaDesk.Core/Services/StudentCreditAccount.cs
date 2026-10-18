namespace ShikkhaDesk.Core.Services;

/// <summary>
/// Derives a student's advance-credit balance and the refundable split from the credit ledger.
/// The balance is always recomputed from movements, never stored as a mutable counter, so it cannot
/// drift away from the underlying transactions.
/// </summary>
public static class StudentCreditAccount
{
    public static Money Balance(IEnumerable<CreditTransactionKind> kinds, IEnumerable<Money> amounts)
    {
        using var kindEnumerator = kinds.GetEnumerator();
        using var amountEnumerator = amounts.GetEnumerator();
        Money balance = Money.Zero;
        while (kindEnumerator.MoveNext() && amountEnumerator.MoveNext())
        {
            balance += Signed(kindEnumerator.Current, amountEnumerator.Current);
        }

        return balance;
    }

    public static Money Balance(IEnumerable<(CreditTransactionKind Kind, Money Amount)> movements) =>
        movements.Aggregate(Money.Zero, (sum, m) => sum + Signed(m.Kind, m.Amount));

    public static Money Signed(CreditTransactionKind kind, Money amount) => kind switch
    {
        CreditTransactionKind.Received => amount.Abs(),
        CreditTransactionKind.Adjusted => amount,
        CreditTransactionKind.Allocated => -amount.Abs(),
        CreditTransactionKind.Refunded => -amount.Abs(),
        _ => Money.Zero
    };
}
