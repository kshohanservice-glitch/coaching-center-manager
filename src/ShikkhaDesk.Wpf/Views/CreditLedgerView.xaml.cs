using System.Windows.Controls;
using ShikkhaDesk.Wpf.Infrastructure;
using ShikkhaDesk.Wpf.ViewModels;

namespace ShikkhaDesk.Wpf.Views;

public partial class CreditLedgerView : UserControl
{
    public CreditLedgerView()
    {
        InitializeComponent();
    }

    private async void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CreditLedgerViewModel { SelectedSearch: not null } model)
        {
            await model.SelectCommand.ExecuteAsync(null);
        }
    }
}
