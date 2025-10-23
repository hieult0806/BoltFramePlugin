using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Models.DataImport;
using System;
using System.Threading.Tasks;

namespace BoltFramePlugin.Services.DataImport
{
    /// <summary>
    /// External event handler for auto-syncing file changes to Revit views
    /// </summary>
    public class AutoSyncEventHandler : IExternalEventHandler
    {
        private readonly ILoggingService _logger;
        private readonly DataImportManager _importManager;
        private SyncRequestedEventArgs? _currentRequest;

        public AutoSyncEventHandler(ILoggingService logger, DataImportManager importManager)
        {
            _logger = logger;
            _importManager = importManager;
        }

        public void Execute(UIApplication app)
        {
            if (_currentRequest == null)
            {
                _logger.LogWarning("AutoSyncEventHandler executed with no request");
                return;
            }

            try
            {
                _logger.LogInformation($"Syncing file: {_currentRequest.FilePath} to view: {_currentRequest.ViewName}");

                // Get the active UIDocument from UIApplication
                UIDocument uidoc = app.ActiveUIDocument;

                // Verify it matches the request document
                if (uidoc == null || !uidoc.Document.Equals(_currentRequest.Document))
                {
                    _logger.LogWarning("Active UIDocument does not match the request document. Sync may not work correctly.");
                }

                // Import and render synchronously (avoid .Wait() which can cause deadlock)
                var view = _importManager.ImportAndRenderAsync(
                    _currentRequest.Document,
                    _currentRequest.FilePath,
                    _currentRequest.ViewName,
                    _currentRequest.ImportConfig,
                    _currentRequest.RenderOptions,
                    uidoc).GetAwaiter().GetResult();

                // Switch to the newly created view (just like re-import does)
                if (uidoc != null && view != null)
                {
                    uidoc.ActiveView = view;
                    _logger.LogInformation($"Switched to synced view: {_currentRequest.ViewName}");
                }

                _logger.LogInformation($"Sync completed successfully for view: {_currentRequest.ViewName}");

                // Show success notification
                Autodesk.Revit.UI.TaskDialog.Show("Sync Complete", $"View '{_currentRequest.ViewName}' has been synced with latest file changes.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during auto-sync: {ex.Message}", ex);
                Autodesk.Revit.UI.TaskDialog.Show("Sync Error", $"Failed to sync changes:\n{ex.Message}");
            }
            finally
            {
                _currentRequest = null;
            }
        }

        public string GetName()
        {
            return "AutoSyncEventHandler";
        }

        public void SetRequest(SyncRequestedEventArgs request)
        {
            _currentRequest = request;
        }
    }
}
