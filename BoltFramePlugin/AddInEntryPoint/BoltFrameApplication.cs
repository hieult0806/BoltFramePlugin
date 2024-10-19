using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using BoltFramePlugin.Services;
using Autodesk.Revit.DB.Events;
using BoltFramePlugin.Helpers;
using BoltFramePlugin.Factories;
using BoltFramePlugin.Constants;
using BoltFramePlugin.Views;
using System.Xml.Linq;

namespace BoltFramePlugin.AddInEntryPoint
{
    public class BoltFrameApplication : IExternalApplication
    {
        // Unique identifier for the dockable pane
        public static readonly DockablePaneId DockablePaneGuid = new DockablePaneId(DockablePaneGuids.SwitchViewShortcut);
        private UIControlledApplication _application;
        public Result OnStartup(UIControlledApplication application)
        {
            _application = application;
            try
            {
                ContainerConfigurator.RegisterServices();
                var container = ContainerConfigurator.Container;

                application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
                application.ControlledApplication.DocumentOpened += OnDocumentOpened;
                application.ControlledApplication.DocumentClosing += OnDocumentClosing;
                application.ControlledApplication.DocumentChanged += OnDocumentChanged;
                application.ControlledApplication.DocumentClosed += OnDocumentClosed;

                string tabName = Resources.Strings.Strings.TabName;
                application.CreateRibbonTab(tabName);

                RibbonPanel panel = application.CreateRibbonPanel(tabName, "General");

                // Main Button
                PushButtonData mainPluginButton = new PushButtonData(
                    "Bolt Frame",
                    "BF Config",
                    System.Reflection.Assembly.GetExecutingAssembly().Location,
                    "BoltFramePlugin.AddInEntryPoint.BoltFrameCommand"
                );

                PushButton settingsButton = panel.AddItem(mainPluginButton) as PushButton;
                settingsButton.ToolTip = "Open Plugin";
                settingsButton.LongDescription = "";

                // Switch View Button
                PushButtonData panelButtonData = new PushButtonData(
                    "Switch View Panel",
                    "View Plans Shortcut",
                    System.Reflection.Assembly.GetExecutingAssembly().Location,
                    "BoltFramePlugin.AddInEntryPoint.ShowSwitchViewPanelCommand"
                );

                PushButton panelButton = panel.AddItem(panelButtonData) as PushButton;
                panelButton.ToolTip = "Click to show/close the Switch View Panel";
                panelButton.LongDescription = "";

                //application.RegisterDockablePane(DockablePaneGuid, $"Switch View Plans | {Resources.Strings.Strings.AppTitle}", CreateDockablePane());

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }

        private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
        {
        }

        private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
        {
        }

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
        }

        private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
        {
            
        }

        private void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs e)
        {

        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;

            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
            application.ControlledApplication.DocumentClosed -= OnDocumentClosed;

            return Result.Succeeded;
        }

        private IDockablePaneProvider CreateDockablePane()
        {
            return new BoltFrameDockablePaneProvider();
        }
    }
}