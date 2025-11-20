using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for highlighting elements in Revit with enhanced visual effects
    /// </summary>
    public interface IElementHighlightService
    {
        /// <summary>
        /// Highlight an element in the active view with enhanced visual effects
        /// </summary>
        /// <param name="uidoc">UI Document</param>
        /// <param name="elementId">Element to highlight</param>
        /// <param name="zoomToElement">Whether to zoom to the element</param>
        /// <param name="useColorOverride">Whether to apply temporary color override</param>
        /// <param name="overrideColor">Color to use for override (null for default cyan)</param>
        void HighlightElement(UIDocument uidoc, ElementId elementId, bool zoomToElement = true, bool useColorOverride = true, Autodesk.Revit.DB.Color overrideColor = null);

        /// <summary>
        /// Highlight an element in a specific view
        /// </summary>
        /// <param name="uidoc">UI Document</param>
        /// <param name="elementId">Element to highlight</param>
        /// <param name="targetView">View to activate</param>
        /// <param name="zoomToElement">Whether to zoom to the element</param>
        /// <param name="useColorOverride">Whether to apply temporary color override</param>
        /// <param name="overrideColor">Color to use for override (null for default cyan)</param>
        void HighlightElementInView(UIDocument uidoc, ElementId elementId, Autodesk.Revit.DB.View targetView, bool zoomToElement = true, bool useColorOverride = true, Autodesk.Revit.DB.Color overrideColor = null);

        /// <summary>
        /// Clear all temporary color overrides in the active view
        /// </summary>
        /// <param name="uidoc">UI Document</param>
        void ClearHighlights(UIDocument uidoc);

        /// <summary>
        /// Clear color override for a specific element
        /// </summary>
        /// <param name="uidoc">UI Document</param>
        /// <param name="elementId">Element to clear</param>
        void ClearElementHighlight(UIDocument uidoc, ElementId elementId);
    }
}
