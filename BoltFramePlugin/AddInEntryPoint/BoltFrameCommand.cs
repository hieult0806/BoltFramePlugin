using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using BoltFramePlugin.Services;
using BoltFramePlugin.ViewModels;
using SimpleInjector.Lifestyles;

namespace BoltFramePlugin.AddInEntryPoint
{
    [Transaction(TransactionMode.Manual)]
    public class BoltFrameCommand : IExternalCommand
    {
        private IWindowManager _windowService;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var container = ContainerConfigurator.Container;
                var mainWindowViewModel = new BoltFrameMainWindowVM(commandData.Application.ActiveUIDocument);

                _windowService = container.GetInstance<IWindowManager>();
                _windowService.Open(mainWindowViewModel);

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