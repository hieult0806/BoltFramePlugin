using System.ComponentModel;

namespace BoltFramePlugin.Features.LimitingDistance.Models
{
    /// <summary>
    /// Represents a required project parameter
    /// </summary>
    public class ProjectParameterInfo : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string ParameterType { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;

        private string _status = "Not Created";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a required custom family
    /// </summary>
    public class RequiredFamilyInfo : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        private string _status = "Not Loaded";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a required filled region type
    /// </summary>
    public class FilledRegionTypeInfo : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string PatternName { get; set; } = string.Empty;
        public string ColorDescription { get; set; } = string.Empty;
        public int ColorR { get; set; }
        public int ColorG { get; set; }
        public int ColorB { get; set; }

        private string _status = "Not Created";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
