using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Services;
using BoltFramePlugin.Models.LimitingDistance;
using System.Collections.Generic;
using System.Linq;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.EventHandlers
{
    /// <summary>
    /// Creates elevation views with colored filled regions showing wall projections grouped by orientation.
    /// Each wall is colored according to its limiting distance group.
    /// </summary>
    internal class CreateWallProjectionsEventHandler : IExternalEventHandler
    {
        #region Constants

        private const double ORIENTATION_TOLERANCE_DEGREES = 5.0;
        private const double WALL_SIMILARITY_THRESHOLD = 0.5;
        private const double VIEW_DISTANCE_FROM_WALLS = 500.0;
        private const double VIEW_FAR_CLIP_OFFSET = 1000.0;
        private const double BOUNDING_BOX_PADDING = 20.0;
        private const double VIEW_DEPTH = 100.0;
        private const double POINT_EQUALITY_TOLERANCE = 1e-6;

        #endregion

        #region Fields

        private UIDocument _uidoc;
        private List<WallInfo> _perimeterWalls;
        private List<ReferenceLineInfo> _referenceLines;
        private DistanceGroupSummary? _distanceGroup;
        private readonly ILoggingService _logger;

        #endregion

        #region Constructor

        public CreateWallProjectionsEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _perimeterWalls = new List<WallInfo>();
            _referenceLines = new List<ReferenceLineInfo>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the parameters for wall projection creation
        /// </summary>
        public void SetParameters(UIDocument uidoc, List<WallInfo> perimeterWalls, List<ReferenceLineInfo> referenceLines, DistanceGroupSummary? distanceGroup = null)
        {
            _uidoc = uidoc;
            _perimeterWalls = perimeterWalls;
            _referenceLines = referenceLines;
            _distanceGroup = distanceGroup;
            _logger.LogInformation($"Parameters set - Walls: {perimeterWalls?.Count ?? 0}, Reference Lines: {referenceLines?.Count ?? 0}, Distance Group: {distanceGroup?.Orientation ?? "All"} {distanceGroup?.DistanceRange ?? ""}");
        }

        /// <summary>
        /// Executes the wall projection creation process
        /// </summary>
        public void Execute(UIApplication app)
        {
            try
            {
                _logger.LogInformation($"Execute started - Creating wall projections for {(_distanceGroup != null ? $"distance group: {_distanceGroup.Orientation} - {_distanceGroup.DistanceRange}" : "all walls")}");

                if (!ValidateInput())
                {
                    _logger.LogWarning("Execute validation failed");
                    return;
                }

                var doc = _uidoc.Document;

                _logger.LogInformation("Starting limiting distance calculation");
                CalculateLimitingDistances();

                _logger.LogInformation("Starting wall filtering");
                var wallsToProject = FilterWallsByOrientation();

                if (wallsToProject.Count == 0)
                {
                    _logger.LogWarning("No walls to project after filtering");
                    return;
                }

                _logger.LogInformation("Starting grouping by orientation");
                var orientationGroups = GroupWallsByOrientation(wallsToProject);
                _logger.LogInformation($"Created {orientationGroups.Count} orientation groups");

                if (orientationGroups.Count == 0)
                {
                    TaskDialog.Show("Info", "No orientation groups created. Ensure reference lines have been detected.");
                    return;
                }

                _logger.LogInformation("Starting view and region creation");
                CreateViewsAndRegions(doc, orientationGroups);

                _logger.LogInformation("Execute completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateWallProjectionsEventHandler.Execute: {ex.Message}\nStack trace: {ex.StackTrace}", ex);
                TaskDialog.Show("Error", $"Error creating wall projections: {ex.Message}\n\nSee log for details.");
            }
        }

        public string GetName()
        {
            return "Create Wall Projections External Event";
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates input parameters before execution
        /// </summary>
        private bool ValidateInput()
        {
            if (_uidoc == null || _perimeterWalls == null || _perimeterWalls.Count == 0)
            {
                TaskDialog.Show("Error", "No perimeter walls found. Please run 'Detect Reference Lines' first.");
                return false;
            }
            return true;
        }

        #endregion

        #region Limiting Distance Calculation

        /// <summary>
        /// Calculates limiting distances for all perimeter walls
        /// </summary>
        private void CalculateLimitingDistances()
        {
            _logger.LogInformation("Calculating limiting distances for walls...");

            foreach (var wallInfo in _perimeterWalls)
            {
                CalculateWallLimitingDistance(wallInfo);
            }
        }

        /// <summary>
        /// Calculates the limiting distance for a single wall
        /// </summary>
        private void CalculateWallLimitingDistance(WallInfo wallInfo)
        {
            var wall = wallInfo.Wall;

            // Set wall orientation
            var wallOrientation = wall.Orientation;
            wallInfo.Orientation = new XYZ(wallOrientation.X, wallOrientation.Y, 0).Normalize();

            // Find reference line for this wall
            var referenceLine = _referenceLines.FirstOrDefault(rl =>
                rl.LineType != ReferenceLineType.ImaginaryLine);

            if (referenceLine != null && referenceLine.IntersectionPoint != null)
            {
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve != null)
                {
                    var wallCurve = locationCurve.Curve;
                    var midpoint = wallCurve.Evaluate(0.5, true);
                    var distance = midpoint.DistanceTo(referenceLine.IntersectionPoint);

                    wallInfo.LimitingDistance = distance;
                    wallInfo.ReferenceLine = referenceLine;

                    _logger.LogInformation($"Wall {wall.Id.Value}: Limiting distance = {distance:F2} ft");
                }
            }
        }

        #endregion

        #region Wall Filtering and Grouping

        /// <summary>
        /// Filters walls by orientation based on the selected distance group
        /// </summary>
        private List<WallInfo> FilterWallsByOrientation()
        {
            if (_distanceGroup != null)
            {
                var filtered = _perimeterWalls
                    .Where(w => w.LimitingDistance.HasValue &&
                               w.Orientation != null &&
                               GetOrientationDescription(w.Orientation) == _distanceGroup.Orientation)
                    .ToList();

                _logger.LogInformation($"Filtered {filtered.Count} walls with orientation {_distanceGroup.Orientation}");

                if (filtered.Count == 0)
                {
                    TaskDialog.Show("Info", $"No walls found with orientation {_distanceGroup.Orientation}");
                }

                return filtered;
            }

            return _perimeterWalls;
        }

        /// <summary>
        /// Groups walls by their orientation
        /// </summary>
        private List<WallGroup> GroupWallsByOrientation(List<WallInfo> walls)
        {
            var groups = new List<WallGroup>();

            foreach (var wallInfo in walls.Where(w => w.LimitingDistance.HasValue && w.Orientation != null))
            {
                var matchingGroup = groups.FirstOrDefault(g =>
                    AreOrientationsSimilar(g.Orientation, wallInfo.Orientation, ORIENTATION_TOLERANCE_DEGREES));

                if (matchingGroup != null)
                {
                    matchingGroup.Walls.Add(wallInfo);
                }
                else
                {
                    var newGroup = new WallGroup
                    {
                        LimitingDistance = wallInfo.LimitingDistance.Value,
                        Orientation = wallInfo.Orientation,
                        ReferenceLine = wallInfo.ReferenceLine
                    };
                    newGroup.Walls.Add(wallInfo);
                    newGroup.GroupName = $"Orientation - {newGroup.OrientationDescription}";
                    groups.Add(newGroup);
                }
            }

            return groups;
        }

        /// <summary>
        /// Determines if two orientations are similar within tolerance
        /// </summary>
        private bool AreOrientationsSimilar(XYZ orientation1, XYZ orientation2, double toleranceDegrees)
        {
            var angle1 = Math.Atan2(orientation1.Y, orientation1.X) * 180 / Math.PI;
            var angle2 = Math.Atan2(orientation2.Y, orientation2.X) * 180 / Math.PI;

            var angleDiff = Math.Abs(angle1 - angle2);
            if (angleDiff > 180)
                angleDiff = 360 - angleDiff;

            return angleDiff < toleranceDegrees;
        }

        /// <summary>
        /// Gets a cardinal direction description for an orientation vector
        /// </summary>
        private static string GetOrientationDescription(XYZ? orientation)
        {
            if (orientation == null)
                return "Unknown";

            var normal = new XYZ(orientation.X, orientation.Y, 0).Normalize();
            var absX = Math.Abs(normal.X);
            var absY = Math.Abs(normal.Y);

            if (absY > absX)
                return normal.Y > 0 ? "North" : "South";
            else
                return normal.X > 0 ? "East" : "West";
        }

        #endregion

        #region View and Region Creation

        /// <summary>
        /// Creates views and filled regions for all orientation groups
        /// </summary>
        private void CreateViewsAndRegions(Document doc, List<WallGroup> orientationGroups)
        {
            using (Transaction trans = new Transaction(doc, "Create Wall Projection Views"))
            {
                trans.Start();

                try
                {
                    int viewsCreated = 0;

                    foreach (var group in orientationGroups)
                    {
                        var elevationView = CreateElevationViewForGroup(doc, group);

                        if (elevationView != null)
                        {
                            CreateRegionsForWalls(doc, elevationView, group);
                            viewsCreated++;
                        }
                    }

                    trans.Commit();

                    TaskDialog.Show("Success",
                        $"Created {viewsCreated} elevation views with wall projections.\n\n" +
                        $"Orientation groups: {orientationGroups.Count}\n" +
                        $"Total walls: {_perimeterWalls.Count}");

                    _logger.LogInformation($"Successfully created {viewsCreated} elevation views");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating projections: {ex.Message}", ex);
                    trans.RollBack();
                    TaskDialog.Show("Error", $"Error creating projections: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates an elevation view for a specific wall orientation group
        /// </summary>
        private ViewSection? CreateElevationViewForGroup(Document doc, WallGroup group)
        {
            try
            {
                var viewFamilyType = GetSectionViewFamilyType(doc);
                if (viewFamilyType == null)
                    return null;

                var boundingBox = CalculateBoundingBoxForWalls(group.Walls);
                var centerPoint = (boundingBox.Min + boundingBox.Max) / 2.0;

                var (viewDirection, rightDirection, upDirection, sectionOrigin) =
                    CalculateViewCoordinateSystem(group.Orientation, centerPoint);

                var sectionBox = CreateSectionBoundingBox(boundingBox, centerPoint, viewDirection, rightDirection, upDirection, sectionOrigin);

                LogViewCreationInfo(group.Orientation, viewDirection, rightDirection, sectionOrigin);

                var sectionView = ViewSection.CreateSection(doc, viewFamilyType.Id, sectionBox);

                if (sectionView != null)
                {
                    ConfigureView(doc, sectionView, group);
                    _logger.LogInformation($"Created section view: {sectionView.Name}");
                }

                return sectionView;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating section view for group {group.GroupName}", ex);
                return null;
            }
        }

        /// <summary>
        /// Gets the section view family type
        /// </summary>
        private ViewFamilyType? GetSectionViewFamilyType(Document doc)
        {
            var viewFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Section);

            if (viewFamilyType == null)
            {
                _logger.LogWarning("No section view family type found");
            }

            return viewFamilyType;
        }

        /// <summary>
        /// Calculates the view coordinate system (direction vectors and origin)
        /// </summary>
        private static (XYZ viewDirection, XYZ rightDirection, XYZ upDirection, XYZ sectionOrigin)
            CalculateViewCoordinateSystem(XYZ groupOrientation, XYZ centerPoint)
        {
            var wallNormal = new XYZ(groupOrientation.X, groupOrientation.Y, 0).Normalize();

            // View looks opposite to wall normal to see walls facing this orientation
            var viewDirection = -wallNormal;

            // Right direction is perpendicular to wall normal
            var rightDirection = new XYZ(-wallNormal.Y, wallNormal.X, 0).Normalize();

            // Up direction is always vertical
            var upDirection = XYZ.BasisZ;

            // Position view origin away from walls
            var sectionOrigin = centerPoint + (wallNormal * VIEW_DISTANCE_FROM_WALLS);

            return (viewDirection, rightDirection, upDirection, sectionOrigin);
        }

        /// <summary>
        /// Creates the bounding box for the section view
        /// </summary>
        private static BoundingBoxXYZ CreateSectionBoundingBox(
            BoundingBoxXYZ wallsBBox, XYZ centerPoint,
            XYZ viewDirection, XYZ rightDirection, XYZ upDirection, XYZ sectionOrigin)
        {
            var minX = wallsBBox.Min.X - centerPoint.X;
            var maxX = wallsBBox.Max.X - centerPoint.X;
            var minY = wallsBBox.Min.Y - centerPoint.Y;
            var maxY = wallsBBox.Max.Y - centerPoint.Y;
            var minZ = wallsBBox.Min.Z;
            var maxZ = wallsBBox.Max.Z;

            var width = Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2)) + BOUNDING_BOX_PADDING;
            var height = maxZ - minZ + BOUNDING_BOX_PADDING;

            var transform = Transform.Identity;
            transform.Origin = sectionOrigin;
            transform.BasisX = rightDirection;
            transform.BasisY = upDirection;
            transform.BasisZ = viewDirection;

            return new BoundingBoxXYZ
            {
                Transform = transform,
                Min = new XYZ(-width / 2, -VIEW_DEPTH / 2, minZ - 10),
                Max = new XYZ(width / 2, VIEW_DEPTH / 2, maxZ + 10)
            };
        }

        /// <summary>
        /// Configures view properties (name, far clip, crop box)
        /// </summary>
        private void ConfigureView(Document doc, ViewSection sectionView, WallGroup group)
        {
            var baseName = $"Wall Projection - {group.GroupName}";
            var uniqueName = GetUniqueViewName(doc, baseName);
            sectionView.Name = uniqueName;

            var farClipParam = sectionView.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
            if (farClipParam != null && !farClipParam.IsReadOnly)
            {
                farClipParam.Set(VIEW_FAR_CLIP_OFFSET);
                _logger.LogInformation($"Set far clip offset to {VIEW_FAR_CLIP_OFFSET}");
            }

            sectionView.CropBoxActive = false;
            sectionView.CropBoxVisible = false;
        }

        /// <summary>
        /// Generates a unique view name by appending incrementing numbers
        /// </summary>
        private string GetUniqueViewName(Document doc, string baseName)
        {
            var existingViews = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.View))
                .Cast<Autodesk.Revit.DB.View>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name)
                .ToList();

            if (!existingViews.Contains(baseName))
                return baseName;

            int counter = 1;
            string uniqueName;
            do
            {
                uniqueName = $"{baseName} ({counter})";
                counter++;
            } while (existingViews.Contains(uniqueName));

            _logger.LogInformation($"Generated unique view name: '{uniqueName}' (base: '{baseName}')");
            return uniqueName;
        }

        /// <summary>
        /// Calculates the combined bounding box for a list of walls
        /// </summary>
        private static BoundingBoxXYZ CalculateBoundingBoxForWalls(List<WallInfo> walls)
        {
            var bbox = new BoundingBoxXYZ();
            var min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue);
            var max = new XYZ(double.MinValue, double.MinValue, double.MinValue);

            foreach (var wallInfo in walls)
            {
                var wallBBox = wallInfo.Wall.get_BoundingBox(null);
                if (wallBBox != null)
                {
                    min = new XYZ(
                        Math.Min(min.X, wallBBox.Min.X),
                        Math.Min(min.Y, wallBBox.Min.Y),
                        Math.Min(min.Z, wallBBox.Min.Z));

                    max = new XYZ(
                        Math.Max(max.X, wallBBox.Max.X),
                        Math.Max(max.Y, wallBBox.Max.Y),
                        Math.Max(max.Z, wallBBox.Max.Z));
                }
            }

            bbox.Min = min;
            bbox.Max = max;
            return bbox;
        }

        /// <summary>
        /// Logs view creation information for debugging
        /// </summary>
        private void LogViewCreationInfo(XYZ wallNormal, XYZ viewDirection, XYZ rightDirection, XYZ sectionOrigin)
        {
            _logger.LogInformation($"Wall normal: ({wallNormal.X:F2}, {wallNormal.Y:F2})");
            _logger.LogInformation($"View direction: ({viewDirection.X:F2}, {viewDirection.Y:F2})");
            _logger.LogInformation($"Right direction: ({rightDirection.X:F2}, {rightDirection.Y:F2})");
            _logger.LogInformation($"Section origin: ({sectionOrigin.X:F2}, {sectionOrigin.Y:F2}, {sectionOrigin.Z:F2})");
        }

        #endregion

        #region Region Creation

        /// <summary>
        /// Creates filled regions for walls in the elevation view
        /// </summary>
        private void CreateRegionsForWalls(Document doc, ViewSection elevationView, WallGroup group)
        {
            try
            {
                var viewOrigin = elevationView.Origin;
                var viewDirection = elevationView.ViewDirection;
                var viewRightDirection = elevationView.RightDirection;
                var viewUpDirection = elevationView.UpDirection;

                LogViewCoordinateSystem(viewOrigin, viewDirection, viewRightDirection, viewUpDirection);

                EnsureSketchPlaneExists(doc, elevationView, viewDirection, viewOrigin);

                var wallsToProject = FilterWallsByGroupOrientation(group);

                _logger.LogInformation($"Creating regions for {wallsToProject.Count} walls (out of {_perimeterWalls.Count} total) in view {elevationView.Name}");

                foreach (var wallInfo in wallsToProject)
                {
                    CreateRegionForWall(doc, elevationView, wallInfo, viewOrigin, viewRightDirection, viewUpDirection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating regions for walls in group {group.GroupName}", ex);
            }
        }

        /// <summary>
        /// Ensures the view has a sketch plane for region creation
        /// </summary>
        private void EnsureSketchPlaneExists(Document doc, ViewSection elevationView, XYZ viewDirection, XYZ viewOrigin)
        {
            if (elevationView.SketchPlane == null)
            {
                var plane = Plane.CreateByNormalAndOrigin(viewDirection, viewOrigin);
                elevationView.SketchPlane = SketchPlane.Create(doc, plane);
                _logger.LogInformation($"Created sketch plane for view {elevationView.Name}");
            }
        }

        /// <summary>
        /// Filters walls to show only those with the same orientation as the group
        /// </summary>
        private List<WallInfo> FilterWallsByGroupOrientation(WallGroup group)
        {
            var groupNormal = new XYZ(group.Orientation.X, group.Orientation.Y, 0).Normalize();

            return _perimeterWalls.Where(w =>
            {
                if (w.Orientation == null || !w.LimitingDistance.HasValue)
                    return false;

                var wallNormal = new XYZ(w.Orientation.X, w.Orientation.Y, 0).Normalize();
                var dotProduct = wallNormal.DotProduct(groupNormal);

                return dotProduct > WALL_SIMILARITY_THRESHOLD;
            }).ToList();
        }

        /// <summary>
        /// Creates a filled region for a single wall
        /// </summary>
        private void CreateRegionForWall(Document doc, ViewSection elevationView, WallInfo wallInfo,
            XYZ viewOrigin, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            try
            {
                var wall = wallInfo.Wall;
                var locationCurve = (wall.Location as LocationCurve)?.Curve;

                if (locationCurve == null)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} has no location curve");
                    return;
                }

                var (baseElevation, topElevation) = GetWallElevations(doc, wall);
                _logger.LogInformation($"Wall {wall.Id.Value}: Base={baseElevation:F2}, Top={topElevation:F2}, Height={topElevation - baseElevation:F2}");

                var rectanglePoints = CalculateWallRectanglePoints(locationCurve, baseElevation, topElevation,
                    viewOrigin, viewRightDirection, viewUpDirection);

                var outerLoop = CreateRectangleCurveLoop(wall.Id, rectanglePoints);

                if (outerLoop != null)
                {
                    // Get wall openings (doors, windows, etc.) and create curve loops for them
                    var openingLoops = GetWallOpeningLoops(doc, wall, baseElevation, viewOrigin, viewRightDirection, viewUpDirection);

                    CreateFilledRegionWithOpenings(doc, elevationView, wall.Id, wallInfo, outerLoop, openingLoops, viewOrigin, viewRightDirection, viewUpDirection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create region for wall {wallInfo.ElementId.Value}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the base and top elevations for a wall
        /// </summary>
        private static (double baseElevation, double topElevation) GetWallElevations(Document doc, Wall wall)
        {
            var baseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0;
            var baseConstraint = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId();
            var topOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.AsDouble() ?? 0;
            var unconnectedHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 10.0;

            Level? baseLevel = null;
            if (baseConstraint != null && baseConstraint != ElementId.InvalidElementId)
            {
                baseLevel = doc.GetElement(baseConstraint) as Level;
            }

            double baseElevation = (baseLevel?.Elevation ?? 0) + baseOffset;
            double topElevation = baseElevation + unconnectedHeight + topOffset;

            return (baseElevation, topElevation);
        }

        /// <summary>
        /// Calculates the 4 corner points of the wall rectangle projected to the view plane
        /// </summary>
        private static XYZ[] CalculateWallRectanglePoints(Curve locationCurve, double baseElevation, double topElevation,
            XYZ viewOrigin, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            var startPoint3D = locationCurve.GetEndPoint(0);
            var endPoint3D = locationCurve.GetEndPoint(1);

            var startBase3D = new XYZ(startPoint3D.X, startPoint3D.Y, baseElevation);
            var endBase3D = new XYZ(endPoint3D.X, endPoint3D.Y, baseElevation);
            var startTop3D = new XYZ(startPoint3D.X, startPoint3D.Y, topElevation);
            var endTop3D = new XYZ(endPoint3D.X, endPoint3D.Y, topElevation);

            return new[]
            {
                ProjectPointToViewPlane(startBase3D, viewOrigin, viewRightDirection, viewUpDirection),
                ProjectPointToViewPlane(endBase3D, viewOrigin, viewRightDirection, viewUpDirection),
                ProjectPointToViewPlane(endTop3D, viewOrigin, viewRightDirection, viewUpDirection),
                ProjectPointToViewPlane(startTop3D, viewOrigin, viewRightDirection, viewUpDirection)
            };
        }

        /// <summary>
        /// Creates a rectangular curve loop from 4 corner points
        /// </summary>
        private CurveLoop? CreateRectangleCurveLoop(ElementId wallId, XYZ[] points)
        {
            var rectangleLoop = new CurveLoop();

            try
            {
                _logger.LogInformation($"Wall {wallId.Value} rectangle points:");
                _logger.LogInformation($"  P1 (start base): ({points[0].X:F3}, {points[0].Y:F3}, {points[0].Z:F3})");
                _logger.LogInformation($"  P2 (end base):   ({points[1].X:F3}, {points[1].Y:F3}, {points[1].Z:F3})");
                _logger.LogInformation($"  P3 (end top):    ({points[2].X:F3}, {points[2].Y:F3}, {points[2].Z:F3})");
                _logger.LogInformation($"  P4 (start top):  ({points[3].X:F3}, {points[3].Y:F3}, {points[3].Z:F3})");

                for (int i = 0; i < 4; i++)
                {
                    var nextIndex = (i + 1) % 4;
                    if (!points[i].IsAlmostEqualTo(points[nextIndex], POINT_EQUALITY_TOLERANCE))
                    {
                        rectangleLoop.Append(Line.CreateBound(points[i], points[nextIndex]));
                    }
                    else
                    {
                        _logger.LogWarning($"Wall {wallId.Value}: P{i+1} and P{nextIndex+1} are identical");
                    }
                }

                if (rectangleLoop.NumberOfCurves() == 4)
                {
                    var isOpen = rectangleLoop.IsOpen();
                    _logger.LogInformation($"Wall {wallId.Value} rectangle: {rectangleLoop.NumberOfCurves()} curves, IsOpen={isOpen}");

                    if (!isOpen)
                        return rectangleLoop;

                    _logger.LogWarning($"Wall {wallId.Value} rectangle is open - cannot use for region");
                }
                else
                {
                    _logger.LogWarning($"Wall {wallId.Value} rectangle has {rectangleLoop.NumberOfCurves()} curves instead of 4");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating rectangle for wall {wallId.Value}: {ex.Message}", ex);
            }

            return null;
        }

        /// <summary>
        /// Gets the opening loops (doors, windows) for a wall projected to the view plane
        /// </summary>
        private List<CurveLoop> GetWallOpeningLoops(Document doc, Wall wall, double baseElevation,
            XYZ viewOrigin, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            var openingLoops = new List<CurveLoop>();

            try
            {
                // Get all inserts (doors, windows) hosted in this wall
                var insertIds = wall.FindInserts(true, true, true, true);

                _logger.LogInformation($"Wall {wall.Id.Value} has {insertIds.Count} openings");

                foreach (var insertId in insertIds)
                {
                    var insert = doc.GetElement(insertId);

                    if (insert is FamilyInstance familyInstance)
                    {
                        var openingLoop = CreateOpeningCurveLoop(doc, familyInstance, baseElevation, viewOrigin, viewRightDirection, viewUpDirection);

                        if (openingLoop != null)
                        {
                            openingLoops.Add(openingLoop);
                            _logger.LogInformation($"Created opening loop for {familyInstance.Category.Name} {familyInstance.Id.Value}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error getting wall openings for wall {wall.Id.Value}: {ex.Message}");
            }

            return openingLoops;
        }

        /// <summary>
        /// Creates a curve loop for a wall opening (door, window, etc.)
        /// </summary>
        private CurveLoop? CreateOpeningCurveLoop(Document doc, FamilyInstance opening, double wallBaseElevation,
            XYZ viewOrigin, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            try
            {
                // Log opening category for debugging
                var category = opening.Category?.Name ?? "Unknown";
                var isDoor = category.Equals("Doors", StringComparison.OrdinalIgnoreCase);

                if (isDoor)
                {
                    _logger.LogInformation($"========== DOOR OPENING DETECTED ==========");
                }

                _logger.LogInformation($"Processing opening {opening.Id.Value} - Category: {category}, Family: {opening.Symbol?.Family?.Name}, Type: {opening.Symbol?.Name}");

                // Get opening parameters
                var sillHeight = opening.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.AsDouble() ?? 0;
                var headHeight = opening.get_Parameter(BuiltInParameter.INSTANCE_HEAD_HEIGHT_PARAM)?.AsDouble();

                _logger.LogInformation($"Opening {opening.Id.Value}: SillHeight={sillHeight:F3}, HeadHeight={headHeight?.ToString("F3") ?? "null"}");

                // Log all parameter attempts for debugging
                var doorWidthInst = opening.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble();
                var windowWidthInst = opening.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsDouble();
                var familyWidthInst = opening.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble();
                var doorWidthSym = opening.Symbol?.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsDouble();
                var windowWidthSym = opening.Symbol?.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsDouble();
                var familyWidthSym = opening.Symbol?.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble();

                _logger.LogInformation($"Opening {opening.Id.Value} WIDTH parameters: DOOR_WIDTH(inst)={doorWidthInst?.ToString("F3") ?? "null"}, WINDOW_WIDTH(inst)={windowWidthInst?.ToString("F3") ?? "null"}, FAMILY_WIDTH(inst)={familyWidthInst?.ToString("F3") ?? "null"}");
                _logger.LogInformation($"Opening {opening.Id.Value} WIDTH Symbol: DOOR_WIDTH(sym)={doorWidthSym?.ToString("F3") ?? "null"}, WINDOW_WIDTH(sym)={windowWidthSym?.ToString("F3") ?? "null"}, FAMILY_WIDTH(sym)={familyWidthSym?.ToString("F3") ?? "null"}");

                var doorHeightInst = opening.get_Parameter(BuiltInParameter.DOOR_HEIGHT)?.AsDouble();
                var windowHeightInst = opening.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)?.AsDouble();
                var familyHeightInst = opening.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();
                var doorHeightSym = opening.Symbol?.get_Parameter(BuiltInParameter.DOOR_HEIGHT)?.AsDouble();
                var windowHeightSym = opening.Symbol?.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)?.AsDouble();
                var familyHeightSym = opening.Symbol?.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble();

                _logger.LogInformation($"Opening {opening.Id.Value} HEIGHT parameters: DOOR_HEIGHT(inst)={doorHeightInst?.ToString("F3") ?? "null"}, WINDOW_HEIGHT(inst)={windowHeightInst?.ToString("F3") ?? "null"}, FAMILY_HEIGHT(inst)={familyHeightInst?.ToString("F3") ?? "null"}");
                _logger.LogInformation($"Opening {opening.Id.Value} HEIGHT Symbol: DOOR_HEIGHT(sym)={doorHeightSym?.ToString("F3") ?? "null"}, WINDOW_HEIGHT(sym)={windowHeightSym?.ToString("F3") ?? "null"}, FAMILY_HEIGHT(sym)={familyHeightSym?.ToString("F3") ?? "null"}");

                // Try to get width and height from parameters first
                var widthParam = doorWidthInst ?? windowWidthInst ?? familyWidthInst ?? doorWidthSym ?? windowWidthSym ?? familyWidthSym;
                var heightParam = doorHeightInst ?? windowHeightInst ?? familyHeightInst ?? doorHeightSym ?? windowHeightSym ?? familyHeightSym;

                // If parameters don't work, use bounding box
                double? width = widthParam;
                double? height = heightParam;

                if (!width.HasValue || width.Value == 0 || !height.HasValue || height.Value == 0)
                {
                    _logger.LogInformation($"Opening {opening.Id.Value}: Parameters failed (width={width?.ToString("F3") ?? "null"}, height={height?.ToString("F3") ?? "null"}), trying bounding box...");

                    var openingBBox = opening.get_BoundingBox(null);
                    if (openingBBox != null)
                    {
                        var bboxWidth = openingBBox.Max.X - openingBBox.Min.X;
                        var bboxHeight = openingBBox.Max.Z - openingBBox.Min.Z;
                        var bboxDepth = openingBBox.Max.Y - openingBBox.Min.Y;

                        _logger.LogInformation($"Opening {opening.Id.Value}: BBox dimensions: X={Math.Abs(bboxWidth):F3}, Y={Math.Abs(bboxDepth):F3}, Z={Math.Abs(bboxHeight):F3}");

                        // Width is the larger of X or Y dimensions
                        width = Math.Max(Math.Abs(bboxWidth), Math.Abs(bboxDepth));
                        height = Math.Abs(bboxHeight);

                        _logger.LogInformation($"Opening {opening.Id.Value}: Using bbox dimensions: width={width:F3}, height={height:F3}");
                    }
                    else
                    {
                        _logger.LogWarning($"Opening {opening.Id.Value}: BoundingBox is null");
                    }
                }
                else
                {
                    _logger.LogInformation($"Opening {opening.Id.Value}: Using parameter dimensions: width={width:F3}, height={height:F3}");
                }

                if (!width.HasValue || width.Value == 0 || !height.HasValue || height.Value == 0)
                {
                    _logger.LogWarning($"Opening {opening.Id.Value} has invalid dimensions (width={width}, height={height})");
                    return null;
                }

                // Get opening's host wall to determine the correct orientation
                var host = opening.Host as Wall;
                if (host == null)
                {
                    _logger.LogWarning($"Opening {opening.Id.Value} has no host wall");
                    return null;
                }

                // Get wall location curve to determine wall direction
                var wallLocationCurve = (host.Location as LocationCurve)?.Curve;
                if (wallLocationCurve == null)
                {
                    _logger.LogWarning($"Opening {opening.Id.Value} host wall has no location curve");
                    return null;
                }

                // Get opening location point (center)
                var locationPoint = (opening.Location as LocationPoint)?.Point;
                if (locationPoint == null)
                {
                    _logger.LogWarning($"Opening {opening.Id.Value} has no location point");
                    return null;
                }

                // Wall direction (along the wall length)
                var wallDirection = (wallLocationCurve.GetEndPoint(1) - wallLocationCurve.GetEndPoint(0)).Normalize();

                _logger.LogInformation($"Opening {opening.Id.Value}: Wall direction = ({wallDirection.X:F3}, {wallDirection.Y:F3}, {wallDirection.Z:F3})");
                _logger.LogInformation($"Opening {opening.Id.Value}: Location = ({locationPoint.X:F3}, {locationPoint.Y:F3}, {locationPoint.Z:F3})");
                _logger.LogInformation($"Opening {opening.Id.Value}: Width = {width.Value:F3}");

                // Calculate opening elevations
                double bottomElevation = wallBaseElevation + sillHeight;
                double topElevation = headHeight.HasValue ? wallBaseElevation + headHeight.Value : bottomElevation + height.Value;

                // Calculate the 4 corners of the opening rectangle
                // The opening extends along the wall direction
                var halfWidth = width.Value / 2;

                // Use the opening location in X,Y plane and extend along wall direction
                var centerInXY = new XYZ(locationPoint.X, locationPoint.Y, 0);
                var p1_3D = centerInXY - (wallDirection * halfWidth);
                var p2_3D = centerInXY + (wallDirection * halfWidth);

                _logger.LogInformation($"Opening {opening.Id.Value}: P1_3D = ({p1_3D.X:F3}, {p1_3D.Y:F3}, {p1_3D.Z:F3})");
                _logger.LogInformation($"Opening {opening.Id.Value}: P2_3D = ({p2_3D.X:F3}, {p2_3D.Y:F3}, {p2_3D.Z:F3})");

                var p1Bottom = new XYZ(p1_3D.X, p1_3D.Y, bottomElevation);
                var p2Bottom = new XYZ(p2_3D.X, p2_3D.Y, bottomElevation);
                var p2Top = new XYZ(p2_3D.X, p2_3D.Y, topElevation);
                var p1Top = new XYZ(p1_3D.X, p1_3D.Y, topElevation);

                // Project to view plane
                var points = new[]
                {
                    ProjectPointToViewPlane(p1Bottom, viewOrigin, viewRightDirection, viewUpDirection),
                    ProjectPointToViewPlane(p2Bottom, viewOrigin, viewRightDirection, viewUpDirection),
                    ProjectPointToViewPlane(p2Top, viewOrigin, viewRightDirection, viewUpDirection),
                    ProjectPointToViewPlane(p1Top, viewOrigin, viewRightDirection, viewUpDirection)
                };

                // Log the opening rectangle points for debugging
                _logger.LogInformation($"Opening {opening.Id.Value} rectangle points:");
                _logger.LogInformation($"  P1 (bottom left):  ({points[0].X:F3}, {points[0].Y:F3}, {points[0].Z:F3})");
                _logger.LogInformation($"  P2 (bottom right): ({points[1].X:F3}, {points[1].Y:F3}, {points[1].Z:F3})");
                _logger.LogInformation($"  P3 (top right):    ({points[2].X:F3}, {points[2].Y:F3}, {points[2].Z:F3})");
                _logger.LogInformation($"  P4 (top left):     ({points[3].X:F3}, {points[3].Y:F3}, {points[3].Z:F3})");

                // Create curve loop first in normal direction
                var loop = new CurveLoop();
                for (int i = 0; i < 4; i++)
                {
                    var nextIndex = (i + 1) % 4;

                    // Check distance in 2D (X,Y only) since we're on a sketch plane
                    var dist2D = Math.Sqrt(
                        Math.Pow(points[nextIndex].X - points[i].X, 2) +
                        Math.Pow(points[nextIndex].Y - points[i].Y, 2));

                    var dist3D = points[i].DistanceTo(points[nextIndex]);

                    _logger.LogInformation($"  Curve {i}: 2D dist={dist2D:F6}, 3D dist={dist3D:F6}");

                    if (dist3D > POINT_EQUALITY_TOLERANCE)
                    {
                        loop.Append(Line.CreateBound(points[i], points[nextIndex]));
                        _logger.LogInformation($"  Added curve {i}: ({points[i].X:F3}, {points[i].Y:F3}, {points[i].Z:F3}) -> ({points[nextIndex].X:F3}, {points[nextIndex].Y:F3}, {points[nextIndex].Z:F3})");
                    }
                    else
                    {
                        _logger.LogWarning($"  Skipped curve {i}: 3D distance too small ({dist3D:F6})");
                    }
                }

                if (loop.NumberOfCurves() >= 3 && !loop.IsOpen())
                {
                    _logger.LogInformation($"Opening {opening.Id.Value} ({category}): Bottom={bottomElevation:F2}, Top={topElevation:F2}, Width={width.Value:F2}, Curves={loop.NumberOfCurves()}");
                    _logger.LogInformation($"Created opening loop for {category} {opening.Id.Value}");
                    return loop;
                }

                _logger.LogWarning($"Opening {opening.Id.Value} ({category}) curve loop is invalid (curves: {loop.NumberOfCurves()}, open: {loop.IsOpen()})");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error creating opening curve loop for {opening.Id.Value}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Creates a filled region with openings and top-most ceilings geometrically subtracted using polygon boolean operations
        /// </summary>
        private void CreateFilledRegionWithOpenings(Document doc, ViewSection elevationView, ElementId wallId,
            WallInfo wallInfo, CurveLoop outerLoop, List<CurveLoop> openingLoops, XYZ viewOrigin, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            var distanceGroup = GetDistanceGroupForWall(wallInfo.LimitingDistance.Value);
            var filledRegionType = GetOrCreateColoredFilledRegionType(doc, distanceGroup);

            if (filledRegionType != null)
            {
                try
                {
                    // Get wall element
                    var wall = doc.GetElement(wallId) as Wall;
                    if (wall == null)
                    {
                        _logger.LogWarning($"Wall {wallId.Value} not found");
                        return;
                    }

                    // Perform boolean subtraction to compute wall fragments
                    var wallPoints = GetCurveLoopPoints(outerLoop);
                    var wallRect = Helpers.PolygonBooleanOperations.Rectangle2D.FromPoints(wallPoints);

                    _logger.LogInformation($"Wall rectangle (2D): U=[{wallRect.MinU:F3}, {wallRect.MaxU:F3}], V=[{wallRect.MinV:F3}, {wallRect.MaxV:F3}]");

                    // Check for joined top-most ceilings and trim wall height to ceiling elevation
                    var connectedCeilings = Helpers.CeilingWallAnalyzer.GetConnectedCeilings(wall, doc);
                    _logger.LogInformation($"Wall {wallId.Value}: Found {connectedCeilings.Count} connected ceiling(s)");

                    double? maxWallHeightIn2D = null;

                    foreach (var relationship in connectedCeilings)
                    {
                        _logger.LogInformation($"  Checking ceiling {relationship.Ceiling.Id.Value}: IsJoined={relationship.IsJoined}");

                        // Check if ceiling is top-most
                        var isTopMostParam = relationship.Ceiling.LookupParameter("IsTopMost");
                        if (isTopMostParam != null)
                        {
                            _logger.LogInformation($"    IsTopMost param exists, StorageType={isTopMostParam.StorageType}, Value={isTopMostParam.AsInteger()}");
                        }
                        else
                        {
                            _logger.LogInformation($"    IsTopMost param NOT found");
                        }

                        if (isTopMostParam != null && isTopMostParam.StorageType == StorageType.Integer && isTopMostParam.AsInteger() == 1)
                        {
                            // Get ceiling elevation in 3D world coordinates
                            var ceilingElevation3D = relationship.CeilingElevation;
                            var wallBaseElevation = relationship.WallBaseElevation;
                            var wallTopElevation = relationship.WallTopElevation;

                            // Project a point at ceiling elevation to the 2D view plane
                            // Use the wall's location curve to get a point on the wall at the ceiling elevation
                            var wallLocationCurve = (wall.Location as LocationCurve)?.Curve;
                            if (wallLocationCurve != null)
                            {
                                var wallMidPoint = wallLocationCurve.Evaluate(0.5, true);
                                var ceilingPoint3D = new XYZ(wallMidPoint.X, wallMidPoint.Y, ceilingElevation3D);
                                _logger.LogInformation($"    Ceiling point 3D: ({ceilingPoint3D.X:F3}, {ceilingPoint3D.Y:F3}, {ceilingPoint3D.Z:F3})");

                                var ceilingPoint2D = ProjectPointToViewPlane(ceilingPoint3D, viewOrigin, viewRightDirection, viewUpDirection);
                                _logger.LogInformation($"    Ceiling point 2D: ({ceilingPoint2D.X:F3}, {ceilingPoint2D.Y:F3}, {ceilingPoint2D.Z:F3})");

                                // The Z component of the projected point is the vertical position in the 2D view
                                var ceilingHeightIn2D = ceilingPoint2D.Z;

                                // Keep track of the lowest ceiling (most restrictive)
                                if (!maxWallHeightIn2D.HasValue || ceilingHeightIn2D < maxWallHeightIn2D.Value)
                                {
                                    maxWallHeightIn2D = ceilingHeightIn2D;
                                    _logger.LogInformation($"    ** SELECTED as limiting ceiling **");
                                    _logger.LogInformation($"    Ceiling 3D elevation: {ceilingElevation3D:F2} ft, projected 2D height: {ceilingHeightIn2D:F3}");
                                    _logger.LogInformation($"    Wall base 3D: {wallBaseElevation:F2} ft, Wall top 3D: {wallTopElevation:F2} ft");
                                    _logger.LogInformation($"    Current wall rect 2D: MinV={wallRect.MinV:F3}, MaxV={wallRect.MaxV:F3}");
                                }
                                else
                                {
                                    _logger.LogInformation($"    Not selected (higher than current limit {maxWallHeightIn2D.Value:F3})");
                                }
                            }
                        }
                    }

                    // If there's a top-most ceiling, trim the wall rectangle to the ceiling height in 2D view coordinates
                    if (maxWallHeightIn2D.HasValue)
                    {
                        _logger.LogInformation($"Wall {wallId.Value}: Will trim from MaxV={wallRect.MaxV:F3} to {maxWallHeightIn2D.Value:F3} (will trim={maxWallHeightIn2D.Value < wallRect.MaxV})");

                        if (maxWallHeightIn2D.Value < wallRect.MaxV)
                        {
                            wallRect = new Helpers.PolygonBooleanOperations.Rectangle2D(
                                wallRect.MinU,
                                wallRect.MaxU,
                                wallRect.MinV,
                                maxWallHeightIn2D.Value
                            );
                            _logger.LogInformation($"  *** TRIMMED *** New MaxV={wallRect.MaxV:F3}");
                        }
                        else
                        {
                            _logger.LogInformation($"  NOT trimmed - ceiling is at or above wall top");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Wall {wallId.Value}: No top-most ceiling found, no trimming");
                    }

                    var cutoutRects = new List<Helpers.PolygonBooleanOperations.Rectangle2D>();

                    // Add door/window openings
                    foreach (var openingLoop in openingLoops)
                    {
                        var openingPoints = GetCurveLoopPoints(openingLoop);
                        var openingRect = Helpers.PolygonBooleanOperations.Rectangle2D.FromPoints(openingPoints);
                        cutoutRects.Add(openingRect);

                        _logger.LogInformation($"Opening rectangle (2D): U=[{openingRect.MinU:F3}, {openingRect.MaxU:F3}], V=[{openingRect.MinV:F3}, {openingRect.MaxV:F3}]");
                    }

                    if (cutoutRects.Count == 0)
                    {
                        // No cutouts, but wall may have been trimmed by ceiling
                        // Need to create loop from the trimmed wallRect, not the original outerLoop
                        var trimmedLoops = Helpers.PolygonBooleanOperations.ToCurveLoops(
                            new List<Helpers.PolygonBooleanOperations.Rectangle2D> { wallRect },
                            wallPoints
                        );

                        if (trimmedLoops.Count > 0)
                        {
                            var wallRegion = FilledRegion.Create(doc, filledRegionType.Id, elevationView.Id, new List<CurveLoop> { trimmedLoops[0] });
                            _logger.LogInformation($"Created wall region for wall {wallId.Value} (no cutouts, trimmed: MaxV={wallRect.MaxV:F3})");
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to create trimmed wall region for wall {wallId.Value} - no valid curves generated");
                        }
                    }
                    else
                    {
                        // Subtract all cutouts (openings + top-most ceilings) from the wall rectangle
                        var resultRectangles = Helpers.PolygonBooleanOperations.SubtractOpenings(wallRect, cutoutRects);
                        _logger.LogInformation($"Boolean subtraction produced {resultRectangles.Count} fragments from wall with {cutoutRects.Count} cutouts ({openingLoops.Count} openings + {cutoutRects.Count - openingLoops.Count} top-most ceilings)");

                        // Convert result rectangles back to CurveLoops and create filled regions
                        var resultLoops = Helpers.PolygonBooleanOperations.ToCurveLoops(resultRectangles, wallPoints);

                        foreach (var loop in resultLoops)
                        {
                            try
                            {
                                var region = FilledRegion.Create(doc, filledRegionType.Id, elevationView.Id, new List<CurveLoop> { loop });
                                _logger.LogInformation($"Created wall fragment region ({loop.NumberOfCurves()} curves)");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to create wall fragment region: {ex.Message}");
                            }
                        }
                    }

                    _logger.LogInformation($"Created region for wall {wallId.Value} with {cutoutRects.Count} cutouts subtracted");
                    _logger.LogInformation($"Wall {wallInfo.ElementId.Value} - LD {wallInfo.LimitingDistance.Value:F1} ft in group {distanceGroup.Label}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to create region for wall {wallId.Value}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Extracts the corner points from a CurveLoop
        /// </summary>
        private static XYZ[] GetCurveLoopPoints(CurveLoop loop)
        {
            var points = new List<XYZ>();
            foreach (Curve curve in loop)
            {
                points.Add(curve.GetEndPoint(0));
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets or creates a filled region type with solid white/background fill for opening cutouts
        /// NOTE: This is no longer used - we use geometric boolean subtraction instead
        /// </summary>
        [Obsolete("No longer used - geometric boolean subtraction is used instead")]
        private FilledRegionType? GetOrCreateBackgroundFilledRegionType(Document doc)
        {
            const string typeName = "Opening_Cutout_Background";

            // Try to find existing type
            var existingType = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault(t => t.Name == typeName);

            if (existingType != null)
            {
                return existingType;
            }

            try
            {
                // Get a default filled region type to duplicate
                var defaultType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault();

                if (defaultType == null)
                {
                    _logger.LogWarning("No default filled region type found to duplicate");
                    return null;
                }

                // Duplicate and rename
                var newType = defaultType.Duplicate(typeName) as FilledRegionType;
                if (newType == null)
                {
                    _logger.LogWarning("Failed to duplicate filled region type");
                    return null;
                }

                // Try to set it to solid white or background color
                // Get the fill pattern for solid fill
                var solidPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

                if (solidPattern != null)
                {
                    newType.ForegroundPatternId = solidPattern.Id;
                    newType.ForegroundPatternColor = new Autodesk.Revit.DB.Color(255, 255, 255); // White

                    _logger.LogInformation($"Created background filled region type '{typeName}' with white solid fill");
                }

                return newType;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error creating background filled region type: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Projects a 3D point onto the view plane and returns it in world coordinates
        /// The returned point lies on the view's sketch plane
        /// </summary>
        private static XYZ ProjectPointToViewPlane(XYZ point, XYZ viewOrigin, XYZ viewRight, XYZ viewUp)
        {
            var toPoint = point - viewOrigin;
            var u = toPoint.DotProduct(viewRight);
            var v = toPoint.DotProduct(viewUp);

            // Convert 2D view coordinates back to 3D world coordinates on the view plane
            // This ensures the point lies exactly on the sketch plane in world coordinates
            return viewOrigin + (u * viewRight) + (v * viewUp);
        }

        /// <summary>
        /// Logs view coordinate system information
        /// </summary>
        private void LogViewCoordinateSystem(XYZ viewOrigin, XYZ viewDirection, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            _logger.LogInformation($"View Origin: ({viewOrigin.X:F2}, {viewOrigin.Y:F2}, {viewOrigin.Z:F2})");
            _logger.LogInformation($"View Direction: ({viewDirection.X:F2}, {viewDirection.Y:F2}, {viewDirection.Z:F2})");
            _logger.LogInformation($"View Right: ({viewRightDirection.X:F2}, {viewRightDirection.Y:F2}, {viewRightDirection.Z:F2})");
            _logger.LogInformation($"View Up: ({viewUpDirection.X:F2}, {viewUpDirection.Y:F2}, {viewUpDirection.Z:F2})");
        }

        #endregion

        #region Distance Groups and Colors

        /// <summary>
        /// Gets the distance group classification for a wall's limiting distance
        /// </summary>
        private static (double Min, double Max, string Label, int ColorIndex) GetDistanceGroupForWall(double limitingDistance)
        {
            var ranges = new[]
            {
                (Min: 0.0, Max: 3.937, Label: "0-1.2m (0-3.9ft)", ColorIndex: 0),
                (Min: 3.937, Max: 4.921, Label: "1.2-1.5m (3.9-4.9ft)", ColorIndex: 1),
                (Min: 4.921, Max: 6.562, Label: "1.5-2m (4.9-6.6ft)", ColorIndex: 2),
                (Min: 6.562, Max: 8.202, Label: "2-2.5m (6.6-8.2ft)", ColorIndex: 3),
                (Min: 8.202, Max: 9.843, Label: "2.5-3m (8.2-9.8ft)", ColorIndex: 4),
                (Min: 9.843, Max: 13.123, Label: "3-4m (9.8-13.1ft)", ColorIndex: 5),
                (Min: 13.123, Max: 16.404, Label: "4-5m (13.1-16.4ft)", ColorIndex: 6),
                (Min: 16.404, Max: 19.685, Label: "5-6m (16.4-19.7ft)", ColorIndex: 7),
                (Min: 19.685, Max: 22.966, Label: "6-7m (19.7-23.0ft)", ColorIndex: 8),
                (Min: 22.966, Max: 26.247, Label: "7-8m (23.0-26.2ft)", ColorIndex: 9),
                (Min: 26.247, Max: 29.528, Label: "8-9m (26.2-29.5ft)", ColorIndex: 10),
                (Min: 29.528, Max: double.MaxValue, Label: "9m+ (29.5ft+)", ColorIndex: 11)
            };

            foreach (var range in ranges)
            {
                if (limitingDistance >= range.Min && limitingDistance < range.Max)
                    return range;
            }

            return ranges[^1];
        }

        /// <summary>
        /// Gets or creates a filled region type with the appropriate color for a distance group
        /// </summary>
        private FilledRegionType? GetOrCreateColoredFilledRegionType(Document doc,
            (double Min, double Max, string Label, int ColorIndex) distanceGroup)
        {
            try
            {
                var typeName = $"LD_{distanceGroup.Label.Replace(" ", "_").Replace("(", "").Replace(")", "")}";

                var existingType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault(frt => frt.Name == typeName);

                if (existingType != null)
                    return existingType;

                var baseType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .FirstOrDefault() as FilledRegionType;

                if (baseType == null)
                {
                    _logger.LogWarning("No base filled region type found");
                    return null;
                }

                var newType = baseType.Duplicate(typeName) as FilledRegionType;

                if (newType != null)
                {
                    ApplyColorToFilledRegionType(doc, newType, distanceGroup);
                    _logger.LogInformation($"Created filled region type '{typeName}' for distance group '{distanceGroup.Label}'");
                }

                return newType;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating colored filled region type for distance group '{distanceGroup.Label}'", ex);
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .FirstOrDefault() as FilledRegionType;
            }
        }

        /// <summary>
        /// Applies color to a filled region type
        /// </summary>
        private void ApplyColorToFilledRegionType(Document doc, FilledRegionType newType,
            (double Min, double Max, string Label, int ColorIndex) distanceGroup)
        {
            var color = GetColorFromDistanceGroupIndex(distanceGroup.ColorIndex);
            var revitColor = new Autodesk.Revit.DB.Color(color.Red, color.Green, color.Blue);

            if (newType.ForegroundPatternId != ElementId.InvalidElementId)
            {
                newType.ForegroundPatternColor = revitColor;
            }

            if (newType.BackgroundPatternId != ElementId.InvalidElementId)
            {
                newType.BackgroundPatternColor = revitColor;
            }

            _logger.LogInformation($"Applied color RGB({color.Red}, {color.Green}, {color.Blue}) to region type");
        }

        /// <summary>
        /// Gets the RGB color values for a distance group index (green to red gradient)
        /// </summary>
        private static (byte Red, byte Green, byte Blue) GetColorFromDistanceGroupIndex(int colorIndex)
        {
            var colors = new (byte Red, byte Green, byte Blue)[]
            {
                (0, 255, 0),     // 0: Bright Green - 0-1.2m
                (127, 255, 0),   // 1: Yellow-Green - 1.2-1.5m
                (255, 255, 0),   // 2: Yellow - 1.5-2m
                (255, 200, 0),   // 3: Yellow-Orange - 2-2.5m
                (255, 165, 0),   // 4: Orange - 2.5-3m
                (255, 128, 0),   // 5: Dark Orange - 3-4m
                (255, 64, 0),    // 6: Red-Orange - 4-5m
                (255, 0, 0),     // 7: Red - 5-6m
                (220, 0, 0),     // 8: Dark Red - 6-7m
                (180, 0, 0),     // 9: Darker Red - 7-8m
                (140, 0, 0),     // 10: Very Dark Red - 8-9m
                (100, 0, 0)      // 11: Darkest Red - 9m+
            };

            return colorIndex >= 0 && colorIndex < colors.Length ? colors[colorIndex] : colors[^1];
        }

        #endregion
    }
}
