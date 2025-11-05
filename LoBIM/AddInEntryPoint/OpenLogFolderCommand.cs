using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
                // Get the window manager from DI container
                var container = DIContainerService.Container;
                var windowManager = container.GetInstance<IWindowManager>();

                // Create and show the Log Window as modeless
                var viewModel = new LogWindowVM();
                windowManager.Open(viewModel);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Failed to open log window: {ex.Message}";
                Autodesk.Revit.UI.TaskDialog.Show("Error", message);
                return Result.Failed;
            }
        }
    }
}
