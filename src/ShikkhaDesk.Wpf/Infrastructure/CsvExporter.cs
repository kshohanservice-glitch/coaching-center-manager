using System.Globalization;
using System.Text;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// CSV export used by every report grid. Exports read the same in-memory rows the grid shows, so an
/// export can never disagree with the on-screen report.
/// </summary>
public static class CsvExporter
{
    public static string Build(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var builder = new StringBuilder();

        // The BOM lets Excel open Bengali text correctly.
        builder.Append('\uFEFF');
        AppendLine(builder, headers);

        foreach (IEnumerable<object?> row in rows)
        {
            AppendLine(builder, row.Select(Cell));
        }

        return builder.ToString();
    }

    public static string BuildFromObjects<T>(IEnumerable<T> items, params (string Header, Func<T, object?> Selector)[] columns)
    {
        return Build(columns.Select(c => c.Header), items.Select(item => columns.Select(c => c.Selector(item))));
    }

    private static string Cell(object? value) => value switch
    {
        null => string.Empty,
        string text => Quote(text),
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => Quote(formattable.ToString(null, CultureInfo.InvariantCulture)),
        _ => Quote(value.ToString() ?? string.Empty)
    };

    private static string Quote(string text)
    {
        string value = text.Replace("\r", " ").Replace("\n", " ");
        return value.Contains('"') || value.Contains(',') || value.Contains(';')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static void AppendLine(StringBuilder builder, IEnumerable<string?> cells) =>
        builder.Append(string.Join(",", cells.Select(c => c ?? string.Empty))).Append("\r\n");
}
