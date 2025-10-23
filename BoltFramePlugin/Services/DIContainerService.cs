using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace BoltFramePlugin.Services
{
    public static class DIContainerService
    {
        private static readonly Container container;
        public static Container Container => container;

        static DIContainerService()
        {
            container = new Container();
        }

        public static void RegisterServices()
        {
            container.RegisterSingleton<ILoggingService, LoggingService>();
            container.RegisterSingleton<IRevitServiceFactory, RevitServiceFactory>();
            container.RegisterSingleton<IThemeService, ThemeService>();
            container.RegisterSingleton<IFileService, FileService>();
            container.RegisterSingleton<IPluginConfigurationManager, PluginConfigurationManager>();
            container.RegisterSingleton<IProjectConfigurationManager, ProjectConfigurationManager>();
            container.RegisterSingleton<IExtensibleStorageService, ExtensibleStorageService>();

            // Data Import Services
            container.RegisterSingleton<BoltFramePlugin.Services.DataImport.CsvImportService>();
            container.RegisterSingleton<BoltFramePlugin.Services.DataImport.ExcelImportService>();
            container.RegisterSingleton<BoltFramePlugin.Services.DataImport.ITableRenderService, BoltFramePlugin.Services.DataImport.RevitTableRenderService>();
            container.RegisterSingleton<BoltFramePlugin.Services.DataImport.DataImportManager>();
            container.RegisterSingleton<BoltFramePlugin.Services.DataImport.FileWatcherService>();
            container.RegisterSingleton<BoltFramePlugin.Services.DataImport.AutoSyncEventHandler>();

            // Register your services and ViewModels
            container.RegisterInstance<IWindowManager>(new WindowManager());
            // Add other registrations as needed
            //container.Register<BoltFrameMainWindowVM>(Lifestyle.Transient);
            //container.Register<ConfigurationWindowVM>(Lifestyle.Transient);
            //container.Register<SwitchViewShortcutDockablePaneVM>(Lifestyle.Transient);

            // Verify the container's configuration
            container.Verify();
        }
    }
}
