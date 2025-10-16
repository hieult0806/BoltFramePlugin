using System.Windows;
using System.Windows.Controls;
using BoltFramePlugin.ViewModels;

namespace BoltFramePlugin.Views
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var viewModel = DataContext as LogWindowVM;
            if (viewModel != null && viewModel.AutoScroll)
            {
                LogTextBox.ScrollToEnd();
            }
        }
    }
}
