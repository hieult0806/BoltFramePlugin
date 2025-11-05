using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using LoBIM.Features.DataImport.Services.Import;
using LoBIM.Features.LimitingDistance.Models;
using LoBIM.Models.Tables;
using LoBIM.Services;
using LoBIM.Services.Rendering;

namespace LoBIM.Features.LimitingDistance.Services
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
    }
}
