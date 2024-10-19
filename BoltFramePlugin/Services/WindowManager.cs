using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using BoltFramePlugin.ViewModels;
using BoltFramePlugin.Views;

namespace BoltFramePlugin.Services
{
    public interface IWindowViewModel
    {
        event EventHandler RequestClose;
        bool DialogResult { get; set; }
    }
    public interface IWindowManager
    {
        void Open(IWindowViewModel viewModel);
        bool OpenDialog(IWindowViewModel viewModel);
        void ShowMessage(string message, string title);

        (IWindowViewModel, Type) GetViewModel(IWindowViewModel viewModel);

        public System.Windows.Controls.UserControl OpenPanel(IWindowViewModel viewModel);
    }
    public class WindowManager : IWindowManager
    {
        private readonly IntPtr _revitHandle;
        private readonly Dictionary<Type, Type> _viewModelViewMapping;

        private Window _currentWindow;

        public WindowManager()
        {
            //TODO: Fix
            _revitHandle = Process.GetCurrentProcess().MainWindowHandle;
            _viewModelViewMapping = new Dictionary<Type, Type>
            {
                { typeof(BoltFrameMainWindowVM), typeof(BoltFrameMainWindow) },
                { typeof(TypeSelectionPopupVM), typeof(TypeSelectionWindow) },
                { typeof(ConfigurationWindowVM), typeof(ConfigurationWindow) },
                { typeof(SwitchViewShortcutDockablePaneVM), typeof(SwitchViewShortcutPanel) }
                // Map other ViewModels to their corresponding Views
            };
        }

        // PInvoke to set focus back to Revit window
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public (IWindowViewModel, Type) GetViewModel(IWindowViewModel viewModel)
        {
            if (!_viewModelViewMapping.TryGetValue(viewModel.GetType(), out Type viewType))
            {
                throw new ArgumentException($"No view found for ViewModel of type {viewModel.GetType()}");
            }
            return (viewModel, viewType);
        }

        public void Open(IWindowViewModel viewModel)
        {
            var viewType = GetViewModel(viewModel).Item2;

            var window = (Window)Activator.CreateInstance(viewType);
            window.DataContext = viewModel;

            new WindowInteropHelper(window).Owner = _revitHandle;
            window.Closed += (s, e) =>
            {
                SetForegroundWindow(_revitHandle);
            };

            // Set the window to open in the center of its owner
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Show();
        }

        public System.Windows.Controls.UserControl OpenPanel(IWindowViewModel viewModel)
        {
            var vm = GetViewModel(viewModel);
            return (System.Windows.Controls.UserControl)Activator.CreateInstance(vm.Item2);
        }

        public bool OpenDialog(IWindowViewModel viewModel)
        {
            if (!_viewModelViewMapping.TryGetValue(viewModel.GetType(), out Type viewType))
            {
                throw new ArgumentException($"No view found for ViewModel of type {viewModel.GetType()}");
            }

            var window = (Window)Activator.CreateInstance(viewType);
            window.DataContext = viewModel;

            // Set the window to open in the center of its owner
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            new WindowInteropHelper(window).Owner = _revitHandle;

            // Subscribe to the RequestClose event
            viewModel.RequestClose += (s, e) =>
            {
                window.DialogResult = viewModel.DialogResult;
                window.Close();
            };

            return window.ShowDialog() == true;
        }

        public void ShowMessage(string message, string title)
        {

        }
    }
}
