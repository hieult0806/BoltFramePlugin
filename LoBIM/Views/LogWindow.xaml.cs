using System.Windows;
using System.Windows.Controls;
using LoBIM.ViewModels;

namespace LoBIM.Views
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
