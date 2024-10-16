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
                ContainerConfigurator.RegisterServices(commandData, elements);
                var container = ContainerConfigurator.Container;

                // Resolve WindowManager and ViewModel
                var windowManager = container.GetInstance<IWindowManager>();
                var mainWindowViewModel = container.GetInstance<BoltFrameMainWindowVM>();

                // Open the dialog
                windowManager.Open(mainWindowViewModel);

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