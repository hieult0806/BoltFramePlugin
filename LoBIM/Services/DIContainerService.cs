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
            container.RegisterSingleton<IFeatureFlagService, FeatureFlagService>();
            container.RegisterSingleton<IRevitServiceFactory, RevitServiceFactory>();
            container.RegisterSingleton<IThemeService, ThemeService>();
            container.RegisterSingleton<IFileService, FileService>();
            container.RegisterSingleton<IPluginConfigurationManager, PluginConfigurationManager>();
            container.RegisterSingleton<IProjectConfigurationManager, ProjectConfigurationManager>();
            container.RegisterSingleton<IExtensibleStorageService, ExtensibleStorageService>();

            // Parameter Management Service
            container.RegisterSingleton<Parameters.IProjectParameterService, Parameters.ProjectParameterService>();

            // Shared Rendering Services
            container.RegisterSingleton<LoBIM.Services.Rendering.ITableRenderService, LoBIM.Services.Rendering.RevitTableRenderService>();

            // Data Import Services
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.Import.CsvImportService>();
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.Import.ExcelImportService>();
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.DataImportManager>();
            container.RegisterSingleton<LoBIM.Features.DataImport.Services.FileWatcherService>();
            container.RegisterSingleton<LoBIM.Features.DataImport.EventHandlers.AutoSyncEventHandler>();

            // NBC Review Services
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.NBCComplianceReportService>();
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.INBCConfigurationService, LoBIM.Features.NBCReview.Services.NBCConfigurationService>();
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.IElementHighlightService, LoBIM.Features.NBCReview.Services.ElementHighlightService>();
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.IGeometryService, LoBIM.Features.NBCReview.Services.GeometryService>();
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.IWallAnalysisService, LoBIM.Features.NBCReview.Services.WallAnalysisService>();
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.INBCComplianceService, LoBIM.Features.NBCReview.Services.NBCComplianceService>();
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.INBCInitializationService, LoBIM.Features.NBCReview.Services.NBCInitializationService>();
            container.RegisterSingleton<LoBIM.Features.NBCReview.Services.IViewNavigationService, LoBIM.Features.NBCReview.Services.ViewNavigationService>();

            // Sheet Management Services
            container.RegisterSingleton<LoBIM.Features.SheetManagement.Services.ISheetManagementService, LoBIM.Features.SheetManagement.Services.SheetManagementService>();

            // View Cloning Services
            container.RegisterSingleton<LoBIM.Features.ViewCloning.Services.IViewCloningService, LoBIM.Features.ViewCloning.Services.ViewCloningService>();
            container.RegisterSingleton<LoBIM.Features.ViewCloning.Services.IViewTemplateTransferService, LoBIM.Features.ViewCloning.Services.ViewTemplateTransferService>();

            // Sheet Cloning Services
            container.RegisterSingleton<LoBIM.Features.SheetCloning.Services.ISheetCloningService, LoBIM.Features.SheetCloning.Services.SheetCloningService>();

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
