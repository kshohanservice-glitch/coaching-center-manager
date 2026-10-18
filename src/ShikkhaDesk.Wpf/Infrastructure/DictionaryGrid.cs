using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// Builds DataGrid columns at run time for reports whose shape is only known when they are generated.
/// Each column binds to the matching key of a row dictionary.
/// </summary>
public static class DictionaryGrid
{
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.RegisterAttached("Columns", typeof(IEnumerable<string>),
            typeof(DictionaryGrid), new PropertyMetadata(null, OnColumnsChanged));

    public static void SetColumns(DependencyObject element, IEnumerable<string> value) =>
        element.SetValue(ColumnsProperty, value);

    public static IEnumerable<string>? GetColumns(DependencyObject element) =>
        (IEnumerable<string>?)element.GetValue(ColumnsProperty);

    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
        {
            return;
        }

        grid.Columns.Clear();

        if (e.NewValue is not IEnumerable<string> headers)
        {
            return;
        }

        foreach (string header in headers)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = new Binding($"[{header}]")
                {
                    // A report row may not carry every column; a missing value is shown blank.
                    FallbackValue = "—",
                    TargetNullValue = "—"
                }
            });
        }
    }
}
