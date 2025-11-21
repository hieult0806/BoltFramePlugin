using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for navigating and highlighting elements in Revit views
    /// </summary>
    public class ViewNavigationService : IViewNavigationService
    {
        private readonly ILoggingService _logger;

        public ViewNavigationService(ILoggingService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void HighlightWallsIn3DView(UIDocument uidoc, List<ElementId> wallIds)
        {
            if (uidoc == null) throw new ArgumentNullException(nameof(uidoc));
            if (wallIds == null) throw new ArgumentNullException(nameof(wallIds));

            _logger.LogInformation($"Highlighting {wallIds.Count} perimeter walls in 3D view...");

            var view3D = FindFirst3DView(uidoc.Document);
            if (view3D == null)
            {
                _logger.LogWarning("No 3D view found.");
                Autodesk.Revit.UI.TaskDialog.Show("No 3D View", "No 3D view found in the document.");
                return;
            }

            // Set the active view to 3D
            uidoc.ActiveView = view3D;

            // Select the walls
            uidoc.Selection.SetElementIds(wallIds);

            _logger.LogInformation($"Highlighted {wallIds.Count} perimeter walls in 3D view.");
        }

        public void HighlightWallIn3DView(UIDocument uidoc, ElementId wallId)
        {
            if (uidoc == null) throw new ArgumentNullException(nameof(uidoc));
            if (wallId == null) throw new ArgumentNullException(nameof(wallId));

            HighlightWallsIn3DView(uidoc, new List<ElementId> { wallId });
        }

        public void HighlightWallInFloorPlan(UIDocument uidoc, Wall wall)
        {
            if (uidoc == null) throw new ArgumentNullException(nameof(uidoc));
            if (wall == null) throw new ArgumentNullException(nameof(wall));

            _logger.LogInformation($"Highlighting wall {wall.Id.Value} in floor plan...");

            var doc = uidoc.Document;

            // Get the wall's base level
            var baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
            if (baseLevelParam != null && baseLevelParam.AsElementId() != ElementId.InvalidElementId)
            {
                var level = doc.GetElement(baseLevelParam.AsElementId()) as Level;
                if (level != null)
                {
                    var floorPlan = FindFloorPlanForLevel(doc, level);
                    if (floorPlan != null)
                    {
                        // Set the active view to floor plan
                        uidoc.ActiveView = floorPlan;

                        // Select the wall
                        uidoc.Selection.SetElementIds(new List<ElementId> { wall.Id });

                        _logger.LogInformation($"Highlighted wall in floor plan {floorPlan.Name}.");
                        return;
                    }
                }
            }

            _logger.LogWarning("Could not find floor plan for this wall's level.");
            Autodesk.Revit.UI.TaskDialog.Show("No Floor Plan", "Could not find floor plan for this wall's level.");
        }

        public View3D FindFirst3DView(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            return new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);
        }

        public ViewPlan FindFloorPlanForLevel(Document doc, Level level)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (level == null) throw new ArgumentNullException(nameof(level));

            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == level.Id);
        }
    }
}
