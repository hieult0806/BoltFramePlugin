using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using LoBIM.Services;
using LoBIM.Features.NBCReview.ViewModels;

namespace LoBIM.Features.NBCReview.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class NBCReviewCommand : IExternalCommand
    {
        private IWindowManager _windowService;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uidoc = commandData.Application.ActiveUIDocument;
                var doc = uidoc.Document;

                // Open the NBC Review window
                var container = DIContainerService.Container;
                var nbcReviewViewModel = new NBCReviewWindowVM(uidoc);

                _windowService = container.GetInstance<IWindowManager>();
                _windowService.Open(nbcReviewViewModel);

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
