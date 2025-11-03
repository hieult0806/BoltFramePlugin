using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using BoltFramePlugin.Services;
using BoltFramePlugin.Features.LimitingDistance.ViewModels;

namespace BoltFramePlugin.Features.LimitingDistance.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LimitingDistanceCommand : IExternalCommand
    {
        private IWindowManager _windowService;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uidoc = commandData.Application.ActiveUIDocument;
                var doc = uidoc.Document;

                // Open the Limiting Distance window
                var container = DIContainerService.Container;
                var limitingDistanceViewModel = new LimitingDistanceWindowVM(uidoc);

                _windowService = container.GetInstance<IWindowManager>();
                _windowService.Open(limitingDistanceViewModel);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
