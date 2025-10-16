using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BoltFramePlugin.ViewModels;

namespace BoltFramePlugin.Views
{
    public partial class LimitingDistanceWindow : Window
    {
        public LimitingDistanceWindow()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the DataGrid
            var dataGrid = sender as DataGrid;
            if (dataGrid == null || dataGrid.SelectedItem == null)
                return;

            // Get the ViewModel
            var viewModel = DataContext as LimitingDistanceWindowVM;
            if (viewModel == null)
                return;

            // Execute the Highlight in 3D command with the selected wall
            if (viewModel.HighlightWallIn3DCommand.CanExecute(dataGrid.SelectedItem))
            {
                viewModel.HighlightWallIn3DCommand.Execute(dataGrid.SelectedItem);
            }
        }
    }
}
