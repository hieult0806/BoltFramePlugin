using LoBIM.Features.SheetManagement.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LoBIM.Features.SheetManagement.Views
{
    public partial class JumpToPositionDialog : Window
    {
        public enum JumpPosition
        {
            First,
            Last,
            Before,
            After
        }

        public JumpPosition SelectedPosition { get; private set; }
        public SheetItemModel TargetSheet { get; private set; }

        private SheetItemModel _currentSheet;

        public JumpToPositionDialog(SheetItemModel currentSheet, List<SheetItemModel> availableSheets)
        {
            InitializeComponent();

            _currentSheet = currentSheet;

            // Set current sheet info
            SelectedSheetNumber.Text = currentSheet.SheetNumber;
            SelectedSheetName.Text = currentSheet.SheetName;

            // Populate combo boxes with all sheets except current one
            var otherSheets = availableSheets.Where(s => s.ElementId != currentSheet.ElementId).ToList();
            ComboBeforeSheet.ItemsSource = otherSheets;
            ComboAfterSheet.ItemsSource = otherSheets;

            if (otherSheets.Any())
            {
                ComboBeforeSheet.SelectedIndex = 0;
                ComboAfterSheet.SelectedIndex = 0;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Determine which position was selected
            if (RadioFirst.IsChecked == true)
            {
                SelectedPosition = JumpPosition.First;
            }
            else if (RadioLast.IsChecked == true)
            {
                SelectedPosition = JumpPosition.Last;
            }
            else if (RadioBefore.IsChecked == true)
            {
                SelectedPosition = JumpPosition.Before;
                TargetSheet = ComboBeforeSheet.SelectedItem as SheetItemModel;

                if (TargetSheet == null)
                {
                    System.Windows.MessageBox.Show("Please select a sheet to move before.", "Selection Required",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }
            else if (RadioAfter.IsChecked == true)
            {
                SelectedPosition = JumpPosition.After;
                TargetSheet = ComboAfterSheet.SelectedItem as SheetItemModel;

                if (TargetSheet == null)
                {
                    System.Windows.MessageBox.Show("Please select a sheet to move after.", "Selection Required",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
