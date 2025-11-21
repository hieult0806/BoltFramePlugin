using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LoBIM.Features.SheetCloning.EventHandlers
{
    /// <summary>
    /// External event handler for opening a sheet in Revit
    /// </summary>
    public class OpenSheetEventHandler : IExternalEventHandler
    {
        private UIDocument _uidoc;
        private ElementId _sheetId;
        private Action<bool, string> _onComplete;

        public void SetParameters(UIDocument uidoc, ElementId sheetId, Action<bool, string> onComplete)
        {
            _uidoc = uidoc;
            _sheetId = sheetId;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (_uidoc == null || _sheetId == null)
                {
                    _onComplete?.Invoke(false, "Invalid parameters");
                    return;
                }

                var doc = _uidoc.Document;
                var sheet = doc.GetElement(_sheetId) as ViewSheet;

                if (sheet == null)
                {
                    _onComplete?.Invoke(false, "Sheet not found");
                    return;
                }

                // Set the active view to the sheet
                _uidoc.ActiveView = sheet;

                // Success
                _onComplete?.Invoke(true, sheet.SheetNumber);
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke(false, ex.Message);
            }
        }

        public string GetName()
        {
            return "OpenSheetEventHandler";
        }
    }
}
