using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Constants;
using LoBIM.Services;
using LoBIM.ViewModels;
using System;

namespace LoBIM.AddInEntryPoint
{
    [Transaction(TransactionMode.Manual)]
    public class OpenLogFolderCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Show the log dockable pane
                var dockablePaneId = new DockablePaneId(DockablePaneGuids.LogPanel);
                var dockablePane = commandData.Application.GetDockablePane(dockablePaneId);

                if (dockablePane != null)
                {
                    dockablePane.Show();
                }
                else
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Error", "Log panel not found. Please restart Revit.");
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Failed to open log panel: {ex.Message}";
                Autodesk.Revit.UI.TaskDialog.Show("Error", message);
                return Result.Failed;
            }
        }
    }
}
