using System.Collections.Generic;

namespace BoltFramePlugin.Models.DataImport
{
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
        /// Column width in feet
        /// </summary>
        public double ColumnWidth { get; set; } = 1.0;

        /// <summary>
        /// Row height in feet
        /// </summary>
        public double RowHeight { get; set; } = 0.25;

        /// <summary>
        /// Text height in feet
        /// </summary>
        public double TextHeight { get; set; } = 0.1;

        /// <summary>
        /// Whether to draw grid lines
        /// </summary>
        public bool DrawGridLines { get; set; } = true;

        /// <summary>
        /// Whether to fill header with background
        /// </summary>
        public bool FillHeaderBackground { get; set; } = true;

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
