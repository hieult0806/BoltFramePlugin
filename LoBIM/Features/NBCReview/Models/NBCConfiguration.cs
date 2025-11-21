using System.Collections.Generic;
using System.Linq;

namespace LoBIM.Features.NBCReview.Models
{
    /// <summary>
    /// RGB color representation
    /// </summary>
    public class RGBColor
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public (byte Red, byte Green, byte Blue) ToTuple() => (R, G, B);
    }

    /// <summary>
    /// Represents a distance range for limiting distance visualization
    /// </summary>
    public class DistanceRange
    {
        public string RangeId { get; set; } = string.Empty;
        public double MinMeters { get; set; }
        public double MaxMeters { get; set; }
        public double MinFeet { get; set; }
        public double MaxFeet { get; set; }
        public string Label { get; set; } = string.Empty;
        public int ColorIndex { get; set; }
        public RGBColor ColorRGB { get; set; } = new RGBColor();
        public string ColorName { get; set; } = string.Empty;

        /// <summary>
        /// Check if a distance in meters falls within this range
        /// </summary>
        public bool ContainsMeters(double distanceMeters)
        {
            return distanceMeters >= MinMeters && distanceMeters < MaxMeters;
        }

        /// <summary>
        /// Check if a distance in feet falls within this range
        /// </summary>
        public bool ContainsFeet(double distanceFeet)
        {
            return distanceFeet >= MinFeet && distanceFeet < MaxFeet;
        }
    }

    /// <summary>
    /// NBC requirement for a specific limiting distance threshold
    /// </summary>
    public class NBCRequirement
    {
        /// <summary>
        /// Limiting distance threshold in meters
        /// This requirement applies when actual distance is >= this value
        /// </summary>
        public double LimitingDistanceMeters { get; set; }

        /// <summary>
        /// Maximum percentage of unprotected openings allowed
        /// </summary>
        public double MaxUnprotectedOpeningPercent { get; set; }

        /// <summary>
        /// Fire Resistance Rating required (e.g., "45min", "1H", "2H", "None")
        /// </summary>
        public string FireResistanceRating { get; set; } = string.Empty;

        /// <summary>
        /// Type of construction required (e.g., "Combustible", "Noncombustible", "Combustible/Noncombustible")
        /// </summary>
        public string ConstructionType { get; set; } = string.Empty;

        /// <summary>
        /// Type of cladding required (e.g., "Combustible", "Noncombustible", "Combustible/Noncombustible")
        /// </summary>
        public string CladdingType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Building classification group (A, C, D, E) with its requirements
    /// </summary>
    public class BuildingClassificationGroup
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<NBCRequirement> Requirements { get; set; } = new List<NBCRequirement>();

        /// <summary>
        /// Get the applicable NBC requirement for a given limiting distance
        /// Returns the requirement with the highest threshold that doesn't exceed the actual distance
        /// </summary>
        public NBCRequirement? GetApplicableRequirement(double actualLimitingDistanceMeters)
        {
            return Requirements
                .Where(r => actualLimitingDistanceMeters >= r.LimitingDistanceMeters)
                .OrderByDescending(r => r.LimitingDistanceMeters)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get requirement for a specific distance threshold
        /// </summary>
        public NBCRequirement? GetRequirementByDistance(double limitingDistanceMeters)
        {
            return Requirements.FirstOrDefault(r =>
                System.Math.Abs(r.LimitingDistanceMeters - limitingDistanceMeters) < 0.01);
        }
    }

    /// <summary>
    /// Table formatting settings for NBC compliance reports
    /// </summary>
    public class TableFormatting
    {
        public List<double> ColumnWidths { get; set; } = new List<double>();
        public double RowHeightFeet { get; set; }
        public List<string> Headers { get; set; } = new List<string>();
    }

    /// <summary>
    /// Occupant group sub-classification (e.g., F1, F2, F3 for Group F)
    /// </summary>
    public class OccupantGroupSubGroup
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Occupant group definition (Group A through F)
    /// </summary>
    public class OccupantGroup
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Examples { get; set; } = new List<string>();
        public List<OccupantGroupSubGroup> SubGroups { get; set; } = new List<OccupantGroupSubGroup>();
    }

    /// <summary>
    /// Building code part definition (Part 3, Part 9, etc.)
    /// </summary>
    public class BuildingCodePart
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Root configuration for NBC requirements
    /// Loaded from NBCRequirements.json
    /// </summary>
    public class NBCConfiguration
    {
        public string CodeReference { get; set; } = "NBC 2020";
        public string Version { get; set; } = "1.0";
        public string LastUpdated { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public List<OccupantGroup> OccupantGroups { get; set; } = new List<OccupantGroup>();
        public List<BuildingCodePart> BuildingCodeParts { get; set; } = new List<BuildingCodePart>();
        public List<DistanceRange> DistanceRanges { get; set; } = new List<DistanceRange>();

        public Dictionary<string, BuildingClassificationGroup> BuildingClassifications { get; set; } =
            new Dictionary<string, BuildingClassificationGroup>();

        public TableFormatting TableFormatting { get; set; } = new TableFormatting();

        /// <summary>
        /// Get distance range that contains the specified distance in meters
        /// </summary>
        public DistanceRange? GetDistanceRangeByMeters(double distanceMeters)
        {
            return DistanceRanges.FirstOrDefault(r => r.ContainsMeters(distanceMeters));
        }

        /// <summary>
        /// Get distance range that contains the specified distance in feet
        /// </summary>
        public DistanceRange? GetDistanceRangeByFeet(double distanceFeet)
        {
            return DistanceRanges.FirstOrDefault(r => r.ContainsFeet(distanceFeet));
        }

        /// <summary>
        /// Get building classification by key (e.g., "GroupA", "GroupD")
        /// </summary>
        public BuildingClassificationGroup? GetClassification(string classificationKey)
        {
            return BuildingClassifications.TryGetValue(classificationKey, out var group) ? group : null;
        }

        /// <summary>
        /// Get applicable NBC requirement for a classification and distance
        /// </summary>
        public NBCRequirement? GetApplicableRequirement(string classificationKey, double actualLimitingDistanceMeters)
        {
            var classification = GetClassification(classificationKey);
            return classification?.GetApplicableRequirement(actualLimitingDistanceMeters);
        }

        /// <summary>
        /// Get all distance ranges as tuples for backwards compatibility
        /// </summary>
        public List<(double Min, double Max, string Label, int ColorIndex)> GetDistanceRangesAsTuples()
        {
            return DistanceRanges
                .Select(r => (r.MinFeet, r.MaxFeet, r.Label, r.ColorIndex))
                .ToList();
        }

        /// <summary>
        /// Get all colors as RGB tuples for backwards compatibility
        /// </summary>
        public List<(byte Red, byte Green, byte Blue)> GetColorsAsTuples()
        {
            return DistanceRanges
                .OrderBy(r => r.ColorIndex)
                .Select(r => r.ColorRGB.ToTuple())
                .ToList();
        }

        /// <summary>
        /// Get occupant group by code (e.g., "Group A", "Group B")
        /// </summary>
        public OccupantGroup? GetOccupantGroup(string code)
        {
            return OccupantGroups.FirstOrDefault(g =>
                g.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all occupant group codes for UI dropdown
        /// </summary>
        public List<string> GetOccupantGroupCodes()
        {
            return OccupantGroups.Select(g => g.Code).ToList();
        }

        /// <summary>
        /// Get all occupant group names for UI dropdown
        /// </summary>
        public List<string> GetOccupantGroupNames()
        {
            return OccupantGroups.Select(g => g.Name).ToList();
        }

        /// <summary>
        /// Get building code part by code (e.g., "Part 3", "Part 9")
        /// </summary>
        public BuildingCodePart? GetBuildingCodePart(string code)
        {
            return BuildingCodeParts.FirstOrDefault(p =>
                p.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all building code part names for UI dropdown
        /// </summary>
        public List<string> GetBuildingCodePartNames()
        {
            return BuildingCodeParts.Select(p => p.Name).ToList();
        }
    }
}
