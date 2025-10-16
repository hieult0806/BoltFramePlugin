using Autodesk.Revit.DB;

namespace BoltFramePlugin.Models.LimitingDistance
{
    /// <summary>
    /// Types of reference lines for Limiting Distance calculations
    /// </summary>
    public enum ReferenceLineType
    {
        RoadCenterline,
        PropertyLine_StreetEdge,
        PropertyLine_NonStreetEdge,
        ImaginaryLine
    }

    /// <summary>
    /// Information about a reference line used in Limiting Distance calculations
    /// </summary>
    public class ReferenceLineInfo
    {
        public Element Element { get; set; }
        public ElementId ElementId { get; set; }
        public ReferenceLineType LineType { get; set; }
        public Curve Curve { get; set; }
        public string Name { get; set; }
        public bool IsStreetEdge { get; set; } // For Property Lines only
        public XYZ? IntersectionPoint { get; set; } // The point where the ray hit the reference line
        public double Distance { get; set; } // Distance from ray start to intersection point

        public string LineTypeFormatted
        {
            get
            {
                return LineType switch
                {
                    ReferenceLineType.RoadCenterline => "Road Centerline",
                    ReferenceLineType.PropertyLine_StreetEdge => "Property Line (Street Edge)",
                    ReferenceLineType.PropertyLine_NonStreetEdge => "Property Line (Non-Street)",
                    ReferenceLineType.ImaginaryLine => "Imaginary Line",
                    _ => "Unknown"
                };
            }
        }
    }
}
