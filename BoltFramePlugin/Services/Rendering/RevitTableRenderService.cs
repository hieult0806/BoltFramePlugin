using Autodesk.Revit.DB;
using BoltFramePlugin.Models.Tables;
using BoltFramePlugin.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BoltFramePlugin.Services.Rendering
{
    /// <summary>
    /// Service for rendering table data to Revit drafting views using Detail Items
    /// </summary>
    public class RevitTableRenderService : ITableRenderService
    {
        private readonly ILoggingService _logger;

        public RevitTableRenderService(ILoggingService logger)
        {
            _logger = logger;
        }

        public ViewDrafting CreateDraftingView(Document doc, string viewName, int viewScale)
        {
            // Find a drafting view type
            var viewFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

            if (viewFamilyType == null)
            {
                throw new InvalidOperationException("No drafting view type found in the document");
            }

            var draftingView = ViewDrafting.Create(doc, viewFamilyType.Id);
            draftingView.Name = viewName;

            // Set the view scale using the Scale property (not the parameter, which is read-only)
            try
            {
                _logger.LogInformation($"Current view scale before setting: {draftingView.Scale}");

                // Set the scale using the Scale property
                draftingView.Scale = viewScale;

                _logger.LogInformation($"Set view scale to: {viewScale}");
                _logger.LogInformation($"View scale after setting: {draftingView.Scale}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting view scale: {ex.Message}\n{ex.StackTrace}");
            }

            _logger.LogInformation($"Created drafting view: {viewName} with scale 1:{viewScale}");
            return draftingView;
        }

        public void RenderToView(Document doc, Autodesk.Revit.DB.View view, ImportedTableData data, TableRenderOptions options)
        {
            if (!(view is ViewDrafting))
            {
                throw new ArgumentException("View must be a drafting view", nameof(view));
            }

            _logger.LogInformation($"Rendering table with {data.RowCount} rows and {data.ColumnCount} columns, {data.MergedCells.Count} merged cells, {data.CellFormats.Count} formatted cells to view {view.Name}");

            try
            {
                // Get text type
                var textType = GetOrCreateTextNoteType(doc, options.TextStyleName, options.TextHeight);

                // Calculate column widths
                var columnWidths = CalculateColumnWidths(data, options);

                // Build merged cell lookup for fast access
                var mergedCellLookup = BuildMergedCellLookup(data.MergedCells);

                // Build cell format lookup for fast access
                var cellFormatLookup = BuildCellFormatLookup(data.CellFormats);

                // We no longer skip entire rows - we handle merged cells at the cell level
                // This ensures that cells adjacent to vertical merges are still rendered

                double currentY = options.StartY;

                // Calculate proportional row heights if Excel data is available
                List<double> rowHeights = CalculateRowHeights(data, options);

                // Render header
                if (data.Headers.Count > 0)
                {
                    // Header is not part of the data rows, so use a dummy index (-1 or similar)
                    // Merged cells are indexed relative to data rows only, not including header
                    double headerHeight = rowHeights.Count > 0 ? rowHeights[0] : options.RowHeight;
                    currentY = RenderRow(doc, view, data.Headers, options.StartX, currentY, columnWidths, options, textType,
                        -1, mergedCellLookup, cellFormatLookup, isHeader: true, rowHeight: headerHeight);
                }

                // Render data rows
                int rowIndex = 0;
                foreach (var row in data.Rows)
                {
                    // We now render all rows, handling merged cells at the cell level
                    _logger.LogInformation($"Rendering data row {rowIndex}: [{string.Join(", ", row.Select(c => $"\"{c}\""))}]");
                    int heightIndex = rowHeights.Count > 0 ? (rowIndex + 1) : 0; // +1 because header is at index 0
                    double dataRowHeight = (heightIndex < rowHeights.Count) ? rowHeights[heightIndex] : options.RowHeight;
                    currentY = RenderRow(doc, view, row, options.StartX, currentY, columnWidths, options, textType,
                        rowIndex, mergedCellLookup, cellFormatLookup, isHeader: false, rowHeight: dataRowHeight);
                    rowIndex++;
                }

                _logger.LogInformation("Table rendering completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error rendering table: {ex.Message}", ex);
                throw;
            }
        }

        private Dictionary<(int Row, int Col), MergedCellRange> BuildMergedCellLookup(List<MergedCellRange> mergedCells)
        {
            var lookup = new Dictionary<(int Row, int Col), MergedCellRange>();
            foreach (var merge in mergedCells)
            {
                for (int r = merge.StartRow; r <= merge.EndRow; r++)
                {
                    for (int c = merge.StartColumn; c <= merge.EndColumn; c++)
                    {
                        lookup[(r, c)] = merge;
                    }
                }
            }
            return lookup;
        }

        private Dictionary<(int Row, int Col), CellFormat> BuildCellFormatLookup(List<CellFormat> cellFormats)
        {
            var lookup = new Dictionary<(int Row, int Col), CellFormat>();
            foreach (var format in cellFormats)
            {
                lookup[(format.Row, format.Column)] = format;
            }
            return lookup;
        }

        private double RenderRow(Document doc, Autodesk.Revit.DB.View view, List<string> cells, double startX, double startY,
            List<double> columnWidths, TableRenderOptions options, TextNoteType textType, int rowIndex,
            Dictionary<(int Row, int Col), MergedCellRange> mergedCellLookup,
            Dictionary<(int Row, int Col), CellFormat> cellFormatLookup, bool isHeader, double rowHeight)
        {
            double currentX = startX;

            // Track the maximum row span in this row (for calculating next Y position)
            int maxRowSpan = 1;

            // Draw filled region for header background
            if (isHeader && options.FillHeaderBackground)
            {
                DrawFilledRegion(doc, view, startX, startY, columnWidths.Sum(), rowHeight, options.HeaderFillTypeName);
            }

            _logger.LogInformation($"Starting row {rowIndex} with {cells.Count} cells");

            // Collect merged cells that start in this row for later rendering
            var mergedCellsToRender = new List<(int colIndex, MergedCellRange merge, string text)>();

            // PASS 1: Render normal cells (skip any cell that's part of a merge)
            currentX = startX;
            for (int colIndex = 0; colIndex < cells.Count && colIndex < columnWidths.Count; colIndex++)
            {
                var cellText = cells[colIndex];
                var cellWidth = columnWidths[colIndex];

                _logger.LogInformation($"Processing cell at row {rowIndex}, col {colIndex}, text='{cellText}'");

                // Check if this cell is part of any merged range
                if (mergedCellLookup.TryGetValue((rowIndex, colIndex), out var merge))
                {
                    _logger.LogInformation($"  Found merge: StartRow={merge.StartRow}, EndRow={merge.EndRow}, StartCol={merge.StartColumn}, EndCol={merge.EndColumn}");

                    // If this is the top-left cell of the merge, save it for later rendering
                    bool isTopLeft = (merge.StartRow == rowIndex && merge.StartColumn == colIndex);
                    if (isTopLeft)
                    {
                        mergedCellsToRender.Add((colIndex, merge, cellText));
                        _logger.LogInformation($"  Saved merged cell for later rendering");

                        // Track max row span
                        if (merge.RowSpan > maxRowSpan)
                        {
                            maxRowSpan = merge.RowSpan;
                        }
                    }

                    // Skip this cell - it's part of a merge and will be rendered in pass 2
                    _logger.LogInformation($"  Skipping cell - part of merge");
                    currentX += cellWidth;
                    continue;
                }

                // This is a normal cell - render it
                RenderNormalCell(doc, view, cellText, currentX, startY, cellWidth, rowHeight, rowIndex, colIndex,
                    textType, cellFormatLookup, options);

                currentX += cellWidth;
                _logger.LogInformation($"  End of col {colIndex}: currentX={currentX:F3}");
            }

            // PASS 2: Render merged cells
            foreach (var (colIndex, merge, text) in mergedCellsToRender)
            {
                RenderMergedCell(doc, view, text, startX, startY, rowHeight, rowIndex, colIndex, merge,
                    columnWidths, textType, cellFormatLookup, options);
            }

            // Draw grid lines for this row
            // We draw grid lines per-cell to handle merged cells properly
            if (options.DrawGridLines)
            {
                currentX = startX;
                for (int colIndex = 0; colIndex < columnWidths.Count; colIndex++)
                {
                    double cellWidth = columnWidths[colIndex];
                    double cellHeight = rowHeight;
                    bool shouldDrawGridForCell = true;

                    // Check if this cell is part of a merge
                    if (mergedCellLookup.TryGetValue((rowIndex, colIndex), out var merge))
                    {
                        bool isTopLeft = merge.StartRow == rowIndex && merge.StartColumn == colIndex;

                        if (isTopLeft)
                        {
                            // This is the top-left of a merge - draw the full merged cell boundary
                            // Calculate merged cell dimensions
                            cellWidth = 0;
                            for (int c = merge.StartColumn; c <= merge.EndColumn && c < columnWidths.Count; c++)
                            {
                                cellWidth += columnWidths[c];
                            }
                            cellHeight = rowHeight * merge.RowSpan;

                            // Draw full boundary of merged cell
                            DrawDetailLine(doc, view, currentX, startY, currentX, startY - cellHeight, options.LineStyleName); // Left
                            DrawDetailLine(doc, view, currentX + cellWidth, startY, currentX + cellWidth, startY - cellHeight, options.LineStyleName); // Right
                            DrawDetailLine(doc, view, currentX, startY, currentX + cellWidth, startY, options.LineStyleName); // Top
                            DrawDetailLine(doc, view, currentX, startY - cellHeight, currentX + cellWidth, startY - cellHeight, options.LineStyleName); // Bottom
                        }

                        // Skip grid drawing for non-top-left cells of merge
                        shouldDrawGridForCell = false;
                    }

                    if (shouldDrawGridForCell)
                    {
                        // Draw grid for normal cell
                        DrawDetailLine(doc, view, currentX, startY, currentX, startY - cellHeight, options.LineStyleName); // Left
                        DrawDetailLine(doc, view, currentX + cellWidth, startY, currentX + cellWidth, startY - cellHeight, options.LineStyleName); // Right
                        DrawDetailLine(doc, view, currentX, startY, currentX + cellWidth, startY, options.LineStyleName); // Top
                        DrawDetailLine(doc, view, currentX, startY - cellHeight, currentX + cellWidth, startY - cellHeight, options.LineStyleName); // Bottom
                    }

                    currentX += columnWidths[colIndex];
                }
            }

            return startY - rowHeight; // Subtract to move down by one row (Y decreases downward in Revit)
        }

        private void RenderNormalCell(Document doc, Autodesk.Revit.DB.View view, string cellText, double x, double y,
            double cellWidth, double cellHeight, int rowIndex, int colIndex, TextNoteType textType,
            Dictionary<(int Row, int Col), CellFormat> cellFormatLookup, TableRenderOptions options)
        {
            // Draw cell background if specified
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var cellFormat))
            {
                if (!string.IsNullOrEmpty(cellFormat.BackgroundColor))
                {
                    DrawCellBackground(doc, view, x, y, cellWidth, cellHeight, cellFormat.BackgroundColor);
                }
            }

            // Check if cell contains an image
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var formatForImage) && formatForImage.ImageData != null && formatForImage.ImageData.Length > 0)
            {
                // Render image instead of text
                _logger.LogInformation($"Cell at row {rowIndex}, col {colIndex} contains image data ({formatForImage.ImageData.Length} bytes), rendering image");
                RenderCellImage(doc, view, formatForImage, x, y, cellWidth, cellHeight);
                return; // Skip text rendering when image is present
            }

            // Determine text alignment
            TextAlignment cellAlignment = options.TextAlign;
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var formatForAlign) && !string.IsNullOrEmpty(formatForAlign.TextAlignment))
            {
                if (formatForAlign.TextAlignment == "Center") cellAlignment = TextAlignment.Center;
                else if (formatForAlign.TextAlignment == "Right") cellAlignment = TextAlignment.Right;
                else if (formatForAlign.TextAlignment == "Left") cellAlignment = TextAlignment.Left;
            }

            // Calculate text position based on alignment
            double textX = x;
            double padding = options.TextHeight * 0.2;

            switch (cellAlignment)
            {
                case TextAlignment.Center:
                    textX = x + cellWidth / 2;
                    break;
                case TextAlignment.Right:
                    textX = x + cellWidth - padding;
                    break;
                default: // Left
                    textX = x + padding;
                    break;
            }

            double textY = y - (cellHeight / 2) - (options.TextHeight / 4);
            textX += options.TextOffsetX;
            textY += options.TextOffsetY;

            // Get text formatting
            bool isBold = false, isItalic = false, isUnderline = false;
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var formatForText))
            {
                isBold = formatForText.IsBold;
                isItalic = formatForText.IsItalic;
                isUnderline = formatForText.IsUnderline;
            }

            // Create text note
            if (!string.IsNullOrEmpty(cellText))
            {
                CreateTextNote(doc, view, cellText, textX, textY, textType, cellAlignment, options.TextHeight, isBold, isItalic, isUnderline);
            }

            // Note: Grid lines are drawn at the row level to handle merged cells properly
        }

        private void RenderMergedCell(Document doc, Autodesk.Revit.DB.View view, string cellText, double startX, double startY,
            double rowHeight, int rowIndex, int colIndex, MergedCellRange merge, List<double> columnWidths,
            TextNoteType textType, Dictionary<(int Row, int Col), CellFormat> cellFormatLookup, TableRenderOptions options)
        {
            // Calculate X position for this merged cell
            double x = startX;
            for (int c = 0; c < colIndex && c < columnWidths.Count; c++)
            {
                x += columnWidths[c];
            }

            // Calculate merged cell width
            double cellWidth = 0;
            int endCol = Math.Min(merge.EndColumn, columnWidths.Count - 1);
            for (int c = merge.StartColumn; c <= endCol && c < columnWidths.Count; c++)
            {
                cellWidth += columnWidths[c];
            }

            // Calculate merged cell height
            double cellHeight = rowHeight * merge.RowSpan;

            _logger.LogInformation($"Rendering merged cell at row {rowIndex}, col {colIndex}: x={x:F3}, width={cellWidth:F3}, height={cellHeight:F3}");

            // Draw cell background if specified
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var cellFormat))
            {
                if (!string.IsNullOrEmpty(cellFormat.BackgroundColor))
                {
                    DrawCellBackground(doc, view, x, startY, cellWidth, cellHeight, cellFormat.BackgroundColor);
                }
            }

            // Check if cell contains an image
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var formatForImage) && formatForImage.ImageData != null && formatForImage.ImageData.Length > 0)
            {
                // Render image instead of text
                _logger.LogInformation($"Merged cell at row {rowIndex}, col {colIndex} contains image data ({formatForImage.ImageData.Length} bytes), rendering image");
                RenderCellImage(doc, view, formatForImage, x, startY, cellWidth, cellHeight);
                return; // Skip text rendering when image is present
            }

            // Determine text alignment
            TextAlignment cellAlignment = options.TextAlign;
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var formatForAlign) && !string.IsNullOrEmpty(formatForAlign.TextAlignment))
            {
                if (formatForAlign.TextAlignment == "Center") cellAlignment = TextAlignment.Center;
                else if (formatForAlign.TextAlignment == "Right") cellAlignment = TextAlignment.Right;
                else if (formatForAlign.TextAlignment == "Left") cellAlignment = TextAlignment.Left;
            }

            // Calculate text position
            double textX = x;
            double padding = options.TextHeight * 0.2;

            switch (cellAlignment)
            {
                case TextAlignment.Center:
                    textX = x + cellWidth / 2;
                    break;
                case TextAlignment.Right:
                    textX = x + cellWidth - padding;
                    break;
                default: // Left
                    textX = x + padding;
                    break;
            }

            double textY = startY - (cellHeight / 2) - (options.TextHeight / 4);
            textX += options.TextOffsetX;
            textY += options.TextOffsetY;

            // Get text formatting
            bool isBold = false, isItalic = false, isUnderline = false;
            if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var formatForText))
            {
                isBold = formatForText.IsBold;
                isItalic = formatForText.IsItalic;
                isUnderline = formatForText.IsUnderline;
            }

            // Create text note
            if (!string.IsNullOrEmpty(cellText))
            {
                CreateTextNote(doc, view, cellText, textX, textY, textType, cellAlignment, options.TextHeight, isBold, isItalic, isUnderline);
            }

            // Note: Grid lines are drawn at the row level to handle merged cells properly
        }

        private List<double> CalculateColumnWidths(ImportedTableData data, TableRenderOptions options)
        {
            var columnWidths = new List<double>();

            // If Excel column widths are available, use proportional scaling
            if (data.ColumnWidths.Count == data.ColumnCount)
            {
                // Calculate average Excel column width
                double avgExcelWidth = data.ColumnWidths.Average();

                // Scale each column proportionally based on options.ColumnWidth as the baseline
                for (int i = 0; i < data.ColumnCount; i++)
                {
                    double ratio = data.ColumnWidths[i] / avgExcelWidth;
                    double scaledWidth = options.ColumnWidth * ratio;
                    columnWidths.Add(scaledWidth);
                }

                _logger.LogInformation($"Using proportional column widths (avg Excel width: {avgExcelWidth:F2}, baseline: {options.ColumnWidth:F4} ft)");
            }
            else if (options.AutoSizeColumns)
            {
                // Calculate based on content (rough estimation)
                for (int i = 0; i < data.ColumnCount; i++)
                {
                    double maxWidth = data.Headers.Count > i ? data.Headers[i].Length * options.TextHeight * 0.6 : options.ColumnWidth;

                    foreach (var row in data.Rows)
                    {
                        if (row.Count > i)
                        {
                            double cellWidth = row[i].Length * options.TextHeight * 0.6;
                            if (cellWidth > maxWidth)
                                maxWidth = cellWidth;
                        }
                    }

                    columnWidths.Add(Math.Max(maxWidth, options.ColumnWidth));
                }
            }
            else
            {
                // Use uniform column width
                for (int i = 0; i < data.ColumnCount; i++)
                {
                    columnWidths.Add(options.ColumnWidth);
                }
            }

            return columnWidths;
        }

        private List<double> CalculateRowHeights(ImportedTableData data, TableRenderOptions options)
        {
            var rowHeights = new List<double>();

            // If Excel row heights are available, use proportional scaling
            if (data.RowHeights.Count > 0)
            {
                // Calculate average Excel row height
                double avgExcelHeight = data.RowHeights.Average();

                // Scale each row proportionally based on options.RowHeight as the baseline
                foreach (var excelHeight in data.RowHeights)
                {
                    double ratio = excelHeight / avgExcelHeight;
                    double scaledHeight = options.RowHeight * ratio;
                    rowHeights.Add(scaledHeight);
                }

                _logger.LogInformation($"Using proportional row heights (avg Excel height: {avgExcelHeight:F2}, baseline: {options.RowHeight:F4} ft, count: {rowHeights.Count})");
            }

            return rowHeights;
        }

        private void CreateTextNote(Document doc, Autodesk.Revit.DB.View view, string text, double x, double y, TextNoteType textType, TextAlignment align, double textHeight, bool isBold = false, bool isItalic = false, bool isUnderline = false)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "Document cannot be null");

            if (view == null)
                throw new ArgumentNullException(nameof(view), "View cannot be null");

            if (textType == null)
                throw new ArgumentNullException(nameof(textType), "TextNoteType cannot be null");

            // Ensure text is properly formatted for Revit
            // Replace common superscript characters with normal equivalents if they don't render
            string processedText = text;

            // Log if text contains special characters
            if (text.Contains("³") || text.Contains("²") || text.Contains("¹"))
            {
                _logger.LogDebug($"Text contains superscript characters: '{text}'");

                // Revit may not display Unicode superscripts correctly, so we can convert them
                // to regular text with ^ notation as an alternative
                processedText = processedText
                    .Replace("m³", "m^3")
                    .Replace("m²", "m^2")
                    .Replace("yd³", "yd^3")
                    .Replace("yd²", "yd^2")
                    .Replace("ft³", "ft^3")
                    .Replace("ft²", "ft^2");

                _logger.LogDebug($"Converted superscript text to: '{processedText}'");
            }

            // Get or create a text type with the specified formatting
            var formattedTextType = GetOrCreateFormattedTextNoteType(doc, textType, textHeight, isBold, isItalic, isUnderline);

            var point = new XYZ(x, y, 0);

            var textNoteOptions = new TextNoteOptions
            {
                TypeId = formattedTextType.Id,
                HorizontalAlignment = align == TextAlignment.Center ? Autodesk.Revit.DB.HorizontalTextAlignment.Center :
                                     align == TextAlignment.Right ? Autodesk.Revit.DB.HorizontalTextAlignment.Right :
                                     Autodesk.Revit.DB.HorizontalTextAlignment.Left
            };

            try
            {
                TextNote.Create(doc, view.Id, point, processedText, textNoteOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to create text note at ({x:F3}, {y:F3}) with text '{processedText}': {ex.Message}");
            }

            // Note: TextNote size is controlled by the TextNoteType, not by individual parameters
            // The textHeight parameter is used when creating/finding the appropriate TextNoteType
        }

        private void DrawDetailLine(Document doc, Autodesk.Revit.DB.View view, double x1, double y1, double x2, double y2, string lineStyleName)
        {
            var start = new XYZ(x1, y1, 0);
            var end = new XYZ(x2, y2, 0);
            var line = Line.CreateBound(start, end);

            var detailLine = doc.Create.NewDetailCurve(view, line);

            // Set line style if specified
            if (!string.IsNullOrEmpty(lineStyleName))
            {
                var lineStyle = GetLineStyle(doc, lineStyleName);
                if (lineStyle != null)
                {
                    detailLine.LineStyle = lineStyle;
                }
            }
        }

        private void DrawFilledRegion(Document doc, Autodesk.Revit.DB.View view, double x, double y, double width, double height, string fillTypeName)
        {
            try
            {
                // Create boundary curves (clockwise, accounting for Y decreasing downward)
                // Top-left to top-right
                // Top-right to bottom-right (y - height since Y decreases downward)
                // Bottom-right to bottom-left
                // Bottom-left to top-left
                var curves = new List<Curve>
                {
                    Line.CreateBound(new XYZ(x, y, 0), new XYZ(x + width, y, 0)),
                    Line.CreateBound(new XYZ(x + width, y, 0), new XYZ(x + width, y - height, 0)),
                    Line.CreateBound(new XYZ(x + width, y - height, 0), new XYZ(x, y - height, 0)),
                    Line.CreateBound(new XYZ(x, y - height, 0), new XYZ(x, y, 0))
                };

                var curveLoop = CurveLoop.Create(curves);

                // Get filled region type
                var filledRegionType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault(frt => !string.IsNullOrEmpty(fillTypeName) && frt.Name.Contains(fillTypeName));

                if (filledRegionType == null)
                {
                    // Use solid fill as default
                    filledRegionType = new FilteredElementCollector(doc)
                        .OfClass(typeof(FilledRegionType))
                        .Cast<FilledRegionType>()
                        .FirstOrDefault(frt => frt.Name.Contains("Solid"));
                }

                if (filledRegionType == null)
                {
                    filledRegionType = new FilteredElementCollector(doc)
                        .OfClass(typeof(FilledRegionType))
                        .Cast<FilledRegionType>()
                        .FirstOrDefault();
                }

                if (filledRegionType != null)
                {
                    FilledRegion.Create(doc, filledRegionType.Id, view.Id, new List<CurveLoop> { curveLoop });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create filled region: {ex.Message}");
            }
        }

        private void DrawCellBackground(Document doc, Autodesk.Revit.DB.View view, double x, double y, double width, double height, string hexColor)
        {
            try
            {
                // Create boundary curves for the cell
                var curves = new List<Curve>
                {
                    Line.CreateBound(new XYZ(x, y, 0), new XYZ(x + width, y, 0)),
                    Line.CreateBound(new XYZ(x + width, y, 0), new XYZ(x + width, y - height, 0)),
                    Line.CreateBound(new XYZ(x + width, y - height, 0), new XYZ(x, y - height, 0)),
                    Line.CreateBound(new XYZ(x, y - height, 0), new XYZ(x, y, 0))
                };

                var curveLoop = CurveLoop.Create(curves);

                // Get or create a FilledRegionType with the specified color
                var filledRegionType = GetOrCreateFilledRegionTypeWithColor(doc, hexColor);

                if (filledRegionType == null)
                {
                    _logger.LogWarning($"Could not get or create filled region type for color {hexColor}");
                    return;
                }

                // Create the filled region
                var filledRegion = FilledRegion.Create(doc, filledRegionType.Id, view.Id, new List<CurveLoop> { curveLoop });

                _logger.LogInformation($"Created cell background at ({x:F2}, {y:F2}) with size {width:F2}x{height:F2}, color: {hexColor}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create cell background: {ex.Message}");
            }
        }

        private FilledRegionType GetOrCreateFilledRegionTypeWithColor(Document doc, string hexColor)
        {
            try
            {
                // Parse hex color (format: AARRGGBB or RRGGBB)
                byte r, g, b;
                if (hexColor.Length == 8) // AARRGGBB
                {
                    r = Convert.ToByte(hexColor.Substring(2, 2), 16);
                    g = Convert.ToByte(hexColor.Substring(4, 2), 16);
                    b = Convert.ToByte(hexColor.Substring(6, 2), 16);
                }
                else if (hexColor.Length == 6) // RRGGBB
                {
                    r = Convert.ToByte(hexColor.Substring(0, 2), 16);
                    g = Convert.ToByte(hexColor.Substring(2, 2), 16);
                    b = Convert.ToByte(hexColor.Substring(4, 2), 16);
                }
                else
                {
                    _logger.LogWarning($"Invalid hex color format: {hexColor}");
                    return null;
                }

                var color = new Autodesk.Revit.DB.Color(r, g, b);
                string typeName = $"TableCell_{hexColor}";

                // Check if type already exists
                var existingType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault(frt => frt.Name == typeName);

                if (existingType != null)
                {
                    return existingType;
                }

                // Get a solid fill pattern
                var solidPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

                if (solidPattern == null)
                {
                    _logger.LogWarning("No solid fill pattern found");
                    return null;
                }

                // Get an existing FilledRegionType to duplicate
                var baseType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault();

                if (baseType == null)
                {
                    _logger.LogWarning("No base FilledRegionType found to duplicate");
                    return null;
                }

                // Duplicate the base type
                var newType = baseType.Duplicate(typeName) as FilledRegionType;

                if (newType != null)
                {
                    // Set the foreground pattern and color
                    newType.ForegroundPatternId = solidPattern.Id;
                    newType.ForegroundPatternColor = color;

                    _logger.LogInformation($"Created FilledRegionType '{typeName}' with color RGB({r},{g},{b})");
                }

                return newType;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to create FilledRegionType for color {hexColor}: {ex.Message}");
                return null;
            }
        }

        private TextNoteType GetOrCreateTextNoteType(Document doc, string styleName, double textHeight)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc), "Document cannot be null");
            }

            // Try to find existing text note type
            TextNoteType textNoteType = null;

            if (!string.IsNullOrEmpty(styleName))
            {
                textNoteType = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault(tnt => tnt.Name != null && tnt.Name.Contains(styleName));
            }

            if (textNoteType == null)
            {
                // Get any text note type as fallback
                textNoteType = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();
            }

            if (textNoteType == null)
            {
                throw new InvalidOperationException("No text note type found in the document. Please ensure the document contains at least one text note type.");
            }

            // First check if a text type with this exact name already exists
            string desiredTypeName = $"Import Text {textHeight:F6}ft";
            var existingCustomType = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(tnt => tnt.Name == desiredTypeName);

            if (existingCustomType != null)
            {
                _logger.LogInformation($"Found existing text type '{existingCustomType.Name}'");
                return existingCustomType;
            }

            // Try to create a new text type with the exact size we need
            try
            {
                // Duplicate the base text type
                TextNoteType newType = textNoteType.Duplicate(desiredTypeName) as TextNoteType;

                if (newType != null)
                {
                    // Set the text size
                    var sizeParam = newType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                    if (sizeParam != null && !sizeParam.IsReadOnly)
                    {
                        sizeParam.Set(textHeight);
                    }

                    // Set text background to transparent
                    var backgroundParam = newType.get_Parameter(BuiltInParameter.TEXT_BACKGROUND);
                    if (backgroundParam != null && !backgroundParam.IsReadOnly)
                    {
                        backgroundParam.Set(1); // 1 = Transparent
                        _logger.LogInformation($"Set text background to transparent for type '{newType.Name}'");
                    }
                    else
                    {
                        _logger.LogWarning($"TEXT_BACKGROUND parameter not available or read-only");
                    }

                    _logger.LogInformation($"Created new text type '{newType.Name}' with size {textHeight:F6} ft");
                    return newType;
                }
            }
            catch (Exception ex)
            {
                // Only log warning if it's not a duplicate name issue (which is expected)
                if (!ex.Message.Contains("name is already in use"))
                {
                    _logger.LogWarning($"Could not create custom text type: {ex.Message}");
                }
                else
                {
                    _logger.LogDebug($"Text type '{desiredTypeName}' already exists, using default");
                }
            }

            // If creation failed, use the default and log a message
            var defaultSize = textNoteType.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble() ?? 0;
            _logger.LogInformation($"Using default text type '{textNoteType.Name}' with size {defaultSize:F6} ft (requested {textHeight:F6} ft)");

            return textNoteType;
        }

        private TextNoteType GetOrCreateFormattedTextNoteType(Document doc, TextNoteType baseType, double textHeight, bool isBold, bool isItalic, bool isUnderline)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "Document cannot be null");

            if (baseType == null)
                throw new ArgumentNullException(nameof(baseType), "Base TextNoteType cannot be null");

            // Build a suffix for the type name based on formatting
            string formatSuffix = "";
            if (isBold) formatSuffix += "B";
            if (isItalic) formatSuffix += "I";
            if (isUnderline) formatSuffix += "U";

            // If no special formatting, use the regular method
            if (string.IsNullOrEmpty(formatSuffix))
            {
                return GetOrCreateTextNoteType(doc, baseType.Name, textHeight);
            }

            // Try to find existing formatted type
            string typeName = $"Import Text {textHeight:F6}ft {formatSuffix}";
            var existingType = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(tnt => tnt.Name == typeName);

            if (existingType != null)
            {
                return existingType;
            }

            // Create new formatted type
            try
            {
                TextNoteType newType = baseType.Duplicate(typeName) as TextNoteType;
                if (newType != null)
                {
                    // Set text size
                    var sizeParam = newType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                    if (sizeParam != null && !sizeParam.IsReadOnly)
                    {
                        sizeParam.Set(textHeight);
                    }

                    // Set text background to transparent
                    var backgroundParam = newType.get_Parameter(BuiltInParameter.TEXT_BACKGROUND);
                    if (backgroundParam != null && !backgroundParam.IsReadOnly)
                    {
                        backgroundParam.Set(0); // 0 = Transparent
                    }

                    // Set bold
                    if (isBold)
                    {
                        var boldParam = newType.get_Parameter(BuiltInParameter.TEXT_FONT);
                        if (boldParam != null && !boldParam.IsReadOnly)
                        {
                            string currentFont = boldParam.AsString() ?? "Arial";
                            // Try to append "Bold" to font name if not already there
                            if (!currentFont.Contains("Bold"))
                            {
                                string boldFont = currentFont + " Bold";
                                try
                                {
                                    boldParam.Set(boldFont);
                                }
                                catch
                                {
                                    _logger.LogWarning($"Could not set bold font '{boldFont}', using TEXT_STYLE_BOLD parameter instead");
                                }
                            }
                        }

                        // Also try setting TEXT_STYLE_BOLD parameter if it exists
                        var boldStyleParam = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD);
                        if (boldStyleParam != null && !boldStyleParam.IsReadOnly)
                        {
                            boldStyleParam.Set(1); // 1 = Bold
                        }
                    }

                    // Set italic
                    if (isItalic)
                    {
                        var italicParam = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC);
                        if (italicParam != null && !italicParam.IsReadOnly)
                        {
                            italicParam.Set(1); // 1 = Italic
                        }
                    }

                    // Set underline
                    if (isUnderline)
                    {
                        var underlineParam = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_UNDERLINE);
                        if (underlineParam != null && !underlineParam.IsReadOnly)
                        {
                            underlineParam.Set(1); // 1 = Underline
                        }
                    }

                    _logger.LogInformation($"Created formatted text type '{newType.Name}' with bold={isBold}, italic={isItalic}, underline={isUnderline}");
                    return newType;
                }
            }
            catch (Exception ex)
            {
                // Only log warning if it's not a duplicate name issue (which is expected)
                if (!ex.Message.Contains("name is already in use"))
                {
                    _logger.LogWarning($"Could not create formatted text type: {ex.Message}");
                }
                else
                {
                    _logger.LogDebug($"Text type '{typeName}' already exists, using base type");
                }
            }

            // Fallback to base type
            return baseType;
        }

        private GraphicsStyle GetLineStyle(Document doc, string lineStyleName)
        {
            try
            {
                var category = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                var subCategories = category.SubCategories;

                foreach (Category subCat in subCategories)
                {
                    if (subCat.Name.Contains(lineStyleName))
                    {
                        return subCat.GetGraphicsStyle(GraphicsStyleType.Projection);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void RenderCellImage(Document doc, Autodesk.Revit.DB.View view, CellFormat cellFormat, double x, double y, double cellWidth, double cellHeight)
        {
            try
            {
                if (cellFormat.ImageData == null || cellFormat.ImageData.Length == 0)
                {
                    _logger.LogWarning("No image data available to render");
                    return;
                }

                _logger.LogInformation($"Starting RenderCellImage: image size {cellFormat.ImageData.Length} bytes, dimensions {cellFormat.ImageWidth}x{cellFormat.ImageHeight}px");

                // Create a temporary file to save the image
                string tempImagePath = Path.Combine(Path.GetTempPath(), $"RevitTableImage_{Guid.NewGuid()}.png");
                _logger.LogInformation($"Temporary image path: {tempImagePath}");

                try
                {
                    // Write image data to temporary file
                    File.WriteAllBytes(tempImagePath, cellFormat.ImageData);
                    _logger.LogInformation($"Wrote {cellFormat.ImageData.Length} bytes to temporary file");

                    // Load the image into Revit
                    // Note: Revit API for creating images varies by version. We'll use reflection to handle it.
                    ImageType? imageType = null;

                    try
                    {
                        // List all available Create methods for debugging
                        var allCreateMethods = typeof(ImageType).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                            .Where(m => m.Name == "Create").ToList();

                        _logger.LogInformation($"Found {allCreateMethods.Count} Create methods on ImageType");
                        foreach (var method in allCreateMethods)
                        {
                            var parameters = method.GetParameters();
                            _logger.LogInformation($"  Create({string.Join(", ", parameters.Select(p => p.ParameterType.Name))})");
                        }

                        // Try to use ImageType.Create with string path directly
                        var createMethods = allCreateMethods.Where(m => m.GetParameters().Length == 2).ToList();

                        foreach (var method in createMethods)
                        {
                            var parameters = method.GetParameters();
                            if (parameters[0].ParameterType == typeof(Document) && parameters[1].ParameterType == typeof(string))
                            {
                                _logger.LogInformation($"Attempting to call ImageType.Create(Document, string)");
                                imageType = method.Invoke(null, new object[] { doc, tempImagePath }) as ImageType;
                                _logger.LogInformation($"ImageType created successfully: {imageType != null}");
                                break;
                            }
                        }

                        if (imageType == null)
                        {
                            _logger.LogWarning("Could not find or invoke suitable ImageType.Create method signature");
                        }
                    }
                    catch (Exception createEx)
                    {
                        _logger.LogError($"Failed to create ImageType: {createEx.Message}\n{createEx.StackTrace}");
                        if (createEx.InnerException != null)
                        {
                            _logger.LogError($"Inner exception: {createEx.InnerException.Message}\n{createEx.InnerException.StackTrace}");
                        }
                    }

                    if (imageType == null)
                    {
                        _logger.LogWarning("Failed to create ImageType from file");
                        return;
                    }

                    // Calculate scale to fit image within cell while maintaining aspect ratio
                    // Convert pixels to feet (assuming 96 DPI: 1 inch = 96 pixels, 1 foot = 12 inches = 1152 pixels)
                    const double pixelsPerFoot = 1152.0; // 96 DPI * 12 inches/foot

                    double imageWidthFeet = cellFormat.ImageWidth / pixelsPerFoot;
                    double imageHeightFeet = cellFormat.ImageHeight / pixelsPerFoot;

                    // Calculate scaling to fit within cell
                    double scaleX = cellWidth / imageWidthFeet;
                    double scaleY = cellHeight / imageHeightFeet;
                    double scale = Math.Min(scaleX, scaleY);

                    // Calculate final dimensions
                    double scaledWidth = imageWidthFeet * scale;
                    double scaledHeight = imageHeightFeet * scale;

                    // Center the image in the cell
                    double imageX = x + (cellWidth - scaledWidth) / 2;
                    double imageY = y - (cellHeight - scaledHeight) / 2; // Top of image

                    // Create image instance at the calculated position
                    XYZ imageLocation = new XYZ(imageX, imageY, 0);
                    ImagePlacementOptions placementOptions = new ImagePlacementOptions(imageLocation, BoxPlacement.TopLeft);
                    ImageInstance imageInstance = ImageInstance.Create(doc, view, imageType.Id, placementOptions);

                    if (imageInstance != null)
                    {
                        // Set the image width (height scales proportionally)
                        var widthParam = imageInstance.get_Parameter(BuiltInParameter.RASTER_SHEETWIDTH);
                        if (widthParam != null && !widthParam.IsReadOnly)
                        {
                            widthParam.Set(scaledWidth);
                        }

                        _logger.LogInformation($"Created image instance at ({imageX:F3}, {imageY:F3}) with size {scaledWidth:F3}x{scaledHeight:F3} ft");
                    }
                }
                finally
                {
                    // Clean up temporary file
                    try
                    {
                        if (File.Exists(tempImagePath))
                        {
                            File.Delete(tempImagePath);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning($"Could not delete temporary image file: {cleanupEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error rendering cell image: {ex.Message}", ex);
            }
        }
    }
}
