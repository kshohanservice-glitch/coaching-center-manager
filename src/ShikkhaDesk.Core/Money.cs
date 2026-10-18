using System.Globalization;

namespace ShikkhaDesk.Core;

/// <summary>
/// Authoritative money value. Stored and calculated exclusively in integer minor units
/// (1 BDT = 100 poisa). Binary floating point is never used as an authoritative amount.
/// </summary>
public readonly struct Money : IComparable<Money>, IEquatable<Money>, IFormattable
{
    public const int DecimalPlaces = 2;

    private static readonly long[] Powers =
    [
        1L, 10L, 100L, 1000L, 10000L, 100000L, 1000000L, 10000000L, 100000000L, 1000000000L
    ];

    public long MinorUnits { get; }

    public static readonly Money Zero = new(0L);

    private Money(long minorUnits) => MinorUnits = minorUnits;

    public static Money FromMinorUnits(long minorUnits) => new(minorUnits);

    /// <summary>Deterministic conversion from decimal: banker's rounding, never silent truncation.</summary>
    public static Money FromDecimal(decimal value, MidpointRounding rounding = MidpointRounding.ToEven) =>
        new((long)Math.Round(value * Powers[DecimalPlaces], 0, rounding));

    public static Money FromMajor(long major) => new(major * Powers[DecimalPlaces]);

    public decimal ToDecimal() => MinorUnits / (decimal)Powers[DecimalPlaces];

    public static Money operator +(Money a, Money b) => new(checked(a.MinorUnits + b.MinorUnits));
    public static Money operator -(Money a, Money b) => new(checked(a.MinorUnits - b.MinorUnits));
    public static Money operator -(Money a) => new(checked(-a.MinorUnits));
    public static Money operator *(Money a, int count) => new(checked(a.MinorUnits * count));

    public static bool operator ==(Money a, Money b) => a.MinorUnits == b.MinorUnits;
    public static bool operator !=(Money a, Money b) => a.MinorUnits != b.MinorUnits;
    public static bool operator <(Money a, Money b) => a.MinorUnits < b.MinorUnits;
    public static bool operator <=(Money a, Money b) => a.MinorUnits <= b.MinorUnits;
    public static bool operator >(Money a, Money b) => a.MinorUnits > b.MinorUnits;
    public static bool operator >=(Money a, Money b) => a.MinorUnits >= b.MinorUnits;

    public bool IsNegative => MinorUnits < 0;
    public bool IsPositive => MinorUnits > 0;
    public bool IsZero => MinorUnits == 0;

    public Money Abs() => new(Math.Abs(MinorUnits));
    public static Money Min(Money a, Money b) => a <= b ? a : b;
    public static Money Max(Money a, Money b) => a >= b ? a : b;

    /// <summary>
    /// Applies a rate expressed in basis points (10,000 = 100%). Rounds half away from zero so the
    /// result is deterministic and reproducible for the same inputs.
    /// </summary>
    public Money ApplyBasisPoints(int basisPoints)
    {
        long product = checked(MinorUnits * basisPoints);
        long quotient = product / 10000L;
        long remainder = Math.Abs(product % 10000L);
        if (remainder * 2 >= 10000L)
        {
            quotient += product < 0 ? -1 : 1;
        }

        return new Money(quotient);
    }

    public Money Percentage(int percent) => ApplyBasisPoints(percent * 100);

    /// <summary>Rounds to the nearest whole currency unit (e.g. nearest Taka).</summary>
    public Money RoundToMajor() => new((long)Math.Round(MinorUnits / (double)Powers[DecimalPlaces],
        MidpointRounding.AwayFromZero) * Powers[DecimalPlaces]);

    /// <summary>
    /// Splits an amount across the supplied weights without losing or creating a single minor unit,
    /// using the largest-remainder method. The result always sums back to <paramref name="total"/>.
    /// </summary>
    public static IReadOnlyList<Money> Allocate(Money total, IReadOnlyList<long> weights)
    {
        if (weights.Count == 0)
        {
            return Array.Empty<Money>();
        }

        long weightSum = 0;
        foreach (long w in weights)
        {
            weightSum += w;
        }

        var result = new Money[weights.Count];
        if (weightSum <= 0)
        {
            result[0] = total;
            return result;
        }

        var remainders = new List<(int Index, long Remainder)>(weights.Count);
        long distributed = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            long exact = checked(total.MinorUnits * weights[i]);
            long quotient = exact / weightSum;
            result[i] = new Money(quotient);
            distributed += quotient;
            remainders.Add((i, Math.Abs(exact % weightSum)));
        }

        long outstanding = total.MinorUnits - distributed;
        if (outstanding != 0)
        {
            int sign = Math.Sign(outstanding);
            remainders.Sort((a, b) =>
            {
                int byRemainder = b.Remainder.CompareTo(a.Remainder);
                return byRemainder != 0 ? byRemainder : a.Index.CompareTo(b.Index);
            });
            int steps = (int)Math.Min(Math.Abs(outstanding), remainders.Count);
            for (int k = 0; k < steps; k++)
            {
                int index = remainders[k].Index;
                result[index] = new Money(result[index].MinorUnits + sign);
            }
        }

        return result;
    }

    public int CompareTo(Money other) => MinorUnits.CompareTo(other.MinorUnits);
    public bool Equals(Money other) => MinorUnits == other.MinorUnits;
    public override bool Equals(object? obj) => obj is Money other && Equals(other);
    public override int GetHashCode() => MinorUnits.GetHashCode();

    public override string ToString() => ToString("N", CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        ToDecimal().ToString(format ?? "N", formatProvider ?? CultureInfo.InvariantCulture);

    /// <summary>Parses user input, accepting Bengali or ASCII digits, currency symbols and separators.</summary>
    public static bool TryParse(string? text, out Money value)
    {
        value = Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = Digits.ToAsciiDigits(text.Trim())
            .Replace(",", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("৳", string.Empty)
            .Replace("Tk", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (normalized.StartsWith('(') && normalized.EndsWith(')'))
        {
            normalized = "-" + normalized[1..^1];
        }

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
        {
            return false;
        }

        value = FromDecimal(parsed);
        return true;
    }

    public static Money Parse(string? text)
    {
        if (!TryParse(text, out Money value))
        {
            throw new FormatException($"'{text}' একটি বৈধ টাকার পরিমাণ নয়।");
        }

        return value;
    }
}
