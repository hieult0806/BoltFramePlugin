using Autodesk.Revit.UI;
using BoltFramePlugin.Services;
using BoltFramePlugin.ViewModels;
using BoltFramePlugin.Views;

namespace BoltFramePlugin.Factories
{
    public class BoltFrameDockablePaneProvider : IDockablePaneProvider
    {
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            //var windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
            // Set the content of the pane
            var switchViewShortcutPanel = new SwitchViewShortcutPanel();
            var switchViewShortcutPanelVM = new SwitchViewShortcutDockablePaneVM();
            switchViewShortcutPanel.DataContext = switchViewShortcutPanel;
            data.FrameworkElement = (System.Windows.FrameworkElement) switchViewShortcutPanel;

            // Set the initial state of the pane (optional)
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }
    }
}
