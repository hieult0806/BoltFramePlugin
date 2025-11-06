using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using LoBIM.Features.ViewCloning.ViewModels;

namespace LoBIM.Features.ViewCloning.Views
{
    public partial class ViewCloningWindow : Window
    {
        public ViewCloningWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // Set Revit as the owner window so this window stays above Revit
            // but not always on top of everything
            var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (revitWindow != IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                helper.Owner = revitWindow;
            }

            var viewModel = new ViewCloningWindowVM(uidoc);
            DataContext = viewModel;

            // Subscribe to RequestClose event
            viewModel.RequestClose += OnRequestClose;
        }

        private void OnRequestClose(object sender, EventArgs e)
        {
            Close();
        }
    }
}
