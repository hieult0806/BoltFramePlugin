using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Models.Tables;
using LoBIM.Features.DataImport.Services.Import;
using LoBIM.Services.Rendering;
using LoBIM.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LoBIM.Features.DataImport.Services
{
    /// <summary>
    /// Manager for coordinating data import and rendering operations
    /// </summary>
    public class DataImportManager
    {
        private readonly List<IDataImportService> _importServices;
        private readonly ITableRenderService _renderService;
        private readonly ILoggingService _logger;

        public DataImportManager(
            ITableRenderService renderService,
            ILoggingService logger)
        {
            _renderService = renderService;
            _logger = logger;

            // Register all import services
            _importServices = new List<IDataImportService>
            {
                new CsvImportService(logger),
                new ExcelImportService(logger)
            };
        }

        /// <summary>
        /// Import data from a file
        /// </summary>
        public async Task<ImportedTableData> ImportDataAsync(string filePath, ImportConfiguration config = null)
        {
            config = config ?? new ImportConfiguration();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var service = _importServices.FirstOrDefault(s => s.SupportsFileType(filePath));

            if (service == null)
            {
                throw new NotSupportedException($"File type not supported: {Path.GetExtension(filePath)}");
            }

            // Copy file to temp location to avoid file locking issues
            string? tempFilePath = null;
            try
            {
                tempFilePath = Path.Combine(Path.GetTempPath(), $"BoltFrame_Import_{Guid.NewGuid()}{Path.GetExtension(filePath)}");
                _logger.LogInformation($"Copying file to temp location: {tempFilePath}");
                File.Copy(filePath, tempFilePath, overwrite: true);

                _logger.LogInformation($"Importing file from temp location: {tempFilePath}");
                return await service.ImportAsync(tempFilePath, config);
            }
            finally
            {
                // Clean up temp file
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                        _logger.LogInformation($"Deleted temp file: {tempFilePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not delete temp file {tempFilePath}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Import and render data to a drafting view
        /// </summary>
        public async Task<ViewDrafting> ImportAndRenderAsync(
            Document doc,
            string filePath,
            string viewName = null,
            ImportConfiguration importConfig = null,
            TableRenderOptions renderOptions = null,
            UIDocument uidoc = null)
        {
            try
            {
                _logger.LogInformation($"Starting ImportAndRenderAsync. FilePath: {filePath}");

                // Import data
                var data = await ImportDataAsync(filePath, importConfig);
                _logger.LogInformation($"Data imported successfully. Rows: {data?.RowCount ?? 0}, Columns: {data?.ColumnCount ?? 0}");

                if (data == null)
                {
                    throw new InvalidOperationException("ImportDataAsync returned null");
                }

                // Generate view name if not provided
                if (string.IsNullOrEmpty(viewName))
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    viewName = $"Imported Table - {fileName}";
                }

                _logger.LogInformation($"View name: {viewName}");

                ViewDrafting view;

                // Create or use existing view
                using (Transaction trans = new Transaction(doc, "Import Table Data"))
                {
                    trans.Start();
                    _logger.LogInformation("Transaction started");

                    try
                    {
                        // Check if a view with this name already exists and delete it
                        var existingView = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewDrafting))
                            .Cast<ViewDrafting>()
                            .FirstOrDefault(v => v.Name == viewName);

                        if (existingView != null)
                        {
                            _logger.LogInformation($"Found existing view '{viewName}' (Id: {existingView.Id}). Attempting to delete it...");

                            try
                            {
                                var deletedIds = doc.Delete(existingView.Id);
                                _logger.LogInformation($"Existing view deleted (deleted {deletedIds.Count} elements)");
                            }
                            catch (Exception deleteEx)
                            {
                                _logger.LogWarning($"Could not delete existing view: {deleteEx.Message}. Will create view with modified name.");
                                // If we can't delete, modify the view name to avoid conflict
                                viewName = $"{viewName} ({DateTime.Now:HHmmss})";
                                _logger.LogInformation($"Using modified view name: {viewName}");
                            }
                        }

                        // Create drafting view with specified scale
                        _logger.LogInformation($"Creating drafting view with scale 1:{renderOptions.ViewScale}...");
                        view = _renderService.CreateDraftingView(doc, viewName, (int)renderOptions.ViewScale);
                        _logger.LogInformation($"Drafting view created: {view?.Name ?? "NULL"}");

                        if (view == null)
                        {
                            throw new InvalidOperationException("CreateDraftingView returned null");
                        }

                        // Render table
                        _logger.LogInformation("Rendering table to view...");
                        _renderService.RenderToView(doc, view, data, renderOptions ?? new TableRenderOptions());
                        _logger.LogInformation("Table rendered successfully");

                        trans.Commit();
                        _logger.LogInformation($"Transaction committed. Successfully imported and rendered table to view: {viewName}");
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        _logger.LogError($"Error in transaction (rolling back): {ex.Message}\n\nStack: {ex.StackTrace}", ex);
                        throw;
                    }
                }

                return view;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ImportAndRenderAsync: {ex.Message}\n\nStack: {ex.StackTrace}", ex);
                throw;
            }
        }

        /// <summary>
        /// Get supported file extensions
        /// </summary>
        public List<string> GetSupportedExtensions()
        {
            return new List<string> { ".csv", ".txt", ".xlsx", ".xls" };
        }

        /// <summary>
        /// Get file filter for dialog
        /// </summary>
        public string GetFileFilter()
        {
            return "Supported Files (*.csv;*.xlsx;*.xls;*.txt)|*.csv;*.xlsx;*.xls;*.txt|" +
                   "CSV Files (*.csv;*.txt)|*.csv;*.txt|" +
                   "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|" +
                   "All Files (*.*)|*.*";
        }
    }
}
