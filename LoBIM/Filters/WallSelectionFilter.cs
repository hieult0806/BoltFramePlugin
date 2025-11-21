using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace LoBIM.Filters
{
    public class WallSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_Walls;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // We are filtering based on elements, not geometry references.
        }
    }
}