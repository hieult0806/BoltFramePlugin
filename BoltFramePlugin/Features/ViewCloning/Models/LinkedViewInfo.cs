using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BoltFramePlugin.Features.ViewCloning.Models
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
