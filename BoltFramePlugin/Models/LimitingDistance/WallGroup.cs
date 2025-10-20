using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace BoltFramePlugin.Models.LimitingDistance
{
    /// <summary>
    /// Represents a group of walls associated with a reference line
    /// </summary>
    public class WallGroup
    {
        public List<WallInfo> Walls { get; set; }
        public double LimitingDistance { get; set; }
        public XYZ Orientation { get; set; } // Normalized wall normal direction
        public ReferenceLineInfo? ReferenceLine { get; set; }
        public string GroupName { get; set; }

        public WallGroup()
        {
            Walls = new List<WallInfo>();
            Orientation = XYZ.Zero;
            GroupName = string.Empty;
        }
    }
}
