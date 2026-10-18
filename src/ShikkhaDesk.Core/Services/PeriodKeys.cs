using System.Globalization;

namespace ShikkhaDesk.Core.Services;

public static class PeriodKeys
{
    /// <summary>YYYY-MM key used for monthly fee periods, payroll periods and idempotency.</summary>
    public static string FromDate(DateTime date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static DateTime PeriodStart(string periodKey)
    {
        (int year, int month) = Parse(periodKey);
        return new DateTime(year, month, 1);
    }

    public static DateTime PeriodEnd(string periodKey)
    {
        (int year, int month) = Parse(periodKey);
        return new DateTime(year, month, DateTime.DaysInMonth(year, month));
    }

    public static string AddMonths(string periodKey, int months)
    {
        (int year, int month) = Parse(periodKey);
        DateTime shifted = new DateTime(year, month, 1).AddMonths(months);
        return FromDate(shifted);
    }

    private static (int Year, int Month) Parse(string periodKey)
    {
        string[] parts = periodKey.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int month)
            || month is < 1 or > 12)
        {
            throw new ArgumentException($"'{periodKey}' একটি বৈধ মাসকী কী নয় (YYYY-MM)।", nameof(periodKey));
        }

        return (year, month);
    }
}
