using Autodesk.Revit.UI;

namespace BoltFramePlugin.Services
{
    public class RevitServiceFactory : IRevitServiceFactory
    {
        public IRevitService Create(UIDocument uidoc)
        {
            if (uidoc == null)
                throw new ArgumentNullException(nameof(uidoc));

            return new RevitService(uidoc);
        }
    }
}
