using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BoltFramePlugin.ViewModels;
using BoltFramePlugin.Models.LimitingDistance;
using Autodesk.Revit.DB;

namespace BoltFramePlugin.Views
{
    public partial class LimitingDistanceWindow : Window
    {
        public LimitingDistanceWindow()
        {
            InitializeComponent();

            // Subscribe to Closing event to ensure proper cleanup
            Closing += Window_Closing;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Ensure ViewModel cleanup when window closes
            var viewModel = DataContext as LimitingDistanceWindowVM;
            viewModel?.Cleanup();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get screen working area (excludes taskbar)
            var workingArea = SystemParameters.WorkArea;

            // Set window to half screen width and full screen height
            Width = workingArea.Width / 3;
            Height = workingArea.Height;

            // Position window on the right side of the screen
            Left = workingArea.Left + (workingArea.Width / 3) * 2;
            Top = workingArea.Top;
        }

        private void OccupantGroup_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;
            var comboBox = sender as System.Windows.Controls.ComboBox;
            if (comboBox == null)
            {
                viewModel?.Logger?.LogInformation("OccupantGroup_Loaded: sender is not ComboBox");
                return;
            }

            viewModel?.Logger?.LogInformation("OccupantGroup_Loaded: ComboBox loaded");

            // Get the ViewModel and set ItemsSource directly
            if (viewModel != null)
            {
                comboBox.ItemsSource = viewModel.OccupantGroups;
                viewModel.Logger?.LogInformation($"OccupantGroup_Loaded: Set ItemsSource with {viewModel.OccupantGroups.Count} items: {string.Join(", ", viewModel.OccupantGroups)}");
            }
            else
            {
                // Can't log if viewModel is null
            }

            var wallInfo = comboBox.DataContext as WallInfo;
            viewModel?.Logger?.LogInformation($"OccupantGroup_Loaded: DataContext is {(wallInfo != null ? $"WallInfo (ID: {wallInfo.ElementId?.Value})" : "null or not WallInfo")}");

            if (wallInfo != null)
            {
                viewModel?.Logger?.LogInformation($"OccupantGroup_Loaded: WallInfo.OccupantGroup = '{wallInfo.OccupantGroup}'");

                // Set SelectedItem to match the WallInfo's current value
                comboBox.SelectedItem = wallInfo.OccupantGroup;
                viewModel?.Logger?.LogInformation($"OccupantGroup_Loaded: Set ComboBox.SelectedItem to '{comboBox.SelectedItem}'");

                // Store the WallInfo reference in the Tag for later use
                comboBox.Tag = wallInfo;
            }
        }

        private void OccupantGroup_DropDownClosed(object sender, EventArgs e)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;
            viewModel?.Logger?.LogInformation("OccupantGroup_DropDownClosed: EVENT FIRED!");

            var comboBox = sender as System.Windows.Controls.ComboBox;
            if (comboBox == null)
            {
                viewModel?.Logger?.LogInformation("OccupantGroup_DropDownClosed: sender is not ComboBox");
                return;
            }

            viewModel?.Logger?.LogInformation($"OccupantGroup_DropDownClosed: ComboBox SelectedItem = '{comboBox.SelectedItem}'");

            // Get WallInfo from Tag (set during Loaded event)
            var wallInfo = comboBox.Tag as WallInfo;
            if (wallInfo == null)
            {
                // Fallback to DataContext
                wallInfo = comboBox.DataContext as WallInfo;
            }

            if (wallInfo == null)
            {
                viewModel?.Logger?.LogInformation("OccupantGroup_DropDownClosed: WallInfo is null (Tag and DataContext)");
                return;
            }

            var selectedGroup = comboBox.SelectedItem as string;
            viewModel?.Logger?.LogInformation($"OccupantGroup_DropDownClosed: Wall {wallInfo.ElementId?.Value}, Selected: '{selectedGroup}', Current: '{wallInfo.OccupantGroup}'");

            if (selectedGroup != null)
            {
                viewModel?.Logger?.LogInformation($"OccupantGroup_DropDownClosed: Setting OccupantGroup from '{wallInfo.OccupantGroup}' to '{selectedGroup}'");
                wallInfo.OccupantGroup = selectedGroup;
                viewModel?.Logger?.LogInformation($"OccupantGroup_DropDownClosed: After update, OccupantGroup = '{wallInfo.OccupantGroup}'");
            }
            else
            {
                viewModel?.Logger?.LogInformation("OccupantGroup_DropDownClosed: selectedGroup is null");
            }
        }

        private void OccupantGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;
            viewModel?.Logger?.LogInformation("OccupantGroup_SelectionChanged: EVENT FIRED!");

            var comboBox = sender as System.Windows.Controls.ComboBox;
            if (comboBox == null)
            {
                viewModel?.Logger?.LogInformation("OccupantGroup_SelectionChanged: sender is not ComboBox");
                return;
            }

            viewModel?.Logger?.LogInformation($"OccupantGroup_SelectionChanged: ComboBox SelectedItem = '{comboBox.SelectedItem}'");
        }

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;
            viewModel?.Logger?.LogInformation($"DataGrid_BeginningEdit: Column={e.Column.Header}, Row={e.Row.GetIndex()}");

            var wallInfo = e.Row.Item as WallInfo;
            if (wallInfo != null)
            {
                viewModel?.Logger?.LogInformation($"DataGrid_BeginningEdit: Editing wall {wallInfo.ElementId?.Value}, Current OccupantGroup='{wallInfo.OccupantGroup}'");
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var viewModel = DataContext as LimitingDistanceWindowVM;
            viewModel?.Logger?.LogInformation($"DataGrid_CellEditEnding: Column={e.Column.Header}, EditAction={e.EditAction}");

            var wallInfo = e.Row.Item as WallInfo;
            if (wallInfo != null)
            {
                viewModel?.Logger?.LogInformation($"DataGrid_CellEditEnding: Wall {wallInfo.ElementId?.Value}");

                if (e.Column.Header?.ToString() == "Occupant Group")
                {
                    var comboBox = e.EditingElement as System.Windows.Controls.ComboBox;
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
                var comboBox = FindVisualParent<System.Windows.Controls.ComboBox>(originalSource);
                if (comboBox != null)
                {
                    viewModel?.Logger?.LogInformation($"{eventName}: Click is on ComboBox, ignoring");
                    return; // Let the ComboBox handle the click
                }
            }

            // Get the DataGrid
            var dataGrid = sender as DataGrid;
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

        private void DistanceGroupsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the DataGrid
            var dataGrid = sender as DataGrid;
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
