using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Features.SheetManagement.Views;
using BoltFramePlugin.Services;
using System;

namespace BoltFramePlugin.Features.SheetManagement.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SheetManagementCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    message = "No active document found";
                    return Result.Failed;
                }

                // Get logging service
                var logger = DIContainerService.Container.GetInstance<ILoggingService>();
                logger.LogInformation("Opening Sheet Management window");

                // Open the sheet management window as modeless to allow external events to execute
                var window = new SheetManagementWindow(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error opening Sheet Management: {ex.Message}";
                var logger = DIContainerService.Container.GetInstance<ILoggingService>();
                logger.LogError($"Error in SheetManagementCommand: {ex.Message}", ex);
                return Result.Failed;
            }
        }
    }
}
