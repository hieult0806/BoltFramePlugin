using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BoltFramePlugin.Features.SheetManagement.Models
{
    /// <summary>
    /// Represents a sheet item with all its properties for UI binding and manipulation
    /// </summary>
    public class SheetItemModel : INotifyPropertyChanged
    {
        private int _displayOrder;
        private string _sheetNumber;
        private string _sheetName;

        /// <summary>
        /// Revit ElementId of the ViewSheet
        /// </summary>
        public int ElementId { get; set; }

        /// <summary>
        /// Current display order in the list (used for drag-drop reordering)
        /// </summary>
        public int DisplayOrder
        {
            get => _displayOrder;
            set
            {
                if (_displayOrder != value)
                {
                    _displayOrder = value;
                    OnPropertyChanged(nameof(DisplayOrder));
                    OnPropertyChanged(nameof(HasChanged));
                }
            }
        }

        /// <summary>
        /// Sheet number (e.g., "A0.01", "A0.02")
        /// </summary>
        public string SheetNumber
        {
            get => _sheetNumber;
            set
            {
                if (_sheetNumber != value)
                {
                    _sheetNumber = value;
                    OnPropertyChanged(nameof(SheetNumber));
                }
            }
        }

        /// <summary>
        /// Sheet name/title
        /// </summary>
        public string SheetName
        {
            get => _sheetName;
            set
            {
                if (_sheetName != value)
                {
                    _sheetName = value;
                    OnPropertyChanged(nameof(SheetName));
                }
            }
        }

        /// <summary>
        /// Dictionary of parameter names and their values for filtering
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Original sheet number before any modifications (for tracking changes)
        /// </summary>
        public string OriginalSheetNumber { get; set; }

        /// <summary>
        /// Indicates whether this sheet's order has changed from original
        /// </summary>
        public bool HasChanged
        {
            get => DisplayOrder != OriginalDisplayOrder;
        }

        /// <summary>
        /// Original display order when sheet was loaded
        /// </summary>
        public int OriginalDisplayOrder { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Gets a parameter value by name, returns empty string if not found
        /// </summary>
        public string GetParameter(string parameterName)
        {
            return Parameters.TryGetValue(parameterName, out string value) ? value : string.Empty;
        }

        /// <summary>
        /// Checks if this sheet matches the given filter criteria
        /// </summary>
        public bool MatchesFilter(string parameterName, string filterValue)
        {
            if (string.IsNullOrWhiteSpace(filterValue))
                return true;

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                // Search in sheet number and name if no specific parameter selected
                return (SheetNumber?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (SheetName?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            string paramValue = GetParameter(parameterName);
            return paramValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return $"{SheetNumber} - {SheetName}";
        }
    }
}
