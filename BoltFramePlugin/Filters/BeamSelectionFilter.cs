using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BoltFramePlugin.Filters
{
    public class BeamSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null && elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // We are filtering based on elements, not geometry references.
        }
    }
}