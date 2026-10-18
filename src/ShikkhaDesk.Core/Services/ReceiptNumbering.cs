using System.Globalization;

namespace ShikkhaDesk.Core.Services;

/// <summary>
/// Formats receipt, voucher and journal numbers. Numbering itself is allocated inside the same
/// database transaction as the transaction being recorded, which is what makes it concurrency-safe.
/// </summary>
public static class ReceiptNumbering
{
    public static string Format(string prefix, long number, int numberWidth, bool includeYear, DateTime date)
    {
        string numeric = number.ToString("D" + Math.Clamp(numberWidth, 1, 12), CultureInfo.InvariantCulture);
        string year = date.ToString("yyyy", CultureInfo.InvariantCulture);

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            parts.Add(prefix.Trim());
        }

        if (includeYear)
        {
            parts.Add(year);
        }

        parts.Add(numeric);
        return string.Join("-", parts);
    }

    /// <summary>Scope key that isolates one numbering series (e.g. RCP-2026) from another.</summary>
    public static string Scope(string kind, string prefix, bool includeYear, DateTime date)
    {
        string year = includeYear ? date.ToString("yyyy", CultureInfo.InvariantCulture) : "ALL";
        return $"{kind}|{prefix}|{year}";
    }
}
