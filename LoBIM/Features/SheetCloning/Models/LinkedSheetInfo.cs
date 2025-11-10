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

        private bool _isCloned;
        /// <summary>
        /// Whether this sheet has been successfully cloned to the host document
        /// </summary>
        public bool IsCloned
        {
            get => _isCloned;
            set
            {
                if (_isCloned != value)
                {
                    _isCloned = value;
                    OnPropertyChanged(nameof(IsCloned));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        /// <summary>
        /// Status text for UI display
        /// </summary>
        public string StatusText => IsCloned ? "✓ Cloned" : "";

        /// <summary>
        /// Cloned sheet element ID (if cloned)
        /// </summary>
        public ElementId ClonedSheetId { get; set; }

        private string _sourceTrackingFileName;
        /// <summary>
        /// Source file name (from LoBIM_SourceFile parameter in host document)
        /// </summary>
        public string SourceTrackingFileName
        {
            get => _sourceTrackingFileName;
            set
            {
                if (_sourceTrackingFileName != value)
                {
                    _sourceTrackingFileName = value;
                    OnPropertyChanged(nameof(SourceTrackingFileName));
                    OnPropertyChanged(nameof(HasSourceTracking));
                    OnPropertyChanged(nameof(SourceTrackingText));
                }
            }
        }

        private string _sourceTrackingSheetNumber;
        /// <summary>
        /// Source sheet number (from LoBIM_SourceSheet parameter in host document)
        /// </summary>
        public string SourceTrackingSheetNumber
        {
            get => _sourceTrackingSheetNumber;
            set
            {
                if (_sourceTrackingSheetNumber != value)
                {
                    _sourceTrackingSheetNumber = value;
                    OnPropertyChanged(nameof(SourceTrackingSheetNumber));
                    OnPropertyChanged(nameof(SourceTrackingText));
                }
            }
        }

        private string _sourceTrackingSheetId;
        /// <summary>
        /// Source sheet ID (from LoBIM_SourceSheetId parameter in host document)
        /// </summary>
        public string SourceTrackingSheetId
        {
            get => _sourceTrackingSheetId;
            set
            {
                if (_sourceTrackingSheetId != value)
                {
                    _sourceTrackingSheetId = value;
                    OnPropertyChanged(nameof(SourceTrackingSheetId));
                }
            }
        }

        /// <summary>
        /// Whether this sheet in the host document has source tracking information
        /// </summary>
        public bool HasSourceTracking => !string.IsNullOrEmpty(SourceTrackingFileName);

        /// <summary>
        /// Display text showing source tracking information
        /// </summary>
        public string SourceTrackingText
        {
            get
            {
                if (HasSourceTracking)
                    return $"From: {SourceTrackingFileName} > {SourceTrackingSheetNumber}";
                return "";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
