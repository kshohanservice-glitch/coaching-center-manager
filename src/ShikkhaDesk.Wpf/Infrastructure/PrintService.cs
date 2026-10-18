using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ShikkhaDesk.Application.Dto;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// Printing. Receipts and reports are built as flow documents so the operator can print to any
/// installed printer, to PDF, or cancel without losing data.
/// </summary>
public interface IPrintService
{
    void PrintReceipt(ReceiptData receipt, bool showDialog = true);
    void PrintReport(string title, string subtitle, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows, bool landscape = false);
}

public sealed class PrintService : IPrintService
{
    private const double ReceiptWidth = 320;

    public void PrintReceipt(ReceiptData receipt, bool showDialog = true)
    {
        var document = new FlowDocument
        {
            PageWidth = ReceiptWidth,
            PageHeight = 900,
            PagePadding = new Thickness(18),
            ColumnWidth = ReceiptWidth,
            FontFamily = new FontFamily("Nirmala UI, Segoe UI, Arial"),
            FontSize = 11,
            Foreground = Brushes.Black
        };

        var body = new Section();
        body.Blocks.Add(Centered(receipt.InstitutionName, 15, FontWeights.Bold));

        if (!string.IsNullOrWhiteSpace(receipt.InstitutionAddress))
        {
            body.Blocks.Add(Centered(receipt.InstitutionAddress!, 10));
        }

        if (!string.IsNullOrWhiteSpace(receipt.InstitutionPhone))
        {
            body.Blocks.Add(Centered("ফোন: " + receipt.InstitutionPhone, 10));
        }

        body.Blocks.Add(Centered(receipt.IsReprint ? "রশিদ (পুনর্মুদ্রণ)" : "রশিদ", 12, FontWeights.Bold));
        body.Blocks.Add(Separator());

        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = GridLength.Auto });

        var group = new TableRowGroup();
        group.Rows.Add(Row("রশিদ নং", receipt.ReceiptNumber));
        group.Rows.Add(Row("তারিখ", receipt.PaymentDate.ToString("dd-MM-yyyy HH:mm",
            CultureInfo.InvariantCulture)));
        group.Rows.Add(Row("শিক্ষার্থী", $"{receipt.StudentName} ({receipt.StudentCode})"));

        if (!string.IsNullOrWhiteSpace(receipt.ClassOrBatch))
        {
            group.Rows.Add(Row("ব্যাচ", receipt.ClassOrBatch!));
        }

        table.RowGroups.Add(group);
        body.Blocks.Add(table);
        body.Blocks.Add(Separator());

        var lines = new Table { CellSpacing = 0 };
        lines.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        lines.Columns.Add(new TableColumn { Width = GridLength.Auto });

        var lineGroup = new TableRowGroup();
        foreach (ReceiptLine line in receipt.Lines)
        {
            string label = line.PeriodKey is null ? line.Description : $"{line.Description} ({line.PeriodKey})";
            lineGroup.Rows.Add(Row(label, Money(line.Amount, receipt.CurrencySymbol)));
        }

        lines.RowGroups.Add(lineGroup);
        body.Blocks.Add(lines);
        body.Blocks.Add(Separator());

        var totals = new Table { CellSpacing = 0 };
        totals.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        totals.Columns.Add(new TableColumn { Width = GridLength.Auto });

        var totalGroup = new TableRowGroup();
        totalGroup.Rows.Add(Row("পূর্ববর্তী বকেয়া", Money(receipt.PreviousOutstanding, receipt.CurrencySymbol)));
        totalGroup.Rows.Add(Row("এই পেমেন্টে বরাদ্দ", Money(receipt.AmountAllocated, receipt.CurrencySymbol)));
        totalGroup.Rows.Add(Row("অগ্রিম ক্রেডিট", Money(receipt.AdvanceCredit, receipt.CurrencySymbol)));
        totalGroup.Rows.Add(Row("অবশিষ্ট বকেয়া", Money(receipt.RemainingOutstanding, receipt.CurrencySymbol)));
        totalGroup.Rows.Add(Row("মোট প্রদান", Money(receipt.AmountReceived, receipt.CurrencySymbol), true));
        totals.RowGroups.Add(totalGroup);
        body.Blocks.Add(totals);

        body.Blocks.Add(Separator());
        body.Blocks.Add(new Paragraph(new Run("পদ্ধতি: " + receipt.PaymentMethod
            + (string.IsNullOrWhiteSpace(receipt.TransactionReference)
                ? string.Empty
                : " · রেফারেন্স: " + receipt.TransactionReference)))
        {
            FontSize = 10
        });
        body.Blocks.Add(new Paragraph(new Run("আদায়কারী: " + receipt.CollectedBy)) { FontSize = 10 });

        if (receipt.ReprintCount > 0)
        {
            body.Blocks.Add(new Paragraph(new Run($"পুনর্মুদ্রণ সংখ্যা: {receipt.ReprintCount}"))
            {
                FontSize = 9,
                Foreground = Brushes.Gray
            });
        }

        body.Blocks.Add(Centered("ধন্যবাদ", 10));
        document.Blocks.Add(body);

        Print(document, "রশিদ " + receipt.ReceiptNumber, showDialog, ReceiptWidth);
    }

    public void PrintReport(string title, string subtitle, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows, bool landscape = false)
    {
        double width = landscape ? 1122 : 793;

        var document = new FlowDocument
        {
            PageWidth = width,
            PageHeight = landscape ? 793 : 1122,
            PagePadding = new Thickness(42),
            ColumnWidth = width - 84,
            FontFamily = new FontFamily("Nirmala UI, Segoe UI, Arial"),
            FontSize = 11
        };

        document.Blocks.Add(new Paragraph(new Run(title)) { FontSize = 18, FontWeight = FontWeights.Bold });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            document.Blocks.Add(new Paragraph(new Run(subtitle)) { FontSize = 11, Foreground = Brushes.Gray });
        }

        document.Blocks.Add(new Paragraph(new Run("মুদ্রিত: "
            + DateTime.Now.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)))
        {
            FontSize = 10,
            Foreground = Brushes.Gray
        });

        var table = new Table { CellSpacing = 0 };
        foreach (string _ in headers)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        }

        var header = new TableRowGroup();
        var headerRow = new TableRow();
        foreach (string column in headers)
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(column)) { FontWeight = FontWeights.Bold })
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4)
            });
        }

        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (IReadOnlyList<string> row in rows)
        {
            var tableRow = new TableRow();
            foreach (string cell in row)
            {
                tableRow.Cells.Add(new TableCell(new Paragraph(new Run(cell)))
                {
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                });
            }

            body.Rows.Add(tableRow);
        }

        table.RowGroups.Add(body);
        document.Blocks.Add(table);

        Print(document, title, true, width);
    }

    private static void Print(FlowDocument document, string description, bool showDialog, double width)
    {
        var dialog = new PrintDialog();

        if (showDialog && dialog.ShowDialog() != true)
        {
            return;
        }

        if (!showDialog)
        {
            dialog.PrintQueue = System.Printing.LocalPrintServer.GetDefaultPrintQueue();
        }

        IDocumentPaginatorSource source = document;
        dialog.PrintDocument(source.DocumentPaginator, description);
        _ = width;
    }

    private static Paragraph Centered(string text, double size, FontWeight? weight = null) =>
        new(new Run(text))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0, 1, 0, 1)
        };

    private static Block Separator() => new Paragraph(new Run(new string('─', 46)))
    {
        TextAlignment = TextAlignment.Center,
        FontSize = 9,
        Margin = new Thickness(0, 4, 0, 4)
    };

    private static TableRow Row(string label, string value, bool bold = false) => new()
    {
        Cells =
        {
            new TableCell(new Paragraph(new Run(label)))
            {
                Padding = new Thickness(2, 1, 2, 1)
            },
            new TableCell(new Paragraph(new Run(value)) { TextAlignment = TextAlignment.Right })
            {
                Padding = new Thickness(2, 1, 2, 1),
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            }
        }
    };

    private static string Money(Core.Money value, string symbol) =>
        symbol + " " + value.ToDecimal().ToString("N2", CultureInfo.InvariantCulture);
}
