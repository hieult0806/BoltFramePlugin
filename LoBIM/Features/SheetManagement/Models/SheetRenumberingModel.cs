using System.Collections.Generic;

namespace LoBIM.Features.SheetManagement.Models
{
    /// <summary>
    /// Model containing information for sheet renumbering operations
    /// </summary>
    public class SheetRenumberingModel
    {
        /// <summary>
        /// Prefix for the new sheet numbers (e.g., "A0.")
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// Starting number for renumbering (e.g., 1 for "A0.01")
        /// </summary>
        public int StartNumber { get; set; }

        /// <summary>
        /// Increment between sheet numbers (typically 1)
        /// </summary>
        public int Increment { get; set; }

        /// <summary>
        /// Number of digits for padding (e.g., 2 for "01", "02")
        /// </summary>
        public int PaddingDigits { get; set; }

        /// <summary>
        /// List of sheets in their desired order with their element IDs
        /// </summary>
        public List<SheetRenumberingItem> Sheets { get; set; } = new List<SheetRenumberingItem>();

        /// <summary>
        /// Whether to preview changes before applying
        /// </summary>
        public bool PreviewMode { get; set; }

        public SheetRenumberingModel()
        {
            Prefix = "A0.";
            StartNumber = 1;
            Increment = 1;
            PaddingDigits = 2;
            PreviewMode = false;
        }
    }

    /// <summary>
    /// Individual sheet item for renumbering
    /// </summary>
    public class SheetRenumberingItem
    {
        /// <summary>
        /// Revit ElementId of the sheet
        /// </summary>
        public int ElementId { get; set; }

        /// <summary>
        /// New sheet number to assign
        /// </summary>
        public string NewSheetNumber { get; set; }

        /// <summary>
        /// Current sheet number (for comparison/logging)
        /// </summary>
        public string CurrentSheetNumber { get; set; }

        /// <summary>
        /// Order in the renumbering sequence (0-based)
        /// </summary>
        public int Order { get; set; }
    }
}
