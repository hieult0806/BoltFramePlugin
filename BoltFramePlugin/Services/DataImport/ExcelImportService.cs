using BoltFramePlugin.Models.DataImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BoltFramePlugin.Services.DataImport
{
    /// <summary>
    /// Service for importing data from Excel files using ClosedXML library
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

            // Check if ClosedXML is available
            var closedXmlType = Type.GetType("ClosedXML.Excel.XLWorkbook, ClosedXML");
            if (closedXmlType != null)
            {
                return await ImportWithClosedXMLAsync(filePath, config);
            }

            // ClosedXML not found
            throw new InvalidOperationException(
                "ClosedXML library not found. " +
                "Please ensure ClosedXML NuGet package is installed:\n" +
                "Install-Package ClosedXML");
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
                                bool isBold = false;
                                bool isItalic = false;
                                bool isUnderline = false;

                                // Try to get Style - wrap in try-catch as this might fail for merged cells
                                try
                                {
                                    var styleObj = cell.GetType().GetProperty("Style")?.GetValue(cell);
                                    if (styleObj != null)
                                    {
                                        // Extract background color
                                        try
                                        {
                                            var fillObj = styleObj.GetType().GetProperty("Fill")?.GetValue(styleObj);
                                            if (fillObj != null)
                                            {
                                                var bgColorObj = fillObj.GetType().GetProperty("BackgroundColor")?.GetValue(fillObj);
                                                if (bgColorObj != null)
                                                {
                                                    // Check if this is a theme color or a standard color
                                                    var colorTypeProperty = bgColorObj.GetType().GetProperty("ColorType");
                                                    if (colorTypeProperty != null)
                                                    {
                                                        var colorType = colorTypeProperty.GetValue(bgColorObj);
                                                        var colorTypeStr = colorType?.ToString();

                                                        // Only try to extract color if it's a standard color, not a theme color
                                                        if (colorTypeStr == "Color")
                                                        {
                                                            var colorObj = bgColorObj.GetType().GetProperty("Color")?.GetValue(bgColorObj);
                                                            if (colorObj != null && colorObj.GetType().Name == "Color")
                                                            {
                                                                try
                                                                {
                                                                    var toArgbMethod = colorObj.GetType().GetMethod("ToArgb", Type.EmptyTypes);
                                                                    if (toArgbMethod != null)
                                                                    {
                                                                        var argbResult = toArgbMethod.Invoke(colorObj, null);
                                                                        if (argbResult != null)
                                                                        {
                                                                            var argbValue = (int)argbResult;
                                                                            var hexColor = argbValue.ToString("X8");
                                                                            if (hexColor != "00000000" && hexColor != "FFFFFFFF")
                                                                            {
                                                                                bgColor = hexColor;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                catch
                                                                {
                                                                    // Try alternative approach
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // Silently ignore color extraction errors
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

                                        // Extract font formatting (Bold, Italic, Underline)
                                        var fontObj = styleObj.GetType().GetProperty("Font")?.GetValue(styleObj);
                                        if (fontObj != null)
                                        {
                                            var boldProp = fontObj.GetType().GetProperty("Bold")?.GetValue(fontObj);
                                            if (boldProp != null && boldProp is bool)
                                            {
                                                isBold = (bool)boldProp;
                                            }

                                            var italicProp = fontObj.GetType().GetProperty("Italic")?.GetValue(fontObj);
                                            if (italicProp != null && italicProp is bool)
                                            {
                                                isItalic = (bool)italicProp;
                                            }

                                            var underlineProp = fontObj.GetType().GetProperty("Underline")?.GetValue(fontObj);
                                            if (underlineProp != null)
                                            {
                                                // Underline property might be an enum, check if it's not "None"
                                                var underlineStr = underlineProp.ToString();
                                                isUnderline = !string.IsNullOrEmpty(underlineStr) && underlineStr != "None";
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Silently ignore style extraction errors
                                }

                                // Add cell format if there's any formatting
                                if (bgColor != null || alignment != null || isBold || isItalic || isUnderline)
                                {
                                    result.CellFormats.Add(new CellFormat
                                    {
                                        Row = row - dataStartRow,
                                        Column = col - firstColNum,
                                        BackgroundColor = bgColor,
                                        TextAlignment = alignment,
                                        IsBold = isBold,
                                        IsItalic = isItalic,
                                        IsUnderline = isUnderline
                                    });
                                    if (bgColor != null) colorCount++;
                                    _logger.LogInformation($"Found formatting at R{row}C{col} -> data row {row - dataStartRow}, col {col - firstColNum}: color={bgColor}, align={alignment}, bold={isBold}, italic={isItalic}, underline={isUnderline}");
                                }
                            }
                        }
                        catch (Exception cellEx)
                        {
                            // Only log if it's not a color extraction issue we're handling separately
                            if (!cellEx.Message.Contains("Color"))
                            {
                                _logger.LogDebug($"Error extracting formatting from R{row}C{col}: {cellEx.Message}");
                            }
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


        private string GetClosedXMLCellValue(object worksheet, int row, int col)
        {
            try
            {
                var cellMethod = worksheet.GetType().GetMethod("Cell", new[] { typeof(int), typeof(int) });
                var cell = cellMethod?.Invoke(worksheet, new object[] { row, col });
                if (cell == null) return string.Empty;

                // If the cell is part of a merged range, only the top-left cell should return the value.
                var isMergedProp = cell.GetType().GetProperty("IsMerged");
                if (isMergedProp != null && isMergedProp.GetValue(cell) is bool isMerged && isMerged)
                {
                    try
                    {
                        var mergedRange = cell.GetType().GetMethod("MergedRange")?.Invoke(cell, null);
                        var firstCell = mergedRange?.GetType().GetMethod("FirstCell")?.Invoke(mergedRange, null);

                        // Address of current cell
                        var addr = cell.GetType().GetProperty("Address")?.GetValue(cell);
                        var addrRow = (int)(addr?.GetType().GetProperty("RowNumber")?.GetValue(addr) ?? row);
                        var addrCol = (int)(addr?.GetType().GetProperty("ColumnNumber")?.GetValue(addr) ?? col);

                        // Address of first (top-left) cell in the merged block
                        var faddr = firstCell?.GetType().GetProperty("Address")?.GetValue(firstCell);
                        var fRow = (int)(faddr?.GetType().GetProperty("RowNumber")?.GetValue(faddr) ?? row);
                        var fCol = (int)(faddr?.GetType().GetProperty("ColumnNumber")?.GetValue(faddr) ?? col);

                        var isTopLeft = addrRow == fRow && addrCol == fCol;

                        if (isTopLeft)
                        {
                            var v = firstCell?.GetType().GetProperty("Value")?.GetValue(firstCell);
                            return v?.ToString() ?? string.Empty;
                        }
                        else
                        {
                            // Covered cell: keep grid alignment by returning empty string
                            return string.Empty;
                        }
                    }
                    catch
                    {
                        // Fall through to regular value extraction if anything above fails
                    }
                }

                // Regular value extraction for non-merged cells
                var valueProp = cell.GetType().GetProperty("Value");
                var value = valueProp?.GetValue(cell);
                return value?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error getting cell value at R{row}C{col}: {ex.Message}");
                return string.Empty; // Never skip a column; preserve alignment
            }
        }

    }
}
