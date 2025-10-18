using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BoltFramePlugin.Helpers
{
    /// <summary>
    /// Provides 2D polygon boolean operations for filled region creation
    /// </summary>
    public static class PolygonBooleanOperations
    {
        /// <summary>
        /// Represents a 2D rectangle in the elevation view plane
        /// U is horizontal (along wall), V is vertical (height)
        /// </summary>
        public class Rectangle2D
        {
            public double MinU { get; set; }
            public double MaxU { get; set; }
            public double MinV { get; set; }
            public double MaxV { get; set; }

            public Rectangle2D(double minU, double maxU, double minV, double maxV)
            {
                MinU = minU;
                MaxU = maxU;
                MinV = minV;
                MaxV = maxV;
            }

            /// <summary>
            /// Creates a Rectangle2D from four corner points in 3D space
            /// Projects them onto the 2D plane by finding the dominant varying dimensions
            /// </summary>
            public static Rectangle2D FromPoints(XYZ[] points)
            {
                if (points.Length != 4)
                    throw new ArgumentException("Rectangle must have exactly 4 points");

                // Find which dimensions vary (to determine the 2D plane)
                var minX = points.Min(p => p.X);
                var maxX = points.Max(p => p.X);
                var minY = points.Min(p => p.Y);
                var maxY = points.Max(p => p.Y);
                var minZ = points.Min(p => p.Z);
                var maxZ = points.Max(p => p.Z);

                var xRange = maxX - minX;
                var yRange = maxY - minY;
                var zRange = maxZ - minZ;

                // Determine which two dimensions have significant variation
                // The third dimension (with minimal variation) is the plane normal
                double minU, maxU, minV, maxV;

                if (xRange < 0.01) // X is constant, use Y and Z
                {
                    minU = minY;
                    maxU = maxY;
                    minV = minZ;
                    maxV = maxZ;
                }
                else if (yRange < 0.01) // Y is constant, use X and Z
                {
                    minU = minX;
                    maxU = maxX;
                    minV = minZ;
                    maxV = maxZ;
                }
                else // Z is constant, use X and Y
                {
                    minU = minX;
                    maxU = maxX;
                    minV = minY;
                    maxV = maxY;
                }

                return new Rectangle2D(minU, maxU, minV, maxV);
            }

            /// <summary>
            /// Checks if this rectangle intersects with another rectangle
            /// </summary>
            public bool Intersects(Rectangle2D other)
            {
                // Simple 2D AABB intersection test
                if (MaxU <= other.MinU || MinU >= other.MaxU) return false;
                if (MaxV <= other.MinV || MinV >= other.MaxV) return false;
                return true;
            }

            /// <summary>
            /// Checks if this rectangle completely contains another rectangle
            /// </summary>
            public bool Contains(Rectangle2D other)
            {
                return other.MinU >= MinU && other.MaxU <= MaxU &&
                       other.MinV >= MinV && other.MaxV <= MaxV;
            }
        }

        /// <summary>
        /// Subtracts multiple opening rectangles from a wall rectangle
        /// Returns a list of rectangles that represent the wall with openings cut out
        /// </summary>
        public static List<Rectangle2D> SubtractOpenings(Rectangle2D wall, List<Rectangle2D> openings)
        {
            var result = new List<Rectangle2D> { wall };

            foreach (var opening in openings)
            {
                // Only process if the opening intersects with the wall
                if (!wall.Intersects(opening))
                {
                    // Opening is outside, skip it
                    continue;
                }

                var newResult = new List<Rectangle2D>();

                foreach (var rect in result)
                {
                    if (!rect.Intersects(opening))
                    {
                        // No intersection, keep the rectangle as-is
                        newResult.Add(rect);
                    }
                    else
                    {
                        // Subtract the opening from this rectangle
                        var fragments = SubtractRectangle(rect, opening);
                        newResult.AddRange(fragments);
                    }
                }

                result = newResult;
            }

            return result;
        }

        /// <summary>
        /// Subtracts one rectangle from another, returning up to 4 fragments
        /// This creates rectangles around the hole in 2D space
        /// </summary>
        private static List<Rectangle2D> SubtractRectangle(Rectangle2D outer, Rectangle2D hole)
        {
            var fragments = new List<Rectangle2D>();

            // Clamp hole to outer bounds
            var holeMinU = Math.Max(hole.MinU, outer.MinU);
            var holeMaxU = Math.Min(hole.MaxU, outer.MaxU);
            var holeMinV = Math.Max(hole.MinV, outer.MinV);
            var holeMaxV = Math.Min(hole.MaxV, outer.MaxV);

            const double tolerance = 0.001; // 1/1000 ft tolerance

            // Create up to 4 fragments around the hole:
            // 1. Left strip (if there's space to the left of the hole)
            if (holeMinU - outer.MinU > tolerance)
            {
                fragments.Add(new Rectangle2D(
                    outer.MinU, holeMinU,
                    outer.MinV, outer.MaxV
                ));
            }

            // 2. Right strip (if there's space to the right of the hole)
            if (outer.MaxU - holeMaxU > tolerance)
            {
                fragments.Add(new Rectangle2D(
                    holeMaxU, outer.MaxU,
                    outer.MinV, outer.MaxV
                ));
            }

            // 3. Bottom strip (between left and right edges of hole, below hole)
            if (holeMinV - outer.MinV > tolerance)
            {
                fragments.Add(new Rectangle2D(
                    holeMinU, holeMaxU,
                    outer.MinV, holeMinV
                ));
            }

            // 4. Top strip (between left and right edges of hole, above hole)
            if (outer.MaxV - holeMaxV > tolerance)
            {
                fragments.Add(new Rectangle2D(
                    holeMinU, holeMaxU,
                    holeMaxV, outer.MaxV
                ));
            }

            return fragments;
        }

        /// <summary>
        /// Creates CurveLoops from a list of 2D rectangles
        /// Requires the original 3D points to determine which plane we're working on
        /// </summary>
        public static List<CurveLoop> ToCurveLoops(List<Rectangle2D> rectangles, XYZ[] referencePoints)
        {
            var curveLoops = new List<CurveLoop>();

            if (referencePoints.Length != 4)
                return curveLoops;

            // Determine which plane we're on by checking which dimension is constant
            var minX = referencePoints.Min(p => p.X);
            var maxX = referencePoints.Max(p => p.X);
            var minY = referencePoints.Min(p => p.Y);
            var maxY = referencePoints.Max(p => p.Y);
            var minZ = referencePoints.Min(p => p.Z);
            var maxZ = referencePoints.Max(p => p.Z);

            var xRange = maxX - minX;
            var yRange = maxY - minY;
            var zRange = maxZ - minZ;

            foreach (var rect in rectangles)
            {
                var loop = new CurveLoop();
                XYZ[] points = new XYZ[4];

                // Convert 2D rectangle back to 3D points based on which plane we're on
                if (xRange < 0.01) // X is constant, U=Y, V=Z
                {
                    var constX = (minX + maxX) / 2.0;
                    points[0] = new XYZ(constX, rect.MinU, rect.MinV); // Bottom-left
                    points[1] = new XYZ(constX, rect.MaxU, rect.MinV); // Bottom-right
                    points[2] = new XYZ(constX, rect.MaxU, rect.MaxV); // Top-right
                    points[3] = new XYZ(constX, rect.MinU, rect.MaxV); // Top-left
                }
                else if (yRange < 0.01) // Y is constant, U=X, V=Z
                {
                    var constY = (minY + maxY) / 2.0;
                    points[0] = new XYZ(rect.MinU, constY, rect.MinV); // Bottom-left
                    points[1] = new XYZ(rect.MaxU, constY, rect.MinV); // Bottom-right
                    points[2] = new XYZ(rect.MaxU, constY, rect.MaxV); // Top-right
                    points[3] = new XYZ(rect.MinU, constY, rect.MaxV); // Top-left
                }
                else // Z is constant, U=X, V=Y
                {
                    var constZ = (minZ + maxZ) / 2.0;
                    points[0] = new XYZ(rect.MinU, rect.MinV, constZ); // Bottom-left
                    points[1] = new XYZ(rect.MaxU, rect.MinV, constZ); // Bottom-right
                    points[2] = new XYZ(rect.MaxU, rect.MaxV, constZ); // Top-right
                    points[3] = new XYZ(rect.MinU, rect.MaxV, constZ); // Top-left
                }

                // Create the curve loop
                for (int i = 0; i < 4; i++)
                {
                    var nextIndex = (i + 1) % 4;
                    var dist = points[i].DistanceTo(points[nextIndex]);

                    if (dist > 0.001) // Skip very small edges
                    {
                        loop.Append(Line.CreateBound(points[i], points[nextIndex]));
                    }
                }

                if (loop.NumberOfCurves() >= 3 && !loop.IsOpen())
                {
                    curveLoops.Add(loop);
                }
            }

            return curveLoops;
        }
    }
}
