using Autodesk.Revit.UI;
using BoltFramePlugin.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BoltFramePlugin.Views
{
    public partial class DataImportWindow : Window
    {
        public DataImportWindow(UIDocument uidoc)
        {
            InitializeComponent();
            var viewModel = new DataImportWindowVM(uidoc);
            DataContext = viewModel;

            // Subscribe to RequestClose event to close the window
            viewModel.RequestClose += (sender, args) =>
            {
                this.DialogResult = true;
                this.Close();
            };
        }
    }

    /// <summary>
    /// Converter to invert boolean values
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
