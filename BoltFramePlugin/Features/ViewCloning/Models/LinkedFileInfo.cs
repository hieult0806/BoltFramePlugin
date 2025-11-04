using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BoltFramePlugin.Features.ViewCloning.Models
{
    /// <summary>
    /// Represents a linked Revit file with its views
    /// </summary>
    public class LinkedFileInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// The RevitLinkInstance element
        /// </summary>
        public RevitLinkInstance LinkInstance { get; set; }

        /// <summary>
        /// The linked document
        /// </summary>
        public Document LinkedDocument { get; set; }

        /// <summary>
        /// Name of the linked file
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Full path to the linked file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Element ID of the link instance
        /// </summary>
        public ElementId LinkId { get; set; }

        /// <summary>
        /// Whether this link is selected for viewing
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
        /// List of views available in this linked file
        /// </summary>
        public List<LinkedViewInfo> Views { get; set; } = new List<LinkedViewInfo>();

        /// <summary>
        /// Number of views in this linked file
        /// </summary>
        public int ViewCount => Views?.Count ?? 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
