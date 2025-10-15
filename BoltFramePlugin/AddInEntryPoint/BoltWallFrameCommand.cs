using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using BoltFramePlugin.Services;
using BoltFramePlugin.ViewModels;
using SimpleInjector.Lifestyles;

namespace BoltFramePlugin.AddInEntryPoint
{
    [Transaction(TransactionMode.Manual)]
    public class BoltWallFrameCommand : IExternalCommand
    {
        private IWindowManager _windowService;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uidoc = commandData.Application.ActiveUIDocument;
                var doc = uidoc.Document;
                var selectedElementIds = uidoc.Selection.GetElementIds();

                // Check if no elements are selected
                if (selectedElementIds == null || selectedElementIds.Count == 0)
                {
                    message = "Please select at least one wall element.";
                    return Result.Failed;
                }

                // Filter selected elements to include only Walls
                var selectedWalls = new List<Element>();
                foreach (var id in selectedElementIds)
                {
                    var element = doc.GetElement(id);
                    if (element != null && element is Wall)
                    {
                        selectedWalls.Add(element);
                    }
                }

                // If no walls are selected, notify the user
                if (selectedWalls.Count == 0)
                {
                    message = "Please select at least one Wall element.";
                    return Result.Failed;
                }

                // Proceed with opening the window
                var container = DIContainerService.Container;
                var wallFrameViewModel = new BoltWallFrameWindowVM(commandData.Application.ActiveUIDocument);

                _windowService = container.GetInstance<IWindowManager>();
                _windowService.Open(wallFrameViewModel);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // Return the error message if an exception occurs
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
