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

                using (Transaction trans = new Transaction(_uidoc.Document, "Clone Sheets"))
                {
                    trans.Start();

                    try
                    {
                        successCount = _sheetCloningService.CloneSheets(_uidoc.Document, _sheetsToClone, _linkedDocuments);

                        trans.Commit();
                        _logger?.LogInformation($"Successfully cloned {successCount} out of {_sheetsToClone.Count} sheets");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error during sheet cloning transaction: {ex.Message}", ex);
                        trans.RollBack();
                    }
                }

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
