using System.Collections.Generic;

namespace BoltFramePlugin.Models.DataImport
{
    /// <summary>
    /// Represents a merged cell range in the table
    /// </summary>
    public class MergedCellRange
    {
        /// <summary>
        /// Starting row index (0-based, including header row)
        /// </summary>
        public int StartRow { get; set; }

        /// <summary>
        /// Starting column index (0-based)
        /// </summary>
        public int StartColumn { get; set; }

        /// <summary>
        /// Ending row index (0-based, inclusive)
        /// </summary>
        public int EndRow { get; set; }

        /// <summary>
        /// Ending column index (0-based, inclusive)
        /// </summary>
        public int EndColumn { get; set; }

        /// <summary>
        /// Number of rows spanned
        /// </summary>
        public int RowSpan => EndRow - StartRow + 1;

        /// <summary>
        /// Number of columns spanned
        /// </summary>
        public int ColumnSpan => EndColumn - StartColumn + 1;
    }

    /// <summary>
    /// Represents cell formatting information
    /// </summary>
    public class CellFormat
    {
        /// <summary>
        /// Row index (0-based, including header row)
        /// </summary>
        public int Row { get; set; }

        /// <summary>
        /// Column index (0-based)
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Background color in hex format (e.g., "FFFF0000" for red)
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// Text color in hex format
        /// </summary>
        public string? TextColor { get; set; }
    }

    /// <summary>
    /// Represents imported tabular data from CSV or Excel
    /// </summary>
    public class ImportedTableData
    {
        /// <summary>
        /// Column headers
        /// </summary>
        public List<string> Headers { get; set; } = new List<string>();

        /// <summary>
        /// Data rows (each row is a list of cell values)
        /// </summary>
        public List<List<string>> Rows { get; set; } = new List<List<string>>();

        /// <summary>
        /// Merged cell ranges
        /// </summary>
        public List<MergedCellRange> MergedCells { get; set; } = new List<MergedCellRange>();

        /// <summary>
        /// Cell formatting information
        /// </summary>
        public List<CellFormat> CellFormats { get; set; } = new List<CellFormat>();

        /// <summary>
        /// Source file path
        /// </summary>
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Import timestamp
        /// </summary>
        public System.DateTime ImportedAt { get; set; } = System.DateTime.Now;

        /// <summary>
        /// Number of columns
        /// </summary>
        public int ColumnCount => Headers.Count;

        /// <summary>
        /// Number of rows (excluding header)
        /// </summary>
        public int RowCount => Rows.Count;
    }

    /// <summary>
    /// Rendering options for drafting view
    /// </summary>
    public class TableRenderOptions
    {
        /// <summary>
        /// Starting X position in feet
        /// </summary>
        public double StartX { get; set; } = 0;

        /// <summary>
        /// Starting Y position in feet
        /// </summary>
        public double StartY { get; set; } = 0;

        /// <summary>
        /// Column width in feet (model space)
        /// Default: 1.5 ft (appears as 4.5" on paper at scale 4)
        /// </summary>
        public double ColumnWidth { get; set; } = 1.5;

        /// <summary>
        /// Row height in feet (model space)
        /// Default: 0.167 ft (appears as 0.5" on paper at scale 4)
        /// </summary>
        public double RowHeight { get; set; } = 0.167;

        /// <summary>
        /// Text height in feet (paper size)
        /// Calculated from PaperTextHeight / 12
        /// Default: 0.0208 ft = 1/4"
        /// </summary>
        public double TextHeight { get; set; } = 0.0208;

        /// <summary>
        /// View scale (default: 4 for 3" = 1'-0")
        /// Common values: 4 (3"=1'-0"), 12 (1"=1'-0"), 24 (1/2"=1'-0"), 48 (1/4"=1'-0"), 96 (1/8"=1'-0")
        /// </summary>
        public double ViewScale { get; set; } = 4;

        /// <summary>
        /// Paper text height in inches (default: 1/4" Arial)
        /// </summary>
        public double PaperTextHeight { get; set; } = 0.25;

        /// <summary>
        /// Border offset for leader/border in feet (model space)
        /// Default: 0.0208 ft (appears as 1/16" on paper at scale 4)
        /// </summary>
        public double BorderOffset { get; set; } = 0.0208;

        /// <summary>
        /// Text horizontal offset in feet (model space)
        /// Adjusts text position to the right
        /// </summary>
        public double TextOffsetX { get; set; } = 0;

        /// <summary>
        /// Text vertical offset in feet (model space)
        /// Adjusts text position upward
        /// </summary>
        public double TextOffsetY { get; set; } = 0;

        /// <summary>
        /// Whether to draw grid lines
        /// </summary>
        public bool DrawGridLines { get; set; } = true;

        /// <summary>
        /// Whether to fill header with background (default: false for transparent)
        /// </summary>
        public bool FillHeaderBackground { get; set; } = false;

        /// <summary>
        /// Text style name (must exist in Revit)
        /// </summary>
        public string TextStyleName { get; set; } = "Standard";

        /// <summary>
        /// Line style name for grid (must exist in Revit)
        /// </summary>
        public string LineStyleName { get; set; } = "Thin Lines";

        /// <summary>
        /// Filled region type name for header background (must exist in Revit)
        /// </summary>
        public string HeaderFillTypeName { get; set; } = "Solid fill";

        /// <summary>
        /// Auto-size columns based on content
        /// </summary>
        public bool AutoSizeColumns { get; set; } = false;

        /// <summary>
        /// Alignment: Left, Center, Right
        /// </summary>
        public TextAlignment TextAlign { get; set; } = TextAlignment.Left;
    }

    /// <summary>
    /// Text alignment options
    /// </summary>
    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Import configuration
    /// </summary>
    public class ImportConfiguration
    {
        /// <summary>
        /// Whether first row contains headers
        /// </summary>
        public bool HasHeaders { get; set; } = true;

        /// <summary>
        /// CSV delimiter character
        /// </summary>
        public char CsvDelimiter { get; set; } = ',';

        /// <summary>
        /// Excel sheet name to import (null = first sheet)
        /// </summary>
        public string? ExcelSheetName { get; set; } = null;

        /// <summary>
        /// Excel sheet index (0-based, used if SheetName is null)
        /// </summary>
        public int ExcelSheetIndex { get; set; } = 0;

        /// <summary>
        /// Skip empty rows
        /// </summary>
        public bool SkipEmptyRows { get; set; } = true;

        /// <summary>
        /// Trim whitespace from cells
        /// </summary>
        public bool TrimWhitespace { get; set; } = true;
    }
}
