using System.ComponentModel;

namespace LoBIM.Features.LimitingDistance.Models
{
    /// <summary>
    /// Represents building code compliance data for a specific elevation/orientation
    /// Based on NBC (National Building Code) requirements for fire safety
    /// </summary>
    public class BuildingCodeComplianceSummary : INotifyPropertyChanged
    {
        public string OccupancyClassification { get; set; } = string.Empty; // e.g., "Office", "Retail"
        public string Orientation { get; set; } = string.Empty; // e.g., "EAST", "NORTH", "SOUTH", "WEST"

        // Area of Exposing Building Face (Sqm)
        public double ExposingBuildingFaceArea { get; set; }
        public string ExposingBuildingFaceAreaFormatted => $"{ExposingBuildingFaceArea:F1} m²";

        // Limiting Distance (m)
        public double LimitingDistance { get; set; }
        public string LimitingDistanceFormatted => $"{LimitingDistance:F1} m";

        // Maximum Area of Unprotected Openings Permitted, % of Exposing Building Face Area
        public double MaxUnprotectedOpeningsPercent { get; set; }
        public string MaxUnprotectedOpeningsPercentFormatted => $"{MaxUnprotectedOpeningsPercent:F0}%";

        // Maximum Aggregated Area of Unprotected openings in Exterior wall-Proposed %
        public double ProposedUnprotectedOpeningsPercent { get; set; }
        public string ProposedUnprotectedOpeningsPercentFormatted => $"{ProposedUnprotectedOpeningsPercent:F1}%";

        // FRR (Fire Resistance Rating)
        public string FireResistanceRating { get; set; } = string.Empty; // e.g., "1 h", "2 h", "45 min"

        // Type of Construction Required
        public string ConstructionTypeRequired { get; set; } = string.Empty; // e.g., "Combustible / Noncombustible"

        // Type of Cladding Required
        public string CladdingTypeRequired { get; set; } = string.Empty; // e.g., "Combustible / Noncombustible", "Noncombustible"

        // Compliance Status
        public bool IsCompliant => ProposedUnprotectedOpeningsPercent <= MaxUnprotectedOpeningsPercent;
        public string ComplianceStatus => IsCompliant ? "✓ PASS" : "✗ FAIL";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
