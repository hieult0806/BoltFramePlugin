using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.EventHandlers
{
    /// <summary>
    /// External event handler for deleting views
    /// </summary>
    public class DeleteViewEventHandler : IExternalEventHandler
    {
        private readonly ILoggingService _logger;
        private List<ElementId> _viewIdsToDelete = new List<ElementId>();
        private Action? _onDeleteComplete;

        public DeleteViewEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
        }

        /// <summary>
        /// Sets the view IDs to delete
        /// </summary>
        public void SetParameters(List<ElementId> viewIds, Action? onDeleteComplete = null)
        {
            _viewIdsToDelete = viewIds ?? new List<ElementId>();
            _onDeleteComplete = onDeleteComplete;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;

                if (_viewIdsToDelete.Count == 0)
                {
                    _logger.LogWarning("No views to delete");
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Delete Views"))
                {
                    trans.Start();

                    var deletedIds = doc.Delete(_viewIdsToDelete);

                    trans.Commit();

                    _logger.LogInformation($"Deleted {deletedIds.Count} view(s)");
                }

                // Notify completion
                _onDeleteComplete?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting views: {ex.Message}", ex);
                Autodesk.Revit.UI.TaskDialog.Show("Error", $"Failed to delete views: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "Delete View Event Handler";
        }
    }
}
