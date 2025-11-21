using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LoBIM.Features.ViewCloning.EventHandlers
{
    /// <summary>
    /// External event handler for opening a view or sheet in Revit
    /// </summary>
    public class OpenViewEventHandler : IExternalEventHandler
    {
        private UIDocument _uidoc;
        private ElementId _viewId;
        private Action<bool, string> _onComplete;

        public void SetParameters(UIDocument uidoc, ElementId viewId, Action<bool, string> onComplete)
        {
            _uidoc = uidoc;
            _viewId = viewId;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (_uidoc == null || _viewId == null)
                {
                    _onComplete?.Invoke(false, "Invalid parameters");
                    return;
                }

                var doc = _uidoc.Document;
                var view = doc.GetElement(_viewId) as Autodesk.Revit.DB.View;

                if (view == null)
                {
                    _onComplete?.Invoke(false, "View not found");
                    return;
                }

                // Set the active view
                _uidoc.ActiveView = view;

                // Success
                _onComplete?.Invoke(true, view.Name);
            }
            catch (Exception ex)
            {
                _onComplete?.Invoke(false, ex.Message);
            }
        }

        public string GetName()
        {
            return "OpenViewEventHandler";
        }
    }
}
