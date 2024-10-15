using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BoltFramePlugin.Filters
{
    internal class FramingObjectFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            return (element is Wall) || (element
                 is RoofBase) || (element is Floor);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
