using Autodesk.Revit.UI;
using LoBIM.Services;
using LoBIM.ViewModels;
using LoBIM.Views;

namespace LoBIM.Factories
{
    public class LogDockablePaneProvider : IDockablePaneProvider
    {
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            // Create the log panel
            var logPanel = new LogWindow();

            // Create and set the ViewModel
            var logVM = new LogWindowVM();
            logPanel.DataContext = logVM;

            // Set the content of the pane
            data.FrameworkElement = (System.Windows.FrameworkElement)logPanel;

            // Set the initial state of the pane
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Bottom
            };
        }
    }
}
