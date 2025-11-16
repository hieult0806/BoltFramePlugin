using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.LimitingDistance.Models;
using LoBIM.Services;
using LoBIM.Services.Parameters;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.LimitingDistance.EventHandlers
{
    /// <summary>
    /// External event handler to initialize project parameters from JSON configuration
    /// </summary>
    public class InitializeProjectParametersEventHandler : IExternalEventHandler
    {
        private UIDocument? _uidoc;
        private readonly ILoggingService _logger;
        private readonly IProjectParameterService _parameterService;
        private Action<List<ProjectParameterInfo>>? _onComplete;
        private string _configPath = string.Empty;

        public InitializeProjectParametersEventHandler(ILoggingService logger, IProjectParameterService parameterService)
        {
            _logger = logger;
            _parameterService = parameterService;
        }

        /// <summary>
        /// Set parameters for the event handler
        /// </summary>
        public void SetParameters(UIDocument uidoc, string configPath, Action<List<ProjectParameterInfo>> onComplete)
        {
            _uidoc = uidoc;
            _configPath = configPath;
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
            var results = new List<ProjectParameterInfo>();

            try
            {
                _logger.LogInformation($"Loading project parameter configuration from: {_configPath}");

                // Load parameters from JSON using the parameter service
                var parameterDefs = _parameterService.LoadParametersFromJson(_configPath);

                if (!parameterDefs.Any())
                {
                    TaskDialog.Show("Error", $"Failed to load parameters from:\n{_configPath}");
                    _logger.LogError($"Failed to load parameters from configuration file");
                    return;
                }

                _logger.LogInformation($"Found {parameterDefs.Count()} parameters to process.");

                // Use the parameter service to ensure all parameters exist
                var creationResults = _parameterService.EnsureParametersExist(doc, parameterDefs);

                // Convert results to ProjectParameterInfo for UI display
                foreach (var result in creationResults)
                {
                    var paramInfo = new ProjectParameterInfo
                    {
                        Name = result.ParameterName,
                        ParameterType = "N/A", // Not available in result
                        GroupName = "N/A",     // Not available in result
                        Status = result.Status.ToString()
                    };

                    if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        paramInfo.Status = $"Error: {result.ErrorMessage}";
                    }

                    results.Add(paramInfo);
                    _logger.LogInformation($"Parameter '{result.ParameterName}': {result.Status}");
                }

                // Update UI with results
                _onComplete?.Invoke(results);

                // Show summary
                int successful = results.Count(r => r.Status == "Created" || r.Status == "AlreadyExists");
                int failed = results.Count - successful;

                TaskDialog.Show("Project Parameters Initialized",
                    $"Successfully processed {successful} parameters.\n" +
                    $"Failed: {failed}\n\n" +
                    $"See the Project Setup tab for detailed status.");

                _logger.LogInformation($"Project parameter initialization completed. Success: {successful}, Failed: {failed}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing project parameters: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to initialize project parameters:\n{ex.Message}");
            }
        }


        public string GetName()
        {
            return "Initialize Project Parameters Event Handler";
        }
    }
}
