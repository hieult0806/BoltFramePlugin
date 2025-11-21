using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.ViewModels;

namespace LoBIM.Features.ViewCloning.Views
{
    public partial class ViewCloningWindow : Window
    {
        public ViewCloningWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // Set Revit as the owner window so this window stays above Revit
            // but not always on top of everything
            var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindow != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                helper.Owner = revitWindow;
            }

            var viewModel = new ViewCloningWindowVM(uidoc);
            DataContext = viewModel;

            // Subscribe to RequestClose event
            viewModel.RequestClose += OnRequestClose;
        }

        private void OnRequestClose(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handle single click on checkbox cell to toggle checkbox
        /// </summary>
        private void ViewsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null) return;

            // Find the clicked cell
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is DataGridCell))
            {
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridCell cell && cell.Column is DataGridTemplateColumn)
            {
                // Check if this is the checkbox column (first column)
                if (cell.Column.DisplayIndex == 0 && cell.DataContext is LinkedViewInfo viewInfo)
                {
                    // Check if the click was directly on the checkbox itself
                    var clickedElement = e.OriginalSource as DependencyObject;
                    var isCheckBox = false;

                    while (clickedElement != null)
                    {
                        if (clickedElement is System.Windows.Controls.CheckBox)
                        {
                            isCheckBox = true;
                            break;
                        }
                        clickedElement = System.Windows.Media.VisualTreeHelper.GetParent(clickedElement);
                    }

                    // If clicking on the cell but not directly on checkbox, toggle it
                    if (!isCheckBox)
                    {
                        viewInfo.IsSelected = !viewInfo.IsSelected;
                        e.Handled = true; // Prevent default selection behavior
                    }
                }
            }
        }

        /// <summary>
        /// Handle double-click on DataGrid row to open the view
        /// </summary>
        private void ViewsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewCloningWindowVM viewModel)
            {
                viewModel.OpenSelectedView();
            }
        }
    }
}
