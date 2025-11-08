using System.ComponentModel;
using Autodesk.Revit.DB;

namespace LoBIM.Features.SheetCloning.Models
{
    /// <summary>
    /// Represents a sheet from a linked Revit file
    /// </summary>
    public class LinkedSheetInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// The sheet element from the linked document
        /// </summary>
        public ViewSheet Sheet { get; set; }

        /// <summary>
        /// Element ID of the sheet
        /// </summary>
        public ElementId SheetId { get; set; }

        /// <summary>
        /// Sheet number (e.g., "A-101")
        /// </summary>
        public string SheetNumber { get; set; }

        /// <summary>
        /// Sheet name/title
        /// </summary>
        public string SheetName { get; set; }

        /// <summary>
        /// Full display name (SheetNumber - SheetName)
        /// </summary>
        public string DisplayName => $"{SheetNumber} - {SheetName}";

        /// <summary>
        /// The linked file this sheet belongs to
        /// </summary>
        public string SourceFileName { get; set; }

        /// <summary>
        /// Number of views placed on this sheet
        /// </summary>
        public int ViewportCount { get; set; }

        /// <summary>
        /// Display text for viewport count
        /// </summary>
        public string ViewportCountDisplay => ViewportCount == 1 ? "1 view" : $"{ViewportCount} views";

        /// <summary>
        /// Whether this sheet is selected for cloning
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
