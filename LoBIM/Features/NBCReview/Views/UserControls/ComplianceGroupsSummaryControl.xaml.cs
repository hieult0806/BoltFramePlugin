using System.Windows.Input;
using LoBIM.ViewModels;

using LoBIM.Features.NBCReview.ViewModels;
namespace LoBIM.Features.NBCReview.Views.UserControls
{
    /// <summary>
    /// Interaction logic for ComplianceGroupsSummaryControl.xaml
    /// Displays compliance groups summary with highlighting functionality
    /// </summary>
    public partial class ComplianceGroupsSummaryControl : System.Windows.Controls.UserControl
    {
        public ComplianceGroupsSummaryControl()
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
            var viewModel = DataContext as NBCReviewWindowVM;
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
