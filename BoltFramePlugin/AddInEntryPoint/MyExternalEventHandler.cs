using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using View = Autodesk.Revit.DB.View;

namespace BoltFramePlugin.AddInEntryPoint
{
    public class MyExternalEventHandler : IExternalEventHandler
    {
        public static readonly MyExternalEventHandler Instance = new MyExternalEventHandler();

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            string viewName = "Floor Plan 1"; // Replace with your target view

            try
            {
                View targetView = new FilteredElementCollector(doc)
                                    .OfClass(typeof(View))
                                    .Cast<View>()
                                    .FirstOrDefault(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));

                if (targetView == null)
                {
                    TaskDialog.Show("View Activation", $"View '{viewName}' not found.");
                    return;
                }

                if (targetView.IsTemplate)
                {
                    TaskDialog.Show("View Activation", $"View '{viewName}' is a template and cannot be activated.");
                    return;
                }

                // Activate the view
                uidoc.ActiveView = targetView;
                TaskDialog.Show("View Activation", $"View '{viewName}' is now active.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("View Activation Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "YourExternalEventHandler";
        }

        public void Raise()
        {
            ExternalEvent externalEvent = ExternalEvent.Create(this);
            externalEvent.Raise();
        }
    }
}
