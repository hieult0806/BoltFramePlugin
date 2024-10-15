using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System.Drawing;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using BoltFramePlugin.Services;
using Autodesk.Revit.DB.Events;
using BoltFramePlugin.Helpers;

namespace BoltFramePlugin.AddInEntryPoint
{
    public class BoltFrameApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
                application.ControlledApplication.DocumentOpened += OnDocumentOpened;
                string tabName = "Bolt Frame Tools";
                application.CreateRibbonTab(tabName);

                RibbonPanel panel = application.CreateRibbonPanel(tabName, "General");

                // Add Setting button to open configuration menu
                PushButtonData settingsButtonData = new PushButtonData(
                    "SettingsButton",
                    "BF Config",
                    System.Reflection.Assembly.GetExecutingAssembly().Location,
                    "BoltFramePlugin.AddInEntryPoint.BoltFrameCommand"
                );

                PushButton settingsButton = panel.AddItem(settingsButtonData) as PushButton;
                settingsButton.ToolTip = "Open the configuration settings for the wooden frame generation.";
                settingsButton.LongDescription = "This button allows users to configure settings for generating wooden frames, such as stud spacing.";

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }

        private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
        {
            ConfigurationManager.LoadConfigurationFilePath();
            PluginConfiguration config = ConfigurationHandler.LoadPluginConfiguration();
        }

        private void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs e)
        {
            ConfigurationManager.LoadConfigurationFilePath();
            PluginConfiguration config = ConfigurationHandler.LoadPluginConfiguration();
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;

            return Result.Succeeded;
        }
    }
}