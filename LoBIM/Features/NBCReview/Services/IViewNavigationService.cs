using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for navigating and highlighting elements in Revit views
    /// </summary>
    public interface IViewNavigationService
    {
        /// <summary>
        /// Highlights walls in a 3D view
        /// </summary>
        void HighlightWallsIn3DView(UIDocument uidoc, List<ElementId> wallIds);

        /// <summary>
        /// Highlights a single wall in a 3D view
        /// </summary>
        void HighlightWallIn3DView(UIDocument uidoc, ElementId wallId);

        /// <summary>
        /// Highlights a wall in its corresponding floor plan view
        /// </summary>
        void HighlightWallInFloorPlan(UIDocument uidoc, Wall wall);

        /// <summary>
        /// Finds the first available 3D view (non-template)
        /// </summary>
        View3D FindFirst3DView(Document doc);

        /// <summary>
        /// Finds the floor plan view for a given level
        /// </summary>
        ViewPlan FindFloorPlanForLevel(Document doc, Level level);
    }
}
