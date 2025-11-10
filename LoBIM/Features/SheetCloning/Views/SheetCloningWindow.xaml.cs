using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using LoBIM.Features.SheetCloning.Models;
using LoBIM.Features.SheetCloning.ViewModels;

namespace LoBIM.Features.SheetCloning.Views
{
    public partial class SheetCloningWindow : Window
    {
        private readonly SheetCloningWindowVM _viewModel;

        public SheetCloningWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // Set Revit as owner
            var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindow != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                helper.Owner = revitWindow;
            }

            // Initialize ViewModel
            _viewModel = new SheetCloningWindowVM(uidoc);
            DataContext = _viewModel;

            // Subscribe to RequestClose event
            _viewModel.RequestClose += OnRequestClose;

            // Focus back to Revit when window closes
            Closed += (s, e) =>
            {
                if (revitWindow != IntPtr.Zero)
                {
                    SetForegroundWindow(revitWindow);
                }
            };
        }

        private void OnRequestClose(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handle single click on checkbox cell to toggle checkbox
        /// </summary>
        private void SheetsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
                if (cell.Column.DisplayIndex == 0 && cell.DataContext is LinkedSheetInfo sheetInfo)
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
                        sheetInfo.IsSelected = !sheetInfo.IsSelected;
                        e.Handled = true; // Prevent default selection behavior
                    }
                }
            }
        }

        /// <summary>
        /// Handle double-click on DataGrid row to open the sheet
        /// </summary>
        private void SheetsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is SheetCloningWindowVM viewModel)
            {
                viewModel.OpenSelectedSheet();
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
