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

            // Shared Rendering Services
            container.RegisterSingleton<BoltFramePlugin.Services.Rendering.ITableRenderService, BoltFramePlugin.Services.Rendering.RevitTableRenderService>();

            // Data Import Services
            container.RegisterSingleton<BoltFramePlugin.Features.DataImport.Services.Import.CsvImportService>();
            container.RegisterSingleton<BoltFramePlugin.Features.DataImport.Services.Import.ExcelImportService>();
            container.RegisterSingleton<BoltFramePlugin.Features.DataImport.Services.DataImportManager>();
            container.RegisterSingleton<BoltFramePlugin.Features.DataImport.Services.FileWatcherService>();
            container.RegisterSingleton<BoltFramePlugin.Features.DataImport.EventHandlers.AutoSyncEventHandler>();

            // Limiting Distance Services
            container.RegisterSingleton<BoltFramePlugin.Features.LimitingDistance.Services.LimitingDistanceReportService>();

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
