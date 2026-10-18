using System.Windows;

namespace ShikkhaDesk.Wpf.Views;

/// <summary>Small text prompt, used where a reason or explanation must be captured before an action.</summary>
public partial class PromptWindow : Window
{
    public PromptWindow(string title, string prompt, string label, string? defaultValue = null,
        bool multiline = false)
    {
        InitializeComponent();

        Title = title;
        PromptText.Text = prompt;
        LabelText.Text = label;
        ValueBox.Text = defaultValue ?? string.Empty;
        ValueBox.AcceptsReturn = multiline;
        ValueBox.Focus();
    }

    public string Value => ValueBox.Text.Trim();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Value.Length == 0)
        {
            System.Windows.MessageBox.Show("এই তথ্যটি দিতে হবে।", "শিক্ষা ডেস্ক",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
