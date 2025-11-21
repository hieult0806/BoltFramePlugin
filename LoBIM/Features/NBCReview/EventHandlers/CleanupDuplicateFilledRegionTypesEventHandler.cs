using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.NBCReview.EventHandlers
{
    /// <summary>
    /// External event handler to clean up duplicate filled region types
    /// </summary>
    public class CleanupDuplicateFilledRegionTypesEventHandler : IExternalEventHandler
    {
        private UIDocument? _uidoc;
        private ILoggingService _logger;
        private Action<string>? _onComplete;

        public CleanupDuplicateFilledRegionTypesEventHandler(ILoggingService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Set parameters for the event handler
        /// </summary>
        public void SetParameters(UIDocument uidoc, Action<string> onComplete)
        {
            _uidoc = uidoc;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            if (_uidoc == null)
            {
                TaskDialog.Show("Error", "UIDocument is null");
                return;
            }

            var doc = _uidoc.Document;

            try
            {
                _logger.LogInformation("Scanning for duplicate filled region types...");

                // Get all filled region types
                var allFilledRegionTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .ToList();

                // Define the patterns to look for (old naming conventions)
                var duplicatePatterns = new[]
                {
                    "_13.1-16.4ft",
                    "_29.5ft+",
                    "plus_29.5ftplus",
                    "_23.0-26.2ft",
                    "_26.2-29.5ft"
                };

                // Find duplicates
                var duplicatesToDelete = allFilledRegionTypes
                    .Where(frt => duplicatePatterns.Any(pattern => frt.Name.Contains(pattern)))
                    .ToList();

                if (duplicatesToDelete.Count == 0)
                {
                    var message = "No duplicate filled region types found.\n\nAll filled region types are clean!";
                    TaskDialog.Show("Cleanup Complete", message);
                    _onComplete?.Invoke(message);
                    return;
                }

                // Show confirmation dialog
                var confirmResult = TaskDialog.Show("Confirm Delete",
                    $"Found {duplicatesToDelete.Count} duplicate filled region types:\n\n" +
                    string.Join("\n", duplicatesToDelete.Take(10).Select(frt => $"  • {frt.Name}")) +
                    (duplicatesToDelete.Count > 10 ? $"\n  ... and {duplicatesToDelete.Count - 10} more" : "") +
                    "\n\nDo you want to delete these duplicates?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (confirmResult != TaskDialogResult.Yes)
                {
                    _onComplete?.Invoke("Cleanup cancelled by user.");
                    return;
                }

                // Delete duplicates
                int deletedCount = 0;
                int failedCount = 0;
                var errors = new List<string>();

                using (Transaction trans = new Transaction(doc, "Delete Duplicate Filled Region Types"))
                {
                    trans.Start();

                    foreach (var duplicate in duplicatesToDelete)
                    {
                        try
                        {
                            doc.Delete(duplicate.Id);
                            deletedCount++;
                            _logger.LogInformation($"Deleted duplicate: {duplicate.Name}");
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            var errorMsg = $"{duplicate.Name}: {ex.Message}";
                            errors.Add(errorMsg);
                            _logger.LogWarning($"Could not delete '{duplicate.Name}': {ex.Message}");
                        }
                    }

                    trans.Commit();
                }

                // Show result
                var resultMessage = $"Deleted {deletedCount} duplicate filled region types.\n" +
                                  (failedCount > 0 ? $"Failed to delete {failedCount} types (they may be in use)." : "");

                if (errors.Any())
                {
                    resultMessage += "\n\nErrors:\n" + string.Join("\n", errors.Take(5));
                }

                TaskDialog.Show("Cleanup Complete", resultMessage);
                _onComplete?.Invoke(resultMessage);

                _logger.LogInformation($"Cleanup completed. Deleted: {deletedCount}, Failed: {failedCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cleaning up duplicates: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to clean up duplicates:\n{ex.Message}");
            }
        }

        public string GetName()
        {
            return "Cleanup Duplicate Filled Region Types Event Handler";
        }
    }
}
