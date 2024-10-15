using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TextBox = System.Windows.Forms.TextBox;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.AddInEntryPoint
{
    [Transaction(TransactionMode.Manual)]
    public class BoltFrameCommand : IExternalCommand
    {
        private IRevitService _revitService;
        private IDialogService _dialogService;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                _revitService = new RevitService(commandData.Application.ActiveUIDocument, ref message, elements);
                _dialogService = new DialogService(_revitService);

                BoltFrameConfiguration bfcWindow = new BoltFrameConfiguration(_revitService, _dialogService);
                bfcWindow.Show();

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