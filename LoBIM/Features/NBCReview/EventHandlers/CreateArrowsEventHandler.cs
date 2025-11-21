using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;
using LoBIM.Features.NBCReview.Models;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.NBCReview.EventHandlers
{
    internal class CreateArrowsEventHandler : IExternalEventHandler
    {
        private List<WallInfo> _perimeterWalls;
        private UIDocument _uidoc;
        private readonly ILoggingService _logger;

        public CreateArrowsEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _perimeterWalls = new List<WallInfo>();
        }

        public void SetParameters(UIDocument uidoc, List<WallInfo> perimeterWalls)
        {
            _uidoc = uidoc;
            _perimeterWalls = perimeterWalls;
            _logger.LogInformation($"Parameters set - Walls count: {perimeterWalls?.Count ?? 0}");
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _logger.LogInformation("CreateArrowsEventHandler.Execute started");

                if (_uidoc == null || _perimeterWalls == null || _perimeterWalls.Count == 0)
                {
                    _logger.LogError("UIDocument or Walls list is null/empty in Execute");
                    TaskDialog.Show("Error", "Internal error: No walls to process");
                    return;
                }

                var doc = _uidoc.Document;
                int arrowsCreated = 0;
                int groupsCreated = 0;

                // Dictionary to group arrows by floor plan view
                var arrowsByView = new Dictionary<ElementId, List<ElementId>>();

                using (var transaction = new Transaction(doc, "Create Wall Normal Arrows"))
                {
                    transaction.Start();

                    foreach (var wallInfo in _perimeterWalls)
                    {
                        var wall = wallInfo.Wall;

                        // Get the wall's base constraint level
                        var baseLevel = GetWallBaseLevel(wall, doc);
                        if (baseLevel == null)
                        {
                            _logger.LogWarning($"Could not find base level for wall {wall.Id.Value}");
                            continue;
                        }

                        // Find the floor plan view for this level
                        var floorPlan = GetFloorPlanView(doc, baseLevel);
                        if (floorPlan == null)
                        {
                            _logger.LogWarning($"Could not find floor plan for level {baseLevel.Name}");
                            continue;
                        }

                        // Create arrow detail line on the floor plan
                        var arrowElementIds = CreateArrowDetailLine(doc, wall, floorPlan);
                        if (arrowElementIds != null && arrowElementIds.Count > 0)
                        {
                            // Add to the dictionary grouped by view
                            if (!arrowsByView.ContainsKey(floorPlan.Id))
                            {
                                arrowsByView[floorPlan.Id] = new List<ElementId>();
                            }
                            arrowsByView[floorPlan.Id].AddRange(arrowElementIds);

                            arrowsCreated++;
                            _logger.LogInformation($"Created arrow for wall {wall.Id.Value} on {floorPlan.Name}");
                        }
                    }

                    // Create separate groups for each floor plan view
                    foreach (var kvp in arrowsByView)
                    {
                        var viewId = kvp.Key;
                        var arrowIds = kvp.Value;

                        if (arrowIds.Count > 0)
                        {
                            try
                            {
                                var view = doc.GetElement(viewId) as ViewPlan;
                                var group = doc.Create.NewGroup(arrowIds);
                                if (group != null)
                                {
                                    var viewName = view?.Name ?? "Unknown";
                                    group.GroupType.Name = $"Wall Normal Arrows - {viewName}";
                                    groupsCreated++;
                                    _logger.LogInformation($"Created group with {arrowIds.Count} arrows on {viewName}.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Could not create group for arrows on view: {ex.Message}");
                                // Continue without grouping - arrows are still created
                            }
                        }
                    }

                    transaction.Commit();
                }

                _logger.LogInformation($"Created {arrowsCreated} arrow detail lines in {groupsCreated} groups.");
                TaskDialog.Show("Success", $"Created {arrowsCreated} arrow detail lines showing wall normals on their respective floor plans.\n\nArrows have been organized into {groupsCreated} group(s) by floor plan.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateArrowsEventHandler.Execute: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Error creating arrows: {ex.Message}");
            }
        }

        private Level? GetWallBaseLevel(Wall wall, Document doc)
        {
            try
            {
                var baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                if (baseLevelParam != null && baseLevelParam.AsElementId() != ElementId.InvalidElementId)
                {
                    var level = doc.GetElement(baseLevelParam.AsElementId()) as Level;
                    return level;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting base level for wall {wall.Id.Value}", ex);
                return null;
            }
        }

        private ViewPlan? GetFloorPlanView(Document doc, Level level)
        {
            try
            {
                var floorPlans = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == level.Id)
                    .FirstOrDefault();

                return floorPlans;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error finding floor plan for level {level.Name}", ex);
                return null;
            }
        }

        private List<ElementId> CreateArrowDetailLine(Document doc, Wall wall, ViewPlan floorPlan)
        {
            try
            {
                var elementIds = new List<ElementId>();

                // Get the wall location curve
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null)
                    return elementIds;

                var wallCurve = locationCurve.Curve;

                // Get the midpoint of the wall
                var midpoint = wallCurve.Evaluate(0.5, true);

                // Get the wall's orientation (from interior to exterior)
                var wallOrientation = wall.Orientation;
                var normal = new XYZ(wallOrientation.X, wallOrientation.Y, 0).Normalize();

                // Create arrow pointing in the normal direction
                var arrowLength = 3.0; // 3 feet arrow length
                var arrowEnd = midpoint + (normal * arrowLength);

                // Create the main arrow line
                var arrowLine = Line.CreateBound(midpoint, arrowEnd);
                var detailLine = doc.Create.NewDetailCurve(floorPlan, arrowLine);

                // Get arrow line style (or use default)
                var lineStyle = GetArrowLineStyle(doc);
                if (lineStyle != null && detailLine is DetailLine dl)
                {
                    dl.LineStyle = lineStyle;
                }

                // Create arrow head (small triangle at the end)
                var arrowSize = 0.5; // feet (~6 inches)
                var perpendicular = new XYZ(-normal.Y, normal.X, 0);

                // Arrow head base point (slightly back from the end)
                var arrowBase = arrowEnd - (normal * arrowSize);

                // Arrow wing points
                var arrowWing1 = arrowBase + (perpendicular * arrowSize * 0.3);
                var arrowWing2 = arrowBase - (perpendicular * arrowSize * 0.3);

                // Draw two lines forming the arrow head
                var arrowLine1 = Line.CreateBound(arrowEnd, arrowWing1);
                var arrowLine2 = Line.CreateBound(arrowEnd, arrowWing2);

                var arrowHead1 = doc.Create.NewDetailCurve(floorPlan, arrowLine1);
                var arrowHead2 = doc.Create.NewDetailCurve(floorPlan, arrowLine2);

                // Apply line style to arrow heads as well
                if (lineStyle != null)
                {
                    if (arrowHead1 is DetailLine dl1) dl1.LineStyle = lineStyle;
                    if (arrowHead2 is DetailLine dl2) dl2.LineStyle = lineStyle;
                }

                // Return all element IDs (main line + arrow head)
                elementIds.Add(detailLine.Id);
                elementIds.Add(arrowHead1.Id);
                elementIds.Add(arrowHead2.Id);

                return elementIds;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating arrow for wall {wall.Id.Value}", ex);
                return new List<ElementId>();
            }
        }

        private GraphicsStyle? GetArrowLineStyle(Document doc)
        {
            try
            {
                // Try to find "Arrow" line style, otherwise return null to use default
                var lineStyles = new FilteredElementCollector(doc)
                    .OfClass(typeof(GraphicsStyle))
                    .Cast<GraphicsStyle>()
                    .Where(gs => gs.GraphicsStyleCategory.Name == "Lines" &&
                                 gs.Name.Contains("Arrow", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                return lineStyles;
            }
            catch
            {
                return null;
            }
        }

        public string GetName()
        {
            return "Create Wall Arrows External Event";
        }
    }
}
