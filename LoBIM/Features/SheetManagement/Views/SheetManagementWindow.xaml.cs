using Autodesk.Revit.UI;
using LoBIM.Features.SheetManagement.Models;
using LoBIM.Features.SheetManagement.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LoBIM.Features.SheetManagement.Views
{
    public partial class SheetManagementWindow : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isDragging;
        private Window _dragGhostWindow;

        public SheetManagementWindow(UIDocument document)
        {
            InitializeComponent();
            DataContext = new SheetManagementWindowVM(document);

            // Subscribe to window closing event
            Closing += (s, e) =>
            {
                if (DataContext is SheetManagementWindowVM vm)
                {
                    vm.RequestClose -= OnRequestClose;
                }
            };

            if (DataContext is SheetManagementWindowVM viewModel)
            {
                viewModel.RequestClose += OnRequestClose;
            }
        }

        private void OnRequestClose(object sender, EventArgs e)
        {
            Close();
        }

        private void CreateDragGhost(SheetItemModel sheet, System.Windows.Controls.ListBoxItem sourceItem)
        {
            // Create a visual representation of the dragged item
            var ghostContent = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 227, 242, 253)), // Semi-transparent blue
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                MinWidth = 300,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    BlurRadius = 15,
                    Opacity = 0.4,
                    ShadowDepth = 5
                },
                Child = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = sheet.SheetNumber,
                            FontWeight = FontWeights.Bold,
                            FontSize = 14,
                            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
                            Margin = new Thickness(0, 0, 10, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = sheet.SheetName,
                            FontSize = 13,
                            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)),
                            VerticalAlignment = VerticalAlignment.Center,
                            MaxWidth = 250,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }
                    }
                }
            };

            // Create a transparent window to host the ghost
            _dragGhostWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                Content = ghostContent,
                IsHitTestVisible = false
            };

            // Position the ghost at the cursor
            UpdateDragGhostPosition();
            _dragGhostWindow.Show();
        }

        private void UpdateDragGhostPosition()
        {
            if (_dragGhostWindow != null && _dragGhostWindow.IsVisible)
            {
                var cursorPos = GetCursorPos();
                // Offset slightly so cursor doesn't obscure the preview
                _dragGhostWindow.Left = cursorPos.X + 10;
                _dragGhostWindow.Top = cursorPos.Y + 10;
            }
        }

        private void CloseDragGhost()
        {
            if (_dragGhostWindow != null)
            {
                _dragGhostWindow.Close();
                _dragGhostWindow = null;
            }
        }

        private System.Windows.Point GetCursorPos()
        {
            Win32Point w32Point = new Win32Point();
            GetCursorPos(ref w32Point);
            return new System.Windows.Point(w32Point.X, w32Point.Y);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref Win32Point lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        #region Drag and Drop Implementation

        private void SheetListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void SheetListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && !_isDragging)
            {
                System.Windows.Point currentPosition = e.GetPosition(null);
                System.Windows.Vector diff = _startPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    System.Windows.Controls.ListBox listBox = sender as System.Windows.Controls.ListBox;
                    System.Windows.Controls.ListBoxItem listBoxItem = FindAncestor<System.Windows.Controls.ListBoxItem>((DependencyObject)e.OriginalSource);

                    if (listBoxItem != null && listBox != null)
                    {
                        SheetItemModel sheet = (SheetItemModel)listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem);
                        if (sheet != null)
                        {
                            _isDragging = true;

                            // Apply visual feedback: reduce opacity of the item being dragged
                            double originalOpacity = listBoxItem.Opacity;
                            listBoxItem.Opacity = 0.3;

                            // Create the drag ghost window
                            CreateDragGhost(sheet, listBoxItem);

                            try
                            {
                                System.Windows.DragDropEffects dragEffect = System.Windows.DragDrop.DoDragDrop(
                                    listBoxItem,
                                    sheet,
                                    System.Windows.DragDropEffects.Move);
                            }
                            finally
                            {
                                // Close the ghost window
                                CloseDragGhost();

                                // Restore original opacity
                                listBoxItem.Opacity = originalOpacity;
                                _isDragging = false;
                            }
                        }
                    }
                }
            }
        }

        private void SheetListBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            // Hide the drop indicator
            HideDropIndicator();

            if (e.Data.GetDataPresent(typeof(SheetItemModel)))
            {
                SheetItemModel droppedSheet = e.Data.GetData(typeof(SheetItemModel)) as SheetItemModel;
                SheetItemModel targetSheet = GetSheetFromPoint(e.GetPosition(SheetListBox));

                if (droppedSheet != null && targetSheet != null && droppedSheet != targetSheet)
                {
                    var viewModel = DataContext as SheetManagementWindowVM;
                    if (viewModel != null)
                    {
                        int oldIndex = viewModel.FilteredSheets.IndexOf(droppedSheet);
                        int newIndex = viewModel.FilteredSheets.IndexOf(targetSheet);

                        if (oldIndex != -1 && newIndex != -1)
                        {
                            viewModel.MoveSheet(oldIndex, newIndex);
                        }
                    }
                }
            }
        }

        private void SheetListBox_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(SheetItemModel)))
            {
                e.Effects = System.Windows.DragDropEffects.Move;

                // Update ghost position to follow cursor
                UpdateDragGhostPosition();

                // Show drop indicator at the target position
                ShowDropIndicator(e.GetPosition(SheetListBox));
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
                HideDropIndicator();
            }
            e.Handled = true;
        }

        private void ShowDropIndicator(System.Windows.Point position)
        {
            var targetSheet = GetSheetFromPoint(position);
            if (targetSheet != null)
            {
                var viewModel = DataContext as SheetManagementWindowVM;
                if (viewModel != null)
                {
                    int targetIndex = viewModel.FilteredSheets.IndexOf(targetSheet);
                    if (targetIndex >= 0)
                    {
                        var container = SheetListBox.ItemContainerGenerator.ContainerFromIndex(targetIndex)
                            as System.Windows.Controls.ListBoxItem;

                        if (container != null)
                        {
                            // Get the position relative to the container
                            var posInListBox = container.TranslatePoint(new System.Windows.Point(0, 0), SheetListBox);
                            var containerHeight = container.ActualHeight;
                            var relativeY = position.Y - posInListBox.Y;

                            // Determine if we should insert before or after this item
                            bool insertBefore = relativeY < containerHeight / 2;

                            // Calculate the Y position for the indicator
                            double indicatorY = insertBefore ? posInListBox.Y - 2 : posInListBox.Y + containerHeight - 1;

                            // Set the indicator position and size
                            Canvas.SetTop(DropIndicatorLine, indicatorY);
                            DropIndicatorLine.Width = SheetListBox.ActualWidth - 20; // Leave some margin
                            DropIndicatorLine.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            else
            {
                HideDropIndicator();
            }
        }

        private void HideDropIndicator()
        {
            DropIndicatorLine.Visibility = Visibility.Collapsed;
        }

        private void SheetListBox_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            HideDropIndicator();
        }

        private SheetItemModel GetSheetFromPoint(System.Windows.Point point)
        {
            UIElement element = SheetListBox.InputHitTest(point) as UIElement;
            if (element != null)
            {
                object data = DependencyProperty.UnsetValue;
                while (data == DependencyProperty.UnsetValue)
                {
                    data = SheetListBox.ItemContainerGenerator.ItemFromContainer(element);

                    if (data == DependencyProperty.UnsetValue)
                    {
                        element = System.Windows.Media.VisualTreeHelper.GetParent(element) as UIElement;
                    }

                    if (element == SheetListBox)
                    {
                        return null;
                    }
                }

                if (data != DependencyProperty.UnsetValue)
                {
                    return data as SheetItemModel;
                }
            }

            return null;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        #endregion

        #region Jump to Position

        private void MenuItem_Jump_Click(object sender, RoutedEventArgs e)
        {
            ShowJumpToPositionDialog();
        }

        private void SheetListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Only handle double-click on items, not on empty space
            var item = GetSheetFromPoint(e.GetPosition(SheetListBox));
            if (item != null)
            {
                ShowJumpToPositionDialog();
            }
        }

        private void ShowJumpToPositionDialog()
        {
            var selectedSheet = SheetListBox.SelectedItem as SheetItemModel;
            if (selectedSheet == null)
            {
                System.Windows.MessageBox.Show("Please select a sheet first.", "No Selection",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var viewModel = DataContext as SheetManagementWindowVM;
            if (viewModel == null) return;

            var dialog = new JumpToPositionDialog(selectedSheet, viewModel.FilteredSheets.ToList());
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Get the current index
                int currentIndex = viewModel.FilteredSheets.IndexOf(selectedSheet);
                if (currentIndex == -1) return;

                int newIndex = -1;

                // Calculate new index based on selected position
                switch (dialog.SelectedPosition)
                {
                    case JumpToPositionDialog.JumpPosition.First:
                        newIndex = 0;
                        break;

                    case JumpToPositionDialog.JumpPosition.Last:
                        newIndex = viewModel.FilteredSheets.Count - 1;
                        break;

                    case JumpToPositionDialog.JumpPosition.Before:
                        if (dialog.TargetSheet != null)
                        {
                            int targetIndex = viewModel.FilteredSheets.IndexOf(dialog.TargetSheet);
                            if (targetIndex > currentIndex)
                            {
                                // Moving down, so target position shifts
                                newIndex = targetIndex - 1;
                            }
                            else
                            {
                                newIndex = targetIndex;
                            }
                        }
                        break;

                    case JumpToPositionDialog.JumpPosition.After:
                        if (dialog.TargetSheet != null)
                        {
                            int targetIndex = viewModel.FilteredSheets.IndexOf(dialog.TargetSheet);
                            if (targetIndex < currentIndex)
                            {
                                // Moving up, so target position is right after
                                newIndex = targetIndex + 1;
                            }
                            else
                            {
                                newIndex = targetIndex;
                            }
                        }
                        break;
                }

                // Move the sheet
                if (newIndex >= 0 && newIndex != currentIndex)
                {
                    viewModel.MoveSheet(currentIndex, newIndex);

                    // Select the moved item
                    SheetListBox.SelectedIndex = newIndex;
                    SheetListBox.ScrollIntoView(selectedSheet);
                }
            }
        }

        #endregion
    }
}
