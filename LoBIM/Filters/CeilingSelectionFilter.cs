using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace LoBIM.Filters
{
    /// <summary>
    /// Selection filter for ceiling elements
    /// </summary>
    public class CeilingSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Ceiling;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
