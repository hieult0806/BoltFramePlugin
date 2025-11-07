using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Views;
using System;

namespace LoBIM.AddInEntryPoint
{
    [Transaction(TransactionMode.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var aboutWindow = new AboutWindow();
                aboutWindow.ShowDialog();

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
