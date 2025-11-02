using System.Windows.Input;
using BoltFramePlugin.ViewModels;

using BoltFramePlugin.Features.LimitingDistance.ViewModels;
namespace BoltFramePlugin.Features.LimitingDistance.Views.UserControls
{
    /// <summary>
    /// Interaction logic for DistanceGroupsSummaryControl.xaml
    /// Displays distance groups summary with highlighting functionality
    /// </summary>
    public partial class DistanceGroupsSummaryControl : System.Windows.Controls.UserControl
    {
        public DistanceGroupsSummaryControl()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the DataGrid
            var dataGrid = sender as System.Windows.Controls.DataGrid;
            if (dataGrid == null || dataGrid.SelectedItem == null)
                return;

            // Get the ViewModel
            var viewModel = DataContext as LimitingDistanceWindowVM;
            if (viewModel == null)
                return;

            // Execute the Highlight Group command with the selected distance group
            if (viewModel.HighlightDistanceGroupCommand.CanExecute(dataGrid.SelectedItem))
            {
                viewModel.HighlightDistanceGroupCommand.Execute(dataGrid.SelectedItem);
            }
        }
    }
}
