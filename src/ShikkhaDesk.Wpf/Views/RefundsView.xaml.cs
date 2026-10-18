using System.Windows.Controls;
using ShikkhaDesk.Wpf.Infrastructure;
using ShikkhaDesk.Wpf.ViewModels;

namespace ShikkhaDesk.Wpf.Views;

public partial class RefundsView : UserControl
{
    public RefundsView()
    {
        InitializeComponent();
    }

    private async void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is RefundsViewModel { SelectedSearch: not null } model)
        {
            await model.SelectCommand.ExecuteAsync(null);
        }
    }
}
