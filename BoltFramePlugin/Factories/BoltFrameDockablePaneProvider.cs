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
            var windowManager = ContainerConfigurator.Container.GetInstance<IWindowManager>();
            // Set the content of the pane
            var switchViewShortcutPanel = ContainerConfigurator.Container.GetInstance(typeof(SwitchViewShortcutDockablePaneVM));
            data.FrameworkElement = (System.Windows.FrameworkElement)windowManager.OpenPanel((IWindowViewModel)switchViewShortcutPanel);

            // Set the initial state of the pane (optional)
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }
    }
}
