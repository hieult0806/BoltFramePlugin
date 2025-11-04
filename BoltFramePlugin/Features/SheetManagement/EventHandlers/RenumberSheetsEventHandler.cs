using Autodesk.Revit.UI;
using BoltFramePlugin.Features.SheetManagement.Models;
using BoltFramePlugin.Features.SheetManagement.Services;
using BoltFramePlugin.Services;
using System;

namespace BoltFramePlugin.Features.SheetManagement.EventHandlers
{
    /// <summary>
    /// External event handler for renumbering sheets
    /// </summary>
    public class RenumberSheetsEventHandler : IExternalEventHandler
    {
        private readonly ILoggingService _logger;
        private readonly ISheetManagementService _sheetService;
        private SheetRenumberingModel _model;
        private Action<bool> _onCompleted;

        public RenumberSheetsEventHandler(ILoggingService logger, ISheetManagementService sheetService)
        {
            _logger = logger;
            _sheetService = sheetService;
        }

        /// <summary>
        /// Set parameters before raising the event
        /// </summary>
        public void SetParameters(SheetRenumberingModel model, Action<bool> onCompleted = null)
        {
            _model = model;
            _onCompleted = onCompleted;
        }

        public void Execute(UIApplication app)
        {
            bool success = false;

            try
            {
                if (_model == null)
                {
                    _logger.LogError("Renumbering model is null");
                    _onCompleted?.Invoke(false);
                    return;
                }

                _logger.LogInformation($"Starting sheet renumbering for {_model.Sheets.Count} sheets");

                var document = app.ActiveUIDocument.Document;
                success = _sheetService.RenumberSheets(document, _model);

                if (success)
                {
                    _logger.LogInformation("Sheet renumbering completed successfully");
                }
                else
                {
                    _logger.LogError("Sheet renumbering failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RenumberSheetsEventHandler: {ex.Message}", ex);
                success = false;
            }
            finally
            {
                _onCompleted?.Invoke(success);
            }
        }

        public string GetName()
        {
            return "Renumber Sheets Event Handler";
        }
    }
}
