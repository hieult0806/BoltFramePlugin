using System.ComponentModel;

namespace LoBIM.Features.LimitingDistance.Models
{
    public class DistanceGroupSummary : INotifyPropertyChanged
    {
        public string Orientation { get; set; } = string.Empty;
        public string DistanceRange { get; set; } = string.Empty;
        public double MinDistance { get; set; }
        public double MaxDistance { get; set; }

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
