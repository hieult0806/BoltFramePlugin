using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;

namespace LoBIM.Features.ViewCloning.Strategies
{
    /// <summary>
    /// Strategy interface for cloning different types of views
    /// </summary>
    public interface IViewCloningStrategy
    {
        /// <summary>
        /// Determines if this strategy can handle the given view type
        /// </summary>
        bool CanHandle(ViewType viewType);

        /// <summary>
        /// Clones a view from the linked document to the host document
        /// </summary>
        /// <param name="hostDoc">The host document where the view will be cloned</param>
        /// <param name="sourceView">The source view from the linked document</param>
        /// <param name="linkedDoc">The linked document containing the source view</param>
        /// <param name="linkInstance">The link instance</param>
        /// <param name="namePrefix">Optional prefix for the cloned view name</param>
        /// <param name="positioningMode">How to position the cloned view</param>
        /// <returns>The cloned view, or null if cloning failed</returns>
        Autodesk.Revit.DB.View CloneView(
            Document hostDoc,
            Autodesk.Revit.DB.View sourceView,
            Document linkedDoc,
            RevitLinkInstance linkInstance,
            string namePrefix,
            ViewPositioningMode positioningMode);
    }
}
