using System;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Services;
using LoBIM.Services.Parameters;

namespace LoBIM.Features.SheetCloning.Helpers
{
    /// <summary>
    /// Helper class for managing source tracking parameters on sheets
    /// </summary>
    public class SheetSourceTrackingHelper
    {
        private readonly ILoggingService _logger;
        private readonly IProjectParameterService _parameterService;

        public SheetSourceTrackingHelper(ILoggingService logger, IProjectParameterService parameterService)
        {
            _logger = logger;
            _parameterService = parameterService;
        }

        /// <summary>
        /// Ensures that the custom project parameters for sheet source tracking exist
        /// Creates them if they don't exist
        /// This method must be called OUTSIDE of any active transaction
        /// </summary>
        public void EnsureSourceTrackingParameters(Document doc)
        {
            try
            {
                _logger.LogInformation($"Ensuring sheet source tracking parameters exist...");

                // Get predefined sheet source tracking parameters
                var parameterDefs = _parameterService.GetSheetSourceTrackingParameters();

                // Ensure all parameters exist
                var results = _parameterService.EnsureParametersExist(doc, parameterDefs);

                // Log results
                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        _logger.LogInformation($"Parameter {result.ParameterName}: {result.Status}");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to create parameter {result.ParameterName}: {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not ensure sheet source tracking parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Stores source sheet information in the cloned sheet's custom project parameters
        /// This must be called within an active transaction
        /// </summary>
        public void StoreSourceSheetInfo(ViewSheet sourceSheet, ViewSheet clonedSheet, string linkedFileName)
        {
            try
            {
                _logger.LogInformation($"=== STORING SOURCE SHEET METADATA ===");

                // Use the parameter service to store source tracking
                _parameterService.StoreSheetSourceTracking(
                    clonedSheet,
                    linkedFileName,
                    sourceSheet.Name,
                    sourceSheet.Id
                );

                _logger.LogInformation($"Successfully stored source tracking: {linkedFileName} > {sourceSheet.Name} (ID: {sourceSheet.Id})");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not store source sheet info: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
            }
        }
    }
}
