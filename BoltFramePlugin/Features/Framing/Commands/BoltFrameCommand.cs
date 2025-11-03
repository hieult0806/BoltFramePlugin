using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using BoltFramePlugin.Features.Framing.ViewModels;
using BoltFramePlugin.Services;
using SimpleInjector.Lifestyles;

namespace BoltFramePlugin.Features.Framing.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BoltFrameCommand : IExternalCommand
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
                    message = "Please select at least one element.";
                    return Result.Failed;
                }

                // Filter selected elements to include only Walls, Roofs, and Floors
                var selectedRelevantElements = new List<Element>();
                foreach (var id in selectedElementIds)
                {
                    var element = doc.GetElement(id);
                    if (element != null && RevitSelectionService.IsRelevantCategory(element.Category))
                    {
                        selectedRelevantElements.Add(element);
                    }
                }

                // If no relevant elements are selected, notify the user
                if (selectedRelevantElements.Count == 0)
                {
                    message = "Please select at least one Wall, Roof, or Floor element.";
                    return Result.Failed;
                }

                // Proceed with opening the window
                var container = DIContainerService.Container;
                var mainWindowViewModel = new BoltFrameMainWindowVM(commandData.Application.ActiveUIDocument);

                _windowService = container.GetInstance<IWindowManager>();
                _windowService.Open(mainWindowViewModel);

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