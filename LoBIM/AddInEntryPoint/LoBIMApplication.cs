using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using LoBIM.Services;
using Autodesk.Revit.DB.Events;
using LoBIM.Constants;
using LoBIM.Factories;
using LoBIM.Features.DataImport.Commands;
using LoBIM.Features.Framing.Commands;
using LoBIM.Features.NBCReview.Commands;
using LoBIM.Helpers;
using LoBIM.Views;
using Serilog;

namespace LoBIM.AddInEntryPoint
{
    public class LoBIMApplication : IExternalApplication
    {
        // Unique identifier for the dockable pane
        public static readonly DockablePaneId DockablePaneGuid = new DockablePaneId(DockablePaneGuids.SwitchViewShortcut);
        private UIControlledApplication _application;
        private readonly ILoggingService _logger;

        public LoBIMApplication()
        {
            DIContainerService.RegisterServices();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
        }

        public Result OnStartup(UIControlledApplication application)
        {
            _application = application;
            try
            {
                _logger.LogInformation("LoBIM startup initiated.");

                // Subscribe to Revit events
                SubscribeToRevitEvents();

                // Initialize Ribbon UI
                InitializeRibbonUI();

                // Register Dockable Pane
                RegisterDockablePane();

                _logger.LogInformation("LoBIM started successfully.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred during LoBIM startup.", ex);
                TaskDialog.Show("LoBIM - Startup Error", $"An error occurred during startup: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _logger.LogInformation("LoBIM shutdown initiated.");

                // Unsubscribe from Revit events
                UnsubscribeFromRevitEvents();

                _logger.LogInformation("LoBIM shutdown completed successfully.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred during LoBIM shutdown.", ex);
                TaskDialog.Show("LoBIM - Shutdown Error", $"An error occurred during shutdown: {ex.Message}");
                return Result.Failed;
            }
        }

        #region Event Handlers

        private void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs e)
        {
            _logger.LogInformation("Revit application initialized.");
            // Implement additional initialization logic here if necessary
        }

        private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
        {
            _logger.LogInformation($"Document opened: {e.Document.Title}");
            // Implement document opened logic here
        }

        private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
        {
            _logger.LogInformation($"Document closing: {e.Document.Title}");

            // Stop all file watchers for this document
            try
            {
                var fileWatcher = DIContainerService.Container.GetInstance<LoBIM.Features.DataImport.Services.FileWatcherService>();
                fileWatcher.StopAll();
                _logger.LogInformation("All file watchers stopped due to document closing.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error stopping file watchers during document close: {ex.Message}");
            }

            // Close all open plugin windows when document closes
            try
            {
                var windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
                if (windowManager is WindowManager wm)
                {
                    wm.CloseAllWindows();
                    _logger.LogInformation("All plugin windows closed due to document closing.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error closing windows during document close: {ex.Message}");
            }
        }

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
            _logger.LogInformation("Document changed.");
            // Implement document changed logic here
        }

        private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
        {
            _logger.LogInformation($"Document closed: {e.DocumentId}");
            // Implement document closed logic here
        }

        #endregion

        #region Event Subscription Methods

        /// <summary>
        /// Subscribes to relevant Revit events.
        /// </summary>
        private void SubscribeToRevitEvents()
        {
            _application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
            _application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            _application.ControlledApplication.DocumentClosing += OnDocumentClosing;
            _application.ControlledApplication.DocumentChanged += OnDocumentChanged;
            _application.ControlledApplication.DocumentClosed += OnDocumentClosed;

            _logger.LogInformation("Subscribed to Revit events.");
        }

        /// <summary>
        /// Unsubscribes from Revit events.
        /// </summary>
        private void UnsubscribeFromRevitEvents()
        {
            _application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
            _application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            _application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            _application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
            _application.ControlledApplication.DocumentClosed -= OnDocumentClosed;

            _logger.LogInformation("Unsubscribed from Revit events.");
        }

        #endregion

        #region Ribbon UI Initialization

        /// <summary>
        /// Initializes the Ribbon UI by creating tabs, panels, and buttons.
        /// </summary>
        private void InitializeRibbonUI()
        {
            string tabName = Resources.Strings.Strings.TabName;
            _application.CreateRibbonTab(tabName);
            _logger.LogInformation($"Ribbon tab '{tabName}' created.");

            RibbonPanel panel = _application.CreateRibbonPanel(tabName, "General");
            _logger.LogInformation("Ribbon panel 'General' created.");

            // Get feature flag service
            var featureFlagService = DIContainerService.Container.GetInstance<IFeatureFlagService>();
            var enabledFeatures = featureFlagService.GetEnabledFeatures();

            _logger.LogInformation($"Adding {enabledFeatures.Count} enabled features to ribbon.");

            bool separatorAdded = false;

            // Add buttons for all enabled features
            foreach (var feature in enabledFeatures)
            {
                // Add separator before OpenLogs button
                if (!separatorAdded && feature.ButtonName == "OpenLogFolderButton")
                {
                    panel.AddSeparator();
                    separatorAdded = true;
                }

                AddPushButton(
                    panel,
                    name: feature.ButtonName,
                    text: feature.Name,
                    className: feature.ClassName,
                    tooltip: feature.Tooltip,
                    longDescription: feature.LongDescription,
                    iconName: feature.IconName
                );

                _logger.LogInformation($"Added button: {feature.Name}");
            }
        }

        /// <summary>
        /// Adds a PushButton to a given RibbonPanel.
        /// </summary>
        private void AddPushButton(RibbonPanel panel, string name, string text, string className, string tooltip, string longDescription, string? iconName = null)
        {
            try
            {
                // If className contains a period, treat it as a full namespace path, otherwise prepend AddInEntryPoint namespace
                string fullClassName = className.Contains(".") ? className : $"LoBIM.AddInEntryPoint.{className}";

                PushButtonData buttonData = new PushButtonData(
                    name: name,
                    text: text,
                    assemblyName: System.Reflection.Assembly.GetExecutingAssembly().Location,
                    className: fullClassName
                );

                PushButton button = panel.AddItem(buttonData) as PushButton;
                if (button != null)
                {
                    button.ToolTip = tooltip;
                    button.LongDescription = longDescription;

                    // Set icon if provided
                    if (!string.IsNullOrEmpty(iconName))
                    {
                        string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        string? assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyPath);

                        if (!string.IsNullOrEmpty(assemblyDirectory))
                        {
                            // Try PNG first, then SVG
                            string pngPath = System.IO.Path.Combine(assemblyDirectory, "Resources", "Images", $"{iconName}.png");
                            string svgPath = System.IO.Path.Combine(assemblyDirectory, "Resources", "Images", $"{iconName}.svg");

                            string? iconPath = null;
                            if (System.IO.File.Exists(pngPath))
                            {
                                iconPath = pngPath;
                            }
                            else if (System.IO.File.Exists(svgPath))
                            {
                                _logger.LogWarning($"SVG icons are not supported by Revit ribbon buttons. Please convert '{iconName}.svg' to PNG format.");
                                // SVG not supported by BitmapImage - skip
                            }

                            if (!string.IsNullOrEmpty(iconPath))
                            {
                                try
                                {
                                    var uri = new Uri(iconPath, UriKind.Absolute);
                                    button.Image = new System.Windows.Media.Imaging.BitmapImage(uri);
                                    button.LargeImage = new System.Windows.Media.Imaging.BitmapImage(uri);
                                    _logger.LogInformation($"Icon '{System.IO.Path.GetFileName(iconPath)}' set for button '{name}'.");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Failed to load icon '{iconPath}': {ex.Message}");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"Icon file not found for '{iconName}' (tried .png and .svg)");
                            }
                        }
                    }

                    _logger.LogInformation($"PushButton '{name}' added to Ribbon panel.");
                }
                else
                {
                    _logger.LogWarning($"Failed to add PushButton '{name}' to Ribbon panel.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding PushButton '{name}' to Ribbon panel.", ex);
            }
        }

        #endregion

        #region Dockable Pane Registration

        /// <summary>
        /// Registers the Dockable Pane with Revit.
        /// </summary>
        private void RegisterDockablePane()
        {
            try
            {
                // Register Switch View Plans panel
                _application.RegisterDockablePane(
                    DockablePaneGuid,
                    $"Switch View Plans | {Resources.Strings.Strings.AppTitle}",
                    CreateDockablePane()
                );

                // Register Log panel
                _application.RegisterDockablePane(
                    new DockablePaneId(DockablePaneGuids.LogPanel),
                    $"Debug Logs | {Resources.Strings.Strings.AppTitle}",
                    new LogDockablePaneProvider()
                );

                _logger.LogInformation("Dockable panes registered successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to register dockable panes.", ex);
                TaskDialog.Show("LoBIM - Dockable Pane Error", $"Failed to register dockable panes: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates an instance of the Dockable Pane Provider.
        /// </summary>
        private IDockablePaneProvider CreateDockablePane()
        {
            return new BoltFrameDockablePaneProvider();
        }

        #endregion
    }
}