using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace BoltFramePlugin.Features.LimitingDistance.Models
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

        public XYZ WallsMidPoint()
        {
            if (Walls.Count == 0)
                return XYZ.Zero;

            double sumX = 0;
            double sumY = 0;
            double sumZ = 0;

            foreach (var wallInfo in Walls)
            {
                var locationCurve = wallInfo.Wall.Location as LocationCurve;
                if (locationCurve != null)
                {
                    var midPoint = (locationCurve.Curve.GetEndPoint(0) + locationCurve.Curve.GetEndPoint(1)) / 2;
                    sumX += midPoint.X;
                    sumY += midPoint.Y;
                    sumZ += midPoint.Z;
                }
            }

            int count = Walls.Count;
            return new XYZ(sumX / count, sumY / count, sumZ / count);
        }
    }
}
