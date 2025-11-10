using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;

namespace LoBIM.Features.SheetCloning.EventHandlers
{
    /// <summary>
    /// External event handler for highlighting a sheet in the Project Browser.
    /// Uses brief view activation as Revit 2025+ web-based Project Browser doesn't
    /// support direct UI Automation highlighting, and ID_FIND_IN_PROJECT_BROWSER
    /// is not a postable command.
    /// </summary>
    public class HighlightSheetEventHandler : IExternalEventHandler
    {
        private readonly ILoggingService _logger;
        private UIDocument? _uidoc;
        private ElementId? _sheetId;
        private Action<bool, string>? _onComplete;

        public HighlightSheetEventHandler(ILoggingService logger)
        {
            _logger = logger;
        }

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

                // Revit 2025+ uses a web-based Project Browser (Chromium-based) that doesn't
                // expose interactive elements through UI Automation. While ID_FIND_IN_PROJECT_BROWSER
                // exists, it's not a PostableCommand. The only reliable way to make a sheet visible
                // in the Project Browser is to briefly activate it.
                var currentView = _uidoc.ActiveView;

                // Briefly activate the sheet - this makes it visible and highlighted in Project Browser
                _uidoc.ActiveView = sheet;

                // Immediately switch back to the original view
                _uidoc.ActiveView = currentView;

                _logger?.LogInformation($"Sheet '{sheet.SheetNumber}' is now visible in Project Browser");
                _onComplete?.Invoke(true, sheet.SheetNumber);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error highlighting sheet in Project Browser: {ex.Message}", ex);
                _onComplete?.Invoke(false, ex.Message);
            }
        }

        public string GetName()
        {
            return "HighlightSheetEventHandler";
        }
    }
}
