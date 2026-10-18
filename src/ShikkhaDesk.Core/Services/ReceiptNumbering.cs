using System.Globalization;

namespace ShikkhaDesk.Core.Services;

/// <summary>
/// How a numbering series restarts. This is the single answer to "when does the counter go back to
/// one", and it decides both the sequence key and what appears in the printed number — the two are
/// derived from one another here precisely because letting them disagree produces duplicate
/// document numbers.
/// </summary>
public enum NumberingPeriod
{
    /// <summary>Never restarts: RCP-000001, RCP-000002 … across all time.</summary>
    Continuous = 0,

    /// <summary>Restarts each calendar year: RCP-2026-000001.</summary>
    Yearly = 1,

    /// <summary>Restarts each month: RCP-2026-03-000001.</summary>
    Monthly = 2
}

/// <summary>
/// Formats receipt, voucher and journal numbers. Numbering itself is allocated inside the same
/// database transaction as the transaction being recorded, which is what makes it concurrency-safe.
/// </summary>
public static class ReceiptNumbering
{
    /// <summary>The key fragment that identifies one series inside a period.</summary>
    public static string PeriodToken(NumberingPeriod period, DateTime date) => period switch
    {
        NumberingPeriod.Yearly => date.ToString("yyyy", CultureInfo.InvariantCulture),
        NumberingPeriod.Monthly => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => "ALL"
    };

    public static string Format(string prefix, long number, int numberWidth, NumberingPeriod period,
        DateTime date)
    {
        string numeric = number.ToString("D" + Math.Clamp(numberWidth, 1, 12), CultureInfo.InvariantCulture);

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            parts.Add(prefix.Trim());
        }

        switch (period)
        {
            case NumberingPeriod.Yearly:
                parts.Add(date.ToString("yyyy", CultureInfo.InvariantCulture));
                break;

            case NumberingPeriod.Monthly:
                parts.Add(date.ToString("yyyy-MM", CultureInfo.InvariantCulture));
                break;
        }

        parts.Add(numeric);
        return string.Join("-", parts);
    }

    /// <summary>Scope key that isolates one numbering series (e.g. RCPT|RCP|2026) from another.</summary>
    public static string Scope(string kind, string prefix, NumberingPeriod period, DateTime date) =>
        $"{kind}|{prefix}|{PeriodToken(period, date)}";
}
