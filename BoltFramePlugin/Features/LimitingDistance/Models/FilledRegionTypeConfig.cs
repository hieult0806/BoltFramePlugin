using System.Collections.Generic;

namespace BoltFramePlugin.Features.LimitingDistance.Models
{
    /// <summary>
    /// Root configuration class for filled region types JSON
    /// </summary>
    public class FilledRegionTypeConfiguration
    {
        public List<FilledRegionTypeDefinition> FilledRegionTypes { get; set; } = new List<FilledRegionTypeDefinition>();
    }

    /// <summary>
    /// Defines a single filled region type
    /// </summary>
    public class FilledRegionTypeDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string PatternName { get; set; } = string.Empty;
        public string ColorDescription { get; set; } = string.Empty;
        public int ColorR { get; set; }
        public int ColorG { get; set; }
        public int ColorB { get; set; }
        public int LineWeight { get; set; } = 1;
        public double MinDistanceFeet { get; set; }
        public double MaxDistanceFeet { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }
}
