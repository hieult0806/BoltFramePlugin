using Autodesk.Revit.DB;
using LoBIM.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoBIM.Helpers
{
    /// <summary>
    /// Utility class for drawing debug visualizations in Revit floor plans.
    /// Provides methods for drawing rays, arrows, rectangles, lines, and markers for debugging purposes.
    /// </summary>
    public static class RevitDebugVisualizationHelper
    {
        #region Ray and Arrow Drawing

        /// <summary>
        /// Draws reference line arrows (rays) on floor plans with arrowheads.
        /// </summary>
        /// <param name="doc">The Revit document.</param>
        /// <param name="raysToDrawn">List of rays to draw (wall, start point, end point, level ID).</param>
        /// <param name="logger">Logger service for logging information.</param>
        public static void DrawReferenceLineArrows(
            Document doc,
            List<(Wall wall, XYZ start, XYZ end, ElementId levelId)> raysToDrawn,
            ILoggingService logger)
        {
            logger.LogInformation("Drawing distance measurement arrows");

            using (Transaction trans = new Transaction(doc, "Draw Reference Line Rays"))
            {
                trans.Start();
                try
                {
                    var raysByView = GroupRaysByFloorPlan(doc, raysToDrawn, logger);
                    int rayCount = DrawRaysOnFloorPlans(doc, raysByView, logger);

                    logger.LogInformation($"Created {rayCount} total detail lines for rays across {raysByView.Count} floor plans");
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error creating detail lines: {ex.Message}", ex);
                    trans.RollBack();
                }
            }
        }

        /// <summary>
        /// Groups rays by their associated floor plan view.
        /// </summary>
        private static Dictionary<ElementId, List<(XYZ start, XYZ end)>> GroupRaysByFloorPlan(
            Document doc,
            List<(Wall wall, XYZ start, XYZ end, ElementId levelId)> raysToDrawn,
            ILoggingService logger)
        {
            var raysByView = new Dictionary<ElementId, List<(XYZ start, XYZ end)>>();

            foreach (var (wall, start, end, levelId) in raysToDrawn)
            {
                if (levelId == ElementId.InvalidElementId)
                {
                    logger.LogWarning($"Wall {wall.Id.Value} has no base constraint level, skipping ray drawing");
                    continue;
                }

                var level = doc.GetElement(levelId) as Level;
                if (level == null)
                    continue;

                var floorPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == levelId);

                if (floorPlan == null)
                {
                    logger.LogWarning($"No floor plan found for level {level.Name}, skipping wall {wall.Id.Value}");
                    continue;
                }

                if (!raysByView.ContainsKey(floorPlan.Id))
                {
                    raysByView[floorPlan.Id] = new List<(XYZ, XYZ)>();
                }

                raysByView[floorPlan.Id].Add((start, end));
            }

            return raysByView;
        }

        /// <summary>
        /// Draws rays on multiple floor plans.
        /// </summary>
        private static int DrawRaysOnFloorPlans(
            Document doc,
            Dictionary<ElementId, List<(XYZ start, XYZ end)>> raysByView,
            ILoggingService logger)
        {
            int totalRayCount = 0;

            foreach (var kvp in raysByView)
            {
                var viewId = kvp.Key;
                var rays = kvp.Value;
                var view = doc.GetElement(viewId) as ViewPlan;

                if (view == null)
                    continue;

                int viewRayCount = DrawRaysInView(doc, view, rays, logger);
                totalRayCount += viewRayCount;

                logger.LogInformation($"Created {rays.Count} rays on floor plan {view.Name}");
            }

            return totalRayCount;
        }

        /// <summary>
        /// Draws rays in a specific view with arrowheads.
        /// </summary>
        private static int DrawRaysInView(
            Document doc,
            ViewPlan view,
            List<(XYZ start, XYZ end)> rays,
            ILoggingService logger)
        {
            int rayCount = 0;
            const double minLength = 0.003; // ~1/32 inch in feet

            foreach (var (start, end) in rays)
            {
                var start2D = new XYZ(start.X, start.Y, 0);
                var end2D = new XYZ(end.X, end.Y, 0);

                var distance = start2D.DistanceTo(end2D);
                if (distance < minLength)
                {
                    logger.LogWarning($"Skipping ray that is too short ({distance:F6} ft) - below minimum curve length");
                    continue;
                }

                try
                {
                    // Draw the main ray line
                    var line = Line.CreateBound(start2D, end2D);
                    doc.Create.NewDetailCurve(view, line);
                    rayCount++;

                    // Draw arrow head
                    DrawArrowHead(doc, view, start2D, end2D);
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Failed to create ray from ({start2D.X:F2}, {start2D.Y:F2}) to ({end2D.X:F2}, {end2D.Y:F2}): {ex.Message}");
                }
            }

            return rayCount;
        }

        /// <summary>
        /// Draws an arrowhead at the end of a line.
        /// </summary>
        private static void DrawArrowHead(Document doc, ViewPlan view, XYZ start2D, XYZ end2D)
        {
            var direction = (end2D - start2D).Normalize();
            var arrowSize = 0.5; // feet (~6 inches)

            // Calculate perpendicular vector for arrow wings
            var perpendicular = new XYZ(-direction.Y, direction.X, 0);

            // Arrow head base point (slightly back from the end)
            var arrowBase = end2D - (direction * arrowSize);

            // Arrow wing points
            var arrowWing1 = arrowBase + (perpendicular * arrowSize * 0.3);
            var arrowWing2 = arrowBase - (perpendicular * arrowSize * 0.3);

            // Draw two lines forming the arrow head
            var arrowLine1 = Line.CreateBound(end2D, arrowWing1);
            var arrowLine2 = Line.CreateBound(end2D, arrowWing2);

            doc.Create.NewDetailCurve(view, arrowLine1);
            doc.Create.NewDetailCurve(view, arrowLine2);
        }

        #endregion

        #region Distance Line Drawing

        /// <summary>
        /// Draws a debug distance line between two points with green color override.
        /// </summary>
        /// <param name="wall">The wall associated with the distance line.</param>
        /// <param name="pointOnWall">Point on the wall.</param>
        /// <param name="pointOnRefLine">Point on the reference line.</param>
        /// <param name="logger">Logger service.</param>
        public static void DrawDebugDistanceLine(Wall wall, XYZ pointOnWall, XYZ pointOnRefLine, ILoggingService logger)
        {
            try
            {
                var doc = wall.Document;
                var levelId = wall.LookupParameter("Base Constraint")?.AsElementId();

                if (levelId == null || levelId == ElementId.InvalidElementId)
                {
                    logger.LogWarning($"Wall {wall.Id.Value} has no base constraint level, skipping distance line drawing");
                    return;
                }

                var level = doc.GetElement(levelId) as Level;
                if (level == null)
                    return;

                var floorPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == levelId);

                if (floorPlan == null)
                {
                    logger.LogWarning($"No floor plan found for level {level.Name}, skipping distance line drawing for wall {wall.Id.Value}");
                    return;
                }

                // Create a 2D line from the two closest points
                var pointOnWall2D = new XYZ(pointOnWall.X, pointOnWall.Y, 0);
                var pointOnRefLine2D = new XYZ(pointOnRefLine.X, pointOnRefLine.Y, 0);

                // Check minimum distance to avoid creating too-short lines
                const double minLength = 0.003; // ~1/32 inch in feet
                if (pointOnWall2D.DistanceTo(pointOnRefLine2D) < minLength)
                {
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Draw Debug Distance Line"))
                {
                    trans.Start();
                    try
                    {
                        var wallCurve = (wall.Location as LocationCurve);
                        XYZ wallPos = wallCurve.Curve.Evaluate(0.5, true);
                        Line distanceLine = Line.CreateBound(pointOnRefLine2D, pointOnRefLine2D + RotatePoint(wall.Orientation.Normalize()) * wallCurve.Curve.Length);
                        var detailCurve = doc.Create.NewDetailCurve(floorPlan, distanceLine);

                        // Apply green color override
                        var overrideSettings = new OverrideGraphicSettings();
                        var green = new Autodesk.Revit.DB.Color(0, 255, 0);
                        overrideSettings.SetProjectionLineColor(green);
                        overrideSettings.SetProjectionLineWeight(5);
                        floorPlan.SetElementOverrides(detailCurve.Id, overrideSettings);

                        logger.LogInformation($"Drew debug distance line for wall {wall.Id.Value} on {floorPlan.Name}");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Failed to draw debug distance line: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Error in DrawDebugDistanceLine: {ex.Message}");
            }
        }

        #endregion

        #region Rectangle Drawing

        /// <summary>
        /// Draws a debug rectangle showing the search area for a wall.
        /// </summary>
        /// <param name="doc">The Revit document.</param>
        /// <param name="wall">The wall to draw the rectangle for.</param>
        /// <param name="rectangle">The rectangle corners.</param>
        /// <param name="logger">Logger service.</param>
        public static void DrawDebugRectangle(
            Document doc,
            Wall wall,
            (XYZ corner1, XYZ corner2, XYZ corner3, XYZ corner4) rectangle,
            ILoggingService logger)
        {
            try
            {
                var levelId = wall.LookupParameter("Base Constraint")?.AsElementId();

                if (levelId == null || levelId == ElementId.InvalidElementId)
                    return;

                // Find floor plan for this level
                var floorPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == levelId);

                if (floorPlan == null)
                {
                    logger.LogWarning($"No floor plan found for wall {wall.Id.Value}, skipping debug rectangle");
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Draw Debug Rectangle"))
                {
                    trans.Start();
                    try
                    {
                        // Draw the 4 edges of the rectangle
                        var edge1 = Line.CreateBound(rectangle.corner1, rectangle.corner2);
                        var edge2 = Line.CreateBound(rectangle.corner2, rectangle.corner3);
                        var edge3 = Line.CreateBound(rectangle.corner3, rectangle.corner4);
                        var edge4 = Line.CreateBound(rectangle.corner4, rectangle.corner1);

                        doc.Create.NewDetailCurve(floorPlan, edge1);
                        doc.Create.NewDetailCurve(floorPlan, edge2);
                        doc.Create.NewDetailCurve(floorPlan, edge3);
                        doc.Create.NewDetailCurve(floorPlan, edge4);

                        logger.LogInformation($"Drew debug rectangle for wall {wall.Id.Value} on {floorPlan.Name}");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Failed to draw debug rectangle: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Error in DrawDebugRectangle: {ex.Message}");
            }
        }

        #endregion

        #region Trimmed Curve Drawing

        /// <summary>
        /// Draws a debug trimmed curve with red color and endpoint markers.
        /// </summary>
        /// <param name="doc">The Revit document.</param>
        /// <param name="wall">The wall associated with the curve.</param>
        /// <param name="trimmedCurve">The trimmed curve to draw.</param>
        /// <param name="logger">Logger service.</param>
        public static void DrawDebugTrimmedCurve(
            Document doc,
            Wall wall,
            Curve trimmedCurve,
            ILoggingService logger)
        {
            try
            {
                var levelId = wall.LookupParameter("Base Constraint")?.AsElementId();

                if (levelId == null || levelId == ElementId.InvalidElementId)
                    return;

                // Find floor plan for this level
                var floorPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == levelId);

                if (floorPlan == null)
                {
                    logger.LogWarning($"No floor plan found for wall {wall.Id.Value}, skipping debug trimmed curve");
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Draw Debug Trimmed Curve"))
                {
                    trans.Start();
                    try
                    {
                        // Create the trimmed curve as a detail line
                        var detailLine = doc.Create.NewDetailCurve(floorPlan, trimmedCurve);

                        // Create red color override
                        var overrideSettings = new OverrideGraphicSettings();
                        var red = new Autodesk.Revit.DB.Color(255, 0, 0); // RGB: Red
                        overrideSettings.SetProjectionLineColor(red);
                        overrideSettings.SetProjectionLineWeight(5); // Make it thicker for visibility

                        // Apply override to the detail line
                        floorPlan.SetElementOverrides(detailLine.Id, overrideSettings);

                        // Add markers at the endpoints
                        var start = trimmedCurve.GetEndPoint(0);
                        var end = trimmedCurve.GetEndPoint(1);

                        // Draw small circles at endpoints (also in red)
                        double markerRadius = 0.5; // feet
                        var startMarkers = DrawCircleMarkerWithOverride(doc, floorPlan, start, markerRadius);
                        var endMarkers = DrawCircleMarkerWithOverride(doc, floorPlan, end, markerRadius);

                        // Apply red override to markers
                        foreach (var marker in startMarkers.Concat(endMarkers))
                        {
                            floorPlan.SetElementOverrides(marker.Id, overrideSettings);
                        }

                        logger.LogInformation($"Drew debug trimmed curve (RED) for wall {wall.Id.Value} on {floorPlan.Name}");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Failed to draw debug trimmed curve: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Error in DrawDebugTrimmedCurve: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws a circle marker at a point using arc segments with color override.
        /// </summary>
        private static List<DetailCurve> DrawCircleMarkerWithOverride(Document doc, ViewPlan view, XYZ center, double radius)
        {
            // Draw a circle using 4 arc segments
            var center2D = new XYZ(center.X, center.Y, 0);
            var detailCurves = new List<DetailCurve>();

            try
            {
                // Create 4 quarter arcs to form a complete circle
                // Top-right quarter (0° to 90°)
                var p1 = center2D + new XYZ(radius, 0, 0);
                var p2 = center2D + new XYZ(0, radius, 0);
                var arc1 = Arc.Create(p1, p2, center2D + new XYZ(radius, radius, 0).Normalize() * radius);
                detailCurves.Add(doc.Create.NewDetailCurve(view, arc1));

                // Top-left quarter (90° to 180°)
                var p3 = center2D + new XYZ(-radius, 0, 0);
                var arc2 = Arc.Create(p2, p3, center2D + new XYZ(-radius, radius, 0).Normalize() * radius);
                detailCurves.Add(doc.Create.NewDetailCurve(view, arc2));

                // Bottom-left quarter (180° to 270°)
                var p4 = center2D + new XYZ(0, -radius, 0);
                var arc3 = Arc.Create(p3, p4, center2D + new XYZ(-radius, -radius, 0).Normalize() * radius);
                detailCurves.Add(doc.Create.NewDetailCurve(view, arc3));

                // Bottom-right quarter (270° to 360°)
                var arc4 = Arc.Create(p4, p1, center2D + new XYZ(radius, -radius, 0).Normalize() * radius);
                detailCurves.Add(doc.Create.NewDetailCurve(view, arc4));
            }
            catch (Exception)
            {
                // If arc creation fails, fall back to cross marker
                var line1 = Line.CreateBound(
                    center2D + new XYZ(radius, 0, 0),
                    center2D - new XYZ(radius, 0, 0));
                var line2 = Line.CreateBound(
                    center2D + new XYZ(0, radius, 0),
                    center2D - new XYZ(0, radius, 0));

                detailCurves.Add(doc.Create.NewDetailCurve(view, line1));
                detailCurves.Add(doc.Create.NewDetailCurve(view, line2));
            }

            return detailCurves;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Rotates a XYZ point 90 degrees around Z axis (counterclockwise).
        /// </summary>
        /// <param name="point">The point to rotate.</param>
        /// <returns>The rotated point.</returns>
        public static XYZ RotatePoint(XYZ point)
        {
            return new XYZ(-point.Y, point.X, point.Z);
        }

        /// <summary>
        /// Rotates a XYZ point by a specified angle around Z axis.
        /// </summary>
        /// <param name="point">The point to rotate.</param>
        /// <param name="angleRadians">The rotation angle in radians.</param>
        /// <returns>The rotated point.</returns>
        public static XYZ RotatePointByAngle(XYZ point, double angleRadians)
        {
            var cos = Math.Cos(angleRadians);
            var sin = Math.Sin(angleRadians);
            return new XYZ(
                point.X * cos - point.Y * sin,
                point.X * sin + point.Y * cos,
                point.Z);
        }

        #endregion
    }
}
