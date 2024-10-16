using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Models;

namespace BoltFramePlugin.Services
{
    public interface IRevitService
    {
        FamilySymbol GetDefaultBeamFamilySymbol();
        FamilySymbol GetDefaultColumnFamilySymbol();
        Element GetSelectedElement();
        IList<FamilySymbol> LoadColumnTypesFromRevit();
        IList<FamilySymbol> LoadBeamTypesFromRevit();
        Level GetLevelById(ElementId id);
        FamilySymbol GetBeamTypeByUniqueId(AttributeItem attributeItem);
        FamilySymbol GetColumnTypeByUniqueId(AttributeItem attributeItem);
    }
    public class RevitService : IRevitService
    {
        private readonly UIDocument _uidoc;
        public UIDocument UiDoc => _uidoc;

        private readonly Document _doc;
        public Document Document => _doc;

        private string _message;
        private ElementSet _elementSet;

        public RevitService(ExternalCommandData commandData, ElementSet elements)
        {
            _uidoc = commandData.Application.ActiveUIDocument;
            _doc = _uidoc.Document;
            _elementSet = elements;
        }

        public FamilySymbol GetDefaultBeamFamilySymbol()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .FirstOrDefault() as FamilySymbol;
        }

        public FamilySymbol GetDefaultColumnFamilySymbol()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .FirstOrDefault() as FamilySymbol;
        }

        public Element GetSelectedElement()
        {
            var selectedElementId = _uidoc.Selection.GetElementIds().FirstOrDefault();
            return _doc.GetElement(selectedElementId);
        }

        public IList<FamilySymbol> LoadColumnTypesFromRevit()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>()
                .ToList();
        }

        public IList<FamilySymbol> LoadBeamTypesFromRevit()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .ToList();
        }
        public Level GetLevelById(ElementId levelId)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level));

            foreach (Level level in collector)
            {
                if (level.Id.Equals(levelId))
                {
                    return level;
                }
            }

            return null;
        }

        public FamilySymbol GetBeamTypeByUniqueId(AttributeItem attributeItem)
        {
            return new FilteredElementCollector(_doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_StructuralFraming)
                        .Cast<FamilySymbol>() // Ensure the result is cast to FamilySymbol
                        .FirstOrDefault(e => e.UniqueId.Equals(attributeItem.UniqueId));
        }

        public FamilySymbol GetColumnTypeByUniqueId(AttributeItem attributeItem)
        {
            return new FilteredElementCollector(_doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .Cast<FamilySymbol>() // Ensure the result is cast to FamilySymbol
                        .FirstOrDefault(e => e.UniqueId.Equals(attributeItem.UniqueId));
        }
    }
}
