using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.SheetCloning.Helpers;
using LoBIM.Features.SheetCloning.Models;
using LoBIM.Features.SheetCloning.Services;
using LoBIM.Features.ViewCloning.Strategies;
using LoBIM.Services;

namespace LoBIM.Features.SheetCloning.EventHandlers
{
    /// <summary>
    /// External event handler for cloning sheets in a transaction
    /// </summary>
    public class CloneSheetsEventHandler : IExternalEventHandler
    {
        private readonly ILoggingService _logger;
        private readonly ISheetCloningService _sheetCloningService;
        private UIDocument _uidoc;
        private List<LinkedSheetInfo> _sheetsToClone;
        private Dictionary<string, Document> _linkedDocuments;
        private Action<int> _onComplete;

        public CloneSheetsEventHandler(ILoggingService logger, ISheetCloningService sheetCloningService)
        {
            _logger = logger;
            _sheetCloningService = sheetCloningService;
        }

        /// <summary>
        /// Sets the parameters for the sheet cloning operation
        /// </summary>
        public void SetParameters(UIDocument uidoc, List<LinkedSheetInfo> sheets, Dictionary<string, Document> linkedDocuments, Action<int> onComplete)
        {
            _uidoc = uidoc;
            _sheetsToClone = sheets;
            _linkedDocuments = linkedDocuments;
            _onComplete = onComplete;
        }

        public void Execute(UIApplication app)
        {
            if (_uidoc == null || _sheetsToClone == null || _sheetsToClone.Count == 0)
            {
                _logger?.LogWarning("No sheets selected for cloning");
                _onComplete?.Invoke(0);
                return;
            }

            try
            {
                _logger?.LogInformation($"Starting sheet cloning process for {_sheetsToClone.Count} sheets");

                // Ensure source tracking parameters exist BEFORE starting the transaction
                // This must be done outside of any transaction since parameter creation requires its own transaction
                try
                {
                    // Ensure view source tracking parameters
                    var strategy = new PlanViewCloningStrategy(_logger);
                    strategy.EnsureSourceTrackingParameters(_uidoc.Document);

                    // Ensure sheet source tracking parameters
                    var sheetTrackingHelper = new SheetSourceTrackingHelper(_logger);
                    sheetTrackingHelper.EnsureSourceTrackingParameters(_uidoc.Document);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Could not ensure source tracking parameters before sheet cloning: {ex.Message}");
                }

                int successCount = 0;

                // Track sheet numbers across all transactions to avoid conflicts
                var createdSheetNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Clone each sheet in its own transaction to avoid commit conflicts
                // This ensures each sheet is fully committed before the next one starts
                foreach (var sheetInfo in _sheetsToClone)
                {
                    using (Transaction trans = new Transaction(_uidoc.Document, $"Clone Sheet {sheetInfo.SheetNumber}"))
                    {
                        trans.Start();

                        try
                        {
                            // Create a single-item list for this sheet
                            var singleSheetList = new List<LinkedSheetInfo> { sheetInfo };

                            int result = _sheetCloningService.CloneSheets(_uidoc.Document, singleSheetList, _linkedDocuments, createdSheetNumbers);

                            if (result > 0)
                            {
                                _logger?.LogInformation($"About to commit transaction for sheet {sheetInfo.SheetNumber}");
                                _logger?.LogInformation($"Transaction status: HasStarted={trans.HasStarted()}, HasEnded={trans.HasEnded()}");

                                // Commit the transaction
                                var commitStatus = trans.Commit();

                                _logger?.LogInformation($"Transaction commit returned: {commitStatus}");
                                _logger?.LogInformation($"Transaction status after commit: HasStarted={trans.HasStarted()}, HasEnded={trans.HasEnded()}");

                                // Verify the sheet actually exists in the document after commit
                                var verifySheet = new FilteredElementCollector(_uidoc.Document)
                                    .OfClass(typeof(ViewSheet))
                                    .Cast<ViewSheet>()
                                    .Where(s => !s.IsTemplate)
                                    .ToList();

                                _logger?.LogInformation($"Total sheets in document after commit: {verifySheet.Count}");
                                _logger?.LogInformation($"Sheet numbers after commit: {string.Join(", ", verifySheet.Select(s => s.SheetNumber).Take(20))}");

                                successCount++;
                                _logger?.LogInformation($"Successfully committed sheet {sheetInfo.SheetNumber}");
                            }
                            else
                            {
                                trans.RollBack();
                                _logger?.LogWarning($"Failed to clone sheet {sheetInfo.SheetNumber}, rolling back");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError($"Error cloning sheet {sheetInfo.SheetNumber}: {ex.Message}", ex);
                            if (trans.HasStarted() && !trans.HasEnded())
                            {
                                trans.RollBack();
                            }
                        }
                    }
                }

                _logger?.LogInformation($"Successfully cloned {successCount} out of {_sheetsToClone.Count} sheets");

                _onComplete?.Invoke(successCount);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error in CloneSheetsEventHandler: {ex.Message}", ex);
                _onComplete?.Invoke(0);
            }
        }

        public string GetName()
        {
            return "Clone Sheets Event Handler";
        }
    }
}
