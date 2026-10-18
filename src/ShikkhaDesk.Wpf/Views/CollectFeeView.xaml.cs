using System.Windows.Controls;
using ShikkhaDesk.Wpf.Infrastructure;
using ShikkhaDesk.Wpf.ViewModels;

namespace ShikkhaDesk.Wpf.Views;

public partial class CollectFeeView : UserControl
{
    public CollectFeeView()
    {
        InitializeComponent();
    }

    /// <summary>Selecting a row in the search grid loads that student's dues straight away.</summary>
    private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CollectFeeViewModel { SelectedSearch: not null } model)
        {
            await model.SelectCommand.ExecuteAsync(null);
        }
    }
}
