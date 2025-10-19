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

            // Extract merged cells using EPPlus
            try
            {
                var mergedCellsProp = worksheet.GetType().GetProperty("MergedCells");
                var mergedCells = mergedCellsProp?.GetValue(worksheet) as System.Collections.IEnumerable;

                if (mergedCells != null)
                {
                    foreach (var mergedRange in mergedCells)
                    {
                        var rangeStr = mergedRange.ToString();
                        var parts = rangeStr.Split(':');
                        if (parts.Length == 2)
                        {
                            var start = ParseCellAddress(parts[0]);
                            var end = ParseCellAddress(parts[1]);

                            var mergedCell = new MergedCellRange
                            {
                                StartRow = start.Row - dataStartRow,
                                StartColumn = start.Column - startCol,
                                EndRow = end.Row - dataStartRow,
                                EndColumn = end.Column - startCol
                            };

                            _logger.LogInformation($"Merged cell: {rangeStr} -> Row {mergedCell.StartRow}-{mergedCell.EndRow}, Col {mergedCell.StartColumn}-{mergedCell.EndColumn}");
                            result.MergedCells.Add(mergedCell);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to extract merged cells: {ex.Message}");
            }

            // Extract cell background colors using EPPlus
            try
            {
                for (int row = dataStartRow; row <= endRow; row++)
                {
                    for (int col = startCol; col <= endCol; col++)
                    {
                        var cell = GetEPPlusCell(worksheet, row, col);
                        if (cell != null)
                        {
                            var style = cell.GetType().GetProperty("Style")?.GetValue(cell);
                            if (style != null)
                            {
                                var fill = style.GetType().GetProperty("Fill")?.GetValue(style);
                                if (fill != null)
                                {
                                    var bgColor = fill.GetType().GetProperty("BackgroundColor")?.GetValue(fill);
                                    if (bgColor != null)
                                    {
                                        var rgbProp = bgColor.GetType().GetProperty("Rgb");
                                        var rgb = rgbProp?.GetValue(bgColor)?.ToString();

                                        if (!string.IsNullOrEmpty(rgb) && rgb != "00000000")
                                        {
                                            result.CellFormats.Add(new CellFormat
                                            {
                                                Row = row - dataStartRow,
                                                Column = col - startCol,
                                                BackgroundColor = rgb
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to extract cell colors: {ex.Message}");
            }

            _logger.LogInformation($"Successfully imported {result.RowCount} rows with {result.ColumnCount} columns, {result.MergedCells.Count} merged cells, {result.CellFormats.Count} formatted cells");
            return result;
        }

        private (int Row, int Column) ParseCellAddress(string address)
        {
            int col = 0;
            int row = 0;
            int i = 0;

            // Parse column (letters)
            while (i < address.Length && char.IsLetter(address[i]))
            {
                col = col * 26 + (char.ToUpper(address[i]) - 'A' + 1);
                i++;
            }

            // Parse row (numbers)
            while (i < address.Length && char.IsDigit(address[i]))
            {
                row = row * 10 + (address[i] - '0');
                i++;
            }

            return (row, col);
        }

        private object? GetEPPlusCell(object worksheet, int row, int col)
        {
            try
            {
                var cellsProp = worksheet.GetType().GetProperty("Cells");
                var cells = cellsProp?.GetValue(worksheet);
                if (cells != null)
                {
                    var indexer = cells.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) });
                    return indexer?.GetValue(cells, new object[] { row, col });
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
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
                    _logger.LogInformation($"ClosedXML Row {row} -> Data row index {result.Rows.Count - 1}: [{string.Join(", ", rowData.Select(c => $"\"{c}\""))}]");
                }
            }

            // Extract column widths using ClosedXML
            try
            {
                for (int col = firstColNum; col <= lastColNum; col++)
                {
                    var column = worksheet.GetType().GetMethod("Column", new[] { typeof(int) })?.Invoke(worksheet, new object[] { col });
                    if (column != null)
                    {
                        var widthProp = column.GetType().GetProperty("Width");
                        if (widthProp != null)
                        {
                            var width = (double)widthProp.GetValue(column);
                            result.ColumnWidths.Add(width);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to extract column widths: {ex.Message}");
            }

            // Extract row heights using ClosedXML
            try
            {
                // Include header row if present
                int startRow = config.HasHeaders ? firstRowNum : dataStartRow;
                for (int row = startRow; row <= lastRowNum; row++)
                {
                    var rowObj = worksheet.GetType().GetMethod("Row", new[] { typeof(int) })?.Invoke(worksheet, new object[] { row });
                    if (rowObj != null)
                    {
                        var heightProp = rowObj.GetType().GetProperty("Height");
                        if (heightProp != null)
                        {
                            var height = (double)heightProp.GetValue(rowObj);
                            result.RowHeights.Add(height);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to extract row heights: {ex.Message}");
            }

            // Extract merged cells using ClosedXML
            try
            {
                var mergedRangesProp = worksheet.GetType().GetProperty("MergedRanges");
                var mergedRanges = mergedRangesProp?.GetValue(worksheet) as System.Collections.IEnumerable;

                if (mergedRanges != null)
                {
                    foreach (var range in mergedRanges)
                    {
                        var mergeAddress = range.GetType().GetProperty("RangeAddress")?.GetValue(range);
                        if (mergeAddress != null)
                        {
                            var firstRow = (int)mergeAddress.GetType().GetProperty("FirstAddress")?.GetValue(mergeAddress)?.GetType().GetProperty("RowNumber")?.GetValue(mergeAddress.GetType().GetProperty("FirstAddress")?.GetValue(mergeAddress));
                            var lastRow = (int)mergeAddress.GetType().GetProperty("LastAddress")?.GetValue(mergeAddress)?.GetType().GetProperty("RowNumber")?.GetValue(mergeAddress.GetType().GetProperty("LastAddress")?.GetValue(mergeAddress));
                            var firstCol = (int)mergeAddress.GetType().GetProperty("FirstAddress")?.GetValue(mergeAddress)?.GetType().GetProperty("ColumnNumber")?.GetValue(mergeAddress.GetType().GetProperty("FirstAddress")?.GetValue(mergeAddress));
                            var lastCol = (int)mergeAddress.GetType().GetProperty("LastAddress")?.GetValue(mergeAddress)?.GetType().GetProperty("ColumnNumber")?.GetValue(mergeAddress.GetType().GetProperty("LastAddress")?.GetValue(mergeAddress));

                            var mergedCell = new MergedCellRange
                            {
                                StartRow = firstRow - dataStartRow,
                                StartColumn = firstCol - firstColNum,
                                EndRow = lastRow - dataStartRow,
                                EndColumn = lastCol - firstColNum
                            };

                            _logger.LogInformation($"ClosedXML merged cell: Excel R{firstRow}C{firstCol}:R{lastRow}C{lastCol} (dataStartRow={dataStartRow}, firstColNum={firstColNum}) -> Row {mergedCell.StartRow}-{mergedCell.EndRow}, Col {mergedCell.StartColumn}-{mergedCell.EndColumn}");
                            result.MergedCells.Add(mergedCell);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to extract merged cells from ClosedXML: {ex.Message}");
            }

            // Extract cell background colors using ClosedXML
            try
            {
                int colorCount = 0;
                for (int row = dataStartRow; row <= lastRowNum; row++)
                {
                    for (int col = firstColNum; col <= lastColNum; col++)
                    {
                        try
                        {
                            var cell = GetClosedXMLCell(worksheet, row, col);
                            if (cell != null)
                            {
                                string? bgColor = null;
                                string? alignment = null;

                                // Try to get Style
                                var styleObj = cell.GetType().GetProperty("Style")?.GetValue(cell);
                                if (styleObj != null)
                                {
                                    // Extract background color
                                    var fillObj = styleObj.GetType().GetProperty("Fill")?.GetValue(styleObj);
                                    if (fillObj != null)
                                    {
                                        var bgColorObj = fillObj.GetType().GetProperty("BackgroundColor")?.GetValue(fillObj);
                                        if (bgColorObj != null)
                                        {
                                            var colorObj = bgColorObj.GetType().GetProperty("Color")?.GetValue(bgColorObj);
                                            if (colorObj != null)
                                            {
                                                var toArgbMethod = colorObj.GetType().GetMethod("ToArgb");
                                                if (toArgbMethod != null)
                                                {
                                                    var argbValue = (int)toArgbMethod.Invoke(colorObj, null);
                                                    var hexColor = argbValue.ToString("X8");
                                                    if (hexColor != "00000000" && hexColor != "FFFFFFFF")
                                                    {
                                                        bgColor = hexColor;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Extract text alignment
                                    var alignmentObj = styleObj.GetType().GetProperty("Alignment")?.GetValue(styleObj);
                                    if (alignmentObj != null)
                                    {
                                        var horizontalProp = alignmentObj.GetType().GetProperty("Horizontal")?.GetValue(alignmentObj);
                                        if (horizontalProp != null)
                                        {
                                            var horizontalStr = horizontalProp.ToString();
                                            // Map ClosedXML alignment to our format
                                            if (horizontalStr == "Center") alignment = "Center";
                                            else if (horizontalStr == "Right") alignment = "Right";
                                            else if (horizontalStr == "Left") alignment = "Left";
                                            else if (horizontalStr == "General") alignment = null; // Use default
                                        }
                                    }
                                }

                                // Add cell format if there's any formatting
                                if (bgColor != null || alignment != null)
                                {
                                    result.CellFormats.Add(new CellFormat
                                    {
                                        Row = row - dataStartRow,
                                        Column = col - firstColNum,
                                        BackgroundColor = bgColor,
                                        TextAlignment = alignment
                                    });
                                    if (bgColor != null) colorCount++;
                                    _logger.LogInformation($"Found formatting at R{row}C{col} -> data row {row - dataStartRow}, col {col - firstColNum}: color={bgColor}, align={alignment}");
                                }
                            }
                        }
                        catch (Exception cellEx)
                        {
                            _logger.LogWarning($"Error extracting formatting from R{row}C{col}: {cellEx.Message}");
                        }
                    }
                }
                _logger.LogInformation($"Extracted {colorCount} cell colors from ClosedXML");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to extract cell colors from ClosedXML: {ex.Message}");
            }

            _logger.LogInformation($"Successfully imported {result.RowCount} rows with {result.ColumnCount} columns, {result.MergedCells.Count} merged cells, {result.CellFormats.Count} formatted cells");
            return result;
        }

        private object? GetClosedXMLCell(object worksheet, int row, int col)
        {
            try
            {
                var cellMethod = worksheet.GetType().GetMethod("Cell", new[] { typeof(int), typeof(int) });
                return cellMethod?.Invoke(worksheet, new object[] { row, col });
            }
            catch
            {
                // Ignore errors
            }
            return null;
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
