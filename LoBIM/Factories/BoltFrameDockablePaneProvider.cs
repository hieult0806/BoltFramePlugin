using Autodesk.Revit.UI;
using LoBIM.Services;
using LoBIM.ViewModels;
using LoBIM.Views;

namespace LoBIM.Factories
{
    public class BoltFrameDockablePaneProvider : IDockablePaneProvider
    {
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            //var windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
            // Set the content of the pane
            var switchViewShortcutPanel = new SwitchViewShortcutPanel();
            //TODO : ON WORKING
            //var switchViewShortcutPanelVM = new SwitchViewShortcutDockablePaneVM();
            //switchViewShortcutPanel.DataContext = switchViewShortcutPanel;
            data.FrameworkElement = (System.Windows.FrameworkElement) switchViewShortcutPanel;

            // Set the initial state of the pane (optional)
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }
    }
}
