using Autodesk.Revit.DB;
using BoltFramePlugin.Models.DataImport;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BoltFramePlugin.Services.DataImport
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

                // Build set of rows that should be skipped (they're part of merged cells from previous rows)
                var rowsToSkip = new HashSet<int>();
                foreach (var merge in data.MergedCells)
                {
                    // Skip all rows except the first row of the merge
                    for (int r = merge.StartRow + 1; r <= merge.EndRow; r++)
                    {
                        rowsToSkip.Add(r);
                    }
                }

                double currentY = options.StartY;

                // Render header
                if (data.Headers.Count > 0)
                {
                    // Header is not part of the data rows, so use a dummy index (-1 or similar)
                    // Merged cells are indexed relative to data rows only, not including header
                    currentY = RenderRow(doc, view, data.Headers, options.StartX, currentY, columnWidths, options, textType,
                        -1, mergedCellLookup, cellFormatLookup, isHeader: true);
                }

                // Render data rows
                int rowIndex = 0;
                foreach (var row in data.Rows)
                {
                    // Skip rows that are part of merged cells from previous rows
                    if (rowsToSkip.Contains(rowIndex))
                    {
                        _logger.LogInformation($"Skipping data row {rowIndex} (part of merged cell from previous row)");
                        rowIndex++;
                        continue;
                    }

                    _logger.LogInformation($"Rendering data row {rowIndex}: [{string.Join(", ", row.Select(c => $"\"{c}\""))}]");
                    currentY = RenderRow(doc, view, row, options.StartX, currentY, columnWidths, options, textType,
                        rowIndex, mergedCellLookup, cellFormatLookup, isHeader: false);
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
            Dictionary<(int Row, int Col), CellFormat> cellFormatLookup, bool isHeader)
        {
            double currentX = startX;
            double rowHeight = options.RowHeight;

            // Track the maximum row span in this row (for calculating next Y position)
            int maxRowSpan = 1;

            // Draw filled region for header background
            if (isHeader && options.FillHeaderBackground)
            {
                DrawFilledRegion(doc, view, startX, startY, columnWidths.Sum(), rowHeight, options.HeaderFillTypeName);
            }

            // Track which cells have been processed (for merged cells)
            var processedCells = new HashSet<int>();

            // Draw cells
            for (int colIndex = 0; colIndex < cells.Count && colIndex < columnWidths.Count; colIndex++)
            {
                if (processedCells.Contains(colIndex))
                {
                    // Skip this cell - its width was already included in the merged cell
                    continue;
                }

                var cellText = cells[colIndex];
                var cellWidth = columnWidths[colIndex];
                double cellHeight = rowHeight;

                // Check if this cell is part of a merged range
                MergedCellRange? mergedRange = null;
                if (mergedCellLookup.TryGetValue((rowIndex, colIndex), out var merge))
                {
                    mergedRange = merge;

                    // Only render if this is the top-left cell of the merge
                    bool isTopLeft = (merge.StartRow == rowIndex && merge.StartColumn == colIndex);

                    if (!isTopLeft)
                    {
                        // Skip rendering for non-top-left cells in merged range
                        currentX += cellWidth;
                        continue;
                    }

                    // Calculate merged cell dimensions (clamp to available columns)
                    cellWidth = 0;
                    int endCol = Math.Min(merge.EndColumn, columnWidths.Count - 1);

                    _logger.LogInformation($"Rendering merged cell at row {rowIndex}, col {colIndex}: StartCol={merge.StartColumn}, EndCol={merge.EndColumn}, ClampedEndCol={endCol}, ColumnWidths.Count={columnWidths.Count}");

                    for (int c = merge.StartColumn; c <= endCol; c++)
                    {
                        if (c < columnWidths.Count)
                        {
                            cellWidth += columnWidths[c];
                            _logger.LogInformation($"  Adding column {c} width: {columnWidths[c]:F3} ft, cumulative width: {cellWidth:F3} ft");
                            if (c > colIndex)
                            {
                                processedCells.Add(c);
                            }
                        }
                    }
                    cellHeight = rowHeight * merge.RowSpan;

                    _logger.LogInformation($"Final merged cell width: {cellWidth:F3} ft, height: {cellHeight:F3} ft");

                    // Track the maximum row span in this row
                    if (merge.RowSpan > maxRowSpan)
                    {
                        maxRowSpan = merge.RowSpan;
                    }
                }

                // Draw cell background if specified
                if (cellFormatLookup.TryGetValue((rowIndex, colIndex), out var cellFormat))
                {
                    if (!string.IsNullOrEmpty(cellFormat.BackgroundColor))
                    {
                        // Draw filled region for cell background
                        DrawCellBackground(doc, view, currentX, startY, cellWidth, cellHeight, cellFormat.BackgroundColor);
                    }
                }

                // Calculate text position based on alignment
                double textX = currentX;
                double padding = options.TextHeight * 0.2; // Small padding

                switch (options.TextAlign)
                {
                    case TextAlignment.Center:
                        textX = currentX + cellWidth / 2;
                        break;
                    case TextAlignment.Right:
                        textX = currentX + cellWidth - padding;
                        break;
                    default: // Left
                        textX = currentX + padding;
                        break;
                }

                // Position text vertically centered in the cell (accounting for text baseline)
                double textY = startY - (cellHeight / 2) - (options.TextHeight / 4);

                // Apply text offsets
                textX += options.TextOffsetX;
                textY += options.TextOffsetY;

                // Create text note with specified height
                CreateTextNote(doc, view, cellText, textX, textY, textType, options.TextAlign, options.TextHeight);

                // Draw grid lines
                if (options.DrawGridLines)
                {
                    // Left vertical line
                    DrawDetailLine(doc, view, currentX, startY, currentX, startY - cellHeight, options.LineStyleName);

                    // Right vertical line (for last column or merged cell)
                    if (mergedRange != null || colIndex == cells.Count - 1)
                    {
                        DrawDetailLine(doc, view, currentX + cellWidth, startY, currentX + cellWidth, startY - cellHeight, options.LineStyleName);
                    }
                }

                currentX += cellWidth;
            }

            // Draw horizontal grid lines
            if (options.DrawGridLines)
            {
                DrawDetailLine(doc, view, startX, startY, currentX, startY, options.LineStyleName);
                DrawDetailLine(doc, view, startX, startY - (rowHeight * maxRowSpan), currentX, startY - (rowHeight * maxRowSpan), options.LineStyleName);
            }

            return startY - (rowHeight * maxRowSpan); // Subtract to move down (Y decreases downward in Revit)
        }

        private List<double> CalculateColumnWidths(ImportedTableData data, TableRenderOptions options)
        {
            var columnWidths = new List<double>();

            if (options.AutoSizeColumns)
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

        private void CreateTextNote(Document doc, Autodesk.Revit.DB.View view, string text, double x, double y, TextNoteType textType, TextAlignment align, double textHeight)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "Document cannot be null");

            if (view == null)
                throw new ArgumentNullException(nameof(view), "View cannot be null");

            if (textType == null)
                throw new ArgumentNullException(nameof(textType), "TextNoteType cannot be null");

            var point = new XYZ(x, y, 0);

            var textNoteOptions = new TextNoteOptions
            {
                TypeId = textType.Id,
                HorizontalAlignment = align == TextAlignment.Center ? Autodesk.Revit.DB.HorizontalTextAlignment.Center :
                                     align == TextAlignment.Right ? Autodesk.Revit.DB.HorizontalTextAlignment.Right :
                                     Autodesk.Revit.DB.HorizontalTextAlignment.Left
            };

            TextNote.Create(doc, view.Id, point, text, textNoteOptions);

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

                // Get or create a solid fill type
                var filledRegionType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault(frt => frt.Name.Contains("Solid"));

                if (filledRegionType == null)
                {
                    _logger.LogWarning("No solid fill type found for cell background");
                    return;
                }

                // Create the filled region
                var filledRegion = FilledRegion.Create(doc, filledRegionType.Id, view.Id, new List<CurveLoop> { curveLoop });

                // Note: Revit doesn't support setting fill colors directly in the API
                // The fill pattern color is controlled by the FilledRegionType
                // For now, we'll just create the filled region with the default solid fill
                // To support colors, you would need to create different FilledRegionTypes with different fill patterns/colors

                _logger.LogInformation($"Created cell background at ({x:F2}, {y:F2}) with size {width:F2}x{height:F2}, color: {hexColor}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create cell background: {ex.Message}");
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

            // Try to create a new text type with the exact size we need
            try
            {
                // Duplicate the base text type
                TextNoteType newType = textNoteType.Duplicate($"Import Text {textHeight:F6}ft") as TextNoteType;

                if (newType != null)
                {
                    // Set the text size
                    var sizeParam = newType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                    if (sizeParam != null && !sizeParam.IsReadOnly)
                    {
                        sizeParam.Set(textHeight);
                        _logger.LogInformation($"Created new text type '{newType.Name}' with size {textHeight:F6} ft");
                        return newType;
                    }
                    else
                    {
                        _logger.LogWarning($"TEXT_SIZE parameter is read-only on duplicated type, using original");
                        return textNoteType;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create custom text type: {ex.Message}");
            }

            // If creation failed, use the default and log a message
            var defaultSize = textNoteType.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble() ?? 0;
            _logger.LogInformation($"Using default text type '{textNoteType.Name}' with size {defaultSize:F6} ft (requested {textHeight:F6} ft)");

            return textNoteType;
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
    }
}
