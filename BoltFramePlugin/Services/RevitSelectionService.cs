using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;

namespace BoltFramePlugin.Services
{
    public class RevitSelectionService
    {
        private readonly UIDocument _uiDoc;

        public RevitSelectionService(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
        }

        // Get selected elements filtered by categories (Walls, Floors, Roofs)
        public List<Element> GetSelectedElements()
        {
            Selection sel = _uiDoc.Selection;
            ICollection<ElementId> selectedIds = sel.GetElementIds();

            List<Element> selectedElements = new List<Element>();
            foreach (var id in selectedIds)
            {
                var element = _uiDoc.Document.GetElement(id);
                if (element != null && IsRelevantCategory(element.Category))
                {
                    selectedElements.Add(element);
                }
            }

            return selectedElements;
        }

        // Filter for relevant categories (Walls, Floors, Roofs)
        public static bool IsRelevantCategory(Category category)
        {
            if (category == null)
            {
                return false;
            }

            // Check for Walls, Floors, and Roofs categories
            return category.Id.Value == (long)BuiltInCategory.OST_Walls ||
                   category.Id.Value == (long)BuiltInCategory.OST_Floors ||
                   category.Id.Value == (long)BuiltInCategory.OST_Roofs;
        }
    }
}
