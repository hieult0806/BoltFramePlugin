using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace LoBIM.Services
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
            container.RegisterSingleton<LoBIM.Services.Rendering.ITableRenderService, LoBIM.Services.Rendering.RevitTableRenderService>();

            // Data Import Services
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.Import.CsvImportService>();
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.Import.ExcelImportService>();
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.DataImportManager>();
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.FileWatcherService>();
            container.RegisterSingleton<LoBIM.Features.DataImport.EventHandlers.AutoSyncEventHandler>();

            // Limiting Distance Services
            container.RegisterSingleton<LoBIM.Features.LimitingDistance.Services.LimitingDistanceReportService>();
            container.RegisterSingleton<LoBIM.Features.LimitingDistance.Services.INBCConfigurationService, LoBIM.Features.LimitingDistance.Services.NBCConfigurationService>();
            container.RegisterSingleton<LoBIM.Features.LimitingDistance.Services.IElementHighlightService, LoBIM.Features.LimitingDistance.Services.ElementHighlightService>();

            // Sheet Management Services
            container.RegisterSingleton<LoBIM.Features.SheetManagement.Services.ISheetManagementService, LoBIM.Features.SheetManagement.Services.SheetManagementService>();

            // View Cloning Services
            container.RegisterSingleton<LoBIM.Features.ViewCloning.Services.IViewCloningService, LoBIM.Features.ViewCloning.Services.ViewCloningService>();

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
