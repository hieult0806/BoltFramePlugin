using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Services;
using BoltFramePlugin.Views;
using System;

namespace BoltFramePlugin.AddInEntryPoint
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DataImportCommand : IExternalCommand
    {
        private readonly IWindowManager _windowService;
        private readonly ILoggingService _logger;

        public DataImportCommand()
        {
            _windowService = DIContainerService.Container.GetInstance<IWindowManager>();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uidoc = commandData.Application.ActiveUIDocument;

                if (uidoc == null)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Error", "No active document");
                    return Result.Failed;
                }

                _logger.LogInformation("Opening Data Import window");

                // Show the window
                var window = new DataImportWindow(uidoc);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in DataImportCommand: {ex.Message}", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
