using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace LoBIM.Features.LimitingDistance.Models
{
    /// <summary>
    /// Model for tracking created elevation views
    /// </summary>
    public class ViewInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _viewName;
        private string _viewType;
        private string _elementId;
        private DateTime _createdDate;
        private int _wallsCount;

        public string ViewName
        {
            get => _viewName;
            set
            {
                _viewName = value;
                OnPropertyChanged();
            }
        }

        public string ViewType
        {
            get => _viewType;
            set
            {
                _viewType = value;
                OnPropertyChanged();
            }
        }

        public string ElementId
        {
            get => _elementId;
            set
            {
                _elementId = value;
                OnPropertyChanged();
            }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set
            {
                _createdDate = value;
                OnPropertyChanged();
            }
        }

        public int WallsCount
        {
            get => _wallsCount;
            set
            {
                _wallsCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Reference to the actual Revit ViewSection element
        /// </summary>
        public ViewSection? View { get; set; }

        public ViewInfo(ViewSection view, int wallsCount)
        {
            View = view;
            _viewName = view.Name;
            _viewType = "Elevation";
            _elementId = view.Id.ToString();
            _createdDate = DateTime.Now;
            _wallsCount = wallsCount;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
