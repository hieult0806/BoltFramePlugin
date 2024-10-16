using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.ViewModels;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace BoltFramePlugin.Services
{
    public static class ContainerConfigurator
    {
        private static Container container;
        public static Container Container => container;

        public static void RegisterServices(ExternalCommandData commandData, ElementSet elements)
        {
            container = new Container();
            container.RegisterInstance<IRevitService>(new RevitService(commandData, elements));
            // Register your services and ViewModels
            container.RegisterInstance<IWindowManager>(new WindowManager());
            // Add other registrations as needed
            container.Register<BoltFrameMainWindowVM>(Lifestyle.Transient);
            container.Register<ConfigurationWindowVM>(Lifestyle.Transient);

            // Verify the container's configuration
            container.Verify();
        }
    }
}
