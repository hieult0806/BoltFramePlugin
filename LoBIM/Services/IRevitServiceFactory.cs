using Autodesk.Revit.UI;

namespace LoBIM.Services
{
    public interface IRevitServiceFactory
    {
        IRevitService Create(UIDocument uidoc);
    }
}
