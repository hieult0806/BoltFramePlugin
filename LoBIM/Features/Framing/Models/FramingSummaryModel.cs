using System.Collections.Generic;

namespace LoBIM.Features.Framing.Models
{
    /// <summary>
    /// Model for summarizing generated framing elements
    /// </summary>
    public class FramingSummaryModel
    {
        public List<FramingSummaryItem> Items { get; set; } = new List<FramingSummaryItem>();
        public string GroupName { get; set; }
        public bool IsGrouped { get; set; }
    }

    /// <summary>
    /// Individual item in the framing summary
    /// </summary>
    public class FramingSummaryItem
    {
        public string BeamType { get; set; }  // Boundary, Horizontal, Vertical
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public int Count { get; set; }
        public double MinLength { get; set; }
        public double MaxLength { get; set; }
        public double AvgLength { get; set; }
        public double TotalLength { get; set; }

        // Formatted properties for display
        public string MinLengthFt => $"{MinLength:F2} ft";
        public string MaxLengthFt => $"{MaxLength:F2} ft";
        public string AvgLengthFt => $"{AvgLength:F2} ft";
        public string TotalLengthFt => $"{TotalLength:F2} ft";
    }
}
