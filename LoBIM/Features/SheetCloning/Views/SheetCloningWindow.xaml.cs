using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using LoBIM.Features.SheetCloning.ViewModels;

namespace LoBIM.Features.SheetCloning.Views
{
    public partial class SheetCloningWindow : Window
    {
        private readonly SheetCloningWindowVM _viewModel;

        public SheetCloningWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // Set Revit as owner
            var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindow != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                helper.Owner = revitWindow;
            }

            // Initialize ViewModel
            _viewModel = new SheetCloningWindowVM(uidoc);
            DataContext = _viewModel;

            // Subscribe to RequestClose event
            _viewModel.RequestClose += OnRequestClose;

            // Focus back to Revit when window closes
            Closed += (s, e) =>
            {
                if (revitWindow != IntPtr.Zero)
                {
                    SetForegroundWindow(revitWindow);
                }
            };
        }

        private void OnRequestClose(object sender, EventArgs e)
        {
            Close();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
