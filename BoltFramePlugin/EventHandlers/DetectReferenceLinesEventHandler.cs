using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Services;
using BoltFramePlugin.Models.LimitingDistance;
using System.Collections.ObjectModel;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.EventHandlers
{
    internal class DetectReferenceLinesEventHandler : IExternalEventHandler
    {
        private UIDocument _uidoc;
        private ObservableCollection<WallInfo> _perimeterWalls;
        private ObservableCollection<ReferenceLineInfo> _referenceLines;
        private double _rayLengthLimit;
        private double _raycastInterval; // in meters
        private readonly ILoggingService _logger;
        private Action? _onComplete;
        private bool _shouldDrawArrows = false;
        private bool _shouldDrawDebugRectangles = false;

        public DetectReferenceLinesEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _perimeterWalls = new ObservableCollection<WallInfo>();
            _referenceLines = new ObservableCollection<ReferenceLineInfo>();
            _rayLengthLimit = 500.0;
            _raycastInterval = 0.5; // Default: 50cm = 0.5 meters
        }

        public void SetParameters(UIDocument uidoc, ObservableCollection<WallInfo> perimeterWalls, ObservableCollection<ReferenceLineInfo> referenceLines, double rayLengthLimit, bool shouldDrawArrows = false, Action? onComplete = null, double raycastIntervalMeters = 0.5, bool shouldDrawDebugRectangles = false)
        {
            _uidoc = uidoc;
            _perimeterWalls = perimeterWalls;
            _referenceLines = referenceLines;
            _onComplete = onComplete;
            _rayLengthLimit = rayLengthLimit;
            _shouldDrawArrows = shouldDrawArrows;
            _raycastInterval = raycastIntervalMeters;
            _shouldDrawDebugRectangles = shouldDrawDebugRectangles;
            _logger.LogInformation($"Parameters set - Walls: {perimeterWalls?.Count ?? 0}, Ray length: {rayLengthLimit}m, Raycast interval: {raycastIntervalMeters}m, ShouldDrawArrows: {shouldDrawArrows}, ShouldDrawDebugRectangles: {shouldDrawDebugRectangles}");
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _logger.LogInformation("Detecting reference lines by ray casting from exterior walls...");

                if (_uidoc == null)
                {
                    TaskDialog.Show("Error", "Internal error: UIDocument is null");
                    return;
                }

                var doc = _uidoc.Document;
                _referenceLines.Clear();

                // Collect reference elements
                var roadCenterlines = CollectRoadCenterlines(doc);
                var propertyLineCurves = CollectPropertyLineCurves(doc);
                var allPerimeterWalls = _perimeterWalls.Select(w => w.Wall).ToList();

                _logger.LogInformation($"=== COLLECTION SUMMARY ===");
                _logger.LogInformation($"Road CLs: {roadCenterlines.Count}");
                _logger.LogInformation($"Property Line Segments: {propertyLineCurves.Count}");
                _logger.LogInformation($"Perimeter Walls (for intersection): {allPerimeterWalls.Count}");
                _logger.LogInformation($"Walls to process (from _perimeterWalls): {_perimeterWalls.Count}");
                _logger.LogInformation($"=========================");

                // Process walls and collect ray intersection data
                var (raysToDrawn, stats) = ProcessPerimeterWalls(allPerimeterWalls, propertyLineCurves, roadCenterlines);

                // Draw visualization arrows if enabled
                if (_shouldDrawArrows)
                {
                    DrawReferenceLineArrows(doc, raysToDrawn);
                }
                else
                {
                    _logger.LogInformation("Skipping distance measurement arrow drawing (shouldDrawArrows=false)");
                }

                // Log summary
                LogDetectionSummary(stats);

                // Invoke callback if provided
                _onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error detecting reference lines", ex);
                TaskDialog.Show("Error", $"Error detecting reference lines: {ex.Message}");
            }
        }

        private List<FamilyInstance> CollectRoadCenterlines(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol?.Family?.Name == "Road_LD")
                .ToList();
        }

        private List<(Element element, Curve curve)> CollectPropertyLineCurves(Document doc)
        {
            var propertyLines = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_SiteProperty)
                .WhereElementIsNotElementType()
                .ToList();

            _logger.LogInformation($"Property lines found (OST_SiteProperty): {propertyLines.Count}");

            var propertyLineCurves = new List<(Element element, Curve curve)>();

            foreach (var pl in propertyLines)
            {
                ExtractCurvesFromPropertyLine(pl, propertyLineCurves);
            }

            _logger.LogInformation($"Total property line curves extracted: {propertyLineCurves.Count}");
            return propertyLineCurves;
        }

        private void ExtractCurvesFromPropertyLine(Element propertyLine, List<(Element element, Curve curve)> propertyLineCurves)
        {
            var options = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            var geom = propertyLine.get_Geometry(options);
            if (geom == null)
                return;

            int curveCount = 0;

            foreach (var geomObj in geom)
            {
                if (geomObj is Curve curve)
                {
                    propertyLineCurves.Add((propertyLine, curve));
                    curveCount++;
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    var instGeom = geomInst.GetInstanceGeometry();
                    foreach (var instObj in instGeom)
                    {
                        if (instObj is Curve instCurve)
                        {
                            propertyLineCurves.Add((propertyLine, instCurve));
                            curveCount++;
                        }
                    }
                }
            }

            if (curveCount > 0)
            {
                _logger.LogInformation($"Property Line {propertyLine.Id.Value}: Extracted {curveCount} curves");
            }
        }

        private (List<(Wall wall, XYZ start, XYZ end, ElementId levelId)> raysToDrawn, ReferenceLineStats stats) ProcessPerimeterWalls(
            List<Wall> allPerimeterWalls,
            List<(Element element, Curve curve)> propertyLineCurves,
            List<FamilyInstance> roadCenterlines)
        {
            var stats = new ReferenceLineStats();
            var raysToDrawn = new List<(Wall wall, XYZ start, XYZ end, ElementId levelId)>();

            foreach (var wallInfo in _perimeterWalls)
            {
                var wall = wallInfo.Wall;
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null)
                    continue;

                var levelId = GetWallBaseLevelId(wall);
                var wallCurve = locationCurve.Curve;

                _logger.LogInformation($"Wall {wall.Id.Value}: Orientation ({wall.Orientation.X:F2}, {wall.Orientation.Y:F2}, {wall.Orientation.Z:F2})");

                // Distribute raycast points along the wall at the specified interval
                var raycastPoints = DistributeRaycastPoints(wallCurve);
                _logger.LogInformation($"Wall {wall.Id.Value}: Distributed {raycastPoints.Count} raycast points at {_raycastInterval}m intervals");

                // Checked reference line ids to avoid repeat processing
                var referenceLineIdsChecked = new HashSet<ElementId>();

                // Process each raycast point
                for (int i = 0; i < raycastPoints.Count; i++)
                {
                    var point = raycastPoints[i];
                    var (rayStart, rayEnd) = CalculateRayPointsFromPosition(wall, point);

                    string rayLabel = i == 0 ? "Start" :
                                     i == raycastPoints.Count - 1 ? "End" :
                                     $"Point {i}";

                    ProcessSingleRaycast(
                        wallInfo, wall, rayStart, rayEnd,
                        allPerimeterWalls, propertyLineCurves, roadCenterlines,
                        referenceLineIdsChecked, stats, raysToDrawn, levelId,
                        rayLabel);
                }
            }

            return (raysToDrawn, stats);
        }

        private List<XYZ> DistributeRaycastPoints(Curve wallCurve)
        {
            var points = new List<XYZ>();

            // Get wall length in feet (Revit's internal unit)
            double wallLengthFeet = wallCurve.Length;

            // Convert interval from meters to feet
            double intervalFeet = _raycastInterval * 3.28084;

            // Always include the start point (endpoint 0)
            points.Add(wallCurve.GetEndPoint(0));

            // If the wall is shorter than or equal to the interval, only use the 2 endpoints
            if (wallLengthFeet <= intervalFeet)
            {
                points.Add(wallCurve.GetEndPoint(1));
                return points;
            }

            // Wall is longer than interval - add intermediate points
            // Calculate number of intervals
            int numIntervals = (int)Math.Floor(wallLengthFeet / intervalFeet);

            // Add intermediate points
            for (int i = 1; i < numIntervals; i++)
            {
                double normalizedParameter = i * intervalFeet / wallLengthFeet;
                XYZ point = wallCurve.Evaluate(normalizedParameter, true);
                points.Add(point);
            }

            // Always include the end point (endpoint 1)
            // Only add if it's not too close to the last added point
            XYZ endPoint = wallCurve.GetEndPoint(1);
            if (points[^1].DistanceTo(endPoint) > 0.01) // 0.01 feet threshold
            {
                points.Add(endPoint);
            }

            return points;
        }

        private void ProcessSingleRaycast(
            WallInfo wallInfo,
            Wall wall,
            XYZ rayStart,
            XYZ rayEnd,
            List<Wall> allPerimeterWalls,
            List<(Element element, Curve curve)> propertyLineCurves,
            List<FamilyInstance> roadCenterlines,
            HashSet<ElementId> referenceLineIdsChecked,
            ReferenceLineStats stats,
            List<(Wall wall, XYZ start, XYZ end, ElementId levelId)> raysToDrawn,
            ElementId levelId,
            string rayLabel)
        {

            var hitResult = CastRayAndFindIntersection(wall, rayStart, rayEnd, allPerimeterWalls, propertyLineCurves, roadCenterlines);

            raysToDrawn.Add((wall, rayStart, rayEnd, levelId));

            if (hitResult == null)
            {
                return;
            }

            // Check if this is a new reference line (for stats tracking)
            bool isNewReferenceLine = !referenceLineIdsChecked.Contains(hitResult.ElementId);
            if (isNewReferenceLine)
            {
                referenceLineIdsChecked.Add(hitResult.ElementId);
                // Process hit result and update wall info (pass isNewReferenceLine for stats)
                ProcessHitResult(wallInfo, wall, hitResult, rayEnd, stats, isNewReferenceLine);
            }
        }

        private (XYZ rayStart, XYZ rayEnd) CalculateRayPointsFromPosition(Wall wall, XYZ position)
        {
            // Get the wall's orientation vector (points from interior to exterior)
            var wallOrientation = wall.Orientation;

            // Project to 2D (XY plane) and normalize
            var normal = new XYZ(wallOrientation.X, wallOrientation.Y, 0).Normalize();

            var rayEndPoint = position + (normal * (_rayLengthLimit * 3.28084)); // meters to feet

            return (position, rayEndPoint);
        }

        private void ProcessHitResult(
            WallInfo wallInfo,
            Wall wall,
            ReferenceLineInfo hitResult,
            XYZ rayEnd,
            ReferenceLineStats stats,
            bool isNewReferenceLine)
        {
            if (hitResult.IntersectionPoint == null)
            {
                _logger.LogWarning($"Wall {wallInfo.Wall.Id.Value}: No reference line hit");
                return;
            }

            // Add to reference lines collection only if it's new
            if (isNewReferenceLine)
            {
                _referenceLines.Add(hitResult);
                stats.IncrementCount(hitResult.LineType);
            }

            // Calculate limiting distance using rectangle-based trimming approach
            var locationCurve = wall.Location as LocationCurve;
            if (locationCurve != null && hitResult.Curve != null)
            {
                var limitingDistance = CalculateLimitingDistanceWithRectangle(wallInfo, wall, locationCurve.Curve, hitResult.Curve);

                if (limitingDistance.HasValue && (limitingDistance.Value < wallInfo.LimitingDistance || !wallInfo.LimitingDistance.HasValue))
                {
                    wallInfo.LimitingDistance = limitingDistance.Value;
                    wallInfo.ReferenceLine = hitResult;
                    return;
                }
            }

            // Fallback to simple distance calculation if rectangle approach fails
            wallInfo.LimitingDistance = hitResult.Distance;
            wallInfo.ReferenceLine = hitResult;

            return;
        }

        private double? CalculateLimitingDistanceWithRectangle(WallInfo wallInfo, Wall wall, Curve wallCurve, Curve referenceLine)
        {
            try
            {
                // Create rectangle based on wall segment
                var rectangleBounds = CreateWallRectangle(wall, wallCurve);
                if (rectangleBounds == null)
                    return null;

                // Draw debug rectangle if enabled
                DrawDebugRectangle(wall, rectangleBounds.Value);

                // Trim the reference line to the rectangle bounds
                var trimmedCurve = TrimCurveToRectangle(referenceLine, rectangleBounds.Value);
                if (trimmedCurve == null || trimmedCurve.Length < 0.001)
                {
                    _logger.LogInformation($"Reference line does not intersect wall rectangle");
                    return null;
                }

                // Draw debug trimmed curve if enabled
                DrawDebugTrimmedCurve(wall, trimmedCurve);

                // Find the closest point on the trimmed curve to the wall
                var (closestDistance, closestPointOnRefLine, closestPointOnWall) = FindClosestDistanceToWall(wallCurve, trimmedCurve);

                // Draw debug line showing the limiting distance if enabled
                if ((closestDistance < wallInfo.LimitingDistance || !wallInfo.LimitingDistance.HasValue) && closestPointOnRefLine != null && closestPointOnWall != null)
                {
                    DrawDebugDistanceLine(wall, closestPointOnWall, closestPointOnRefLine);
                }

                return closestDistance;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error calculating limiting distance with rectangle: {ex.Message}");
                return null;
            }
        }

        private (XYZ corner1, XYZ corner2, XYZ corner3, XYZ corner4)? CreateWallRectangle(Wall wall, Curve wallCurve)
        {
            try
            {
                // Get wall endpoints
                var wallStart = wallCurve.GetEndPoint(0);
                var wallEnd = wallCurve.GetEndPoint(1);

                // Get wall orientation (normal pointing exterior)
                var wallOrientation = wall.Orientation;
                var normal2D = new XYZ(wallOrientation.X, wallOrientation.Y, 0).Normalize();

                // Extend the rectangle in the direction of the wall orientation (exterior side)
                // Use the ray length limit as the rectangle depth
                var rectangleDepth = _rayLengthLimit * 3.28084; // meters to feet

                // Calculate the 4 corners of the rectangle
                // Corner 1 and 2 are on the wall line
                var corner1 = new XYZ(wallStart.X, wallStart.Y, 0);
                var corner2 = new XYZ(wallEnd.X, wallEnd.Y, 0);

                // Corner 3 and 4 are extended in the normal direction
                var corner3 = new XYZ(wallEnd.X, wallEnd.Y, 0) + (normal2D * rectangleDepth);
                var corner4 = new XYZ(wallStart.X, wallStart.Y, 0) + (normal2D * rectangleDepth);

                return (corner1, corner2, corner3, corner4);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error creating wall rectangle: {ex.Message}");
                return null;
            }
        }

        private Curve? TrimCurveToRectangle(Curve curve, (XYZ corner1, XYZ corner2, XYZ corner3, XYZ corner4) rectangle)
        {
            try
            {
                // Convert curve to 2D
                var curve2D = ConvertCurveTo2D(curve);
                if (curve2D == null)
                    return null;

                // Create the 4 edges of the rectangle as lines
                var edge1 = Line.CreateBound(rectangle.corner1, rectangle.corner2); // Wall edge
                var edge2 = Line.CreateBound(rectangle.corner2, rectangle.corner3); // Right edge
                var edge3 = Line.CreateBound(rectangle.corner3, rectangle.corner4); // Far edge
                var edge4 = Line.CreateBound(rectangle.corner4, rectangle.corner1); // Left edge

                var intersectionPoints = new List<XYZ>();

                // Find all intersection points with rectangle edges
                foreach (var edge in new[] { edge1, edge2, edge3, edge4 })
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    var result = curve2D.Intersect(edge, out IntersectionResultArray results);
#pragma warning restore CS0618 // Type or member is obsolete
                    if (result == SetComparisonResult.Overlap && results != null)
                    {
                        for (int i = 0; i < results.Size; i++)
                        {
                            intersectionPoints.Add(results.get_Item(i).XYZPoint);
                        }
                    }
                }

                // Get curve endpoints
                var curveStart = curve2D.GetEndPoint(0);
                var curveEnd = curve2D.GetEndPoint(1);

                // Check which endpoints are inside the rectangle
                bool startInside = IsPointInRectangle(curveStart, rectangle);
                bool endInside = IsPointInRectangle(curveEnd, rectangle);

                // Add endpoints to trimming points if they're inside
                var trimPoints = new List<XYZ>(intersectionPoints);
                if (startInside)
                    trimPoints.Add(curveStart);
                if (endInside)
                    trimPoints.Add(curveEnd);

                // Need at least 2 points to create a trimmed curve
                if (trimPoints.Count < 2)
                {
                    // No intersection and no endpoints inside - curve doesn't intersect rectangle
                    return null;
                }

                // Sort points along the curve parameter
                var sortedPoints = trimPoints
                    .Select(pt => new { Point = pt, Param = curve2D.Project(pt).Parameter })
                    .OrderBy(x => x.Param)
                    .Select(x => x.Point)
                    .ToList();

                // Create trimmed curve from first to last point
                var trimmedStart = sortedPoints.First();
                var trimmedEnd = sortedPoints.Last();

                // Check if start and end are too close
                if (trimmedStart.DistanceTo(trimmedEnd) < 0.001)
                {
                    return null;
                }

                return Line.CreateBound(trimmedStart, trimmedEnd);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error trimming curve to rectangle: {ex.Message}");
                return null;
            }
        }

        private bool IsPointInRectangle(XYZ point, (XYZ corner1, XYZ corner2, XYZ corner3, XYZ corner4) rectangle)
        {
            // Use cross product to check if point is inside the rectangle
            var p = new XYZ(point.X, point.Y, 0);

            // Vector from corner1 to point
            var v1 = p - rectangle.corner1;
            // Vector along wall edge
            var v2 = rectangle.corner2 - rectangle.corner1;
            // Vector perpendicular to wall
            var v3 = rectangle.corner4 - rectangle.corner1;

            // Project point onto the two edge vectors
            var dot1 = v1.DotProduct(v2) / v2.GetLength();
            var dot2 = v1.DotProduct(v3) / v3.GetLength();

            // Check if projections are within bounds
            return dot1 >= 0 && dot1 <= v2.GetLength() &&
                   dot2 >= 0 && dot2 <= v3.GetLength();
        }

        private (double distance, XYZ? pointOnRefLine, XYZ? pointOnWall) FindClosestDistanceToWall(Curve wallCurve, Curve trimmedReferenceLine)
        {
            // Check both endpoints of the trimmed reference line
            var refLineStart = trimmedReferenceLine.GetEndPoint(0);
            var refLineEnd = trimmedReferenceLine.GetEndPoint(1);

            double minDistance = double.MaxValue;
            XYZ? closestPointOnRefLine = null;
            XYZ? closestPointOnWall = null;

            // Check distance from reference line start point
            var projectionStart = wallCurve.Project(refLineStart);
            if (projectionStart != null)
            {
                double distance = projectionStart.Distance;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPointOnRefLine = refLineStart;
                    closestPointOnWall = projectionStart.XYZPoint;
                }
            }

            // Check distance from reference line end point
            var projectionEnd = wallCurve.Project(refLineEnd);
            if (projectionEnd != null)
            {
                double distance = projectionEnd.Distance;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPointOnRefLine = refLineEnd;
                    closestPointOnWall = projectionEnd.XYZPoint;
                }
            }

            return (minDistance, closestPointOnRefLine, closestPointOnWall);
        }

        private void DrawDebugDistanceLine(Wall wall, XYZ pointOnWall, XYZ pointOnRefLine)
        {
            try
            {
                var doc = wall.Document;
                var levelId = wall.LookupParameter("Base Constraint")?.AsElementId();

                if (levelId == null || levelId == ElementId.InvalidElementId)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} has no base constraint level, skipping distance line drawing");
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
                    _logger.LogWarning($"No floor plan found for level {level.Name}, skipping distance line drawing for wall {wall.Id.Value}");
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
                        var distanceLine = Line.CreateBound(pointOnWall2D, pointOnRefLine2D);
                        var detailCurve = doc.Create.NewDetailCurve(floorPlan, distanceLine);

                        // Apply green color override
                        var overrideSettings = new OverrideGraphicSettings();
                        var green = new Autodesk.Revit.DB.Color(0, 255, 0);
                        overrideSettings.SetProjectionLineColor(green);
                        overrideSettings.SetProjectionLineWeight(5);
                        floorPlan.SetElementOverrides(detailCurve.Id, overrideSettings);

                        _logger.LogInformation($"Drew debug distance line for wall {wall.Id.Value} on {floorPlan.Name}");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to draw debug distance line: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error in DrawDebugDistanceLine: {ex.Message}");
            }
        }

        private void DrawReferenceLineArrows(Document doc, List<(Wall wall, XYZ start, XYZ end, ElementId levelId)> raysToDrawn)
        {
            _logger.LogInformation("Drawing distance measurement arrows (shouldDrawArrows=true)");

            using (Transaction trans = new Transaction(doc, "Draw Reference Line Rays"))
            {
                trans.Start();
                try
                {
                    var raysByView = GroupRaysByFloorPlan(doc, raysToDrawn);
                    int rayCount = DrawRaysOnFloorPlans(doc, raysByView);

                    _logger.LogInformation($"Created {rayCount} total detail lines for rays across {raysByView.Count} floor plans");
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating detail lines: {ex.Message}", ex);
                    trans.RollBack();
                }
            }
        }

        private Dictionary<ElementId, List<(XYZ start, XYZ end)>> GroupRaysByFloorPlan(
            Document doc,
            List<(Wall wall, XYZ start, XYZ end, ElementId levelId)> raysToDrawn)
        {
            var raysByView = new Dictionary<ElementId, List<(XYZ start, XYZ end)>>();

            foreach (var (wall, start, end, levelId) in raysToDrawn)
            {
                if (levelId == ElementId.InvalidElementId)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} has no base constraint level, skipping ray drawing");
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
                    _logger.LogWarning($"No floor plan found for level {level.Name}, skipping wall {wall.Id.Value}");
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

        private int DrawRaysOnFloorPlans(Document doc, Dictionary<ElementId, List<(XYZ start, XYZ end)>> raysByView)
        {
            int totalRayCount = 0;

            foreach (var kvp in raysByView)
            {
                var viewId = kvp.Key;
                var rays = kvp.Value;
                var view = doc.GetElement(viewId) as ViewPlan;

                if (view == null)
                    continue;

                int viewRayCount = DrawRaysInView(doc, view, rays);
                totalRayCount += viewRayCount;

                _logger.LogInformation($"Created {rays.Count} rays on floor plan {view.Name}");
            }

            return totalRayCount;
        }

        private int DrawRaysInView(Document doc, ViewPlan view, List<(XYZ start, XYZ end)> rays)
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
                    _logger.LogWarning($"Skipping ray that is too short ({distance:F6} ft) - below minimum curve length");
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
                    _logger.LogWarning($"Failed to create ray from ({start2D.X:F2}, {start2D.Y:F2}) to ({end2D.X:F2}, {end2D.Y:F2}): {ex.Message}");
                }
            }

            return rayCount;
        }

        private void DrawArrowHead(Document doc, ViewPlan view, XYZ start2D, XYZ end2D)
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

        private void LogDetectionSummary(ReferenceLineStats stats)
        {
            _logger.LogInformation($"Total reference lines detected: {_referenceLines.Count}");
            _logger.LogInformation($"Breakdown - Imaginary Lines: {stats.ImaginaryLineCount}, Property Lines: {stats.PropertyLineCount}, Road CLs: {stats.RoadCLCount}");
        }

        private class ReferenceLineStats
        {
            public int ImaginaryLineCount { get; private set; }
            public int PropertyLineCount { get; private set; }
            public int RoadCLCount { get; private set; }

            public void IncrementCount(ReferenceLineType lineType)
            {
                switch (lineType)
                {
                    case ReferenceLineType.ImaginaryLine:
                        ImaginaryLineCount++;
                        break;
                    case ReferenceLineType.PropertyLine_NonStreetEdge:
                    case ReferenceLineType.PropertyLine_StreetEdge:
                        PropertyLineCount++;
                        break;
                    case ReferenceLineType.RoadCenterline:
                        RoadCLCount++;
                        break;
                }
            }
        }

        /// <summary>
        /// Casts a ray from rayStart to rayEnd and finds the closest intersection with walls, property lines, or road centerlines.
        /// </summary>
        /// <param name="sourceWall">The wall from which the ray is cast.</param>
        /// <param name="rayStart">The starting point of the ray.</param>
        /// <param name="rayEnd">The ending point of the ray.</param>
        /// <param name="allWalls">All walls in the document.</param>
        /// <param name="propertyLineCurves">All property line curves in the document.</param>
        /// <param name="roadCenterlines">All road centerlines in the document.</param>
        /// <returns>The closest intersection information, if found.</returns>
        private ReferenceLineInfo? CastRayAndFindIntersection(
            Wall sourceWall,
            XYZ rayStart,
            XYZ rayEnd,
            List<Wall> allWalls,
            List<(Element element, Curve curve)> propertyLineCurves,
            List<FamilyInstance> roadCenterlines)
        {
            try
            {
                var ray2D = Line.CreateBound(
                    new XYZ(rayStart.X, rayStart.Y, 0),
                    new XYZ(rayEnd.X, rayEnd.Y, 0));

                var allHits = new List<ReferenceLineInfo>();

                var sourceLevelId = GetWallBaseLevelId(sourceWall);

                _logger.LogInformation($"  Checking intersections for wall {sourceWall.Id.Value}: {allWalls.Count} walls, {propertyLineCurves.Count} property lines, {roadCenterlines.Count} road CLs");

                // Collect all intersection hits
                CheckWallIntersections(sourceWall, rayStart, ray2D, allWalls, sourceLevelId, allHits);
                _logger.LogInformation($"    After wall check: {allHits.Count} hits");

                CheckPropertyLineIntersections(rayStart, ray2D, propertyLineCurves, allHits);
                _logger.LogInformation($"    After property line check: {allHits.Count} hits");

                CheckRoadCenterlineIntersections(rayStart, ray2D, roadCenterlines, allHits);
                _logger.LogInformation($"    After road CL check: {allHits.Count} hits");

                // Select and return the best hit based on priority
                var bestHit = SelectBestHit(allHits);
                if (bestHit != null)
                {
                    _logger.LogInformation($"  Selected best hit: {bestHit.Name} at distance {bestHit.Distance:F2} ft");
                }
                else
                {
                    _logger.LogInformation($"  No hits found for wall {sourceWall.Id.Value}");
                }

                return bestHit;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ray casting for wall {sourceWall.Id.Value}", ex);
                return null;
            }
        }

        private ElementId GetWallBaseLevelId(Wall wall)
        {
            var baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
            return baseLevelParam?.AsElementId() ?? ElementId.InvalidElementId;
        }

        private bool ShouldCheckFireCompartment(Wall sourceWall, Wall targetWall)
        {
            string? targetFireCompartment = targetWall.LookupParameter("FireCompartment")?.AsString();
            string? sourceFireCompartment = sourceWall.LookupParameter("FireCompartment")?.AsString();

            // If either wall doesn't have FireCompartment value, skip this wall (treat as same compartment)
            if (string.IsNullOrEmpty(targetFireCompartment) || string.IsNullOrEmpty(sourceFireCompartment))
                return false;

            // Both have values - compare them
            // Return true if DIFFERENT compartments (should check)
            // Return false if SAME compartments (skip - don't check)
            return targetFireCompartment != sourceFireCompartment;
        }

        private void CheckWallIntersections(
            Wall sourceWall,
            XYZ rayStart,
            Line ray2D,
            List<Wall> allWalls,
            ElementId sourceLevelId,
            List<ReferenceLineInfo> allHits)
        {
            int sameWallSkipped = 0;
            int sameFireCompartmentSkipped = 0;
            int differentLevelSkipped = 0;
            int noCurveSkipped = 0;
            int noIntersectionSkipped = 0;
            int tooCloseSkipped = 0;
            int hitsAdded = 0;

            foreach (var wall in allWalls)
            {
                // Skip if same wall
                if (wall.Id == sourceWall.Id)
                {
                    sameWallSkipped++;
                    continue;
                }

                // Skip if same fire compartment
                if (!ShouldCheckFireCompartment(sourceWall, wall))
                {
                    sameFireCompartmentSkipped++;
                    continue;
                }

                // Only check walls on the same level
                var wallLevelId = GetWallBaseLevelId(wall);
                if (sourceLevelId != wallLevelId)
                {
                    differentLevelSkipped++;
                    continue;
                }

                // Get wall curve and check intersection
                var wallCurve2D = GetWallCurve2D(wall);
                if (wallCurve2D == null)
                {
                    noCurveSkipped++;
                    continue;
                }

                var intersection = FindIntersection(ray2D, wallCurve2D);
                if (intersection == null)
                {
                    noIntersectionSkipped++;
                    continue;
                }

                var distance = Calculate2DDistance(rayStart, intersection);
                if (distance <= 0.01) // Minimum distance check
                {
                    tooCloseSkipped++;
                    continue;
                }

                var imaginaryLine = Line.CreateBound(rayStart, intersection);
                allHits.Add(new ReferenceLineInfo
                {
                    Element = wall,
                    ElementId = wall.Id,
                    LineType = ReferenceLineType.ImaginaryLine,
                    Curve = imaginaryLine,
                    Name = $"Imaginary Line (Wall {sourceWall.Id.Value} ↔ Wall {wall.Id.Value})",
                    IsStreetEdge = false,
                    IntersectionPoint = intersection,
                    Distance = distance
                });
                hitsAdded++;
            }

            _logger.LogInformation($"      Wall intersection stats: Same={sameWallSkipped}, SameFireComp={sameFireCompartmentSkipped}, DiffLevel={differentLevelSkipped}, NoCurve={noCurveSkipped}, NoIntersect={noIntersectionSkipped}, TooClose={tooCloseSkipped}, HitsAdded={hitsAdded}");
        }

        private void CheckPropertyLineIntersections(
            XYZ rayStart,
            Line ray2D,
            List<(Element element, Curve curve)> propertyLineCurves,
            List<ReferenceLineInfo> allHits)
        {
            foreach (var (propLine, curve) in propertyLineCurves)
            {
                if (curve == null)
                    continue;

                var curve2D = ConvertCurveTo2D(curve);
                if (curve2D == null)
                    continue;

                var intersection = FindIntersection(ray2D, curve2D);
                if (intersection == null)
                    continue;

                var distance = Calculate2DDistance(rayStart, intersection);
                if (distance <= 0.01) // Minimum distance check
                    continue;

                _logger.LogInformation($"Property line {propLine.Id.Value} hit at distance {distance} ft");

                allHits.Add(new ReferenceLineInfo
                {
                    Element = propLine,
                    ElementId = propLine.Id,
                    LineType = ReferenceLineType.PropertyLine_StreetEdge, // TODO: Determine street edge
                    Curve = curve,
                    Name = $"Property Line - {propLine.Id.Value}",
                    IsStreetEdge = true,
                    IntersectionPoint = intersection,
                    Distance = distance
                });
            }
        }

        private void CheckRoadCenterlineIntersections(
            XYZ rayStart,
            Line ray2D,
            List<FamilyInstance> roadCenterlines,
            List<ReferenceLineInfo> allHits)
        {
            foreach (var roadCL in roadCenterlines)
            {
                var locationCurve = roadCL.Location as LocationCurve;
                if (locationCurve == null)
                    continue;

                var roadCurve = locationCurve.Curve;
                var roadCurve2D = Line.CreateBound(
                    new XYZ(roadCurve.GetEndPoint(0).X, roadCurve.GetEndPoint(0).Y, 0),
                    new XYZ(roadCurve.GetEndPoint(1).X, roadCurve.GetEndPoint(1).Y, 0));

                var intersection = FindIntersection(ray2D, roadCurve2D);
                if (intersection == null)
                    continue;

                var distance = Calculate2DDistance(rayStart, intersection);
                if (distance <= 0.01) // Minimum distance check
                    continue;

                _logger.LogInformation($"Road centerline {roadCL.Id.Value} hit at distance {distance} ft");

                allHits.Add(new ReferenceLineInfo
                {
                    Element = roadCL,
                    ElementId = roadCL.Id,
                    LineType = ReferenceLineType.RoadCenterline,
                    Curve = roadCurve,
                    Name = $"Road Centerline - {roadCL.Id.Value}",
                    IsStreetEdge = false,
                    IntersectionPoint = intersection,
                    Distance = distance
                });
            }
        }

        private Line? GetWallCurve2D(Wall wall)
        {
            var wallLocationCurve = wall.Location as LocationCurve;
            if (wallLocationCurve == null)
                return null;

            var wallCurve = wallLocationCurve.Curve;
            return Line.CreateBound(
                new XYZ(wallCurve.GetEndPoint(0).X, wallCurve.GetEndPoint(0).Y, 0),
                new XYZ(wallCurve.GetEndPoint(1).X, wallCurve.GetEndPoint(1).Y, 0));
        }

        private Curve? ConvertCurveTo2D(Curve curve)
        {
            if (curve is Line line)
            {
                return Line.CreateBound(
                    new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, 0),
                    new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, 0));
            }
            else if (curve is Arc arc)
            {
                var start2D = new XYZ(arc.GetEndPoint(0).X, arc.GetEndPoint(0).Y, 0);
                var end2D = new XYZ(arc.GetEndPoint(1).X, arc.GetEndPoint(1).Y, 0);
                var mid = arc.Evaluate(0.5, true);
                var mid2D = new XYZ(mid.X, mid.Y, 0);

                try
                {
                    return Arc.Create(start2D, end2D, mid2D);
                }
                catch
                {
                    return Line.CreateBound(start2D, end2D);
                }
            }
            else
            {
                return Line.CreateBound(
                    new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0),
                    new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0));
            }
        }

        private XYZ? FindIntersection(Line ray2D, Curve targetCurve)
        {
            var result = ray2D.Intersect(targetCurve, out IntersectionResultArray results);
            if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
            {
                return results.get_Item(0).XYZPoint;
            }
            return null;
        }

        private double Calculate2DDistance(XYZ rayStart, XYZ intersectionPoint)
        {
            var rayStart2D = new XYZ(rayStart.X, rayStart.Y, 0);
            var intersection2D = new XYZ(intersectionPoint.X, intersectionPoint.Y, 0);
            return rayStart2D.DistanceTo(intersection2D);
        }

        private ReferenceLineInfo? SelectBestHit(List<ReferenceLineInfo> allHits)
        {
            if (allHits.Count == 0)
                return null;

            // Sort by distance
            var sortedHits = allHits.OrderBy(h => h.Distance).ToList();

            // Priority 1: Non-property-line hits (walls, road centerlines)
            var nonPropertyLineHit = sortedHits.FirstOrDefault(h =>
                h.LineType != ReferenceLineType.PropertyLine_StreetEdge &&
                h.LineType != ReferenceLineType.PropertyLine_NonStreetEdge);

            // Priority 2: Property lines (only if no other hits)
            return nonPropertyLineHit ?? sortedHits.First();
        }

        private void DrawDebugRectangle(Wall wall, (XYZ corner1, XYZ corner2, XYZ corner3, XYZ corner4) rectangle)
        {
            try
            {
                var doc = _uidoc.Document;
                var levelId = GetWallBaseLevelId(wall);

                if (levelId == ElementId.InvalidElementId)
                    return;

                // Find floor plan for this level
                var floorPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == levelId);

                if (floorPlan == null)
                {
                    _logger.LogWarning($"No floor plan found for wall {wall.Id.Value}, skipping debug rectangle");
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Draw Debug Rectangle"))
                {
                    trans.Start();
                    try
                    {
                        // Draw the 4 edges of the rectangle in a different color/style
                        var edge1 = Line.CreateBound(rectangle.corner1, rectangle.corner2);
                        var edge2 = Line.CreateBound(rectangle.corner2, rectangle.corner3);
                        var edge3 = Line.CreateBound(rectangle.corner3, rectangle.corner4);
                        var edge4 = Line.CreateBound(rectangle.corner4, rectangle.corner1);

                        doc.Create.NewDetailCurve(floorPlan, edge1);
                        doc.Create.NewDetailCurve(floorPlan, edge2);
                        doc.Create.NewDetailCurve(floorPlan, edge3);
                        doc.Create.NewDetailCurve(floorPlan, edge4);

                        _logger.LogInformation($"Drew debug rectangle for wall {wall.Id.Value} on {floorPlan.Name}");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to draw debug rectangle: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error in DrawDebugRectangle: {ex.Message}");
            }
        }

        private void DrawDebugTrimmedCurve(Wall wall, Curve trimmedCurve)
        {
            try
            {
                var doc = _uidoc.Document;
                var levelId = GetWallBaseLevelId(wall);

                if (levelId == ElementId.InvalidElementId)
                    return;

                // Find floor plan for this level
                var floorPlan = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == levelId);

                if (floorPlan == null)
                {
                    _logger.LogWarning($"No floor plan found for wall {wall.Id.Value}, skipping debug trimmed curve");
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

                        _logger.LogInformation($"Drew debug trimmed curve (RED) for wall {wall.Id.Value} on {floorPlan.Name}");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to draw debug trimmed curve: {ex.Message}");
                        trans.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error in DrawDebugTrimmedCurve: {ex.Message}");
            }
        }

        private void DrawCircleMarker(Document doc, ViewPlan view, XYZ center, double radius)
        {
            // Draw a small cross marker instead of a circle (simpler)
            var center2D = new XYZ(center.X, center.Y, 0);

            var line1 = Line.CreateBound(
                center2D + new XYZ(radius, 0, 0),
                center2D - new XYZ(radius, 0, 0));
            var line2 = Line.CreateBound(
                center2D + new XYZ(0, radius, 0),
                center2D - new XYZ(0, radius, 0));

            doc.Create.NewDetailCurve(view, line1);
            doc.Create.NewDetailCurve(view, line2);
        }

        private List<DetailCurve> DrawCircleMarkerWithOverride(Document doc, ViewPlan view, XYZ center, double radius)
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

        public string GetName()
        {
            return "Detect Reference Lines External Event";
        }
    }
}
