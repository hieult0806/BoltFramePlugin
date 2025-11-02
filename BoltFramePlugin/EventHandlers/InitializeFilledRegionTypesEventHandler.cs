using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Models.LimitingDistance;
using BoltFramePlugin.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.EventHandlers
{
    /// <summary>
    /// External event handler to create filled region types with patterns and colors
    /// </summary>
    public class InitializeFilledRegionTypesEventHandler : IExternalEventHandler
    {
        private UIDocument? _uidoc;
        private ILoggingService _logger;
        private Action<List<FilledRegionTypeInfo>>? _onComplete;
        private List<FilledRegionTypeDefinition> _regionTypeDefs = new List<FilledRegionTypeDefinition>();

        public InitializeFilledRegionTypesEventHandler(ILoggingService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Set parameters for the event handler
        /// </summary>
        public void SetParameters(UIDocument uidoc, List<FilledRegionTypeDefinition> regionTypeDefs, Action<List<FilledRegionTypeInfo>> onComplete)
        {
            _uidoc = uidoc;
            _regionTypeDefs = regionTypeDefs;
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
            var results = new List<FilledRegionTypeInfo>();

            try
            {
                _logger.LogInformation($"Creating filled region types...");

                if (_regionTypeDefs == null || _regionTypeDefs.Count == 0)
                {
                    TaskDialog.Show("Error", "No filled region type definitions provided.");
                    _logger.LogError("No filled region type definitions provided.");
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Initialize Filled Region Types"))
                {
                    trans.Start();

                    foreach (var regionDef in _regionTypeDefs)
                    {
                        try
                        {
                            var result = CreateFilledRegionType(doc, regionDef);
                            results.Add(result);
                            _logger.LogInformation($"Filled region type '{regionDef.Name}': {result.Status}");
                        }
                        catch (Exception ex)
                        {
                            var errorResult = new FilledRegionTypeInfo
                            {
                                Name = regionDef.Name,
                                PatternName = regionDef.PatternName,
                                ColorDescription = regionDef.ColorDescription,
                                ColorR = regionDef.ColorR,
                                ColorG = regionDef.ColorG,
                                ColorB = regionDef.ColorB,
                                Status = $"Error: {ex.Message}"
                            };
                            results.Add(errorResult);
                            _logger.LogError($"Error creating filled region type '{regionDef.Name}': {ex.Message}", ex);
                        }
                    }

                    trans.Commit();
                }

                // Update UI with results
                _onComplete?.Invoke(results);

                // Show summary
                int successful = results.Count(r => r.Status == "Created" || r.Status == "Already Exists");
                int failed = results.Count - successful;

                TaskDialog.Show("Filled Region Types Initialized",
                    $"Successfully processed {successful} filled region types.\n" +
                    $"Failed: {failed}\n\n" +
                    $"See the Project Setup tab for detailed status.");

                _logger.LogInformation($"Filled region type initialization completed. Success: {successful}, Failed: {failed}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing filled region types: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to initialize filled region types:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Create a single filled region type with pattern and color
        /// </summary>
        private FilledRegionTypeInfo CreateFilledRegionType(Document doc, FilledRegionTypeDefinition regionDef)
        {
            var result = new FilledRegionTypeInfo
            {
                Name = regionDef.Name,
                PatternName = regionDef.PatternName,
                ColorDescription = regionDef.ColorDescription,
                ColorR = regionDef.ColorR,
                ColorG = regionDef.ColorG,
                ColorB = regionDef.ColorB,
                Status = "Processing..."
            };

            // Check if filled region type already exists
            var existingType = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault(frt => frt.Name == regionDef.Name);

            if (existingType != null)
            {
                result.Status = "Already Exists";
                return result;
            }

            // Find or create the fill pattern
            FillPattern fillPattern = null;
            FillPatternElement fillPatternElement = null;

            // Try to find existing solid fill pattern
            var fillPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .ToList();

            // Look for solid fill pattern
            fillPatternElement = fillPatterns.FirstOrDefault(fp =>
                fp.GetFillPattern().IsSolidFill);

            if (fillPatternElement == null)
            {
                // Use the default solid fill pattern - just find the first solid pattern
                // In Revit, solid fills are built-in, so we should always find one
                result.Status = "Error: No solid fill pattern found";
                _logger.LogError("No solid fill pattern found in document");
                return result;
            }

            // Get a filled region type to duplicate
            var baseFilledRegionType = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault();

            if (baseFilledRegionType == null)
            {
                result.Status = "Error: No base filled region type found to duplicate";
                return result;
            }

            // Duplicate the base type
            var newFilledRegionType = baseFilledRegionType.Duplicate(regionDef.Name) as FilledRegionType;

            if (newFilledRegionType == null)
            {
                result.Status = "Error: Failed to duplicate filled region type";
                return result;
            }

            // Set the fill pattern
            newFilledRegionType.ForegroundPatternId = fillPatternElement.Id;

            // Set the color (using Autodesk.Revit.DB.Color)
            var color = new Autodesk.Revit.DB.Color((byte)regionDef.ColorR, (byte)regionDef.ColorG, (byte)regionDef.ColorB);
            newFilledRegionType.ForegroundPatternColor = color;

            // Set line weight
            newFilledRegionType.LineWeight = regionDef.LineWeight;

            result.Status = "Created";
            _logger.LogInformation($"Created filled region type '{regionDef.Name}' with color RGB({regionDef.ColorR}, {regionDef.ColorG}, {regionDef.ColorB})");

            return result;
        }

        public string GetName()
        {
            return "Initialize Filled Region Types Event Handler";
        }
    }
}
