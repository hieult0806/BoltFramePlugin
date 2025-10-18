using System.Windows.Input;
using BoltFramePlugin.ViewModels;

namespace BoltFramePlugin.Views
{
    public partial class ViewTrackerControl : System.Windows.Controls.UserControl
    {
        public ViewTrackerControl()
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

            // Execute the Open View command
            if (viewModel.OpenViewCommand.CanExecute(dataGrid.SelectedItem))
            {
                viewModel.OpenViewCommand.Execute(dataGrid.SelectedItem);
            }
        }
    }
}
