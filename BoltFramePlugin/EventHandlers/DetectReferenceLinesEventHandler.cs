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
        private readonly ILoggingService _logger;
        private Action? _onComplete;
        private bool _shouldDrawArrows = false;

        public DetectReferenceLinesEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _perimeterWalls = new ObservableCollection<WallInfo>();
            _referenceLines = new ObservableCollection<ReferenceLineInfo>();
            _rayLengthLimit = 500.0;
        }

        public void SetParameters(UIDocument uidoc, ObservableCollection<WallInfo> perimeterWalls, ObservableCollection<ReferenceLineInfo> referenceLines, double rayLengthLimit, bool shouldDrawArrows = false, Action? onComplete = null)
        {
            _uidoc = uidoc;
            _perimeterWalls = perimeterWalls;
            _referenceLines = referenceLines;
            _onComplete = onComplete;
            _rayLengthLimit = rayLengthLimit;
            _shouldDrawArrows = shouldDrawArrows;
            _logger.LogInformation($"Parameters set - Walls: {perimeterWalls?.Count ?? 0}, Ray length: {rayLengthLimit}m, ShouldDrawArrows: {shouldDrawArrows}");
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

                _logger.LogInformation($"Found {roadCenterlines.Count} Road CLs, {propertyLineCurves.Count} Property Line Segments, {allPerimeterWalls.Count} Perimeter Walls");

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
            var createdImaginaryLines = new HashSet<string>();
            var stats = new ReferenceLineStats();
            var raysToDrawn = new List<(Wall wall, XYZ start, XYZ end, ElementId levelId)>();

            foreach (var wallInfo in _perimeterWalls)
            {
                var wall = wallInfo.Wall;
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null)
                    continue;

                var (rayStart, rayEnd) = CalculateRayPoints(wall, locationCurve);

                _logger.LogInformation($"Wall {wall.Id.Value}: Orientation ({wall.Orientation.X:F2}, {wall.Orientation.Y:F2}, {wall.Orientation.Z:F2})");
                _logger.LogInformation($"Wall {wall.Id.Value}: Ray from ({rayStart.X:F2}, {rayStart.Y:F2}) to ({rayEnd.X:F2}, {rayEnd.Y:F2})");

                var hitResult = CastRayAndFindIntersection(wall, rayStart, rayEnd, allPerimeterWalls, propertyLineCurves, roadCenterlines);

                if (hitResult != null)
                {
                    _logger.LogInformation($"Wall {wall.Id.Value}: Ray hit '{hitResult.Name}' (Type: {hitResult.LineType}, ElementId: {hitResult.ElementId.Value})");
                }

                var levelId = GetWallBaseLevelId(wall);
                var actualEndPoint = ProcessHitResult(wallInfo, hitResult, rayEnd, createdImaginaryLines, stats);

                raysToDrawn.Add((wall, rayStart, actualEndPoint, levelId));
            }

            return (raysToDrawn, stats);
        }

        private (XYZ rayStart, XYZ rayEnd) CalculateRayPoints(Wall wall, LocationCurve locationCurve)
        {
            var wallCurve = locationCurve.Curve;
            var midpoint = wallCurve.Evaluate(0.5, true);

            // Get the wall's orientation vector (points from interior to exterior)
            var wallOrientation = wall.Orientation;

            // Project to 2D (XY plane) and normalize
            var normal = new XYZ(wallOrientation.X, wallOrientation.Y, 0).Normalize();

            var rayEndPoint = midpoint + (normal * (_rayLengthLimit * 3.28084)); // meters to feet

            return (midpoint, rayEndPoint);
        }

        private XYZ ProcessHitResult(
            WallInfo wallInfo,
            ReferenceLineInfo? hitResult,
            XYZ rayEnd,
            HashSet<string> createdImaginaryLines,
            ReferenceLineStats stats)
        {
            if (hitResult == null || hitResult.IntersectionPoint == null)
            {
                _logger.LogWarning($"Wall {wallInfo.Wall.Id.Value}: No reference line hit");
                return rayEnd;
            }

            var actualEndPoint = hitResult.IntersectionPoint;
            var key = $"{hitResult.LineType}_{hitResult.ElementId.Value}";
            ReferenceLineInfo canonicalReferenceLine;

            if (!createdImaginaryLines.Contains(key))
            {
                // First time seeing this reference line - add it
                _referenceLines.Add(hitResult);
                createdImaginaryLines.Add(key);
                canonicalReferenceLine = hitResult;

                stats.IncrementCount(hitResult.LineType);
            }
            else
            {
                // Reference line already exists - find the canonical instance
                canonicalReferenceLine = _referenceLines.First(rl =>
                    rl.LineType == hitResult.LineType &&
                    rl.ElementId.Value == hitResult.ElementId.Value);
            }

            // Assign the canonical instance to the wall
            wallInfo.LimitingDistance = hitResult.Distance;
            wallInfo.ReferenceLine = canonicalReferenceLine;
            _logger.LogInformation($"Wall {wallInfo.Wall.Id.Value}: Limiting distance = {hitResult.Distance:F2} ft to {hitResult.LineTypeFormatted}");

            return actualEndPoint;
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

                // Collect all intersection hits
                CheckWallIntersections(sourceWall, rayStart, ray2D, allWalls, sourceLevelId, allHits);
                CheckPropertyLineIntersections(rayStart, ray2D, propertyLineCurves, allHits);
                CheckRoadCenterlineIntersections(rayStart, ray2D, roadCenterlines, allHits);

                // Select and return the best hit based on priority
                return SelectBestHit(allHits);
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

            // If target wall doesn't have FireCompartment value, skip it (treat as same compartment)
            if (string.IsNullOrEmpty(targetFireCompartment))
                return false;

            // If target has value but source doesn't, they're in different compartments
            if (string.IsNullOrEmpty(sourceFireCompartment))
                return true;

            // Both have values - compare them
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
            foreach (var wall in allWalls)
            {
                // Skip if same wall or same fire compartment
                if (wall.Id == sourceWall.Id || !ShouldCheckFireCompartment(sourceWall, wall))
                    continue;

                // Only check walls on the same level
                var wallLevelId = GetWallBaseLevelId(wall);
                if (sourceLevelId != wallLevelId)
                    continue;

                // Get wall curve and check intersection
                var wallCurve2D = GetWallCurve2D(wall);
                if (wallCurve2D == null)
                    continue;

                var intersection = FindIntersection(ray2D, wallCurve2D);
                if (intersection == null)
                    continue;

                var distance = Calculate2DDistance(rayStart, intersection);
                if (distance <= 0.01) // Minimum distance check
                    continue;

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
            }
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

        public string GetName()
        {
            return "Detect Reference Lines External Event";
        }
    }
}
