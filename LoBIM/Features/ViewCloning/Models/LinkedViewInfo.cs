using System.ComponentModel;
using Autodesk.Revit.DB;

namespace LoBIM.Features.ViewCloning.Models
{
    /// <summary>
    /// Represents a view from a linked Revit file
    /// </summary>
    public class LinkedViewInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isCloned;

        /// <summary>
        /// The view from the linked document
        /// </summary>
        public Autodesk.Revit.DB.View View { get; set; }

        /// <summary>
        /// Parent linked file info
        /// </summary>
        public LinkedFileInfo ParentLink { get; set; }

        /// <summary>
        /// View name
        /// </summary>
        public string ViewName { get; set; }

        /// <summary>
        /// View type (FloorPlan, Section, Elevation, etc.)
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// View ID
        /// </summary>
        public ElementId ViewId { get; set; }

        /// <summary>
        /// Whether this view is selected for cloning
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

        /// <summary>
        /// Whether this view has been successfully cloned
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
        /// Cloned view element ID (if cloned)
        /// </summary>
        public ElementId? ClonedViewId { get; set; }

        /// <summary>
        /// Level associated with the view (if applicable)
        /// </summary>
        public string LevelName { get; set; }

        /// <summary>
        /// View scale
        /// </summary>
        public int Scale { get; set; }

        private string _sourceFileName;
        /// <summary>
        /// Source file name (from LoBIM_SourceFile parameter in host document)
        /// Indicates this view was cloned from a linked file
        /// </summary>
        public string SourceFileName
        {
            get => _sourceFileName;
            set
            {
                if (_sourceFileName != value)
                {
                    _sourceFileName = value;
                    OnPropertyChanged(nameof(SourceFileName));
                    OnPropertyChanged(nameof(HasSourceTracking));
                    OnPropertyChanged(nameof(SourceTrackingText));
                }
            }
        }

        private string _sourceViewName;
        /// <summary>
        /// Source view name (from LoBIM_SourceView parameter in host document)
        /// Indicates the original view name this was cloned from
        /// </summary>
        public string SourceViewName
        {
            get => _sourceViewName;
            set
            {
                if (_sourceViewName != value)
                {
                    _sourceViewName = value;
                    OnPropertyChanged(nameof(SourceViewName));
                    OnPropertyChanged(nameof(SourceTrackingText));
                }
            }
        }

        private string _sourceViewId;
        /// <summary>
        /// Source view ID (from LoBIM_SourceViewId parameter in host document)
        /// Indicates the original view ID this was cloned from
        /// </summary>
        public string SourceViewId
        {
            get => _sourceViewId;
            set
            {
                if (_sourceViewId != value)
                {
                    _sourceViewId = value;
                    OnPropertyChanged(nameof(SourceViewId));
                }
            }
        }

        /// <summary>
        /// Whether this view in the host document has source tracking information
        /// </summary>
        public bool HasSourceTracking => !string.IsNullOrEmpty(SourceFileName);

        /// <summary>
        /// Display text showing source tracking information
        /// </summary>
        public string SourceTrackingText
        {
            get
            {
                if (HasSourceTracking)
                    return $"From: {SourceFileName} > {SourceViewName}";
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
