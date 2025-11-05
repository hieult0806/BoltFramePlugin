using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.LimitingDistance.Models;
using LoBIM.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.LimitingDistance.EventHandlers
{
    /// <summary>
    /// External event handler to initialize project parameters from JSON configuration
    /// </summary>
    public class InitializeProjectParametersEventHandler : IExternalEventHandler
    {
        private UIDocument? _uidoc;
        private ILoggingService _logger;
        private Action<List<ProjectParameterInfo>>? _onComplete;
        private string _configPath = string.Empty;

        public InitializeProjectParametersEventHandler(ILoggingService logger)
        {
            _logger = logger;
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

                // Read and parse JSON configuration
                if (!File.Exists(_configPath))
                {
                    TaskDialog.Show("Error", $"Configuration file not found:\n{_configPath}");
                    _logger.LogError($"Configuration file not found: {_configPath}");
                    return;
                }

                var jsonContent = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<ProjectParameterConfiguration>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null || config.ProjectParameters == null || config.ProjectParameters.Count == 0)
                {
                    TaskDialog.Show("Error", "Failed to parse configuration or no parameters defined.");
                    _logger.LogError("Failed to parse configuration or no parameters defined.");
                    return;
                }

                _logger.LogInformation($"Found {config.ProjectParameters.Count} parameters to process.");

                using (Transaction trans = new Transaction(doc, "Initialize Project Parameters"))
                {
                    trans.Start();

                    foreach (var paramDef in config.ProjectParameters)
                    {
                        try
                        {
                            var result = CreateProjectParameter(doc, paramDef);
                            results.Add(result);
                            _logger.LogInformation($"Parameter '{paramDef.Name}': {result.Status}");
                        }
                        catch (Exception ex)
                        {
                            var errorResult = new ProjectParameterInfo
                            {
                                Name = paramDef.Name,
                                ParameterType = paramDef.ParameterType,
                                GroupName = paramDef.GroupName,
                                Status = $"Error: {ex.Message}"
                            };
                            results.Add(errorResult);
                            _logger.LogError($"Error creating parameter '{paramDef.Name}': {ex.Message}", ex);
                        }
                    }

                    trans.Commit();
                }

                // Update UI with results
                _onComplete?.Invoke(results);

                // Show summary
                int successful = results.Count(r => r.Status == "Created" || r.Status == "Already Exists");
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

        /// <summary>
        /// Create a single project parameter
        /// </summary>
        private ProjectParameterInfo CreateProjectParameter(Document doc, ProjectParameterDefinition paramDef)
        {
            var result = new ProjectParameterInfo
            {
                Name = paramDef.Name,
                ParameterType = paramDef.ParameterType,
                GroupName = paramDef.GroupName,
                Status = "Processing..."
            };

            // Check if parameter already exists
            var existingParam = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>()
                .FirstOrDefault(p => p.Name == paramDef.Name);

            if (existingParam != null)
            {
                result.Status = "Already Exists";
                return result;
            }

            // Get categories to bind to
            var categorySet = new CategorySet();
            foreach (var catName in paramDef.Categories)
            {
                var category = GetCategoryByName(doc, catName);
                if (category != null)
                {
                    categorySet.Insert(category);
                }
            }

            if (categorySet.IsEmpty)
            {
                result.Status = "Error: No valid categories found";
                return result;
            }

            // Create shared parameter file in memory or use existing
            // For simplicity, we'll create project parameters directly using ParameterElement
            // Note: This approach creates project parameters, not shared parameters

            // Map parameter type string to ForgeTypeId
            var parameterType = GetParameterType(paramDef.ParameterType);
            if (parameterType == null)
            {
                result.Status = $"Error: Unknown parameter type '{paramDef.ParameterType}'";
                return result;
            }

            // Map group name to ForgeTypeId
            var groupId = GetParameterGroup(paramDef.GroupName);

            // Create the parameter binding
            var binding = paramDef.IsInstance
                ? (ElementBinding)new InstanceBinding(categorySet)
                : (ElementBinding)new TypeBinding(categorySet);

            // Add parameter to document
            var bindingMap = doc.ParameterBindings;
            var paramGuid = Guid.NewGuid();

            // Create external definition (simplified approach)
            // In production, you'd use a shared parameter file
            var definition = CreateParameterDefinition(doc, paramDef.Name, parameterType, groupId);

            if (definition != null && bindingMap.Insert(definition, binding, groupId))
            {
                result.Status = "Created";
            }
            else
            {
                result.Status = "Error: Failed to bind parameter";
            }

            return result;
        }

        /// <summary>
        /// Create a parameter definition using shared parameters
        /// </summary>
        private Definition? CreateParameterDefinition(Document doc, string name, ForgeTypeId paramType, ForgeTypeId groupId)
        {
            // Get or create shared parameter file
            var app = doc.Application;
            var originalSharedParamFile = app.SharedParametersFilename;

            try
            {
                // Create temporary shared parameter file path
                var tempSharedParamFile = Path.Combine(Path.GetTempPath(), "BoltFrame_SharedParameters.txt");

                // Set the shared parameter file
                app.SharedParametersFilename = tempSharedParamFile;

                // Create the file if it doesn't exist
                if (!File.Exists(tempSharedParamFile))
                {
                    File.WriteAllText(tempSharedParamFile, "# Shared Parameter File\n");
                }

                // Open the shared parameter file
                var defFile = app.OpenSharedParameterFile();
                if (defFile == null)
                {
                    _logger.LogError($"Failed to open shared parameter file for '{name}'");
                    return null;
                }

                // Get or create the definition group
                var defGroup = defFile.Groups.get_Item("BoltFrame_LimitingDistance")
                    ?? defFile.Groups.Create("BoltFrame_LimitingDistance");

                // Check if definition already exists
                var existingDef = defGroup.Definitions.get_Item(name);
                if (existingDef != null)
                {
                    return existingDef;
                }

                // Create new external definition with GUID
                var options = new ExternalDefinitionCreationOptions(name, paramType)
                {
                    GUID = Guid.NewGuid()
                };

                var definition = defGroup.Definitions.Create(options) as ExternalDefinition;

                return definition;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating parameter definition for '{name}': {ex.Message}", ex);
                return null;
            }
            finally
            {
                // Restore original shared parameter file
                if (!string.IsNullOrEmpty(originalSharedParamFile))
                {
                    app.SharedParametersFilename = originalSharedParamFile;
                }
            }
        }

        /// <summary>
        /// Map parameter type string to ForgeTypeId
        /// </summary>
        private ForgeTypeId? GetParameterType(string typeName)
        {
            return typeName.ToUpper() switch
            {
                "TEXT" => SpecTypeId.String.Text,
                "INTEGER" => SpecTypeId.Int.Integer,
                "NUMBER" => SpecTypeId.Number,
                "LENGTH" => SpecTypeId.Length,
                "AREA" => SpecTypeId.Area,
                "VOLUME" => SpecTypeId.Volume,
                "ANGLE" => SpecTypeId.Angle,
                "YESNO" => SpecTypeId.Boolean.YesNo,
                "URL" => SpecTypeId.String.Url,
                "MATERIAL" => SpecTypeId.Reference.Material,
                _ => null
            };
        }

        /// <summary>
        /// Map group name string to ForgeTypeId
        /// </summary>
        private ForgeTypeId GetParameterGroup(string groupName)
        {
            return groupName.ToUpper() switch
            {
                "IDENTITY DATA" => GroupTypeId.IdentityData,
                "CONSTRAINTS" => GroupTypeId.Constraints,
                "DIMENSIONS" => GroupTypeId.Data, // Changed from Dimensions
                "CONSTRUCTION" => GroupTypeId.Construction,
                "MATERIALS AND FINISHES" => GroupTypeId.Materials, // Changed from MaterialsAndFinishes
                "PHASING" => GroupTypeId.Phasing,
                "STRUCTURAL" => GroupTypeId.Structural,
                "MECHANICAL" => GroupTypeId.Mechanical,
                "ELECTRICAL" => GroupTypeId.Electrical,
                "PLUMBING" => GroupTypeId.Plumbing,
                _ => GroupTypeId.IdentityData // Default
            };
        }

        /// <summary>
        /// Get category by name
        /// </summary>
        private Category? GetCategoryByName(Document doc, string categoryName)
        {
            try
            {
                var builtInCategory = (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), "OST_" + categoryName);
                return Category.GetCategory(doc, builtInCategory);
            }
            catch
            {
                // Try direct match
                foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
                {
                    try
                    {
                        var cat = Category.GetCategory(doc, bic);
                        if (cat != null && cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                        {
                            return cat;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        public string GetName()
        {
            return "Initialize Project Parameters Event Handler";
        }
    }
}
