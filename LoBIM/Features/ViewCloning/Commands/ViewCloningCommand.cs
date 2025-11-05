using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.ViewCloning.Views;
using LoBIM.Services;

namespace LoBIM.Features.ViewCloning.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewCloningCommand : IExternalCommand
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

                // Open the view cloning window
                var window = new ViewCloningWindow(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                var logger = DIContainerService.Container.GetInstance<ILoggingService>();
                logger?.LogError($"Error opening View Cloning window: {ex.Message}", ex);

                message = $"Error: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}
