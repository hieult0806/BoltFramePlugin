using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.SheetCloning.Views;
using LoBIM.Services;

namespace LoBIM.Features.SheetCloning.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SheetCloningCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    message = "No active document found.";
                    return Result.Failed;
                }

                // Open the sheet cloning window
                var window = new SheetCloningWindow(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                var logger = DIContainerService.Container.GetInstance<ILoggingService>();
                logger?.LogError($"Error opening Sheet Cloning window: {ex.Message}", ex);

                message = $"Error: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}
