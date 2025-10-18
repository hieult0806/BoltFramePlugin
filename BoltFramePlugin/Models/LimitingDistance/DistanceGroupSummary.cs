using System.ComponentModel;

namespace BoltFramePlugin.Models.LimitingDistance
{
    public class DistanceGroupSummary : INotifyPropertyChanged
    {
        private int _wallCount;

        public string Orientation { get; set; } = string.Empty; // e.g., "North", "South", "East", "West"
        public string DistanceRange { get; set; } = string.Empty;
        public double MinDistance { get; set; }
        public double MaxDistance { get; set; }

        public int WallCount
        {
            get => _wallCount;
            set
            {
                if (_wallCount != value)
                {
                    _wallCount = value;
                    OnPropertyChanged(nameof(WallCount));
                }
            }
        }

        public double TotalGrossArea { get; set; }
        public double TotalOpeningsArea { get; set; }
        public double TotalNetArea => TotalGrossArea - TotalOpeningsArea;

        public string TotalGrossAreaFormatted => $"{TotalGrossArea:F2} ft²";
        public string TotalOpeningsAreaFormatted => $"{TotalOpeningsArea:F2} ft²";
        public string TotalNetAreaFormatted => $"{TotalNetArea:F2} ft²";

        public double AverageOpeningPercentage => TotalGrossArea > 0 ? (TotalOpeningsArea / TotalGrossArea * 100) : 0;
        public string AverageOpeningPercentageFormatted => $"{AverageOpeningPercentage:F1}%";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
