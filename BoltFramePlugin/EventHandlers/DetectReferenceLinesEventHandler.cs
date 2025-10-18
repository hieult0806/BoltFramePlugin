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

        public DetectReferenceLinesEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _perimeterWalls = new ObservableCollection<WallInfo>();
            _referenceLines = new ObservableCollection<ReferenceLineInfo>();
            _rayLengthLimit = 500.0;
        }

        public void SetParameters(UIDocument uidoc, ObservableCollection<WallInfo> perimeterWalls, ObservableCollection<ReferenceLineInfo> referenceLines, double rayLengthLimit, Action? onComplete = null)
        {
            _uidoc = uidoc;
            _perimeterWalls = perimeterWalls;
            _referenceLines = referenceLines;
            _onComplete = onComplete;
            _rayLengthLimit = rayLengthLimit;
            _logger.LogInformation($"Parameters set - Walls: {perimeterWalls?.Count ?? 0}, Ray length: {rayLengthLimit}m");
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

                // Collect all potential reference elements
                var roadCenterlines = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol?.Family?.Name == "Road_LD")
                    .ToList();

                var propertyLines = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_SiteProperty)
                    .WhereElementIsNotElementType()
                    .ToList();

                _logger.LogInformation($"Property lines found (OST_SiteProperty): {propertyLines.Count}");

                var allPerimeterWalls = _perimeterWalls.Select(w => w.Wall).ToList();

                // Extract curves from property lines
                var propertyLineCurves = new List<(Element element, Curve curve)>();

                foreach (var pl in propertyLines)
                {
                    var options = new Options
                    {
                        ComputeReferences = false,
                        DetailLevel = ViewDetailLevel.Fine
                    };

                    var geom = pl.get_Geometry(options);
                    if (geom != null)
                    {
                        int curveCount = 0;
                        foreach (var geomObj in geom)
                        {
                            if (geomObj is Curve curve)
                            {
                                propertyLineCurves.Add((pl, curve));
                                curveCount++;
                            }
                            else if (geomObj is GeometryInstance geomInst)
                            {
                                var instGeom = geomInst.GetInstanceGeometry();
                                foreach (var instObj in instGeom)
                                {
                                    if (instObj is Curve instCurve)
                                    {
                                        propertyLineCurves.Add((pl, instCurve));
                                        curveCount++;
                                    }
                                }
                            }
                        }

                        if (curveCount > 0)
                        {
                            _logger.LogInformation($"Property Line {pl.Id.Value}: Extracted {curveCount} curves");
                        }
                    }
                }

                _logger.LogInformation($"Total property line curves extracted: {propertyLineCurves.Count}");
                _logger.LogInformation($"Found {roadCenterlines.Count} Road CLs, {propertyLineCurves.Count} Property Line Segments, {allPerimeterWalls.Count} Perimeter Walls");

                // Process each perimeter wall and cast rays
                var createdImaginaryLines = new HashSet<string>();
                int imaginaryLineCount = 0;
                int propertyLineCount = 0;
                int roadCLCount = 0;

                // Store rays with their intersection points and base levels
                var raysToDrawn = new List<(Wall wall, XYZ start, XYZ end, ElementId levelId)>();

                foreach (var wallInfo in _perimeterWalls)
                {
                    var wall = wallInfo.Wall;
                    var locationCurve = wall.Location as LocationCurve;
                    if (locationCurve == null) continue;

                    var wallCurve = locationCurve.Curve;
                    var midpoint = wallCurve.Evaluate(0.5, true);

                    // Get the wall's orientation vector (points from interior to exterior)
                    // Wall.Orientation gives us the normal vector pointing from interior face to exterior face
                    var wallOrientation = wall.Orientation;

                    // Project to 2D (XY plane) and normalize
                    var normal = new XYZ(wallOrientation.X, wallOrientation.Y, 0).Normalize();

                    var rayEndPoint = midpoint + (normal * (_rayLengthLimit * 3.28084)); // meters to feet

                    _logger.LogInformation($"Wall {wall.Id.Value}: Orientation ({wallOrientation.X:F2}, {wallOrientation.Y:F2}, {wallOrientation.Z:F2})");

                    _logger.LogInformation($"Wall {wall.Id.Value}: Ray from ({midpoint.X:F2}, {midpoint.Y:F2}) to ({rayEndPoint.X:F2}, {rayEndPoint.Y:F2})");

                    var hitResult = CastRayAndFindIntersection(wall, midpoint, rayEndPoint, allPerimeterWalls, propertyLineCurves, roadCenterlines);

                    // Get wall's base constraint level
                    var baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    var levelId = baseLevelParam?.AsElementId() ?? ElementId.InvalidElementId;

                    // If hit, use the intersection point; otherwise use full ray length
                    XYZ actualEndPoint = rayEndPoint;
                    if (hitResult != null && hitResult.IntersectionPoint != null)
                    {
                        actualEndPoint = hitResult.IntersectionPoint;

                        // Update wall's limiting distance immediately
                        wallInfo.LimitingDistance = hitResult.Distance;
                        wallInfo.ReferenceLine = hitResult;
                        _logger.LogInformation($"Wall {wall.Id.Value}: Limiting distance = {hitResult.Distance:F2} ft to {hitResult.LineTypeFormatted}");
                    }
                    else
                    {
                        _logger.LogWarning($"Wall {wall.Id.Value}: No reference line hit");
                    }

                    raysToDrawn.Add((wall, midpoint, actualEndPoint, levelId));

                    if (hitResult != null)
                    {
                        var key = $"{hitResult.LineType}_{hitResult.ElementId.Value}";
                        if (!createdImaginaryLines.Contains(key))
                        {
                            _referenceLines.Add(hitResult);
                            createdImaginaryLines.Add(key);

                            switch (hitResult.LineType)
                            {
                                case ReferenceLineType.ImaginaryLine:
                                    imaginaryLineCount++;
                                    break;
                                case ReferenceLineType.PropertyLine_NonStreetEdge:
                                case ReferenceLineType.PropertyLine_StreetEdge:
                                    propertyLineCount++;
                                    break;
                                case ReferenceLineType.RoadCenterline:
                                    roadCLCount++;
                                    break;
                            }
                        }
                    }
                }

                // Draw rays as Detail Lines on their respective floor plans
                using (Transaction trans = new Transaction(doc, "Draw Reference Line Rays"))
                {
                    trans.Start();
                    try
                    {
                        int rayCount = 0;
                        var raysByView = new Dictionary<ElementId, List<(XYZ start, XYZ end)>>();

                        // Group rays by their floor plan views
                        foreach (var (wall, start, end, levelId) in raysToDrawn)
                        {
                            if (levelId == ElementId.InvalidElementId)
                            {
                                _logger.LogWarning($"Wall {wall.Id.Value} has no base constraint level, skipping ray drawing");
                                continue;
                            }

                            // Find floor plan for this level
                            var level = doc.GetElement(levelId) as Level;
                            if (level == null) continue;

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

                        // Draw rays on each floor plan
                        foreach (var kvp in raysByView)
                        {
                            var viewId = kvp.Key;
                            var rays = kvp.Value;
                            var view = doc.GetElement(viewId) as ViewPlan;

                            if (view == null) continue;

                            foreach (var (start, end) in rays)
                            {
                                var start2D = new XYZ(start.X, start.Y, 0);
                                var end2D = new XYZ(end.X, end.Y, 0);

                                // Check if the curve is long enough for Revit's tolerance (minimum ~1/32 inch)
                                var distance = start2D.DistanceTo(end2D);
                                var minLength = 0.003; // ~1/32 inch in feet
                                if (distance < minLength)
                                {
                                    _logger.LogWarning($"Skipping ray that is too short ({distance:F6} ft) - below minimum curve length");
                                    continue;
                                }

                                try
                                {
                                    // Draw the main ray line
                                    var line = Line.CreateBound(start2D, end2D);
                                    var detailLine = doc.Create.NewDetailCurve(view, line);
                                    rayCount++;

                                    // Draw arrow head (small triangle at the end)
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
                                catch (Exception ex)
                                {
                                    _logger.LogWarning($"Failed to create ray from ({start2D.X:F2}, {start2D.Y:F2}) to ({end2D.X:F2}, {end2D.Y:F2}): {ex.Message}");
                                }
                            }

                            _logger.LogInformation($"Created {rays.Count} rays on floor plan {view.Name}");
                        }

                        _logger.LogInformation($"Created {rayCount} total detail lines for rays across {raysByView.Count} floor plans");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error creating detail lines: {ex.Message}", ex);
                        trans.RollBack();
                    }
                }

                _logger.LogInformation($"Total reference lines detected: {_referenceLines.Count}");
                _logger.LogInformation($"Breakdown - Imaginary Lines: {imaginaryLineCount}, Property Lines: {propertyLineCount}, Road CLs: {roadCLCount}");

                TaskDialog.Show("Reference Lines Detected",
                    $"Detected {_referenceLines.Count} reference lines:\n" +
                    $"- Imaginary Lines (between walls): {imaginaryLineCount}\n" +
                    $"- Property Line Segments: {propertyLineCount}\n" +
                    $"- Road Centerlines: {roadCLCount}\n\n" +
                    $"Limiting distances have been updated in the table.");

                // Invoke callback if provided
                _onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error detecting reference lines", ex);
                TaskDialog.Show("Error", $"Error detecting reference lines: {ex.Message}");
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
                var rayLine = Line.CreateBound(rayStart, rayEnd);
                var ray2D = Line.CreateBound(new XYZ(rayStart.X, rayStart.Y, 0), new XYZ(rayEnd.X, rayEnd.Y, 0));

                // Collect all hits (walls, property lines, road centerlines)
                var allHits = new List<ReferenceLineInfo>();

                // Get source wall's base constraint level for filtering
                var sourceBaseLevelParam = sourceWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                var sourceLevelId = sourceBaseLevelParam?.AsElementId() ?? ElementId.InvalidElementId;

                // 1. Check intersection with other walls
                foreach (var wall in allWalls)
                {
                    if (wall.Id == sourceWall.Id) continue;

                    // Only check walls on the same level
                    var wallBaseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    var wallLevelId = wallBaseLevelParam?.AsElementId() ?? ElementId.InvalidElementId;

                    if (sourceLevelId != wallLevelId)
                    {
                        continue; // Skip walls on different levels
                    }

                    var wallLocationCurve = wall.Location as LocationCurve;
                    if (wallLocationCurve == null) continue;

                    var wallCurve = wallLocationCurve.Curve;
                    var wallCurve2D = Line.CreateBound(
                        new XYZ(wallCurve.GetEndPoint(0).X, wallCurve.GetEndPoint(0).Y, 0),
                        new XYZ(wallCurve.GetEndPoint(1).X, wallCurve.GetEndPoint(1).Y, 0));

                    var result = ray2D.Intersect(wallCurve2D, out IntersectionResultArray results);
                    if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
                    {
                        var intersection = results.get_Item(0);
                        var distance = rayStart.DistanceTo(intersection.XYZPoint);

                        if (distance > 0.01) // Minimum distance check
                        {
                            var imaginaryLine = Line.CreateBound(rayStart, intersection.XYZPoint);

                            allHits.Add(new ReferenceLineInfo
                            {
                                Element = wall,
                                ElementId = wall.Id,
                                LineType = ReferenceLineType.ImaginaryLine,
                                Curve = imaginaryLine,
                                Name = $"Imaginary Line (Wall {sourceWall.Id.Value} ↔ Wall {wall.Id.Value})",
                                IsStreetEdge = false,
                                IntersectionPoint = intersection.XYZPoint,
                                Distance = distance
                            });
                        }
                    }
                }

                // 2. Check intersection with property lines
                foreach (var (propLine, curve) in propertyLineCurves)
                {
                    if (curve == null) continue;

                    Curve curve2D = null;
                    if (curve is Line line)
                    {
                        curve2D = Line.CreateBound(
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
                            curve2D = Arc.Create(start2D, end2D, mid2D);
                        }
                        catch
                        {
                            curve2D = Line.CreateBound(start2D, end2D);
                        }
                    }
                    else
                    {
                        curve2D = Line.CreateBound(
                            new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0),
                            new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0));
                    }

                    if (curve2D == null) continue;

                    var result = ray2D.Intersect(curve2D, out IntersectionResultArray results);
                    if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
                    {
                        var intersection = results.get_Item(0);
                        var distance = rayStart.DistanceTo(intersection.XYZPoint);

                        _logger.LogInformation($"Property line {propLine.Id.Value} hit at distance {distance} ft");

                        if (distance > 0.01) // Minimum distance check
                        {
                            allHits.Add(new ReferenceLineInfo
                            {
                                Element = propLine,
                                ElementId = propLine.Id,
                                LineType = ReferenceLineType.PropertyLine_StreetEdge, // TODO: Determine street edge
                                Curve = curve,
                                Name = $"Property Line - {propLine.Id.Value}",
                                IsStreetEdge = true,
                                IntersectionPoint = intersection.XYZPoint,
                                Distance = distance
                            });
                        }
                    }
                }

                // 3. Check intersection with road centerlines
                foreach (var roadCL in roadCenterlines)
                {
                    // Road centerlines are linear family instances
                    var locationCurve = roadCL.Location as LocationCurve;
                    if (locationCurve == null) continue;

                    var roadCurve = locationCurve.Curve;
                    var roadCurve2D = Line.CreateBound(
                        new XYZ(roadCurve.GetEndPoint(0).X, roadCurve.GetEndPoint(0).Y, 0),
                        new XYZ(roadCurve.GetEndPoint(1).X, roadCurve.GetEndPoint(1).Y, 0));

                    var result = ray2D.Intersect(roadCurve2D, out IntersectionResultArray results);
                    if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
                    {
                        var intersection = results.get_Item(0);
                        var distance = rayStart.DistanceTo(intersection.XYZPoint);

                        _logger.LogInformation($"Road centerline {roadCL.Id.Value} hit at distance {distance} ft");

                        if (distance > 0.01) // Minimum distance check
                        {
                            allHits.Add(new ReferenceLineInfo
                            {
                                Element = roadCL,
                                ElementId = roadCL.Id,
                                LineType = ReferenceLineType.RoadCenterline,
                                Curve = roadCurve,
                                Name = $"Road Centerline - {roadCL.Id.Value}",
                                IsStreetEdge = false,
                                IntersectionPoint = intersection.XYZPoint,
                                Distance = distance
                            });
                        }
                    }
                }

                // Select the best hit:
                // Priority 1: Non-property-line hits (walls, road centerlines)
                // Priority 2: Property lines (only if no other hits)
                if (allHits.Count == 0)
                    return null;

                // Sort by distance
                var sortedHits = allHits.OrderBy(h => h.Distance).ToList();

                // Find first non-property-line hit
                var nonPropertyLineHit = sortedHits.FirstOrDefault(h =>
                    h.LineType != ReferenceLineType.PropertyLine_StreetEdge &&
                    h.LineType != ReferenceLineType.PropertyLine_NonStreetEdge);

                // Return non-property-line if exists, otherwise return closest property line
                return nonPropertyLineHit ?? sortedHits.First();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ray casting for wall {sourceWall.Id.Value}", ex);
                return null;
            }
        }

        public string GetName()
        {
            return "Detect Reference Lines External Event";
        }
    }
}
