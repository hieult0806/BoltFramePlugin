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

        /// <summary>
        /// Gets the orientation description - uses reference line name if available,
        /// otherwise falls back to cardinal direction
        /// </summary>
        public string OrientationDescription
        {
            get
            {
                // If this group is associated with a reference line, use its name
                if (ReferenceLine != null && !string.IsNullOrEmpty(ReferenceLine.Name))
                {
                    return ReferenceLine.Name;
                }

                // Fallback to cardinal direction based on orientation
                var angle = Math.Atan2(Orientation.Y, Orientation.X) * 180 / Math.PI;
                if (angle < 0) angle += 360;

                if (angle >= 337.5 || angle < 22.5) return "East";
                if (angle >= 22.5 && angle < 67.5) return "NorthEast";
                if (angle >= 67.5 && angle < 112.5) return "North";
                if (angle >= 112.5 && angle < 157.5) return "NorthWest";
                if (angle >= 157.5 && angle < 202.5) return "West";
                if (angle >= 202.5 && angle < 247.5) return "SouthWest";
                if (angle >= 247.5 && angle < 292.5) return "South";
                return "SouthEast";
            }
        }
    }
}
