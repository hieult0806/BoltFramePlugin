using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoltFramePlugin.Models.DataImport;

namespace BoltFramePlugin.Models
{
    public class ProjectConfigurationModel
    {
        public Guid ProjectId { get; set; } // Unique identifier for the Revit project
        public bool EnableAdvancedFeatures { get; set; }
        public int DefaultFrameSize { get; set; }

        // Store tracked files for data import syncing
        public List<TrackedFileConfig> TrackedFiles { get; set; } = new List<TrackedFileConfig>();
    }

    /// <summary>
    /// Configuration for a tracked file that can be persisted
    /// </summary>
    public class TrackedFileConfig
    {
        public string FilePath { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }

        // Import configuration
        public bool HasHeaders { get; set; }
        public char CsvDelimiter { get; set; } = ',';
        public bool SkipEmptyRows { get; set; }
        public bool TrimWhitespace { get; set; }

        // Render options
        public double ColumnWidth { get; set; }
        public double RowHeight { get; set; }
        public double TextHeight { get; set; }
        public int ViewScale { get; set; }
        public double PaperTextHeight { get; set; }
        public double BorderOffset { get; set; }
        public double TextOffsetX { get; set; }
        public double TextOffsetY { get; set; }
        public bool DrawGridLines { get; set; }
        public bool FillHeaderBackground { get; set; }
        public bool AutoSizeColumns { get; set; }
        public int TextAlign { get; set; } // Stored as int for serialization
    }
}
