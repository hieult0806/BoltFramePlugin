using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Features.NBCReview.Models;
using LoBIM.Services;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for analyzing walls - distance calculations, area calculations, opening analysis
    /// </summary>
    public class WallAnalysisService : IWallAnalysisService
    {
        private readonly ILoggingService _logger;
        private readonly IGeometryService _geometryService;

        public WallAnalysisService(ILoggingService logger, IGeometryService geometryService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _geometryService = geometryService ?? throw new ArgumentNullException(nameof(geometryService));
        }

        public List<WallInfo> FindPerimeterWalls(Document doc, Element propertyLine, double rayLengthLimit, Transform? propertyLineTransform = null)
        {
            var perimeterWalls = new List<WallInfo>();

            try
            {
                _logger.LogInformation("Finding perimeter walls inside property line boundaries...");

                if (propertyLine == null)
                {
                    _logger.LogWarning("No property line selected.");
                    return perimeterWalls;
                }

                // Get the property line boundary curve loop (apply transform if from linked file)
                var propertyLineBoundary = _geometryService.GetPropertyLineBoundary(propertyLine, propertyLineTransform);

                // Get all walls in the host document
                var walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .ToList();

                _logger.LogInformation($"Total walls found in host document: {walls.Count}");

                // Get walls from linked Revit files (with their transforms)
                var linkedWallsWithTransforms = GetWallsFromLinkedFiles(doc);
                _logger.LogInformation($"Total walls found in linked files: {linkedWallsWithTransforms.Count}");

                // Diagnostic counters
                int wallsWithParameter = 0;
                int wallsWithParameterTrue = 0;
                int wallsInsidePropertyLine = 0;

                // Process host document walls
                foreach (var wall in walls)
                {
                    var parameter = wall.LookupParameter("IsPerimeter");

                    if (parameter != null)
                    {
                        wallsWithParameter++;

                        if (parameter.AsInteger() == 1)
                        {
                            wallsWithParameterTrue++;

                            // Check if wall is inside the property line boundary (no transform for host walls)
                            if (IsWallInsidePropertyLine(wall, propertyLineBoundary, null))
                            {
                                wallsInsidePropertyLine++;

                                // Calculate areas
                                var grossArea = CalculateWallGrossArea(wall);
                                var openingsArea = CalculateWallOpeningsArea(wall);

                                var wallInfo = new WallInfo
                                {
                                    Wall = wall,
                                    ElementId = wall.Id,
                                    Name = wall.Name,
                                    Length = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0,
                                    GrossArea = grossArea,
                                    OpeningsArea = openingsArea,
                                    Orientation = wall.Orientation,
                                    LinkTransform = null // Host document wall
                                };

                                perimeterWalls.Add(wallInfo);
                                _logger.LogInformation($"Perimeter wall found inside property line: {wall.Name} (ID: {wall.Id.Value})");
                            }
                        }
                    }
                }

                // Process linked document walls
                foreach (var linkedWallInfo in linkedWallsWithTransforms)
                {
                    var wall = linkedWallInfo.Item1;
                    var transform = linkedWallInfo.Item2;
                    var parameter = wall.LookupParameter("IsPerimeter");

                    if (parameter != null)
                    {
                        wallsWithParameter++;

                        if (parameter.AsInteger() == 1)
                        {
                            wallsWithParameterTrue++;

                            // Check if wall is inside the property line boundary (with transform for linked walls)
                            if (IsWallInsidePropertyLine(wall, propertyLineBoundary, transform))
                            {
                                wallsInsidePropertyLine++;

                                // Calculate areas
                                var grossArea = CalculateWallGrossArea(wall);
                                var openingsArea = CalculateWallOpeningsArea(wall);

                                var wallInfo = new WallInfo
                                {
                                    Wall = wall,
                                    ElementId = wall.Id,
                                    Name = $"{wall.Name} (Linked)",
                                    Length = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0,
                                    GrossArea = grossArea,
                                    OpeningsArea = openingsArea,
                                    Orientation = transform.OfVector(wall.Orientation), // Transform orientation
                                    LinkTransform = transform // Store transform for linked wall
                                };

                                perimeterWalls.Add(wallInfo);
                                _logger.LogInformation($"Perimeter wall found inside property line (linked): {wall.Name} (ID: {wall.Id.Value})");
                            }
                        }
                    }
                }

                // Log diagnostic information
                _logger.LogInformation($"Walls with IsPerimeter parameter: {wallsWithParameter}/{walls.Count + linkedWallsWithTransforms.Count}");
                _logger.LogInformation($"Walls with IsPerimeter=true: {wallsWithParameterTrue}/{wallsWithParameter}");
                _logger.LogInformation($"Walls inside property line: {wallsInsidePropertyLine}/{wallsWithParameterTrue}");
                _logger.LogInformation($"Total perimeter walls found inside property line: {perimeterWalls.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error finding perimeter walls", ex);
            }

            return perimeterWalls;
        }

        public bool IsWallInsidePropertyLine(Wall wall, CurveLoop propertyLineBoundary, Transform? linkTransform = null)
        {
            try
            {
                if (propertyLineBoundary == null)
                {
                    _logger.LogWarning($"Property line boundary is null for wall {wall.Id.Value}");
                    return false;
                }

                // Get the wall location curve
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null)
                {
                    _logger.LogWarning($"Wall {wall.Id.Value} has no location curve");
                    return false;
                }

                var wallCurve = locationCurve.Curve;

                // Get the midpoint of the wall
                var midpoint = wallCurve.Evaluate(0.5, true);

                // Apply transform if this is a linked wall
                if (linkTransform != null)
                {
                    midpoint = linkTransform.OfPoint(midpoint);
                }

                // Extract polygon points from the curve loop
                var polygonPoints = new List<XYZ>();
                foreach (var curve in propertyLineBoundary)
                {
                    polygonPoints.Add(curve.GetEndPoint(0));
                }

                _logger.LogInformation($"Checking wall {wall.Id.Value} at midpoint ({midpoint.X:F2}, {midpoint.Y:F2}) against {polygonPoints.Count} polygon points");

                // Use ray casting algorithm for point-in-polygon test
                bool isInside = _geometryService.IsPointInsidePolygon(midpoint, polygonPoints);

                _logger.LogInformation($"Wall {wall.Id.Value} is {(isInside ? "INSIDE" : "OUTSIDE")} property line");

                return isInside;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking if wall {wall.Id.Value} is inside property line", ex);
                return false;
            }
        }

        public double CalculateWallGrossArea(Wall wall)
        {
            try
            {
                // Try to get the gross area from built-in parameter
                var areaParam = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null && areaParam.HasValue)
                {
                    return areaParam.AsDouble();
                }

                // Fallback: calculate from height and length
                var heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                var lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);

                if (heightParam != null && lengthParam != null)
                {
                    var height = heightParam.AsDouble();
                    var length = lengthParam.AsDouble();
                    return height * length;
                }

                return 0.0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating gross area for wall {wall.Id.Value}", ex);
                return 0.0;
            }
        }

        public double CalculateWallOpeningsArea(Wall wall)
        {
            try
            {
                double totalOpeningsArea = 0.0;

                // Get all inserts (windows, doors, etc.) hosted in this wall
                var insertIds = wall.FindInserts(true, true, true, true);

                foreach (var insertId in insertIds)
                {
                    var insert = wall.Document.GetElement(insertId);

                    if (insert is FamilyInstance familyInstance)
                    {
                        // Try to get the area from the instance
                        var areaParam = familyInstance.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                        if (areaParam != null && areaParam.HasValue)
                        {
                            totalOpeningsArea += areaParam.AsDouble();
                        }
                        else
                        {
                            // Fallback: calculate from width and height parameters
                            var widthParam = familyInstance.Symbol?.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
                            var heightParam = familyInstance.Symbol?.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM);

                            if (widthParam != null && heightParam != null)
                            {
                                var width = widthParam.AsDouble();
                                var height = heightParam.AsDouble();
                                totalOpeningsArea += width * height;
                            }
                        }
                    }
                }

                return totalOpeningsArea;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating openings area for wall {wall.Id.Value}", ex);
                return 0.0;
            }
        }

        public List<DistanceGroupSummary> CalculateDistanceGroupsFromRegions(
            List<WallInfo> wallsWithRegions,
            List<FilledRegionTypeDefinition> distanceRanges)
        {
            var distanceGroups = new List<DistanceGroupSummary>();

            try
            {
                _logger.LogInformation("Calculating distance groups from walls with created regions...");
                _logger.LogInformation($"Walls with regions: {wallsWithRegions?.Count ?? 0}");

                if (wallsWithRegions == null || wallsWithRegions.Count == 0)
                {
                    _logger.LogWarning("No walls with regions to calculate distance groups");
                    return distanceGroups;
                }

                if (distanceRanges == null || distanceRanges.Count == 0)
                {
                    _logger.LogWarning("No distance ranges provided for grouping");
                    return distanceGroups;
                }

                // Group walls by their assigned reference line
                var wallsByOrientation = wallsWithRegions
                    .Where(w => w.LimitingDistance.HasValue && w.ReferenceLine != null)
                    .GroupBy(w => w.Orientation)
                    .ToList();

                _logger.LogInformation($"Grouped into {wallsByOrientation.Count} orientation groups");
                _logger.LogInformation($"Using {distanceRanges.Count} distance ranges from configuration");

                foreach (var orientationGroup in wallsByOrientation)
                {
                    var orientationName = DirectionNaming.BuildViewNameFromNormal(orientationGroup.Key);

                    foreach (var range in distanceRanges)
                    {
                        // For the last range, handle very large max values properly
                        var wallsInRange = orientationGroup
                            .Where(w => w.LimitingDistance.Value >= range.MinDistanceFeet &&
                                       (w.LimitingDistance.Value < range.MaxDistanceFeet || range.MaxDistanceFeet > 900000))
                            .ToList();

                        if (wallsInRange.Any())
                        {
                            var group = new DistanceGroupSummary
                            {
                                Orientation = orientationName,
                                DistanceRange = range.Label,
                                MinDistance = range.MinDistanceFeet,
                                MaxDistance = range.MaxDistanceFeet,
                                TotalGrossArea = wallsInRange.Sum(w => w.GrossArea),
                                TotalOpeningsArea = wallsInRange.Sum(w => w.OpeningsArea)
                            };

                            distanceGroups.Add(group);
                            _logger.LogInformation($"Added group: {orientationName} - {range.Label}");
                        }
                    }
                }

                _logger.LogInformation($"Distance groups calculated: {distanceGroups.Count} groups");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating distance groups from regions", ex);
            }

            return distanceGroups;
        }

        /// <summary>
        /// Gets all walls from linked Revit files with their transforms
        /// </summary>
        private List<(Wall, Transform)> GetWallsFromLinkedFiles(Document hostDoc)
        {
            var linkedWalls = new List<(Wall, Transform)>();

            try
            {
                // Get all RevitLinkInstance elements in the host document
                var linkInstances = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                _logger.LogInformation($"Found {linkInstances.Count} linked Revit files");

                foreach (var linkInstance in linkInstances)
                {
                    try
                    {
                        // Get the linked document
                        var linkedDoc = linkInstance.GetLinkDocument();
                        if (linkedDoc == null)
                        {
                            _logger.LogWarning($"Linked document is not loaded for link instance {linkInstance.Id.Value}");
                            continue;
                        }

                        _logger.LogInformation($"Processing linked file: {linkedDoc.Title}");

                        // Get the transform from the link instance
                        var transform = linkInstance.GetTotalTransform();

                        // Get all walls from the linked document
                        var wallsInLink = new FilteredElementCollector(linkedDoc)
                            .OfClass(typeof(Wall))
                            .Cast<Wall>()
                            .ToList();

                        _logger.LogInformation($"  Found {wallsInLink.Count} walls in {linkedDoc.Title}");

                        // Store walls with their transforms
                        foreach (var wall in wallsInLink)
                        {
                            linkedWalls.Add((wall, transform));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing linked file {linkInstance.Id.Value}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting walls from linked files", ex);
            }

            return linkedWalls;
        }
    }
}
