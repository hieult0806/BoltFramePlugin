using BoltFramePlugin.Models.DataImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BoltFramePlugin.Services.DataImport
{
    /// <summary>
    /// Service for importing data from Excel files
    /// NOTE: This requires EPPlus or ClosedXML NuGet package to be installed
    /// Install via: Install-Package EPPlus (or ClosedXML)
    /// </summary>
    public class ExcelImportService : IDataImportService
    {
        private readonly ILoggingService _logger;

        public ExcelImportService(ILoggingService logger)
        {
            _logger = logger;
        }

        public bool SupportsFileType(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".xlsx" || extension == ".xls";
        }

        public async Task<ImportedTableData> ImportAsync(string filePath, ImportConfiguration config)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Excel file not found: {filePath}");
            }

            _logger.LogInformation($"Importing Excel file: {filePath}");

            // Check if EPPlus is available
            var epPlusType = Type.GetType("OfficeOpenXml.ExcelPackage, EPPlus");
            if (epPlusType != null)
            {
                return await ImportWithEPPlusAsync(filePath, config);
            }

            // Check if ClosedXML is available
            var closedXmlType = Type.GetType("ClosedXML.Excel.XLWorkbook, ClosedXML");
            if (closedXmlType != null)
            {
                return await ImportWithClosedXMLAsync(filePath, config);
            }

            // Fallback: Try to use COM automation (Excel Interop) - not recommended
            _logger.LogWarning("No Excel library found (EPPlus or ClosedXML). Attempting COM automation.");
            return await ImportWithComAutomationAsync(filePath, config);
        }

        private async Task<ImportedTableData> ImportWithEPPlusAsync(string filePath, ImportConfiguration config)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use reflection to avoid compile-time dependency
                    var epPlusAssembly = System.Reflection.Assembly.Load("EPPlus");
                    var packageType = epPlusAssembly.GetType("OfficeOpenXml.ExcelPackage");
                    var fileInfo = new FileInfo(filePath);

                    // Set EPPlus license context (required for EPPlus 5.0+)
                    var licenseContextType = epPlusAssembly.GetType("OfficeOpenXml.ExcelPackage+LicenseContext");
                    if (licenseContextType != null)
                    {
                        var licenseContextProperty = packageType.GetProperty("LicenseContext");
                        if (licenseContextProperty != null)
                        {
                            // Set to NonCommercial (1)
                            licenseContextProperty.SetValue(null, 1);
                        }
                    }

                    var package = Activator.CreateInstance(packageType, fileInfo);
                    try
                    {
                        var workbookProp = packageType.GetProperty("Workbook");
                        var workbook = workbookProp.GetValue(package);
                        var worksheetsProp = workbook.GetType().GetProperty("Worksheets");
                        var worksheets = worksheetsProp.GetValue(workbook);

                        // Get worksheet
                        object worksheet;
                        if (!string.IsNullOrEmpty(config.ExcelSheetName))
                        {
                            var getByNameMethod = worksheets.GetType().GetMethod("get_Item", new[] { typeof(string) });
                            worksheet = getByNameMethod.Invoke(worksheets, new object[] { config.ExcelSheetName });
                        }
                        else
                        {
                            var getByIndexMethod = worksheets.GetType().GetMethod("get_Item", new[] { typeof(int) });
                            worksheet = getByIndexMethod.Invoke(worksheets, new object[] { config.ExcelSheetIndex });
                        }

                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet not found");
                        }

                        return ExtractDataFromWorksheet(worksheet, config, filePath);
                    }
                    finally
                    {
                        if (package is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error importing Excel with EPPlus: {ex.Message}", ex);
                    throw;
                }
            });
        }

        private async Task<ImportedTableData> ImportWithClosedXMLAsync(string filePath, ImportConfiguration config)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var closedXmlAssembly = System.Reflection.Assembly.Load("ClosedXML");
                    var workbookType = closedXmlAssembly.GetType("ClosedXML.Excel.XLWorkbook");

                    var workbook = Activator.CreateInstance(workbookType, filePath);
                    try
                    {
                        // Get worksheet
                        object worksheet;
                        var worksheetsProp = workbookType.GetProperty("Worksheets");
                        var worksheets = worksheetsProp.GetValue(workbook);

                        if (!string.IsNullOrEmpty(config.ExcelSheetName))
                        {
                            var getByNameMethod = worksheets.GetType().GetMethod("Worksheet", new[] { typeof(string) });
                            worksheet = getByNameMethod.Invoke(worksheets, new object[] { config.ExcelSheetName });
                        }
                        else
                        {
                            var getByIndexMethod = worksheets.GetType().GetMethod("Worksheet", new[] { typeof(int) });
                            worksheet = getByIndexMethod.Invoke(worksheets, new object[] { config.ExcelSheetIndex + 1 }); // ClosedXML is 1-based
                        }

                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet not found");
                        }

                        return ExtractDataFromClosedXMLWorksheet(worksheet, config, filePath);
                    }
                    finally
                    {
                        if (workbook is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error importing Excel with ClosedXML: {ex.Message}", ex);
                    throw;
                }
            });
        }

        private Task<ImportedTableData> ImportWithComAutomationAsync(string filePath, ImportConfiguration config)
        {
            throw new NotImplementedException(
                "Excel COM automation is not implemented. " +
                "Please install EPPlus or ClosedXML NuGet package:\n" +
                "Install-Package EPPlus\n" +
                "or\n" +
                "Install-Package ClosedXML");
        }

        private ImportedTableData ExtractDataFromWorksheet(object worksheet, ImportConfiguration config, string filePath)
        {
            var result = new ImportedTableData
            {
                SourceFilePath = filePath,
                ImportedAt = DateTime.Now
            };

            // Get dimension property
            var dimensionProp = worksheet.GetType().GetProperty("Dimension");
            var dimension = dimensionProp.GetValue(worksheet);
            if (dimension == null)
            {
                _logger.LogWarning("Worksheet is empty");
                return result;
            }

            var startRow = (int)dimension.GetType().GetProperty("Start").GetValue(dimension).GetType().GetProperty("Row").GetValue(dimension.GetType().GetProperty("Start").GetValue(dimension));
            var endRow = (int)dimension.GetType().GetProperty("End").GetValue(dimension).GetType().GetProperty("Row").GetValue(dimension.GetType().GetProperty("End").GetValue(dimension));
            var startCol = (int)dimension.GetType().GetProperty("Start").GetValue(dimension).GetType().GetProperty("Column").GetValue(dimension.GetType().GetProperty("Start").GetValue(dimension));
            var endCol = (int)dimension.GetType().GetProperty("End").GetValue(dimension).GetType().GetProperty("Column").GetValue(dimension.GetType().GetProperty("End").GetValue(dimension));

            int dataStartRow = startRow;

            // Extract headers
            if (config.HasHeaders)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    var cellValue = GetCellValue(worksheet, startRow, col);
                    result.Headers.Add(config.TrimWhitespace ? cellValue.Trim() : cellValue);
                }
                dataStartRow++;
            }
            else
            {
                // Generate default headers
                for (int col = startCol; col <= endCol; col++)
                {
                    result.Headers.Add($"Column{col}");
                }
            }

            // Extract data rows
            for (int row = dataStartRow; row <= endRow; row++)
            {
                var rowData = new List<string>();
                bool isEmptyRow = true;

                for (int col = startCol; col <= endCol; col++)
                {
                    var cellValue = GetCellValue(worksheet, row, col);
                    if (!string.IsNullOrWhiteSpace(cellValue))
                        isEmptyRow = false;

                    rowData.Add(config.TrimWhitespace ? cellValue.Trim() : cellValue);
                }

                if (!isEmptyRow || !config.SkipEmptyRows)
                {
                    result.Rows.Add(rowData);
                }
            }

            _logger.LogInformation($"Successfully imported {result.RowCount} rows with {result.ColumnCount} columns");
            return result;
        }

        private ImportedTableData ExtractDataFromClosedXMLWorksheet(object worksheet, ImportConfiguration config, string filePath)
        {
            var result = new ImportedTableData
            {
                SourceFilePath = filePath,
                ImportedAt = DateTime.Now
            };

            // Get used range - specify empty parameter types to get the parameterless overload
            var rangeUsedMethod = worksheet.GetType().GetMethod("RangeUsed", Type.EmptyTypes);
            var usedRange = rangeUsedMethod?.Invoke(worksheet, null);
            if (usedRange == null)
            {
                _logger.LogWarning("Worksheet is empty");
                return result;
            }

            // Get row and column bounds using ClosedXML API
            var rangeAddressProp = usedRange.GetType().GetProperty("RangeAddress");
            if (rangeAddressProp == null)
            {
                throw new InvalidOperationException("Could not find RangeAddress property on ClosedXML range");
            }

            var rangeAddress = rangeAddressProp.GetValue(usedRange);
            if (rangeAddress == null)
            {
                throw new InvalidOperationException("RangeAddress is null");
            }

            var firstAddressProp = rangeAddress.GetType().GetProperty("FirstAddress");
            var lastAddressProp = rangeAddress.GetType().GetProperty("LastAddress");

            if (firstAddressProp == null || lastAddressProp == null)
            {
                throw new InvalidOperationException("Could not find FirstAddress or LastAddress properties");
            }

            var firstAddress = firstAddressProp.GetValue(rangeAddress);
            var lastAddress = lastAddressProp.GetValue(rangeAddress);

            var firstRowNum = (int)firstAddress.GetType().GetProperty("RowNumber").GetValue(firstAddress);
            var lastRowNum = (int)lastAddress.GetType().GetProperty("RowNumber").GetValue(lastAddress);
            var firstColNum = (int)firstAddress.GetType().GetProperty("ColumnNumber").GetValue(firstAddress);
            var lastColNum = (int)lastAddress.GetType().GetProperty("ColumnNumber").GetValue(lastAddress);

            int dataStartRow = firstRowNum;

            // Extract headers
            if (config.HasHeaders)
            {
                for (int col = firstColNum; col <= lastColNum; col++)
                {
                    var cellValue = GetClosedXMLCellValue(worksheet, firstRowNum, col);
                    result.Headers.Add(config.TrimWhitespace ? cellValue.Trim() : cellValue);
                }
                dataStartRow++;
            }
            else
            {
                for (int col = firstColNum; col <= lastColNum; col++)
                {
                    result.Headers.Add($"Column{col}");
                }
            }

            // Extract data rows
            for (int row = dataStartRow; row <= lastRowNum; row++)
            {
                var rowData = new List<string>();
                bool isEmptyRow = true;

                for (int col = firstColNum; col <= lastColNum; col++)
                {
                    var cellValue = GetClosedXMLCellValue(worksheet, row, col);
                    if (!string.IsNullOrWhiteSpace(cellValue))
                        isEmptyRow = false;

                    rowData.Add(config.TrimWhitespace ? cellValue.Trim() : cellValue);
                }

                if (!isEmptyRow || !config.SkipEmptyRows)
                {
                    result.Rows.Add(rowData);
                }
            }

            _logger.LogInformation($"Successfully imported {result.RowCount} rows with {result.ColumnCount} columns");
            return result;
        }

        private string GetCellValue(object worksheet, int row, int col)
        {
            try
            {
                var cellsProp = worksheet.GetType().GetProperty("Cells");
                var cells = cellsProp.GetValue(worksheet);
                var getItemMethod = cells.GetType().GetMethod("get_Item", new[] { typeof(int), typeof(int) });
                var cell = getItemMethod.Invoke(cells, new object[] { row, col });

                if (cell == null)
                    return string.Empty;

                var valueProp = cell.GetType().GetProperty("Value");
                var value = valueProp.GetValue(cell);

                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetClosedXMLCellValue(object worksheet, int row, int col)
        {
            try
            {
                var cellMethod = worksheet.GetType().GetMethod("Cell", new[] { typeof(int), typeof(int) });
                var cell = cellMethod.Invoke(worksheet, new object[] { row, col });

                if (cell == null)
                    return string.Empty;

                var valueProp = cell.GetType().GetProperty("Value");
                var value = valueProp.GetValue(cell);

                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
