using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;

namespace LoBIM.Features.ViewCloning.EventHandlers
{
    /// <summary>
    /// External event handler for highlighting a view in the Project Browser.
    /// Uses brief view activation as Revit 2025+ web-based Project Browser doesn't
    /// support direct UI Automation highlighting.
    /// </summary>
    public class HighlightViewEventHandler : IExternalEventHandler
    {
        private readonly ILoggingService _logger;
        private UIDocument? _uidoc;
        private ElementId? _viewId;
        private Action<bool, string>? _onComplete;

        public HighlightViewEventHandler(ILoggingService logger)
        {
            _logger = logger;
        }

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

                // Revit 2025+ uses a web-based Project Browser (Chromium-based) that doesn't
                // expose interactive elements through UI Automation. The only reliable way to
                // make a view visible in the Project Browser is to briefly activate it.
                var currentView = _uidoc.ActiveView;

                // Briefly activate the view - this makes it visible and highlighted in Project Browser
                _uidoc.ActiveView = view;

                // Immediately switch back to the original view
                _uidoc.ActiveView = currentView;

                _logger?.LogInformation($"View '{view.Name}' is now visible in Project Browser");
                _onComplete?.Invoke(true, view.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error highlighting view in Project Browser: {ex.Message}", ex);
                _onComplete?.Invoke(false, ex.Message);
            }
        }

        public string GetName()
        {
            return "HighlightViewEventHandler";
        }
    }
}
