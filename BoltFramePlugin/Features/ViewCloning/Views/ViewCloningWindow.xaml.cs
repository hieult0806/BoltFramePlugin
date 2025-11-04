using System;
using System.Windows;
using Autodesk.Revit.UI;
using BoltFramePlugin.Features.ViewCloning.ViewModels;

namespace BoltFramePlugin.Features.ViewCloning.Views
{
    public partial class ViewCloningWindow : Window
    {
        public ViewCloningWindow(UIDocument uidoc)
        {
            InitializeComponent();

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
