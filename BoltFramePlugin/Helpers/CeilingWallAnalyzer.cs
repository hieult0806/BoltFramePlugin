using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BoltFramePlugin.Helpers
{
    /// <summary>
    /// Provides methods to analyze relationships between ceilings and walls
    /// </summary>
    public static class CeilingWallAnalyzer
    {
        /// <summary>
        /// Finds all ceilings that are connected to the specified wall
        /// </summary>
        /// <param name="wall">The wall to analyze</param>
        /// <param name="document">The Revit document</param>
        /// <param name="tolerance">Distance tolerance in feet (default: 0.01 ft = ~1/8 inch)</param>
        /// <returns>List of ceilings connected to the wall with their relationship type</returns>
        public static List<WallCeilingRelationship> GetConnectedCeilings(Wall wall, Document document, double tolerance = 0.01)
        {
            if (wall == null || document == null)
                throw new ArgumentNullException();

            var relationships = new List<WallCeilingRelationship>();

            // Get wall elevations
            var wallBaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0.0;
            var wallHeightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            var wallHeight = wallHeightParam?.AsDouble() ?? 10.0;

            var wallLevel = document.GetElement(wall.LevelId) as Level;
            if (wallLevel == null)
                return relationships;

            var wallBaseElevation = wallLevel.Elevation + wallBaseOffset;
            var wallTopElevation = wallBaseElevation + wallHeight;

            // Get wall location curve
            var wallLocationCurve = (wall.Location as LocationCurve)?.Curve;
            if (wallLocationCurve == null)
                return relationships;

            // Get all ceilings in the document
            var ceilingCollector = new FilteredElementCollector(document)
                .OfClass(typeof(Ceiling))
                .WhereElementIsNotElementType()
                .Cast<Ceiling>();

            foreach (var ceiling in ceilingCollector)
            {
                var ceilingLevel = document.GetElement(ceiling.LevelId) as Level;
                if (ceilingLevel == null)
                    continue;

                var ceilingElevation = ceilingLevel.Elevation +
                    (ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0.0);

                var ceilingBoundaries = GetCeilingBoundaries(ceiling);
                if (ceilingBoundaries.Count == 0)
                    continue;

                var relationship = AnalyzeWallCeilingRelationship(
                    wall,
                    ceiling,
                    ceilingBoundaries,
                    ceilingElevation,
                    wallBaseElevation,
                    wallTopElevation,
                    wallLocationCurve,
                    tolerance);

                if (relationship != null)
                {
                    relationships.Add(relationship);
                }
            }

            return relationships;
        }

        /// <summary>
        /// Finds all walls that are connected to, touching, or relating to the specified ceiling
        /// </summary>
        /// <param name="ceiling">The ceiling to analyze</param>
        /// <param name="document">The Revit document</param>
        /// <param name="tolerance">Distance tolerance in feet (default: 0.01 ft = ~1/8 inch)</param>
        /// <returns>List of walls connected to the ceiling with their relationship type</returns>
        public static List<WallCeilingRelationship> GetConnectedWalls(Ceiling ceiling, Document document, double tolerance = 0.01)
        {
            if (ceiling == null || document == null)
                throw new ArgumentNullException();

            var relationships = new List<WallCeilingRelationship>();

            // Get ceiling boundary and elevation
            var ceilingLevel = document.GetElement(ceiling.LevelId) as Level;
            if (ceilingLevel == null)
                return relationships;

            // Get ceiling elevation (bottom face)
            var ceilingElevation = ceilingLevel.Elevation + ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0.0;

            // Get ceiling boundary curves
            var ceilingBoundaries = GetCeilingBoundaries(ceiling);
            if (ceilingBoundaries.Count == 0)
                return relationships;

            // Get all walls in the document
            var wallCollector = new FilteredElementCollector(document)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>();

            foreach (var wall in wallCollector)
            {
                // Get wall elevations
                var wallBaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0.0;
                var wallHeightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                var wallHeight = wallHeightParam?.AsDouble() ?? 10.0;

                var wallLevel = document.GetElement(wall.LevelId) as Level;
                if (wallLevel == null)
                    continue;

                var wallBaseElevation = wallLevel.Elevation + wallBaseOffset;
                var wallTopElevation = wallBaseElevation + wallHeight;

                // Get wall location curve
                var wallLocationCurve = (wall.Location as LocationCurve)?.Curve;
                if (wallLocationCurve == null)
                    continue;

                var relationship = AnalyzeWallCeilingRelationship(
                    wall,
                    ceiling,
                    ceilingBoundaries,
                    ceilingElevation,
                    wallBaseElevation,
                    wallTopElevation,
                    wallLocationCurve,
                    tolerance);

                if (relationship != null)
                {
                    relationships.Add(relationship);
                }
            }

            return relationships;
        }

        /// <summary>
        /// Finds all perimeter walls that are not connected to any top-most ceiling
        /// </summary>
        /// <param name="document">The Revit document</param>
        /// <param name="tolerance">Distance tolerance in feet (default: 0.01 ft = ~1/8 inch)</param>
        /// <returns>List of perimeter walls without top-most ceiling connections</returns>
        public static List<Wall> GetPerimeterWallsWithoutTopMostCeiling(Document document, double tolerance = 0.01)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var perimeterWallsWithoutTopCeiling = new List<Wall>();

            // Get all walls in the document
            var allWalls = new FilteredElementCollector(document)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .ToList();

            // Get all ceilings with IsTopMost property checked
            var topMostCeilings = new FilteredElementCollector(document)
                .OfClass(typeof(Ceiling))
                .WhereElementIsNotElementType()
                .Cast<Ceiling>()
                .Where(c => IsTopMostCeiling(c))
                .ToList();

            // Check each wall
            foreach (var wall in allWalls)
            {
                // Check if wall is a perimeter wall
                if (!IsPerimeterWall(wall))
                    continue;

                // Check if wall is connected to any top-most ceiling
                bool isConnectedToTopMostCeiling = false;

                foreach (var ceiling in topMostCeilings)
                {
                    var ceilingLevel = document.GetElement(ceiling.LevelId) as Level;
                    if (ceilingLevel == null)
                        continue;

                    var ceilingElevation = ceilingLevel.Elevation +
                        (ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0.0);

                    var ceilingBoundaries = GetCeilingBoundaries(ceiling);
                    if (ceilingBoundaries.Count == 0)
                        continue;

                    // Get wall parameters
                    var wallBaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0.0;
                    var wallHeightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                    var wallHeight = wallHeightParam?.AsDouble() ?? 10.0;

                    var wallLevel = document.GetElement(wall.LevelId) as Level;
                    if (wallLevel == null)
                        continue;

                    var wallBaseElevation = wallLevel.Elevation + wallBaseOffset;
                    var wallTopElevation = wallBaseElevation + wallHeight;

                    var wallLocationCurve = (wall.Location as LocationCurve)?.Curve;
                    if (wallLocationCurve == null)
                        continue;

                    var relationship = AnalyzeWallCeilingRelationship(
                        wall,
                        ceiling,
                        ceilingBoundaries,
                        ceilingElevation,
                        wallBaseElevation,
                        wallTopElevation,
                        wallLocationCurve,
                        tolerance);

                    if (relationship != null)
                    {
                        isConnectedToTopMostCeiling = true;
                        break;
                    }
                }

                // If wall is not connected to any top-most ceiling, add to list
                if (!isConnectedToTopMostCeiling)
                {
                    perimeterWallsWithoutTopCeiling.Add(wall);
                }
            }

            return perimeterWallsWithoutTopCeiling;
        }

        /// <summary>
        /// Checks if a ceiling has the IsTopMost property checked
        /// </summary>
        private static bool IsTopMostCeiling(Ceiling ceiling)
        {
            // Check for "Top Most" parameter (custom parameter or built-in)
            var topMostParam = ceiling.LookupParameter("IsTopMost");
            if (topMostParam != null && topMostParam.StorageType == StorageType.Integer)
            {
                return topMostParam.AsInteger() == 1;
            }

            // Alternative parameter names
            topMostParam = ceiling.LookupParameter("Top Most");
            if (topMostParam != null && topMostParam.StorageType == StorageType.Integer)
            {
                return topMostParam.AsInteger() == 1;
            }

            topMostParam = ceiling.LookupParameter("TopMost");
            if (topMostParam != null && topMostParam.StorageType == StorageType.Integer)
            {
                return topMostParam.AsInteger() == 1;
            }

            return false;
        }

        /// <summary>
        /// Checks if a wall is a perimeter wall based on LD_IsPerimeter parameter
        /// </summary>
        private static bool IsPerimeterWall(Wall wall)
        {
            // Check for LD_IsPerimeter parameter
            var isPerimeterParam = wall.LookupParameter("LD_IsPerimeter");
            if (isPerimeterParam != null && isPerimeterParam.StorageType == StorageType.Integer)
            {
                return isPerimeterParam.AsInteger() == 1;
            }

            return false;
        }

        /// <summary>
        /// Gets the boundary curves of a ceiling
        /// </summary>
        private static List<CurveLoop> GetCeilingBoundaries(Ceiling ceiling)
        {
            var boundaries = new List<CurveLoop>();

            try
            {
                // Try to get sketch-based ceiling boundaries
                var sketch = ceiling.GetDependentElements(new ElementClassFilter(typeof(Sketch)))
                    .Select(id => ceiling.Document.GetElement(id) as Sketch)
                    .FirstOrDefault();

                if (sketch != null)
                {
                    foreach (CurveArray curveArray in sketch.Profile)
                    {
                        var curveLoop = new CurveLoop();
                        foreach (Curve curve in curveArray)
                        {
                            curveLoop.Append(curve);
                        }
                        boundaries.Add(curveLoop);
                    }
                }
                else
                {
                    // Try to get face-based boundaries
                    var options = new Options { ComputeReferences = true };
                    var geometry = ceiling.get_Geometry(options);
                    if (geometry != null)
                    {
                        foreach (GeometryObject obj in geometry)
                        {
                            if (obj is Solid solid && solid.Faces.Size > 0)
                            {
                                // Get the bottom face (largest horizontal face)
                                Face bottomFace = null;
                                double maxArea = 0;

                                foreach (Face face in solid.Faces)
                                {
                                    var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
                                    // Check if face is horizontal (pointing down or up)
                                    if (Math.Abs(faceNormal.Z) > 0.9)
                                    {
                                        var area = face.Area;
                                        if (area > maxArea)
                                        {
                                            maxArea = area;
                                            bottomFace = face;
                                        }
                                    }
                                }

                                if (bottomFace != null)
                                {
                                    var edgeLoops = bottomFace.GetEdgesAsCurveLoops();
                                    boundaries.AddRange(edgeLoops);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list if boundary extraction fails
            }

            return boundaries;
        }

        /// <summary>
        /// Analyzes the relationship between a wall and ceiling
        /// </summary>
        private static WallCeilingRelationship AnalyzeWallCeilingRelationship(
            Wall wall,
            Ceiling ceiling,
            List<CurveLoop> ceilingBoundaries,
            double ceilingElevation,
            double wallBaseElevation,
            double wallTopElevation,
            Curve wallLocationCurve,
            double tolerance)
        {
            if (wallLocationCurve == null)
                return null;

            // First, check if wall and ceiling are joined using Join Geometry
            bool areJoined = JoinGeometryUtils.AreElementsJoined(wall.Document, wall, ceiling);

            // Determine relationships
            var verticalRelationship = DetermineVerticalRelationship(
                wallTopElevation,
                ceilingElevation,
                tolerance);

            var horizontalRelationship = DetermineHorizontalRelationship(
                wallLocationCurve,
                ceilingBoundaries,
                tolerance);

            // If joined, always return the relationship
            if (areJoined)
            {
                return new WallCeilingRelationship
                {
                    Wall = wall,
                    Ceiling = ceiling,
                    VerticalRelationship = verticalRelationship,
                    HorizontalRelationship = horizontalRelationship,
                    WallBaseElevation = wallBaseElevation,
                    WallTopElevation = wallTopElevation,
                    CeilingElevation = ceilingElevation,
                    IsJoined = true
                };
            }

            // If not joined, check both vertical and horizontal relationships
            if (verticalRelationship == VerticalRelationshipType.None)
                return null; // Wall doesn't touch ceiling vertically

            if (horizontalRelationship == HorizontalRelationshipType.None)
                return null; // Wall doesn't touch ceiling horizontally

            return new WallCeilingRelationship
            {
                Wall = wall,
                Ceiling = ceiling,
                VerticalRelationship = verticalRelationship,
                HorizontalRelationship = horizontalRelationship,
                WallBaseElevation = wallBaseElevation,
                WallTopElevation = wallTopElevation,
                CeilingElevation = ceilingElevation,
                IsJoined = false
            };
        }

        /// <summary>
        /// Determines the vertical relationship between wall and ceiling
        /// Only returns TouchesAtTop if wall top elevation matches ceiling elevation
        /// </summary>
        private static VerticalRelationshipType DetermineVerticalRelationship(
            double wallTop,
            double ceilingElevation,
            double tolerance)
        {
            // Only check if wall top touches ceiling (must match elevation exactly)
            if (Math.Abs(wallTop - ceilingElevation) <= tolerance)
                return VerticalRelationshipType.TouchesAtTop;

            return VerticalRelationshipType.None;
        }

        /// <summary>
        /// Determines the horizontal relationship between wall and ceiling boundaries
        /// </summary>
        private static HorizontalRelationshipType DetermineHorizontalRelationship(
            Curve wallCurve,
            List<CurveLoop> ceilingBoundaries,
            double tolerance)
        {
            bool hasIntersection = false;
            bool hasOverlap = false;
            bool isOnBoundary = false;

            foreach (var boundary in ceilingBoundaries)
            {
                foreach (Curve boundaryCurve in boundary)
                {
                    // Check if curves are coincident or overlap
                    var result = wallCurve.Intersect(boundaryCurve, out IntersectionResultArray results);

                    if (result == SetComparisonResult.Overlap)
                    {
                        hasOverlap = true;
                        isOnBoundary = true;
                    }
                    else if (result == SetComparisonResult.Subset || result == SetComparisonResult.Superset)
                    {
                        isOnBoundary = true;
                    }
                    else if (results != null && results.Size > 0)
                    {
                        hasIntersection = true;
                    }

                    // Check proximity
                    var wallStart = wallCurve.GetEndPoint(0);
                    var wallEnd = wallCurve.GetEndPoint(1);
                    var boundaryStart = boundaryCurve.GetEndPoint(0);
                    var boundaryEnd = boundaryCurve.GetEndPoint(1);

                    // Project to 2D (ignore Z)
                    var wallStart2D = new XYZ(wallStart.X, wallStart.Y, 0);
                    var wallEnd2D = new XYZ(wallEnd.X, wallEnd.Y, 0);
                    var boundaryStart2D = new XYZ(boundaryStart.X, boundaryStart.Y, 0);
                    var boundaryEnd2D = new XYZ(boundaryEnd.X, boundaryEnd.Y, 0);

                    if (wallStart2D.DistanceTo(boundaryStart2D) <= tolerance ||
                        wallStart2D.DistanceTo(boundaryEnd2D) <= tolerance ||
                        wallEnd2D.DistanceTo(boundaryStart2D) <= tolerance ||
                        wallEnd2D.DistanceTo(boundaryEnd2D) <= tolerance)
                    {
                        hasIntersection = true;
                    }
                }
            }

            // Only return walls that are on or overlapping the boundary
            if (isOnBoundary)
                return HorizontalRelationshipType.OnBoundary;
            if (hasOverlap)
                return HorizontalRelationshipType.Overlaps;
            if (hasIntersection)
                return HorizontalRelationshipType.Intersects;

            // Don't include walls that are completely inside the boundary
            return HorizontalRelationshipType.None;
        }

    }

    /// <summary>
    /// Represents the relationship between a wall and ceiling
    /// </summary>
    public class WallCeilingRelationship
    {
        public Wall Wall { get; set; }
        public Ceiling Ceiling { get; set; }
        public VerticalRelationshipType VerticalRelationship { get; set; }
        public HorizontalRelationshipType HorizontalRelationship { get; set; }
        public double WallBaseElevation { get; set; }
        public double WallTopElevation { get; set; }
        public double CeilingElevation { get; set; }
        public bool IsJoined { get; set; }

        public string GetDescription()
        {
            var joinedText = IsJoined ? " [JOINED]" : "";
            return $"Wall {Wall.Id.Value}: {VerticalRelationship} & {HorizontalRelationship}{joinedText}";
        }
    }

    /// <summary>
    /// Types of vertical relationships between wall and ceiling
    /// </summary>
    public enum VerticalRelationshipType
    {
        None,
        TouchesAtTop           // Wall top elevation matches ceiling elevation
    }

    /// <summary>
    /// Types of horizontal relationships between wall and ceiling
    /// </summary>
    public enum HorizontalRelationshipType
    {
        None,
        OnBoundary,      // Wall is on the ceiling boundary
        Overlaps,        // Wall overlaps with ceiling boundary
        Intersects       // Wall intersects ceiling boundary
    }
}
