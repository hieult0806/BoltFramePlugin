using Autodesk.Revit.DB;
using BoltFramePlugin.Features.LimitingDistance.Models;
using BoltFramePlugin.Features.LimitingDistance.ViewModels;
using BoltFramePlugin.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfDataGrid = System.Windows.Controls.DataGrid;

using BoltFramePlugin.Features.LimitingDistance.ViewModels;
namespace BoltFramePlugin.Features.LimitingDistance.Views.UserControls
{
    /// <summary>
    /// Interaction logic for PerimeterWallsControl.xaml
    /// Displays perimeter walls with editing capabilities and context-sensitive highlighting
    /// </summary>
    public partial class PerimeterWallsControl : System.Windows.Controls.UserControl
    {
        public PerimeterWallsControl()
        {
            InitializeComponent();
        }

        private void DataGrid_BeginningEdit(object sender, System.Windows.Controls.DataGridBeginningEditEventArgs e)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;
            viewModel?.Logger?.LogInformation($"DataGrid_BeginningEdit: Column={e.Column.Header}, Row={e.Row.GetIndex()}");

            var wallInfo = e.Row.Item as WallInfo;
            if (wallInfo != null)
            {
                viewModel?.Logger?.LogInformation($"DataGrid_BeginningEdit: Editing wall {wallInfo.ElementId?.Value}, Current OccupantGroup='{wallInfo.OccupantGroup}'");
            }
        }

        private void DataGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;
            viewModel?.Logger?.LogInformation($"DataGrid_CellEditEnding: Column={e.Column.Header}, EditAction={e.EditAction}");

            var wallInfo = e.Row.Item as WallInfo;
            if (wallInfo != null)
            {
                viewModel?.Logger?.LogInformation($"DataGrid_CellEditEnding: Wall {wallInfo.ElementId?.Value}");

                if (e.Column.Header?.ToString() == "Occupant Group")
                {
                    var comboBox = e.EditingElement as WpfComboBox;
                    if (comboBox != null)
                    {
                        viewModel?.Logger?.LogInformation($"DataGrid_CellEditEnding: ComboBox SelectedItem='{comboBox.SelectedItem}', Current WallInfo.OccupantGroup='{wallInfo.OccupantGroup}'");
                    }
                    else
                    {
                        viewModel?.Logger?.LogInformation($"DataGrid_CellEditEnding: EditingElement is NOT a ComboBox, it's {e.EditingElement?.GetType().Name}");
                    }
                }
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            HandleDataGridClick(sender, e, "DataGrid_MouseDoubleClick");
        }

        private void DataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HandleDataGridClick(sender, e, "DataGrid_MouseLeftButtonUp");
        }

        private void HandleDataGridClick(object sender, MouseButtonEventArgs e, string eventName)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;

            // Check if the click originated from a ComboBox - if so, let it handle the event
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource != null)
            {
                // Walk up the visual tree to see if we're clicking on a ComboBox
                var comboBox = FindVisualParent<WpfComboBox>(originalSource);
                if (comboBox != null)
                {
                    viewModel?.Logger?.LogInformation($"{eventName}: Click is on ComboBox, ignoring");
                    return; // Let the ComboBox handle the click
                }
            }

            // Get the DataGrid
            var dataGrid = sender as WpfDataGrid;
            if (dataGrid == null || dataGrid.SelectedItem == null)
                return;

            // Check if viewModel is available
            if (viewModel == null)
                return;

            // Determine which view is currently active and execute the appropriate command
            try
            {
                var uidoc = viewModel.GetUIDocument();
                if (uidoc?.ActiveView != null)
                {
                    var activeView = uidoc.ActiveView;
                    var viewType = activeView.ViewType;

                    viewModel.Logger?.LogInformation($"{eventName}: Active view type is {viewType}");

                    ICommand commandToExecute;

                    if (viewType == ViewType.ThreeD)
                    {
                        // 3D View - use 3D highlight command
                        commandToExecute = viewModel.HighlightWallIn3DCommand;
                        viewModel.Logger?.LogInformation($"{eventName}: Executing HighlightWallIn3DCommand");
                    }
                    else if (viewType == ViewType.FloorPlan ||
                             viewType == ViewType.CeilingPlan ||
                             viewType == ViewType.AreaPlan ||
                             viewType == ViewType.EngineeringPlan)
                    {
                        // Floor Plan or similar plan views - use floor plan highlight command
                        commandToExecute = viewModel.HighlightWallInFloorPlanCommand;
                        viewModel.Logger?.LogInformation($"{eventName}: Executing HighlightWallInFloorPlanCommand");
                    }
                    else if (viewType == ViewType.Elevation ||
                             viewType == ViewType.Section)
                    {
                        // Elevation or Section views - use floor plan highlight command (same behavior)
                        commandToExecute = viewModel.HighlightWallInFloorPlanCommand;
                        viewModel.Logger?.LogInformation($"{eventName}: Executing HighlightWallInFloorPlanCommand for Elevation/Section view");
                    }
                    else
                    {
                        // Default to 3D view command for other view types
                        commandToExecute = viewModel.HighlightWallIn3DCommand;
                        viewModel.Logger?.LogInformation($"{eventName}: Unknown view type {viewType}, defaulting to HighlightWallIn3DCommand");
                    }

                    // Execute the selected command
                    if (commandToExecute.CanExecute(dataGrid.SelectedItem))
                    {
                        commandToExecute.Execute(dataGrid.SelectedItem);
                    }
                }
                else
                {
                    viewModel.Logger?.LogWarning($"{eventName}: No active view, defaulting to HighlightWallIn3DCommand");
                    // Fallback to 3D command if we can't determine the active view
                    if (viewModel.HighlightWallIn3DCommand.CanExecute(dataGrid.SelectedItem))
                    {
                        viewModel.HighlightWallIn3DCommand.Execute(dataGrid.SelectedItem);
                    }
                }
            }
            catch (Exception ex)
            {
                viewModel?.Logger?.LogError($"{eventName}: Error determining active view: {ex.Message}", ex);
                // Fallback to 3D command if there's an error
                if (viewModel.HighlightWallIn3DCommand.CanExecute(dataGrid.SelectedItem))
                {
                    viewModel.HighlightWallIn3DCommand.Execute(dataGrid.SelectedItem);
                }
            }
        }

        // Helper method to find a parent of a specific type in the visual tree
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }
    }
}
