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
                { typeof(ConfigurationWindowVM), typeof(ConfigurationWindow) }
                // Map other ViewModels to their corresponding Views
            };
        }

        public void Open(IWindowViewModel viewModel)
        {
            if (!_viewModelViewMapping.TryGetValue(viewModel.GetType(), out Type viewType))
            {
                throw new ArgumentException($"No view found for ViewModel of type {viewModel.GetType()}");
            }

            var window = (Window)Activator.CreateInstance(viewType);
            window.DataContext = viewModel;

            new WindowInteropHelper(window).Owner = _revitHandle;

            window.Show();
        }

        public bool OpenDialog(IWindowViewModel viewModel)
        {
            if (!_viewModelViewMapping.TryGetValue(viewModel.GetType(), out Type viewType))
            {
                throw new ArgumentException($"No view found for ViewModel of type {viewModel.GetType()}");
            }

            var window = (Window)Activator.CreateInstance(viewType);
            window.DataContext = viewModel;

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
