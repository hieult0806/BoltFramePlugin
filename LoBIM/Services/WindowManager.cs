using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using LoBIM.Features.Framing.ViewModels;
using LoBIM.Features.Framing.Views;
using LoBIM.Features.NBCReview.ViewModels;
using LoBIM.Features.NBCReview.Views;
using LoBIM.Models;
using LoBIM.ViewModels;
using LoBIM.Views;

namespace LoBIM.Services
{
    /// <summary>
    /// Interface for managing window operations within the application.
    /// </summary>
    public interface IWindowManager
    {
        void Open(IWindowViewModel viewModel);
        bool OpenDialog(IWindowViewModel viewModel);
        void ShowMessage(string message, string title);
        UserControl OpenPanel(IWindowViewModel viewModel);
    }

    /// <summary>
    /// Implementation of IWindowManager for managing window operations.
    /// </summary>
    public class WindowManager : IWindowManager
    {
        private readonly nint _revitHandle;
        private readonly Dictionary<Type, Type> _viewModelViewMapping;
        private readonly List<Window> _openWindows = new List<Window>();

        /// <summary>
        /// Initializes a new instance of the WindowManager class.
        /// </summary>
        public WindowManager()
        {
            _revitHandle = GetRevitMainWindowHandle();
            _viewModelViewMapping = InitializeViewModelViewMapping();
        }

        /// <summary>
        /// Closes all open windows managed by this WindowManager.
        /// </summary>
        public void CloseAllWindows()
        {
            // Create a copy of the list to avoid modification during iteration
            var windowsToClose = _openWindows.ToList();
            foreach (var window in windowsToClose)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                    // Ignore errors when closing windows
                }
            }
            _openWindows.Clear();
        }

        /// <summary>
        /// Opens a non-modal window associated with the specified ViewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel to associate with the window.</param>
        public void Open(IWindowViewModel viewModel)
        {
            var window = CreateWindow(viewModel, isModal: false);
            window.Show();
        }

        /// <summary>
        /// Opens a modal dialog window associated with the specified ViewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel to associate with the dialog.</param>
        /// <returns>True if the dialog result is true; otherwise, false.</returns>
        public bool OpenDialog(IWindowViewModel viewModel)
        {
            var window = CreateWindow(viewModel, isModal: true);
            window.ShowDialog();
            return window.DialogResult ?? false;
        }

        /// <summary>
        /// Displays a message box with the specified message and title.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">The title of the message box.</param>
        public void ShowMessage(string message, string title)
        {

        }

        /// <summary>
        /// Opens a UserControl panel associated with the specified ViewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel to associate with the UserControl.</param>
        /// <returns>The instantiated UserControl.</returns>
        public UserControl OpenPanel(IWindowViewModel viewModel)
        {
            var viewType = GetViewTypeForViewModel(viewModel);
            if (!typeof(UserControl).IsAssignableFrom(viewType))
            {
                throw new InvalidOperationException($"The view for {viewModel.GetType().Name} is not a UserControl.");
            }

            try
            {
                var userControl = (UserControl)Activator.CreateInstance(viewType);
                userControl.DataContext = viewModel;
                return userControl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating UserControl: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves the Type of the View associated with the given ViewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel instance.</param>
        /// <returns>The Type of the associated View.</returns>
        private Type GetViewTypeForViewModel(IWindowViewModel viewModel)
        {
            var viewModelType = viewModel.GetType();
            if (!_viewModelViewMapping.TryGetValue(viewModelType, out Type viewType))
            {
                throw new ArgumentException($"No view found for ViewModel of type {viewModelType.Name}");
            }
            return viewType;
        }

        /// <summary>
        /// Creates and initializes a Window associated with the specified ViewModel.
        /// </summary>
        /// <param name="viewModel">The ViewModel to associate with the Window.</param>
        /// <returns>The instantiated and initialized Window.</returns>
        private Window CreateWindow(IWindowViewModel viewModel, bool isModal = false)
        {
            var viewType = GetViewTypeForViewModel(viewModel);

            if (!(Activator.CreateInstance(viewType) is Window window))
            {
                throw new InvalidOperationException($"The view {viewType.Name} is not a Window.");
            }

            window.DataContext = viewModel;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Set the Revit window as the owner
            WindowInteropHelper helper = new WindowInteropHelper(window)
            {
                Owner = _revitHandle
            };

            // Subscribe to the RequestClose event
            viewModel.RequestClose += (s, e) =>
            {
                if (isModal)
                {
                    window.DialogResult = viewModel.DialogResult;
                }
                window.Close();
            };

            // Track the window and clean up when closed
            _openWindows.Add(window);
            window.Closed += (s, e) =>
            {
                _openWindows.Remove(window);
                NativeMethods.SetForegroundWindow(_revitHandle);
            };

            return window;
        }

        /// <summary>
        /// Initializes the mapping between ViewModels and their corresponding Views.
        /// </summary>
        /// <returns>A dictionary mapping ViewModel Types to View Types.</returns>
        private Dictionary<Type, Type> InitializeViewModelViewMapping()
        {
            return new Dictionary<Type, Type>
            {
                { typeof(BoltFrameMainWindowVM), typeof(BoltFrameMainWindow) },
                { typeof(BoltWallFrameWindowVM), typeof(BoltWallFrameWindow) },
                { typeof(FramingSummaryVM), typeof(FramingSummaryWindow) },
                { typeof(NBCReviewWindowVM), typeof(NBCReviewWindow) },
                { typeof(TypeSelectionPopupVM), typeof(TypeSelectionWindow) },
                { typeof(ConfigurationWindowVM), typeof(Views.ConfigurationWindow) },
                { typeof(SwitchViewShortcutDockablePaneVM), typeof(Views.SwitchViewShortcutPanel) },
                { typeof(LogWindowVM), typeof(Views.LogWindow) }
                // Add additional ViewModel-View mappings here
            };
        }

        /// <summary>
        /// Retrieves the main window handle of the Revit process.
        /// </summary>
        /// <returns>The handle to the Revit main window.</returns>
        private nint GetRevitMainWindowHandle()
        {
            nint handle = Process.GetCurrentProcess().MainWindowHandle;

            // If the handle is not available, you might need to implement an alternative retrieval method
            if (handle == nint.Zero)
            {
                throw new InvalidOperationException("Unable to retrieve Revit main window handle.");
            }

            return handle;
        }

        /// <summary>
        /// Static class for encapsulating external method calls.
        /// </summary>
        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(nint hWnd);
        }
    }
}