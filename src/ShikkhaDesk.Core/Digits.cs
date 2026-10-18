using System.Globalization;
using System.Text;

namespace ShikkhaDesk.Core;

/// <summary>
/// Bengali/ASCII digit conversion. Stored numeric values are invariant; digit preference is a
/// presentation-layer concern only and is never applied to persisted data.
/// </summary>
public static class Digits
{
    private const string BengaliDigits = "০১২৩৪৫৬৭৮৯";

    public static string ToBengaliDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(c is >= '0' and <= '9' ? BengaliDigits[c - '0'] : c);
        }

        return builder.ToString();
    }

    public static string ToAsciiDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (c >= '০' && c <= '৯')
            {
                builder.Append((char)('0' + (c - '০')));
            }
            else if (c >= '٠' && c <= '٩')
            {
                builder.Append((char)('0' + (c - '٠')));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    public static string Format(decimal value, int decimals, bool useBengaliDigits)
    {
        string formatted = value.ToString("N" + decimals, CultureInfo.InvariantCulture);
        return useBengaliDigits ? ToBengaliDigits(formatted) : formatted;
    }

    public static string Format(Money value, bool useBengaliDigits, string? currency = null)
    {
        string formatted = value.ToDecimal().ToString("N2", CultureInfo.InvariantCulture);
        string withCurrency = currency is null ? formatted : currency + " " + formatted;
        return useBengaliDigits ? ToBengaliDigits(withCurrency) : withCurrency;
    }

    public static string Format(long value, bool useBengaliDigits) =>
        useBengaliDigits ? ToBengaliDigits(value.ToString(CultureInfo.InvariantCulture))
                         : value.ToString(CultureInfo.InvariantCulture);

    public static string Format(DateTime value, bool useBengaliDigits, string format = "dd-MM-yyyy") =>
        useBengaliDigits ? ToBengaliDigits(value.ToString(format, CultureInfo.InvariantCulture))
                         : value.ToString(format, CultureInfo.InvariantCulture);
}
