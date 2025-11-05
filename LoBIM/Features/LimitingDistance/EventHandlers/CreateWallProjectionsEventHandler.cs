using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;
using LoBIM.Features.LimitingDistance.Models;
using LoBIM.Features.LimitingDistance.Services;
using LoBIM.Services.Rendering;
using LoBIM.Models.Tables;
using System.Collections.Generic;
using System.Linq;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.LimitingDistance.EventHandlers
{
    /// <summary>
    /// Creates elevation views with colored filled regions showing wall projections grouped by orientation.
    /// Each wall is colored according to its limiting distance group.
    /// </summary>
    internal class CreateWallProjectionsEventHandler : IExternalEventHandler
    {
        #region Constants

        private const double ORIENTATION_TOLERANCE_DEGREES = 5.0;
        private const double VIEW_DISTANCE_FROM_WALLS = 0;
        private const double VIEW_FAR_CLIP_OFFSET = 1000.0;
        private const double BOUNDING_BOX_PADDING = 0;
        private const double VIEW_DEPTH = 100.0;
        private const double POINT_EQUALITY_TOLERANCE = 1e-6;

        #endregion

        #region Fields

        private UIDocument _uidoc;
        private List<WallInfo> _perimeterWalls;
        private List<ReferenceLineInfo> _referenceLines;
        private DistanceGroupSummary? _distanceGroup;
        private readonly ILoggingService _logger;
        private readonly LimitingDistanceReportService _reportService;
        private readonly INBCConfigurationService _nbcConfig;
        private Action<ViewSection, int>? _onViewCreated;
        private Action<List<WallInfo>>? _onProjectionsCompleted;

        #endregion

        #region Constructor

        public CreateWallProjectionsEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _reportService = DIContainerService.Container.GetInstance<LimitingDistanceReportService>();
            _nbcConfig = DIContainerService.Container.GetInstance<INBCConfigurationService>();
            _perimeterWalls = new List<WallInfo>();
            _referenceLines = new List<ReferenceLineInfo>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the parameters for wall projection creation
        /// </summary>
        public void SetParameters(UIDocument uidoc, List<WallInfo> perimeterWalls, List<ReferenceLineInfo> referenceLines, DistanceGroupSummary? distanceGroup = null, Action<ViewSection, int>? onViewCreated = null, Action<List<WallInfo>>? onProjectionsCompleted = null)
        {
            _uidoc = uidoc;
            _perimeterWalls = perimeterWalls;
            _referenceLines = referenceLines;
            _distanceGroup = distanceGroup;
            _onViewCreated = onViewCreated;
            _onProjectionsCompleted = onProjectionsCompleted;
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

                _logger.LogInformation("Starting grouping by orientation");
                var orientationGroups = GroupWallsByOrientation(_perimeterWalls);
                _logger.LogInformation($"Created {orientationGroups.Count} orientation groups");

                if (orientationGroups.Count == 0)
                {
                    TaskDialog.Show("Info", "No orientation groups created. Ensure walls have valid orientations.");
                    return;
                }

                _logger.LogInformation("Starting view and region creation");
                CreateViewsAndRegions(doc, orientationGroups);

                // Generate NBC Compliance Report Drafting View
                _logger.LogInformation("Generating NBC Compliance Report");
                GenerateNBCComplianceReport(doc);

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

        #region Wall Filtering and Grouping

        /// <summary>
        /// Groups walls by their orientation
        /// Creates groups of walls with similar orientations (parallel/anti-parallel)
        /// </summary>
        private List<WallGroup> GroupWallsByOrientation(List<WallInfo> walls)
        {
            var groups = new List<WallGroup>();

            _logger.LogInformation($"GroupWallsByOrientation: Processing {walls.Count} walls");

            // Log all walls and their orientations
            _logger.LogInformation("=== Wall Orientations ===");
            foreach (var wallInfo in walls)
            {
                _logger.LogInformation($"  Wall {wallInfo.Wall.Id.Value} -> Orientation: X={wallInfo.Orientation.X:F3}, Y={wallInfo.Orientation.Y:F3}");
            }
            _logger.LogInformation("=========================");

            // Group walls by similar orientation
            var ungroupedWalls = new List<WallInfo>(walls);
            int groupIndex = 1;

            while (ungroupedWalls.Count > 0)
            {
                var seedWall = ungroupedWalls[0];
                var seedOrientation = seedWall.Wall.Orientation;

                if (seedOrientation == null)
                {
                    _logger.LogWarning($"Skipping wall {seedWall.Wall.Id.Value} - could not get orientation");
                    ungroupedWalls.RemoveAt(0);
                    continue;
                }

                _logger.LogInformation($"Creating group {groupIndex} with seed wall {seedWall.Wall.Id.Value}, orientation: X={seedOrientation.X:F3}, Y={seedOrientation.Y:F3}");

                // Find all walls with similar orientation
                var groupWalls = new List<WallInfo>();
                var wallsToRemove = new List<WallInfo>();

                foreach (var wallInfo in ungroupedWalls)
                {
                    var wallOrientation = wallInfo.Wall.Orientation;

                    // Check if orientations are similar (within tolerance)
                    if (AreOrientationsSimilar(seedOrientation, wallOrientation))
                    {
                        groupWalls.Add(wallInfo);
                        wallsToRemove.Add(wallInfo);
                        _logger.LogInformation($"  Added wall {wallInfo.Wall.Id.Value} to group {groupIndex}");
                    }
                }

                // Remove grouped walls from ungrouped list
                foreach (var wall in wallsToRemove)
                {
                    ungroupedWalls.Remove(wall);
                }

                if (groupWalls.Count > 0)
                {
                    // Calculate average orientation for the group
                    var avgOrientation = CalculateAverageOrientation(groupWalls.Select(w => w.Wall.Orientation).ToList());

                    // Calculate average limiting distance
                    var avgLimitingDistance = groupWalls.Average(w => w.LimitingDistance ?? 0);

                    var newGroup = new WallGroup
                    {
                        LimitingDistance = avgLimitingDistance,
                        Orientation = avgOrientation,
                        ReferenceLine = groupWalls.FirstOrDefault()?.ReferenceLine, // Use reference line from first wall
                        GroupName = $"Orientation Group {groupIndex} ({groupWalls.Count} walls)"
                    };

                    foreach (var wallInfo in groupWalls)
                    {
                        newGroup.Walls.Add(wallInfo);
                    }

                    groups.Add(newGroup);
                    _logger.LogInformation($"Created orientation group {groupIndex} with {groupWalls.Count} walls, avg LD: {avgLimitingDistance:F2} ft");
                    groupIndex++;
                }
            }

            _logger.LogInformation($"GroupWallsByOrientation: Created {groups.Count} groups total");

            return groups;
        }

        private bool AreOrientationsSimilar(XYZ a, XYZ b, double relColTol = 1e-6, double lenTol = 1e-9)
        {

            double la = a.GetLength(), lb = b.GetLength();
            if (la < lenTol || lb < lenTol) return false;

            double dot = a.DotProduct(b);
            double crossLen = a.CrossProduct(b).GetLength();

            bool colinear = crossLen <= relColTol * la * lb; // thẳng hàng theo ngưỡng tương đối
            bool sameSign = dot > 0;                          // cùng chiều
            return colinear && sameSign;
        }

        private XYZ CalculateAverageOrientation(List<XYZ> orientations)
        {
            if (orientations.Count == 0)
                return XYZ.BasisX;

            // Normalize all orientations to point in similar direction (avoid averaging opposite vectors)
            var reference = orientations[0];
            var normalizedOrientations = orientations.Select(o =>
            {
                // If dot product is negative, flip the orientation
                return o.DotProduct(reference) < 0 ? o.Negate() : o;
            }).ToList();

            // Calculate average
            var sumX = normalizedOrientations.Sum(o => o.X);
            var sumY = normalizedOrientations.Sum(o => o.Y);

            return new XYZ(sumX, sumY, 0).Normalize();
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
                    var wallsWithRegions = new List<WallInfo>();

                    foreach (var group in orientationGroups)
                    {
                        var elevationView = CreateCustomElevationViewForGroup(doc, group);

                        if (elevationView != null)
                        {
                            var createdWalls = CreateRegionsForWalls(doc, elevationView, group);
                            wallsWithRegions.AddRange(createdWalls);
                            viewsCreated++;
                        }
                    }

                    trans.Commit();

                    // Invoke callback to update ViewModel with walls that have regions
                    _onProjectionsCompleted?.Invoke(wallsWithRegions);

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
        /// Creates a custom elevation view for a specific wall orientation group
        /// </summary>
        private ViewSection? CreateCustomElevationViewForGroup(Document doc, WallGroup group)
        {
            try
            {
                var viewFamilyType = GetSectionViewFamilyType(doc);
                if (viewFamilyType == null)
                    return null;

                var boundingBox = CalculateBoundingBoxForWalls(group.Walls);

                // Use the reference line's XY position, but use the walls' average Z position
                XYZ centerPoint = group.WallsMidPoint() + group.Orientation.Normalize() * VIEW_DEPTH / 2;

                var (viewDirection, rightDirection, upDirection, sectionOrigin) =
                    CalculateViewCoordinateSystem(group.Orientation, centerPoint);

                var sectionBox = CreateSectionBoundingBox(boundingBox, centerPoint, viewDirection, rightDirection, upDirection, sectionOrigin);

                var sectionView = ViewSection.CreateSection(doc, viewFamilyType.Id, sectionBox);

                if (sectionView != null)
                {
                    ConfigureView(doc, sectionView, group);
                    _logger.LogInformation($"Created section view: {sectionView.Name}");

                    // Notify that a view was created
                    _onViewCreated?.Invoke(sectionView, group.Walls.Count);
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
        /// groupOrientation is now the reference line direction (parallel to the wall)
        /// </summary>
        private static (XYZ viewDirection, XYZ rightDirection, XYZ upDirection, XYZ sectionOrigin)
            CalculateViewCoordinateSystem(XYZ groupOrientation, XYZ centerPoint)
        {
            // groupOrientation is the reference line direction (parallel to walls)
            var lineDirection = new XYZ(groupOrientation.Y, -groupOrientation.X, 0).Normalize();

            // View looks opposite to wall normal to see the walls
            var viewDirection = groupOrientation.Normalize().Negate();

            // Right direction is parallel to the reference line (parallel to walls)
            var rightDirection = lineDirection;

            // Up direction is always vertical
            var upDirection = XYZ.BasisZ;

            // Position view origin away from walls
            var sectionOrigin = centerPoint + (viewDirection * VIEW_DISTANCE_FROM_WALLS);

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
                Min = new XYZ(-width / 2, -VIEW_DEPTH / 2, minZ),
                Max = new XYZ(width / 2, VIEW_DEPTH / 2, maxZ)
            };
        }

        /// <summary>
        /// Configures view properties (name, far clip, crop box)
        /// </summary>
        private void ConfigureView(Document doc, ViewSection sectionView, WallGroup group)
        {
            string viewName = DirectionNaming.BuildViewNameFromNormal(group.Orientation);
            var baseName = $"Wall Projection - {viewName}";
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

        #endregion

        #region Region Creation

        /// <summary>
        /// Creates filled regions for walls in the elevation view
        /// </summary>
        private List<WallInfo> CreateRegionsForWalls(Document doc, ViewSection elevationView, WallGroup group)
        {
            var wallsWithRegions = new List<WallInfo>();

            try
            {
                var viewOrigin = elevationView.Origin;
                var viewDirection = elevationView.ViewDirection;
                var viewRightDirection = elevationView.RightDirection;
                var viewUpDirection = elevationView.UpDirection;

                LogViewCoordinateSystem(viewOrigin, viewDirection, viewRightDirection, viewUpDirection);

                // Log the reference line direction to help diagnose angled views
                _logger.LogInformation($"Reference line direction: X={group.Orientation.X:F3}, Y={group.Orientation.Y:F3}");
                var angle = Math.Atan2(group.Orientation.Y, group.Orientation.X) * 180 / Math.PI;
                _logger.LogInformation($"Reference line angle from X-axis: {angle:F2}°");

                EnsureSketchPlaneExists(doc, elevationView, viewDirection, viewOrigin);

                // Use the walls that were assigned to this reference line during detection
                var wallsToProject = group.Walls;

                _logger.LogInformation($"Creating regions for {wallsToProject.Count} walls in view {elevationView.Name} (group: {group.GroupName})");

                foreach (var wallInfo in wallsToProject)
                {
                    bool success = CreateRegionForWall(doc, elevationView, wallInfo, viewOrigin, viewRightDirection, viewUpDirection);
                    if (success)
                    {
                        wallsWithRegions.Add(wallInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating regions for walls in group {group.GroupName}", ex);
            }

            return wallsWithRegions;
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
        /// Creates a filled region for a single wall using Revit's geometry projection
        /// Applies ceiling trimming if top-most ceilings are detected
        /// Returns true if region was successfully created and links the region to the wall
        /// </summary>
        private bool CreateRegionForWall(Document doc, ViewSection elevationView, WallInfo wallInfo,
            XYZ viewOrigin, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            try
            {
                var wall = wallInfo.Wall;

                _logger.LogInformation($"=== Processing wall {wall.Id.Value} for region creation ===");

                // Get geometry as it appears in the section view - this is the key!
                // Note: When View is set, DetailLevel is automatically taken from the view
                var options = new Options
                {
                    View = elevationView,
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false
                };

                var geometryElement = wall.get_Geometry(options);

                if (geometryElement == null)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} has no geometry in section view");
                    return false;
                }

                // Extract curve loops from the geometry as it appears in the view
                var curveLoops = ExtractCurveLoopsFromGeometry(geometryElement, wall.Id);

                if (curveLoops == null || curveLoops.Count == 0)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} produced no curve loops in section view");
                    return false;
                }

                _logger.LogInformation($"Wall {wall.Id.Value} extracted {curveLoops.Count} curve loops from view geometry");

                // Project all curve loops onto the view's sketch plane
                // First loop is the outer boundary, subsequent loops are openings (windows/doors)
                var projectedLoops = new List<CurveLoop>();
                foreach (var loop in curveLoops)
                {
                    var projectedLoop = ProjectCurveLoopToViewPlane(loop, viewOrigin, viewRightDirection, viewUpDirection, wall.Id);
                    if (projectedLoop != null)
                    {
                        projectedLoops.Add(projectedLoop);
                    }
                }

                if (projectedLoops.Count == 0)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} failed to project any curve loops to view plane");
                    return false;
                }

                _logger.LogInformation($"Wall {wall.Id.Value}: Successfully projected {projectedLoops.Count} curve loops (1 outer + {projectedLoops.Count - 1} openings)");

                // Check if we need to trim by top-most ceiling (only applies to outer loop)
                var trimmedLoops = ApplyCeilingTrimming(doc, wall, projectedLoops[0], viewOrigin, viewRightDirection, viewUpDirection);

                // If ceiling trimming split the outer loop into multiple pieces, we need to handle each piece separately
                // For now, use the first trimmed loop as the outer boundary
                var finalOuterLoop = trimmedLoops[0];

                // Build the final list: trimmed outer loop + all opening loops
                var finalLoops = new List<CurveLoop> { finalOuterLoop };
                if (projectedLoops.Count > 1)
                {
                    // Add opening loops (skip the first one which is the outer loop)
                    finalLoops.AddRange(projectedLoops.Skip(1));
                }

                // Try to create filled region(s) with the extracted geometry
                if (!wallInfo.LimitingDistance.HasValue)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} has no limiting distance");
                    return false;
                }

                var distanceGroup = GetDistanceGroupForWall(wallInfo.LimitingDistance.Value);
                _logger.LogInformation($"Wall {wall.Id.Value}: Limiting distance = {wallInfo.LimitingDistance.Value:F2} ft, Distance group: {distanceGroup.Label}");

                var filledRegionType = GetOrCreateColoredFilledRegionType(doc, distanceGroup);

                if (filledRegionType != null)
                {
                    _logger.LogInformation($"Wall {wall.Id.Value}: Using filled region type '{filledRegionType.Name}' (Id: {filledRegionType.Id.Value})");
                    _logger.LogInformation($"Wall {wall.Id.Value}: Creating filled region with {finalLoops.Count} loops (1 outer + {finalLoops.Count - 1} openings)");

                    // Create a single filled region with all loops (outer boundary + openings)
                    var region = FilledRegion.Create(doc, filledRegionType.Id, elevationView.Id, finalLoops);
                    _logger.LogInformation($"Successfully created region for wall {wall.Id.Value} with openings cut out");

                    // Link the created region to the wall for area calculation
                    wallInfo.LinkedRegion = region;
                    _logger.LogInformation($"Linked region {region.Id.Value} to wall {wall.Id.Value}");

                    // Calculate and update the Gross Area from the region's area
                    UpdateWallGrossAreaFromRegion(wallInfo, region);

                    return true;
                }
                else
                {
                    _logger.LogWarning($"Wall {wall.Id.Value}: Failed to get/create filled region type for distance group '{distanceGroup.Label}'");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not create region for wall {wallInfo.ElementId.Value}: {ex.Message}", ex);
                _logger.LogError($"Wall orientation: X={wallInfo.Orientation?.X:F3}, Y={wallInfo.Orientation?.Y:F3}");
                _logger.LogError($"View Right: X={viewRightDirection.X:F3}, Y={viewRightDirection.Y:F3}, Z={viewRightDirection.Z:F3}");
                _logger.LogError($"View Up: X={viewUpDirection.X:F3}, Y={viewUpDirection.Y:F3}, Z={viewUpDirection.Z:F3}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Projects a curve loop from 3D world coordinates onto the view's sketch plane
        /// </summary>
        private CurveLoop? ProjectCurveLoopToViewPlane(CurveLoop loop, XYZ viewOrigin, XYZ viewRight, XYZ viewUp, ElementId wallId)
        {
            try
            {
                var projectedLoop = new CurveLoop();

                foreach (Curve curve in loop)
                {
                    var startPoint = curve.GetEndPoint(0);
                    var endPoint = curve.GetEndPoint(1);

                    // Project both points onto the view plane
                    var projectedStart = ProjectPointToViewPlane(startPoint, viewOrigin, viewRight, viewUp);
                    var projectedEnd = ProjectPointToViewPlane(endPoint, viewOrigin, viewRight, viewUp);

                    // Check if points are too close (degenerate curve)
                    if (projectedStart.DistanceTo(projectedEnd) > POINT_EQUALITY_TOLERANCE)
                    {
                        var projectedCurve = Line.CreateBound(projectedStart, projectedEnd);
                        projectedLoop.Append(projectedCurve);
                    }
                    else
                    {
                        _logger.LogWarning($"Wall {wallId.Value}: Skipping degenerate curve (length: {projectedStart.DistanceTo(projectedEnd):F6})");
                    }
                }

                if (projectedLoop.NumberOfCurves() < 3)
                {
                    _logger.LogWarning($"Wall {wallId.Value}: Projected loop has only {projectedLoop.NumberOfCurves()} curves, need at least 3");
                    return null;
                }

                if (projectedLoop.IsOpen())
                {
                    _logger.LogWarning($"Wall {wallId.Value}: Projected loop is open");
                    return null;
                }

                _logger.LogInformation($"Wall {wallId.Value}: Successfully projected {projectedLoop.NumberOfCurves()} curves to view plane");
                return projectedLoop;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error projecting curve loop for wall {wallId.Value}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Trims a curve loop at a specific height (Z coordinate in the projected view plane)
        /// </summary>
        private CurveLoop? TrimCurveLoopAtHeight(CurveLoop loop, double maxHeight, ElementId wallId)
        {
            try
            {
                // Get all points from the loop
                var points = new List<XYZ>();
                foreach (Curve curve in loop)
                {
                    points.Add(curve.GetEndPoint(0));
                }

                // Find min and max Z (vertical in the view)
                double minZ = points.Min(p => p.Z);
                double maxZ = points.Max(p => p.Z);

                _logger.LogInformation($"Wall {wallId.Value}: Original loop Z range: [{minZ:F3}, {maxZ:F3}], trimming to max {maxHeight:F3}");

                // If the ceiling is above the wall top, no trimming needed
                if (maxHeight >= maxZ)
                {
                    _logger.LogInformation($"  NOT trimmed - ceiling ({maxHeight:F3}) is at or above wall top ({maxZ:F3})");
                    return loop;
                }

                // If the ceiling is below the wall base, the wall is completely hidden
                if (maxHeight <= minZ)
                {
                    _logger.LogWarning($"Wall {wallId.Value}: Ceiling ({maxHeight:F3}) is below wall base ({minZ:F3}), wall completely hidden");
                    return null;
                }

                // The wall needs trimming - modify the top edge
                // Assume rectangular wall: bottom-left, bottom-right, top-right, top-left
                var trimmedPoints = new List<XYZ>();

                foreach (var point in points)
                {
                    if (point.Z <= maxHeight)
                    {
                        // Point is below ceiling, keep as is
                        trimmedPoints.Add(point);
                    }
                    else
                    {
                        // Point is above ceiling, clamp to ceiling height
                        trimmedPoints.Add(new XYZ(point.X, point.Y, maxHeight));
                    }
                }

                // Create new curve loop from trimmed points
                var trimmedLoop = new CurveLoop();
                for (int i = 0; i < trimmedPoints.Count; i++)
                {
                    var nextIndex = (i + 1) % trimmedPoints.Count;
                    var p1 = trimmedPoints[i];
                    var p2 = trimmedPoints[nextIndex];

                    if (p1.DistanceTo(p2) > POINT_EQUALITY_TOLERANCE)
                    {
                        trimmedLoop.Append(Line.CreateBound(p1, p2));
                    }
                }

                if (trimmedLoop.NumberOfCurves() >= 3 && !trimmedLoop.IsOpen())
                {
                    _logger.LogInformation($"  *** TRIMMED *** New Z max: {maxHeight:F3} (was {maxZ:F3})");
                    return trimmedLoop;
                }
                else
                {
                    _logger.LogWarning($"Wall {wallId.Value}: Trimmed loop is invalid (curves: {trimmedLoop.NumberOfCurves()}, open: {trimmedLoop.IsOpen()})");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error trimming curve loop for wall {wallId.Value}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Applies ceiling trimming to a wall's curve loop if top-most ceilings are detected
        /// Returns one or more trimmed curve loops (wall may be split by ceiling)
        /// </summary>
        private List<CurveLoop> ApplyCeilingTrimming(Document doc, Wall wall, CurveLoop outerLoop,
            XYZ viewOrigin, XYZ viewRightDirection, XYZ viewUpDirection)
        {
            var resultLoops = new List<CurveLoop>();

            try
            {
                // Get connected ceilings
                var connectedCeilings = Helpers.CeilingWallAnalyzer.GetConnectedCeilings(wall, doc);
                _logger.LogInformation($"Wall {wall.Id.Value}: Found {connectedCeilings.Count} connected ceiling(s)");

                double? maxWallHeightIn2D = null;

                // Find the top-most ceiling that limits the wall
                foreach (var relationship in connectedCeilings)
                {
                    _logger.LogInformation($"  Checking ceiling {relationship.Ceiling.Id.Value}: IsJoined={relationship.IsJoined}");

                    // Check if ceiling is top-most
                    var isTopMostParam = relationship.Ceiling.LookupParameter("IsTopMost");
                    if (isTopMostParam != null && isTopMostParam.StorageType == StorageType.Integer && isTopMostParam.AsInteger() == 1)
                    {
                        // Get ceiling elevation in 3D world coordinates
                        var ceilingElevation3D = relationship.CeilingElevation;
                        var wallBaseElevation = relationship.WallBaseElevation;
                        var wallTopElevation = relationship.WallTopElevation;

                        // Project a point at ceiling elevation to the 2D view plane
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
                            }
                            else
                            {
                                _logger.LogInformation($"    Not selected (higher than current limit {maxWallHeightIn2D.Value:F3})");
                            }
                        }
                    }
                }

                // If there's a top-most ceiling, trim the wall curve loop
                if (maxWallHeightIn2D.HasValue)
                {
                    _logger.LogInformation($"Wall {wall.Id.Value}: Applying ceiling trim at height {maxWallHeightIn2D.Value:F3}");

                    // The outerLoop is already projected onto the view plane
                    // Extract the actual 2D coordinates (in view's local coordinate system)
                    var trimmedLoop = TrimCurveLoopAtHeight(outerLoop, maxWallHeightIn2D.Value, wall.Id);

                    if (trimmedLoop != null)
                    {
                        resultLoops.Add(trimmedLoop);
                        _logger.LogInformation($"Wall {wall.Id.Value}: Successfully trimmed curve loop at ceiling height");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to trim curve loop for wall {wall.Id.Value}, using original");
                        resultLoops.Add(outerLoop);
                    }
                }
                else
                {
                    _logger.LogInformation($"Wall {wall.Id.Value}: No top-most ceiling found, no trimming");
                    resultLoops.Add(outerLoop);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying ceiling trimming for wall {wall.Id.Value}: {ex.Message}", ex);
                // Fall back to original loop if trimming fails
                resultLoops.Add(outerLoop);
            }

            return resultLoops;
        }

        /// <summary>
        /// Extracts curve loops from geometry element (as it appears in the view)
        /// </summary>
        private List<CurveLoop> ExtractCurveLoopsFromGeometry(GeometryElement geometryElement, ElementId wallId)
        {
            var curveLoops = new List<CurveLoop>();

            try
            {
                foreach (GeometryObject geomObj in geometryElement)
                {
                    if (geomObj is Solid solid && solid.Faces.Size > 0)
                    {
                        _logger.LogInformation($"Wall {wallId.Value}: Found solid with {solid.Faces.Size} faces");

                        // Get the largest planar face (this should be the wall face in the section)
                        Face largestFace = null;
                        double largestArea = 0;

                        foreach (Face face in solid.Faces)
                        {
                            if (face is PlanarFace planarFace)
                            {
                                double area = face.Area;
                                if (area > largestArea)
                                {
                                    largestArea = area;
                                    largestFace = face;
                                }
                            }
                        }

                        if (largestFace != null)
                        {
                            _logger.LogInformation($"Wall {wallId.Value}: Using largest planar face with area {largestArea:F2} ft²");

                            // Get edge loops from the face
                            var edgeArrays = largestFace.EdgeLoops;
                            foreach (EdgeArray edgeArray in edgeArrays)
                            {
                                var curveLoop = new CurveLoop();
                                foreach (Edge edge in edgeArray)
                                {
                                    var curve = edge.AsCurve();
                                    curveLoop.Append(curve);
                                }

                                if (curveLoop.NumberOfCurves() > 0)
                                {
                                    curveLoops.Add(curveLoop);
                                    _logger.LogInformation($"Wall {wallId.Value}: Extracted curve loop with {curveLoop.NumberOfCurves()} curves");
                                }
                            }
                        }
                    }
                    else if (geomObj is GeometryInstance geometryInstance)
                    {
                        // Recursively process geometry instances
                        var instGeometry = geometryInstance.GetInstanceGeometry();
                        if (instGeometry != null)
                        {
                            var nestedLoops = ExtractCurveLoopsFromGeometry(instGeometry, wallId);
                            curveLoops.AddRange(nestedLoops);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting curve loops for wall {wallId.Value}: {ex.Message}", ex);
            }

            return curveLoops;
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

        /// <summary>
        /// Updates the wall's Gross Area based on the created region's area
        /// The region area represents the actual visible wall area in the section view
        /// Updates are dispatched to the UI thread to ensure DataGrid refreshes
        /// </summary>
        private void UpdateWallGrossAreaFromRegion(WallInfo wallInfo, FilledRegion region)
        {
            try
            {
                // Get the area parameter from the region
                var areaParam = region.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null && areaParam.HasValue)
                {
                    var regionArea = areaParam.AsDouble();
                    var oldGrossArea = wallInfo.GrossArea;

                    // Update the property on the UI thread to ensure DataGrid binding updates
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        wallInfo.GrossArea = regionArea;
                    });

                    _logger.LogInformation($"Wall {wallInfo.Wall.Id.Value}: Updated Gross Area from {oldGrossArea:F2} ft² to {regionArea:F2} ft² (based on region area)");
                    _logger.LogInformation($"  Region ID: {region.Id.Value}, Net Area: {wallInfo.NetArea:F2} ft²");
                }
                else
                {
                    _logger.LogWarning($"Wall {wallInfo.Wall.Id.Value}: Could not read area from region {region.Id.Value}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating wall gross area from region: {ex.Message}", ex);
            }
        }

        #endregion

        #region Distance Groups and Colors

        /// <summary>
        /// Gets the distance group classification for a wall's limiting distance
        /// Uses NBCConfigurationService to load distance ranges from NBCRequirements.json
        /// </summary>
        private (double Min, double Max, string Label, int ColorIndex) GetDistanceGroupForWall(double limitingDistance)
        {
            // Load ranges from NBC configuration
            var ranges = _nbcConfig.Configuration.GetDistanceRangesAsTuples();

            foreach (var range in ranges)
            {
                // For the last range, only check lower bound since Max could be infinity
                // For all other ranges, check both bounds with < for exclusive upper bound
                if (limitingDistance >= range.Min && (limitingDistance < range.Max || range.Max >= 999000))
                    return range;
            }

            // Fallback to last range if available
            if (ranges.Count > 0)
                return ranges[^1];

            // Ultimate fallback if no ranges configured
            _logger.LogWarning($"No distance range found for {limitingDistance} ft, using fallback");
            return (0, double.MaxValue, "Unknown", 0);
        }

        /// <summary>
        /// Gets or creates a filled region type with the appropriate color for a distance group
        /// </summary>
        private FilledRegionType? GetOrCreateColoredFilledRegionType(Document doc,
            (double Min, double Max, string Label, int ColorIndex) distanceGroup)
        {
            try
            {
                var typeName = $"LD_{distanceGroup.Label.Replace(" ", "_").Replace("(", "").Replace("(", "").Replace(")", "").Replace("+", "plus")}";

                var existingType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault(frt => frt.Name == typeName);

                if (existingType != null)
                {
                    // Always update the pattern to ensure solid fill is applied
                    // (in case the type was created before solid fill was implemented)
                    ApplyColorToFilledRegionType(doc, existingType, distanceGroup);
                    _logger.LogInformation($"Updated existing filled region type '{typeName}' for distance group '{distanceGroup.Label}'");
                    return existingType;
                }

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
        /// Applies color to a filled region type with solid fill pattern
        /// </summary>
        private void ApplyColorToFilledRegionType(Document doc, FilledRegionType newType,
            (double Min, double Max, string Label, int ColorIndex) distanceGroup)
        {
            _logger.LogInformation($"ApplyColorToFilledRegionType called for distance group '{distanceGroup.Label}' (ColorIndex: {distanceGroup.ColorIndex})");

            var color = GetColorFromDistanceGroupIndex(distanceGroup.ColorIndex);
            var revitColor = new Autodesk.Revit.DB.Color(color.Red, color.Green, color.Blue);

            _logger.LogInformation($"Color for group '{distanceGroup.Label}': RGB({color.Red}, {color.Green}, {color.Blue})");

            // Get solid fill pattern
            var solidPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

            if (solidPattern != null)
            {
                _logger.LogInformation($"Found solid fill pattern (Id: {solidPattern.Id.Value}, Name: {solidPattern.Name})");

                // Set foreground pattern to solid fill with the specified color
                newType.ForegroundPatternId = solidPattern.Id;
                newType.ForegroundPatternColor = revitColor;
                _logger.LogInformation($"Applied solid fill pattern with color RGB({color.Red}, {color.Green}, {color.Blue}) to region type '{newType.Name}'");
            }
            else
            {
                _logger.LogWarning($"Solid fill pattern not found in document!");

                // Fallback: just set color if solid pattern not found
                if (newType.ForegroundPatternId != ElementId.InvalidElementId)
                {
                    newType.ForegroundPatternColor = revitColor;
                    _logger.LogInformation($"Set foreground color to existing pattern (Id: {newType.ForegroundPatternId.Value})");
                }

                if (newType.BackgroundPatternId != ElementId.InvalidElementId)
                {
                    newType.BackgroundPatternColor = revitColor;
                    _logger.LogInformation($"Set background color to existing pattern (Id: {newType.BackgroundPatternId.Value})");
                }

                _logger.LogWarning($"Applied color RGB({color.Red}, {color.Green}, {color.Blue}) to existing pattern instead of solid fill");
            }
        }

        /// <summary>
        /// Gets the RGB color values for a distance group index (green to red gradient)
        /// Uses NBCConfigurationService to load colors from NBCRequirements.json
        /// </summary>
        private (byte Red, byte Green, byte Blue) GetColorFromDistanceGroupIndex(int colorIndex)
        {
            // Load colors from NBC configuration
            var colors = _nbcConfig.Configuration.GetColorsAsTuples();

            if (colors.Count > 0 && colorIndex >= 0 && colorIndex < colors.Count)
            {
                return colors[colorIndex];
            }

            // Fallback: return red if index out of range or no colors configured
            _logger.LogWarning($"Color index {colorIndex} out of range (total: {colors.Count}), using fallback red");
            return (255, 0, 0);
        }

        #endregion

        #region NBC Compliance Report

        /// <summary>
        /// Creates the NBC compliance table data structure with actual calculated results
        /// </summary>
        private ImportedTableData CreateNBCComplianceTableData()
        {
            // Load table formatting from NBC configuration
            var tableFormatting = _nbcConfig.Configuration.TableFormatting;

            var tableData = new ImportedTableData
            {
                Headers = new List<string>(tableFormatting.Headers),
                Rows = new List<List<string>>(),
                MergedCells = new List<MergedCellRange>(),
                CellFormats = new List<CellFormat>(),
                ColumnWidths = new List<double>(tableFormatting.ColumnWidths),
                RowHeights = new List<double>()
            };

            // Group walls by orientation (only walls with regions/projections created)
            var orientationGroups = _perimeterWalls
                .Where(w => w.LinkedRegion != null)
                .GroupBy(w => w.Orientation)
                .OrderBy(g => g.Key);

            _logger.LogInformation($"Processing {orientationGroups.Count()} orientation groups for NBC report");

            foreach (IGrouping<XYZ?, WallInfo>? group in orientationGroups)
            {
                var orientationNumber = group.Key;
                var orientationName = DirectionNaming.BuildViewNameFromNormal(group.Key);
                var wallsInOrientation = group.ToList();

                // Calculate totals for this orientation
                double totalGrossArea = wallsInOrientation.Sum(w => w.GrossArea);
                double totalOpeningArea = wallsInOrientation.Sum(w => w.OpeningsArea);
                double openingPercentage = totalGrossArea > 0 ? (totalOpeningArea / totalGrossArea * 100) : 0;

                // Get minimum limiting distance for this orientation (in meters)
                double minLimitingDistance = double.MaxValue;
                foreach (var wall in wallsInOrientation)
                {
                    if (wall.LimitingDistance.HasValue)
                    {
                        double distanceMeters = wall.LimitingDistance.Value / 3.28084; // Convert feet to meters
                        if (distanceMeters < minLimitingDistance)
                            minLimitingDistance = distanceMeters;
                    }
                }

                // Convert areas from ft² to m²
                double totalGrossAreaM2 = totalGrossArea * 0.092903;

                // Determine NBC requirements based on limiting distance
                var (maxAllowance, frr, construction, cladding) = GetNBCRequirements(minLimitingDistance, totalGrossAreaM2);

                // Add main row for this orientation
                var status = openingPercentage <= maxAllowance ? "✓" : "✗";

                tableData.Rows.Add(new List<string>
                {
                    orientationName,
                    "Group D",
                    minLimitingDistance == double.MaxValue ? "N/A" : $"{minLimitingDistance:F1}",
                    $"{totalGrossAreaM2:F1}",
                    $"{maxAllowance:F0}",
                    $"{openingPercentage:F1}",
                    frr,
                    construction,
                    cladding,
                    status
                });
            }

            // Set all rows to same height using NBC configuration
            for (int i = 0; i < tableData.Rows.Count + 1; i++) // +1 for header
            {
                tableData.RowHeights.Add(tableFormatting.RowHeightFeet);
            }

            _logger.LogInformation($"Created NBC compliance report with {tableData.Rows.Count} rows");
            return tableData;
        }

        /// <summary>
        /// Get NBC requirements based on limiting distance and area
        /// Uses NBCConfigurationService to load requirements from NBCRequirements.json
        /// </summary>
        private (double maxAllowance, string frr, string construction, string cladding) GetNBCRequirements(double limitingDistanceM, double areaM2)
        {
            // Default to Group D (Residential) - can be parameterized later
            const string classificationKey = "GroupD";

            var requirement = _nbcConfig.GetRequirement(classificationKey, limitingDistanceM);

            if (requirement != null)
            {
                return (
                    requirement.MaxUnprotectedOpeningPercent,
                    requirement.FireResistanceRating,
                    requirement.ConstructionType,
                    requirement.CladdingType
                );
            }

            // Fallback to most restrictive requirements if no match found
            _logger.LogWarning($"No NBC requirement found for {classificationKey} at {limitingDistanceM}m, using fallback");
            return (0, "45min", "Noncombustible", "Noncombustible");
        }

        /// <summary>
        /// Generates NBC Compliance Report as a Drafting View
        /// </summary>
        private void GenerateNBCComplianceReport(Document doc)
        {
            try
            {
                _logger.LogInformation("Generating NBC Compliance Report table");

                // Generate hard-coded NBC compliance table
                ImportedTableData tableData = CreateNBCComplianceTableData();

                // Create drafting view and render table within a transaction
                using (var transaction = new Transaction(doc, "Create NBC Compliance Report"))
                {
                    transaction.Start();

                    try
                    {
                        // Get table render service
                        var tableRenderService = DIContainerService.Container.GetInstance<ITableRenderService>();

                        // Create drafting view
                        var view = tableRenderService.CreateDraftingView(doc, "NBC Compliance Report", 48);
                        _logger.LogInformation($"Created drafting view: {view.Name}");

                        // Render the table
                        var options = new TableRenderOptions
                        {
                            ViewScale = 48, // 1/4" = 1'-0"
                            PaperTextHeight = 0.125, // 1/8" text
                            DrawGridLines = true,
                            TextStyleName = "Standard",
                            ColumnWidth = 1.5,
                            RowHeight = 0.167
                        };

                        tableRenderService.RenderToView(doc, view, tableData, options);
                        _logger.LogInformation("Table rendered successfully");

                        transaction.Commit();

                        TaskDialog.Show("Success", $"NBC Compliance Report generated:\n{view.Name}\n\nRows: {tableData.Rows.Count}");
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating NBC Compliance Report: {ex.Message}\nStack: {ex.StackTrace}", ex);
                TaskDialog.Show("Error", $"Error generating NBC Compliance Report:\n{ex.Message}\n\nSee log for details.");
                // Don't throw - allow wall projections to succeed even if report fails
            }
        }

        #endregion
    }
}
