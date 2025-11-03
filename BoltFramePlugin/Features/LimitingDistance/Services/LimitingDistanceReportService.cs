using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using BoltFramePlugin.Features.DataImport.Services.Import;
using BoltFramePlugin.Features.LimitingDistance.Models;
using BoltFramePlugin.Models.Tables;
using BoltFramePlugin.Services;
using BoltFramePlugin.Services.Rendering;

namespace BoltFramePlugin.Features.LimitingDistance.Services
{
    /// <summary>
    /// Service for generating Limiting Distance reports using Excel templates
    /// Utilizes the DataImport feature's ExcelImportService
    /// </summary>
    public class LimitingDistanceReportService
    {
        private readonly ExcelImportService _excelImportService;
        private readonly ITableRenderService _tableRenderService;
        private readonly ILoggingService _logger;

        public LimitingDistanceReportService(
            ExcelImportService excelImportService,
            ITableRenderService tableRenderService,
            ILoggingService logger)
        {
            _excelImportService = excelImportService;
            _tableRenderService = tableRenderService;
            _logger = logger;
        }

        /// <summary>
        /// Load NBC compliance requirements from the template Excel file
        /// </summary>
        public async Task<NBCComplianceTable> LoadNBCComplianceTemplateAsync(string templatePath)
        {
            _logger.LogInformation($"Loading NBC compliance template from: {templatePath}");

            var config = new ImportConfiguration
            {
                HasHeaders = true,
                ExcelSheetIndex = 0, // First sheet
                SkipEmptyRows = true,
                TrimWhitespace = true
            };

            var tableData = await _excelImportService.ImportAsync(templatePath, config);

            var complianceTable = new NBCComplianceTable
            {
                CodeReference = "NBC 2020",
                CheckDate = DateTime.Now
            };

            // Parse each row into hierarchical structure
            // Expected columns based on screenshot:
            // 0: Orientation
            // 1: Classification of building
            // 2: Limiting Distance (m)
            // 3: Area of building exposing face (sqm)
            // 4: Maximum Allowance by NBC (%)
            // 5: Proposed opening (%)
            // 6: FRR
            // 7: Type of Construction Required
            // 8: Type of Cladding Required

            var orientationDict = new Dictionary<int, Models.Orientation>();

            foreach (var row in tableData.Rows)
            {
                if (row.Count < 9) continue; // Skip incomplete rows

                try
                {
                    int orientationId = ParseInt(row[0]);
                    string classification = row[1]?.Trim() ?? "";

                    // Get or create Orientation
                    if (!orientationDict.TryGetValue(orientationId, out var orientation))
                    {
                        orientation = new Models.Orientation { ID = orientationId };
                        orientationDict[orientationId] = orientation;
                    }

                    // Get or create BuildingClassification
                    var buildingClassification = orientation.FindClassification(classification);
                    if (buildingClassification == null)
                    {
                        buildingClassification = new BuildingClassification { GroupName = classification };
                        orientation.Classifications.Add(buildingClassification);
                    }

                    // Create EBFDetails
                    var ebfDetails = new EBFDetails
                    {
                        LimitingDistanceM = ParseDouble(row[2]),
                        AreaOfBuildingExposingFaceSqm = ParseInt(row[3]),
                        MaximumAllowanceByNBC = row[4]?.Trim() ?? "",
                        ProposedOpening = row[5]?.Trim() ?? "",
                        FRR = row[6]?.Trim() ?? "",
                        TypeOfConstructionRequired = row[7]?.Trim() ?? "",
                        TypeOfCladdingRequired = row[8]?.Trim() ?? ""
                    };

                    buildingClassification.EBFDetailsList.Add(ebfDetails);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse row: {string.Join(", ", row)}. Error: {ex.Message}");
                }
            }

            // Add all orientations to the compliance table
            complianceTable.Orientations.AddRange(orientationDict.Values.OrderBy(o => o.ID));

            int totalRequirements = complianceTable.Orientations.Sum(o =>
                o.Classifications.Sum(c => c.EBFDetailsList.Count));
            _logger.LogInformation($"Loaded {totalRequirements} NBC compliance requirements across {complianceTable.Orientations.Count} orientations");
            return complianceTable;
        }

        /// <summary>
        /// Generate a compliance report for a specific wall/orientation
        /// Populates the template with actual project data
        /// </summary>
        public async Task<string> GenerateComplianceReportAsync(
            string templatePath,
            List<DistanceGroupSummary> distanceGroups,
            BuildingCodeComplianceSummary complianceSummary,
            string outputPath = null)
        {
            _logger.LogInformation("Generating Limiting Distance compliance report");

            // If no output path specified, create a temp file
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(Path.GetTempPath(), $"LimitingDistance_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }

            // Copy template to output location
            File.Copy(templatePath, outputPath, true);

            // Use ClosedXML to populate the template with actual data
            // This requires direct ClosedXML access (we'll implement this next)
            await PopulateTemplateWithDataAsync(outputPath, distanceGroups, complianceSummary);

            _logger.LogInformation($"Report generated: {outputPath}");
            return outputPath;
        }

        /// <summary>
        /// Render a report to a Revit Drafting View
        /// </summary>
        public async Task<ViewDrafting> RenderReportToDraftingViewAsync(
            Document doc,
            string reportPath,
            string viewName,
            TableRenderOptions options = null)
        {
            _logger.LogInformation($"Rendering report to drafting view: {viewName}");

            // Import the Excel report
            var config = new ImportConfiguration { HasHeaders = true };
            var tableData = await _excelImportService.ImportAsync(reportPath, config);

            // Create drafting view
            var view = _tableRenderService.CreateDraftingView(doc, viewName, (int)(options?.ViewScale ?? 48));

            // Render the table
            if (options == null)
            {
                options = new TableRenderOptions
                {
                    ViewScale = 48, // 1/4" = 1'-0"
                    PaperTextHeight = 0.125, // 1/8" text
                    DrawGridLines = true,
                    TextStyleName = "Standard",
                    ColumnWidth = 1.5, // 1.5 ft
                    RowHeight = 0.167 // 2 inches
                };
            }

            using (var transaction = new Transaction(doc, "Render Limiting Distance Report"))
            {
                transaction.Start();
                _tableRenderService.RenderToView(doc, view, tableData, options);
                transaction.Commit();
            }

            _logger.LogInformation("Report rendered successfully");
            return view;
        }

        /// <summary>
        /// Populate Excel template with actual project data
        /// </summary>
        private async Task PopulateTemplateWithDataAsync(
            string excelPath,
            List<DistanceGroupSummary> distanceGroups,
            BuildingCodeComplianceSummary complianceSummary)
        {
            // TODO: Implement using ClosedXML to write data back to Excel
            // This will populate cells with actual measured values from your project
            // For now, this is a placeholder

            await Task.CompletedTask;
            _logger.LogInformation("Template population not yet implemented - using template as-is");
        }

        #region Helper Methods

        private int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (int.TryParse(value.Trim(), out int result))
                return result;

            return 0;
        }

        private double ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0.0;

            if (double.TryParse(value.Trim(), out double result))
                return result;

            return 0.0;
        }

        #endregion
    }
}
