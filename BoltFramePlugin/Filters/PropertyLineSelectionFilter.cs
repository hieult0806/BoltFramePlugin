using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BoltFramePlugin.Filters
{
    public class PropertyLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            // Check if the element is a property line
            // Property lines are typically in the OST_SitePropertyLineSegment category
            // if (elem.Category != null &&
            //     elem.Category.Id.Value == (long)BuiltInCategory.OST_SitePropertyLineSegment)
            // {
            //     return true;
            // }

            // Also check for property lines in OST_SiteProperty category
            if (elem.Category != null &&
                elem.Category.Id.Value == (long)BuiltInCategory.OST_SiteProperty)
            {
                return true;
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // We are filtering based on elements, not geometry references.
        }
    }
}
