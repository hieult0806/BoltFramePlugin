using System;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Services;

namespace LoBIM.Features.SheetCloning.EventHandlers
{
    /// <summary>
    /// Failure preprocessor to handle sheet number conflicts during cloning
    /// </summary>
    public class SheetCloningFailurePreprocessor : IFailuresPreprocessor
    {
        private readonly ILoggingService _logger;

        public SheetCloningFailurePreprocessor(ILoggingService logger)
        {
            _logger = logger;
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            try
            {
                var failures = failuresAccessor.GetFailureMessages();

                _logger?.LogInformation($"=== FAILURE PREPROCESSOR CALLED ===");
                _logger?.LogInformation($"Number of failures: {failures.Count}");

                foreach (var failure in failures)
                {
                    var severity = failure.GetSeverity();
                    var description = failure.GetDescriptionText();
                    var failureId = failure.GetFailureDefinitionId();

                    _logger?.LogInformation($"Failure - Severity: {severity}, Description: {description}");
                    _logger?.LogInformation($"Failure ID: {failureId}");

                    // Check if this is a sheet number conflict error
                    if (description.Contains("Sheet Number is already in use", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogWarning($"Detected sheet number conflict: {description}");

                        // Try to resolve by deleting the conflicting elements
                        var failingElementIds = failure.GetFailingElementIds();
                        _logger?.LogInformation($"Failing element IDs: {string.Join(", ", failingElementIds)}");

                        // This is a warning - delete it to suppress the dialog
                        if (severity == FailureSeverity.Warning)
                        {
                            failuresAccessor.DeleteWarning(failure);
                            _logger?.LogInformation($"Deleted sheet number conflict warning");
                        }
                        else if (severity == FailureSeverity.Error)
                        {
                            // If it's an error, we can't just delete it - need to resolve
                            _logger?.LogError($"Sheet number conflict is an ERROR, not a warning - cannot suppress");
                            // Return Continue to show the dialog and let user decide
                            return FailureProcessingResult.Continue;
                        }
                    }
                    else if (severity == FailureSeverity.Warning)
                    {
                        // For other warnings, just log them but don't suppress
                        _logger?.LogInformation($"Warning (not suppressing): {description}");
                    }
                    else if (severity == FailureSeverity.Error)
                    {
                        _logger?.LogError($"Critical error during commit: {description}");
                        // For other errors, don't try to auto-resolve - let the user see them
                        return FailureProcessingResult.Continue;
                    }
                }

                // Return ProceedWithCommit to continue with the transaction
                _logger?.LogInformation($"=== FAILURE PREPROCESSOR COMPLETE - Proceeding with commit ===");
                return FailureProcessingResult.ProceedWithCommit;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error in failure preprocessor: {ex.Message}", ex);
                return FailureProcessingResult.Continue;
            }
        }
    }
}
