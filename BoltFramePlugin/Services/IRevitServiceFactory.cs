using Autodesk.Revit.UI;

namespace BoltFramePlugin.Services
{
    public interface IRevitServiceFactory
    {
        IRevitService Create(UIDocument uidoc);
    }
}
