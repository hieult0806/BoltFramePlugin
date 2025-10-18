using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Diagnostics;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.Models.LimitingDistance
{
    public class ParameterUpdateEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public string ParameterValue { get; }

        public ParameterUpdateEventArgs(string parameterName, string parameterValue)
        {
            ParameterName = parameterName;
            ParameterValue = parameterValue;
        }
    }

    public class WallInfo : INotifyPropertyChanged
    {
        private double? _limitingDistance;
        private ReferenceLineInfo? _referenceLine;
        private string _occupantGroup = "Group A";
        private static ILoggingService? _logger;

        public Wall Wall { get; set; }
        public ElementId ElementId { get; set; }
        public string Name { get; set; }
        public double Length { get; set; }
        public string LengthFormatted => $"{Length:F2} ft";

        // Event fired when a parameter needs to be updated in Revit
        public event EventHandler<ParameterUpdateEventArgs>? ParameterUpdateRequested;

        // Static method to set logger (called once from ViewModel)
        public static void SetLogger(ILoggingService logger)
        {
            _logger = logger;
        }

        // Occupant Group (Group A through F)
        public string OccupantGroup
        {
            get => _occupantGroup;
            set
            {
                if (_occupantGroup != value)
                {
                    _logger?.LogInformation($"WallInfo {ElementId?.Value}: OccupantGroup changing from '{_occupantGroup}' to '{value}'");
                    _occupantGroup = value;
                    OnPropertyChanged(nameof(OccupantGroup));

                    // Request parameter update in Revit
                    ParameterUpdateRequested?.Invoke(this, new ParameterUpdateEventArgs("OccupantGroup", value));
                }
            }
        }

        // Area properties
        public double GrossArea { get; set; }
        public double OpeningsArea { get; set; }
        public double NetArea => GrossArea - OpeningsArea;

        public string GrossAreaFormatted => $"{GrossArea:F2} ft²";
        public string OpeningsAreaFormatted => $"{OpeningsArea:F2} ft²";
        public string NetAreaFormatted => $"{NetArea:F2} ft²";

        // Opening percentage (Openings Area / Gross Area * 100)
        public double OpeningPercentage => GrossArea > 0 ? (OpeningsArea / GrossArea * 100) : 0;
        public string OpeningPercentageFormatted => $"{OpeningPercentage:F1}%";

        // Limiting Distance properties
        public double? LimitingDistance
        {
            get => _limitingDistance;
            set
            {
                if (_limitingDistance != value)
                {
                    _limitingDistance = value;
                    OnPropertyChanged(nameof(LimitingDistance));
                    OnPropertyChanged(nameof(LimitingDistanceFormatted));
                }
            }
        }

        public string LimitingDistanceFormatted => LimitingDistance.HasValue ? $"{LimitingDistance.Value:F2} ft" : "N/A";

        public ReferenceLineInfo? ReferenceLine
        {
            get => _referenceLine;
            set
            {
                if (_referenceLine != value)
                {
                    _referenceLine = value;
                    OnPropertyChanged(nameof(ReferenceLine));
                }
            }
        }

        public XYZ? Orientation { get; set; } // Wall normal direction (from interior to exterior)

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            _logger?.LogInformation($"WallInfo.OnPropertyChanged: Property '{propertyName}' changed for wall {ElementId?.Value}");

            if (PropertyChanged == null)
            {
                _logger?.LogWarning($"WallInfo.OnPropertyChanged: WARNING - No PropertyChanged subscribers for wall {ElementId?.Value}");
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            _logger?.LogInformation($"WallInfo.OnPropertyChanged: PropertyChanged event invoked for '{propertyName}'");
        }
    }
}
