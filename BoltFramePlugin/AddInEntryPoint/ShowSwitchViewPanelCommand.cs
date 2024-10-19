using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using BoltFramePlugin.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.AddInEntryPoint
{
    [Transaction(TransactionMode.Manual)]
    public class ShowSwitchViewPanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                DockablePaneId paneId = new DockablePaneId(DockablePaneGuids.SwitchViewShortcut);
                DockablePane pane = uiApp.GetDockablePane(paneId);

                if (pane == null)
                {
                    message = "Dockable pane not found.";
                    return Result.Failed;
                }

                // Toggle the visibility of the pane
                if (pane.IsShown())
                {
                    pane.Hide();
                }
                else
                {
                    pane.Show();
                }

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
