using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.NBCReview.Models;
using LoBIM.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.NBCReview.EventHandlers
{
    /// <summary>
    /// External event handler to load family files from a specified directory
    /// </summary>
    public class LoadFamiliesEventHandler : IExternalEventHandler
    {
        private UIDocument? _uidoc;
        private ILoggingService _logger;
        private Action<List<RequiredFamilyInfo>>? _onComplete;
        private string _familiesPath = string.Empty;

        public LoadFamiliesEventHandler(ILoggingService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Set parameters for the event handler
        /// </summary>
        public void SetParameters(UIDocument uidoc, string familiesPath, Action<List<RequiredFamilyInfo>> onComplete)
        {
            _uidoc = uidoc;
            _familiesPath = familiesPath;
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
            var results = new List<RequiredFamilyInfo>();

            try
            {
                _logger.LogInformation($"Loading families from: {_familiesPath}");

                // Check if directory exists
                if (!Directory.Exists(_familiesPath))
                {
                    TaskDialog.Show("Error", $"Families directory not found:\n{_familiesPath}");
                    _logger.LogError($"Families directory not found: {_familiesPath}");
                    return;
                }

                // Get all .rfa files in the directory, excluding Revit backup files (.0001.rfa, .0002.rfa, etc.)
                var allFamilyFiles = Directory.GetFiles(_familiesPath, "*.rfa");
                var familyFiles = allFamilyFiles.Where(f => !System.Text.RegularExpressions.Regex.IsMatch(
                    Path.GetFileName(f),
                    @"\.\d{4}\.rfa$")).ToArray();

                if (familyFiles.Length == 0)
                {
                    TaskDialog.Show("Information", $"No family files (.rfa) found in:\n{_familiesPath}");
                    _logger.LogWarning($"No family files found in: {_familiesPath}");
                    return;
                }

                int backupFilesCount = allFamilyFiles.Length - familyFiles.Length;
                if (backupFilesCount > 0)
                {
                    _logger.LogInformation($"Filtered out {backupFilesCount} Revit backup files.");
                }

                _logger.LogInformation($"Found {familyFiles.Length} family files to load.");

                using (Transaction trans = new Transaction(doc, "Load Required Families"))
                {
                    trans.Start();

                    foreach (var familyPath in familyFiles)
                    {
                        try
                        {
                            var result = LoadFamily(doc, familyPath);
                            results.Add(result);
                            _logger.LogInformation($"Family '{result.Name}': {result.Status}");
                        }
                        catch (Exception ex)
                        {
                            var errorResult = new RequiredFamilyInfo
                            {
                                Name = Path.GetFileNameWithoutExtension(familyPath),
                                Category = "Unknown",
                                Purpose = "Family loading failed",
                                FilePath = familyPath,
                                Status = $"Error: {ex.Message}"
                            };
                            results.Add(errorResult);
                            _logger.LogError($"Error loading family '{familyPath}': {ex.Message}", ex);
                        }
                    }

                    trans.Commit();
                }

                // Update UI with results
                _onComplete?.Invoke(results);

                // Show summary
                int successful = results.Count(r => r.Status == "Loaded" || r.Status == "Already Loaded");
                int failed = results.Count - successful;

                TaskDialog.Show("Families Loaded",
                    $"Successfully loaded {successful} families.\n" +
                    $"Failed: {failed}\n\n" +
                    $"See the Project Setup tab for detailed status.");

                _logger.LogInformation($"Family loading completed. Success: {successful}, Failed: {failed}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading families: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to load families:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Load a single family file
        /// </summary>
        private RequiredFamilyInfo LoadFamily(Document doc, string familyPath)
        {
            var familyName = Path.GetFileNameWithoutExtension(familyPath);
            var result = new RequiredFamilyInfo
            {
                Name = familyName,
                Category = "Generic Model",
                Purpose = "Loaded from Resources/Families",
                FilePath = familyPath,
                Status = "Processing..."
            };

            // Check if family is already loaded
            var existingFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name == familyName);

            if (existingFamily != null)
            {
                result.Status = "Already Loaded";
                result.Category = existingFamily.FamilyCategory?.Name ?? "Unknown";
                return result;
            }

            // Load the family
            Family loadedFamily;
            bool loaded = doc.LoadFamily(familyPath, out loadedFamily);

            if (loaded && loadedFamily != null)
            {
                result.Status = "Loaded";
                result.Category = loadedFamily.FamilyCategory?.Name ?? "Generic Model";
            }
            else
            {
                result.Status = "Error: Failed to load family";
            }

            return result;
        }

        public string GetName()
        {
            return "Load Families Event Handler";
        }
    }
}
