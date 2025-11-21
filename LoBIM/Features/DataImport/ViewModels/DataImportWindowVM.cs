using Autodesk.Revit.UI;
using LoBIM.Features.DataImport.EventHandlers;
using LoBIM.Models.Tables;
using LoBIM.Features.DataImport.Services;
using LoBIM.Models;
using LoBIM.Services;
using LoBIM.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace LoBIM.Features.DataImport.ViewModels
{
    public class DataImportWindowVM : BaseViewModel
    {
        private readonly ILoggingService _logger;
        private readonly DataImportManager _importManager;
        private readonly UIDocument _uidoc;
        private readonly FileWatcherService _fileWatcher;
        private readonly AutoSyncEventHandler _autoSyncHandler;
        private readonly ExternalEvent _autoSyncEvent;
        private readonly IProjectConfigurationManager _projectConfig;

        private string _filePath = string.Empty;
        private bool _hasHeaders = true;
        private char _csvDelimiter = ',';
        private bool _skipEmptyRows = false; // Keep empty rows to preserve table structure
        private bool _trimWhitespace = true;
        private string _viewName = string.Empty;
        private double _columnWidth = 1.5; // 1.5 feet in model space (at scale 4, appears as 4.5" on paper)
        private double _rowHeight = 0.167; // 0.167 feet in model space (at scale 4, appears as 0.5" on paper)
        private double _borderOffset = 0.0208; // 0.0208 feet in model space (at scale 4, appears as 5/64" on paper)
        private double _textHeight = 0.0208; // Calculated from paper text height
        private double _textOffsetX = 0; // Text horizontal offset in feet
        private double _textOffsetY = 0; // Text vertical offset in feet
        private double _viewScale = 4; // Default: 3" = 1'-0" (1:4 scale)
        private double _scaleFactor = 1.0; // Scale factor for adjusting text size
        private double _widthScaleFactor = 1.0; // Scale factor for column width
        private double _heightScaleFactor = 1.0; // Scale factor for row height
        private double _paperTextHeight = 0.25; // 1/4" Arial on paper
        private bool _drawGridLines = true;
        private bool _fillHeaderBackground = false; // Transparent background
        private bool _autoSizeColumns = false;
        private TextAlignment _textAlignment = TextAlignment.Left;
        private ImportedTableData? _previewData = null;
        private bool _isImporting = false;
        private string _statusMessage = "Ready";
        private bool _enableAutoSync = false;
        private ObservableCollection<TrackedFileViewModel> _trackedFiles = new ObservableCollection<TrackedFileViewModel>();
        private string _trackedFilesMessage = string.Empty;

        public DataImportWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _uidoc = uidoc;
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();

            // Get services from DI container (singletons that persist after window closes)
            _importManager = DIContainerService.Container.GetInstance<DataImportManager>();
            _fileWatcher = DIContainerService.Container.GetInstance<FileWatcherService>();
            _autoSyncHandler = DIContainerService.Container.GetInstance<AutoSyncEventHandler>();
            _autoSyncEvent = ExternalEvent.Create(_autoSyncHandler);
            _projectConfig = DIContainerService.Container.GetInstance<IProjectConfigurationManager>();

            // Subscribe to sync requests from file watcher
            _fileWatcher.OnSyncRequested += OnFileWatcherSyncRequested;

            // Initialize commands
            SelectFileCommand = new RelayCommand(SelectFile);
            PreviewDataCommand = new RelayCommand(async param => await PreviewDataAsync(), param => CanPreview);
            ImportAndRenderCommand = new RelayCommand(async param => await ImportAndRenderAsync(), param => CanImportAndRender);
            CloseCommand = new RelayCommand(Close);
            RefreshTrackedFilesCommand = new RelayCommand(param => RefreshTrackedFiles());
            SyncFileCommand = new RelayCommand(async param => await SyncFileAsync(param as TrackedFileViewModel));
            StopTrackingCommand = new RelayCommand(param => StopTracking(param as TrackedFileViewModel));

            // Calculate initial dimensions based on default scale
            RecalculateDimensionsFromScale();

            // Load settings from project configuration
            LoadSettings();

            // Load tracked files from configuration
            _fileWatcher.LoadTrackedFiles(_uidoc.Document);

            // Refresh tracked files list in UI
            RefreshTrackedFiles();

            _logger.LogInformation("DataImportWindowVM initialized");
        }

        #region Properties

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged(nameof(FilePath));
                CommandManager.InvalidateRequerySuggested();

                // Auto-generate view name from file
                if (!string.IsNullOrEmpty(value))
                {
                    ViewName = $"Imported - {System.IO.Path.GetFileNameWithoutExtension(value)}";
                }
            }
        }

        public bool HasHeaders
        {
            get => _hasHeaders;
            set
            {
                _hasHeaders = value;
                OnPropertyChanged(nameof(HasHeaders));
            }
        }

        public string CsvDelimiter
        {
            get => _csvDelimiter.ToString();
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _csvDelimiter = value[0];
                    OnPropertyChanged(nameof(CsvDelimiter));
                }
            }
        }

        public bool SkipEmptyRows
        {
            get => _skipEmptyRows;
            set
            {
                _skipEmptyRows = value;
                OnPropertyChanged(nameof(SkipEmptyRows));
            }
        }

        public bool TrimWhitespace
        {
            get => _trimWhitespace;
            set
            {
                _trimWhitespace = value;
                OnPropertyChanged(nameof(TrimWhitespace));
            }
        }

        public string ViewName
        {
            get => _viewName;
            set
            {
                _viewName = value;
                OnPropertyChanged(nameof(ViewName));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public double ColumnWidth
        {
            get => _columnWidth;
        }

        public double RowHeight
        {
            get => _rowHeight;
        }

        public double TextHeight
        {
            get => _textHeight;
        }

        public double ViewScale
        {
            get => _viewScale;
            set
            {
                _viewScale = value;
                OnPropertyChanged(nameof(ViewScale));
                // Recalculate all dimensions when scale changes
                RecalculateDimensionsFromScale();
            }
        }

        public double ScaleFactor
        {
            get => _scaleFactor;
            set
            {
                // Validate input: must be between 0.1 and 100
                if (value < 0.1) value = 0.1;
                if (value > 100) value = 100;

                _scaleFactor = value;
                OnPropertyChanged(nameof(ScaleFactor));
                // Recalculate all dimensions when scale factor changes
                RecalculateDimensionsFromScale();
            }
        }

        public double WidthScaleFactor
        {
            get => _widthScaleFactor;
            set
            {
                // Validate input: must be between 0.1 and 10
                if (value < 0.1) value = 0.1;
                if (value > 10) value = 10;

                _widthScaleFactor = value;
                OnPropertyChanged(nameof(WidthScaleFactor));
                RecalculateDimensionsFromScale();
            }
        }

        public double HeightScaleFactor
        {
            get => _heightScaleFactor;
            set
            {
                // Validate input: must be between 0.1 and 10
                if (value < 0.1) value = 0.1;
                if (value > 10) value = 10;

                _heightScaleFactor = value;
                OnPropertyChanged(nameof(HeightScaleFactor));
                RecalculateDimensionsFromScale();
            }
        }

        public double PaperTextHeight
        {
            get => _paperTextHeight;
        }

        public double BorderOffset
        {
            get => _borderOffset;
        }

        public double TextOffsetX
        {
            get => _textOffsetX;
        }

        public double TextOffsetY
        {
            get => _textOffsetY;
        }

        public bool DrawGridLines
        {
            get => _drawGridLines;
            set
            {
                _drawGridLines = value;
                OnPropertyChanged(nameof(DrawGridLines));
            }
        }

        public bool FillHeaderBackground
        {
            get => _fillHeaderBackground;
            set
            {
                _fillHeaderBackground = value;
                OnPropertyChanged(nameof(FillHeaderBackground));
            }
        }

        public bool AutoSizeColumns
        {
            get => _autoSizeColumns;
            set
            {
                _autoSizeColumns = value;
                OnPropertyChanged(nameof(AutoSizeColumns));
            }
        }

        public ObservableCollection<string> TextAlignments { get; } = new ObservableCollection<string>
        {
            "Left", "Center", "Right"
        };

        public string SelectedTextAlignment
        {
            get => _textAlignment.ToString();
            set
            {
                if (Enum.TryParse<TextAlignment>(value, out var alignment))
                {
                    _textAlignment = alignment;
                    OnPropertyChanged(nameof(SelectedTextAlignment));
                }
            }
        }

        public ImportedTableData? PreviewData
        {
            get => _previewData;
            set
            {
                _previewData = value;
                OnPropertyChanged(nameof(PreviewData));
                OnPropertyChanged(nameof(HasPreviewData));
                OnPropertyChanged(nameof(PreviewSummary));
            }
        }

        public bool HasPreviewData => _previewData != null;

        public string PreviewSummary =>
            _previewData != null
                ? $"{_previewData.RowCount} rows × {_previewData.ColumnCount} columns"
                : "No preview available";

        public bool IsImporting
        {
            get => _isImporting;
            set
            {
                _isImporting = value;
                OnPropertyChanged(nameof(IsImporting));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public bool EnableAutoSync
        {
            get => _enableAutoSync;
            set
            {
                _enableAutoSync = value;
                OnPropertyChanged(nameof(EnableAutoSync));
                _logger.LogInformation($"Auto-sync {(value ? "enabled" : "disabled")}");
            }
        }

        public ObservableCollection<TrackedFileViewModel> TrackedFiles
        {
            get => _trackedFiles;
            set
            {
                _trackedFiles = value;
                OnPropertyChanged(nameof(TrackedFiles));
            }
        }

        public string TrackedFilesMessage
        {
            get => _trackedFilesMessage;
            set
            {
                _trackedFilesMessage = value;
                OnPropertyChanged(nameof(TrackedFilesMessage));
            }
        }

        #endregion

        #region Commands

        public ICommand SelectFileCommand { get; }
        public ICommand PreviewDataCommand { get; }
        public ICommand ImportAndRenderCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand RefreshTrackedFilesCommand { get; }
        public ICommand SyncFileCommand { get; }
        public ICommand StopTrackingCommand { get; }

        private bool CanPreview => !string.IsNullOrEmpty(FilePath) && !IsImporting;
        private bool CanImportAndRender => !string.IsNullOrEmpty(FilePath) && !string.IsNullOrEmpty(ViewName) && !IsImporting;

        #endregion

        #region Methods

        private void SelectFile(object parameter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = _importManager.GetFileFilter(),
                Title = "Select File to Import"
            };

            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
                StatusMessage = $"Selected: {System.IO.Path.GetFileName(FilePath)}";
                _logger.LogInformation($"File selected: {FilePath}");
            }
        }

        private async Task PreviewDataAsync()
        {
            try
            {
                IsImporting = true;
                StatusMessage = "Loading preview...";

                var config = new ImportConfiguration
                {
                    HasHeaders = HasHeaders,
                    CsvDelimiter = _csvDelimiter,
                    SkipEmptyRows = SkipEmptyRows,
                    TrimWhitespace = TrimWhitespace
                };

                PreviewData = await _importManager.ImportDataAsync(FilePath, config);
                StatusMessage = $"Preview loaded: {PreviewSummary}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError($"Error previewing data: {ex.Message}", ex);
                Autodesk.Revit.UI.TaskDialog.Show("Error", $"Failed to preview data:\n{ex.Message}");
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task ImportAndRenderAsync()
        {
            try
            {
                IsImporting = true;
                StatusMessage = "Importing and rendering...";

                var importConfig = new ImportConfiguration
                {
                    HasHeaders = HasHeaders,
                    CsvDelimiter = _csvDelimiter,
                    SkipEmptyRows = SkipEmptyRows,
                    TrimWhitespace = TrimWhitespace
                };

                var renderOptions = new TableRenderOptions
                {
                    ColumnWidth = ColumnWidth,
                    RowHeight = RowHeight,
                    TextHeight = TextHeight,
                    ViewScale = ViewScale,
                    PaperTextHeight = PaperTextHeight,
                    BorderOffset = BorderOffset,
                    TextOffsetX = TextOffsetX,
                    TextOffsetY = TextOffsetY,
                    DrawGridLines = DrawGridLines,
                    FillHeaderBackground = FillHeaderBackground,
                    AutoSizeColumns = AutoSizeColumns,
                    TextAlign = _textAlignment
                };

                var view = await _importManager.ImportAndRenderAsync(
                    _uidoc.Document,
                    FilePath,
                    ViewName,
                    importConfig,
                    renderOptions,
                    _uidoc);

                StatusMessage = $"Successfully created view: {ViewName}";
                _logger.LogInformation($"Successfully imported and rendered to view: {ViewName}");

                // Track file for manual syncing if enabled
                _logger.LogInformation($"Checking track file status: EnableAutoSync = {EnableAutoSync}");
                if (EnableAutoSync)
                {
                    _logger.LogInformation($"Tracking file for manual sync...");
                    _fileWatcher.TrackFile(FilePath, _uidoc.Document, ViewName, importConfig, renderOptions);
                    StatusMessage += " (File tracked)";
                    _logger.LogInformation($"File tracking setup completed for: {FilePath}");
                }
                else
                {
                    _logger.LogInformation("File tracking is disabled");
                }

                // Open the created view (no transaction needed - setting ActiveView doesn't modify the document)
                _uidoc.ActiveView = view;

                string message = $"Table successfully imported to view:\n{ViewName}";
                if (EnableAutoSync)
                {
                    message += "\n\nAuto-sync is enabled. The view will automatically update when you save changes to the file.";
                }

                Autodesk.Revit.UI.TaskDialog.Show("Success", message);

                // Close window
                Close(null);
            }
            catch (Exception ex)
            {
                var errorDetails = $"Message: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nInner Exception: {ex.InnerException?.Message ?? "None"}";
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError($"Error importing and rendering: {errorDetails}", ex);
                Autodesk.Revit.UI.TaskDialog.Show("Error", $"Failed to import and render:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message ?? "None"}\n\nCheck logs for full stack trace.");
            }
            finally
            {
                IsImporting = false;
            }
        }

        private void Close(object parameter)
        {
            // Save settings before closing
            SaveSettings();

            DialogResult = true;
            OnRequestClose(EventArgs.Empty);
        }

        /// <summary>
        /// Recalculate all table dimensions based on the view scale
        /// This ensures consistent sizing across different scales
        /// Standard paper sizes:
        /// - Text: 1/4" (0.25")
        /// - Column width: 4.5" on paper
        /// - Row height: 0.5" on paper
        /// - Border offset: 1/16" on paper
        /// View scale N means 1:N ratio. Model space = Paper space × Scale / 12
        /// Example: At scale 48 (1/4"=1'-0"), 0.5" on paper = 0.5 × 48 / 12 = 2 ft in model
        /// </summary>
        private void RecalculateDimensionsFromScale()
        {
            // Base cell dimensions in model space (feet) - multiplied by their respective scale factors
            const double baseColumnWidth = 1.5;      // 1.5 ft base column width
            const double baseRowHeight = 0.167;      // 0.167 ft (2") base row height
            const double baseBorderOffset = 0.0208;  // 0.0208 ft (1/4") base border offset

            _columnWidth = baseColumnWidth * _widthScaleFactor;
            _rowHeight = baseRowHeight * _heightScaleFactor;
            _borderOffset = baseBorderOffset;  // Border offset stays fixed

            // Text size inversely proportional to ViewScale, then multiplied by ScaleFactor
            // Higher ScaleFactor = larger text
            // Model text height (ft) = Paper size (inches) / ViewScale / 12 * ScaleFactor
            const double paperTextSize = 0.25;  // 1/4" text on paper (base size)
            const double minTextHeight = 3.0 / 256.0 / 12.0;
            const double maxTextHeight = (16.0 + 73.0 / 256.0) / 12.0;
            double calculatedTextHeight = (paperTextSize / _viewScale / 12.0) * _scaleFactor;
            _textHeight = Math.Max(minTextHeight, Math.Min(maxTextHeight, calculatedTextHeight));

            // Text offset calculation - inversely proportional to ViewScale, multiplied by ScaleFactor
            const double baseOffsetX = 0.05;  // Base offset
            const double baseOffsetY = 0.05;  // Base offset

            _textOffsetX = (baseOffsetX / _viewScale) * _scaleFactor;
            _textOffsetY = (baseOffsetY / _viewScale) * _scaleFactor;

            // Notify property changes
            OnPropertyChanged(nameof(ColumnWidth));
            OnPropertyChanged(nameof(RowHeight));
            OnPropertyChanged(nameof(BorderOffset));
            OnPropertyChanged(nameof(TextHeight));
            OnPropertyChanged(nameof(TextOffsetX));
            OnPropertyChanged(nameof(TextOffsetY));

            _logger.LogInformation($"Dimensions for 1:{_viewScale} (Text: {_scaleFactor:F1}x, Width: {_widthScaleFactor:F1}x, Height: {_heightScaleFactor:F1}x) - Column: {_columnWidth:F3}ft, Row: {_rowHeight:F3}ft, Text: {_textHeight:F6}ft ({_textHeight * 12:F4}\"), TextOffset: ({_textOffsetX:F6}, {_textOffsetY:F6})");
        }

        private void LoadSettings()
        {
            try
            {
                if (_uidoc.Document.ProjectInformation == null)
                {
                    _logger.LogWarning("Cannot load settings - no project information");
                    return;
                }

                // Use ProjectInformation ElementId as a stable identifier
                var projectIdString = _uidoc.Document.ProjectInformation.UniqueId;
                Guid projectId;

                // Try to parse as GUID, if fails create a deterministic GUID from the string
                if (!Guid.TryParse(projectIdString, out projectId))
                {
                    // Create a deterministic GUID from the unique ID string
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(projectIdString));
                        projectId = new Guid(hash);
                    }
                    _logger.LogInformation($"Created GUID from UniqueId: {projectId}");
                }

                var config = _projectConfig.LoadProjectConfiguration(projectId);

                if (config.DataImportSettings == null)
                {
                    _logger.LogInformation("No saved data import settings found, using defaults");
                    return;
                }

                var settings = config.DataImportSettings;

                // Restore import configuration (use backing fields to avoid triggering events)
                _hasHeaders = settings.HasHeaders;
                _csvDelimiter = settings.CsvDelimiter;
                _skipEmptyRows = settings.SkipEmptyRows;
                _trimWhitespace = settings.TrimWhitespace;

                // Restore view configuration (use backing fields)
                _viewName = settings.ViewName;
                _viewScale = settings.ViewScale;

                // Restore scale factors (use backing fields)
                _scaleFactor = settings.TextScaleFactor;
                _widthScaleFactor = settings.WidthScaleFactor;
                _heightScaleFactor = settings.HeightScaleFactor;

                // Restore appearance (use backing fields)
                _drawGridLines = settings.DrawGridLines;
                _fillHeaderBackground = settings.FillHeaderBackground;
                _autoSizeColumns = settings.AutoSizeColumns;
                _textAlignment = (TextAlignment)settings.TextAlignment;

                // Recalculate dimensions with loaded scale factors
                RecalculateDimensionsFromScale();

                // Notify all property changes
                OnPropertyChanged(nameof(HasHeaders));
                OnPropertyChanged(nameof(CsvDelimiter));
                OnPropertyChanged(nameof(SkipEmptyRows));
                OnPropertyChanged(nameof(TrimWhitespace));
                OnPropertyChanged(nameof(ViewName));
                OnPropertyChanged(nameof(ViewScale));
                OnPropertyChanged(nameof(ScaleFactor));
                OnPropertyChanged(nameof(WidthScaleFactor));
                OnPropertyChanged(nameof(HeightScaleFactor));
                OnPropertyChanged(nameof(DrawGridLines));
                OnPropertyChanged(nameof(FillHeaderBackground));
                OnPropertyChanged(nameof(AutoSizeColumns));
                OnPropertyChanged(nameof(SelectedTextAlignment));

                _logger.LogInformation("Loaded data import settings from project configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading settings: {ex.Message}", ex);
            }
        }

        private void SaveSettings()
        {
            try
            {
                if (_uidoc.Document.ProjectInformation == null)
                {
                    _logger.LogWarning("Cannot save settings - no project information");
                    return;
                }

                // Use ProjectInformation ElementId as a stable identifier
                var projectIdString = _uidoc.Document.ProjectInformation.UniqueId;
                Guid projectId;

                // Try to parse as GUID, if fails create a deterministic GUID from the string
                if (!Guid.TryParse(projectIdString, out projectId))
                {
                    // Create a deterministic GUID from the unique ID string
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(projectIdString));
                        projectId = new Guid(hash);
                    }
                }

                var config = _projectConfig.LoadProjectConfiguration(projectId);

                config.DataImportSettings = new DataImportSettings
                {
                    // Import configuration
                    HasHeaders = _hasHeaders,
                    CsvDelimiter = _csvDelimiter,
                    SkipEmptyRows = _skipEmptyRows,
                    TrimWhitespace = _trimWhitespace,

                    // View configuration
                    ViewName = _viewName,
                    ViewScale = (int)_viewScale,

                    // Scale factors
                    TextScaleFactor = _scaleFactor,
                    WidthScaleFactor = _widthScaleFactor,
                    HeightScaleFactor = _heightScaleFactor,

                    // Appearance
                    DrawGridLines = _drawGridLines,
                    FillHeaderBackground = _fillHeaderBackground,
                    AutoSizeColumns = _autoSizeColumns,
                    TextAlignment = (int)_textAlignment
                };

                _projectConfig.SaveProjectConfiguration(config);
                _logger.LogInformation("Saved data import settings to project configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving settings: {ex.Message}", ex);
            }
        }

        private void OnFileWatcherSyncRequested(object? sender, SyncRequestedEventArgs e)
        {
            try
            {
                _logger.LogInformation($"File watcher sync requested for: {e.FilePath}");

                // Set the request parameters in the handler
                _autoSyncHandler.SetRequest(e);

                // Raise the external event to process on Revit main thread
                _autoSyncEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling sync request: {ex.Message}", ex);
            }
        }

        private void RefreshTrackedFiles()
        {
            try
            {
                TrackedFiles.Clear();

                var watchedFiles = _fileWatcher.GetWatchedFiles();

                if (!watchedFiles.Any())
                {
                    TrackedFilesMessage = "No files are currently being tracked.";
                    return;
                }

                TrackedFilesMessage = string.Empty;

                foreach (var watchedFile in watchedFiles)
                {
                    var vm = new TrackedFileViewModel
                    {
                        FilePath = watchedFile.FilePath,
                        FileName = Path.GetFileName(watchedFile.FilePath),
                        ViewName = watchedFile.ViewName,
                        LastModified = watchedFile.LastModified
                    };

                    // Check if file has changed
                    if (_fileWatcher.HasFileChanged(watchedFile.FilePath, watchedFile.ViewName))
                    {
                        vm.Status = "Modified";
                        vm.StatusColor = System.Windows.Media.Brushes.Orange;
                    }
                    else
                    {
                        vm.Status = "Up-to-date";
                        vm.StatusColor = System.Windows.Media.Brushes.Green;
                    }

                    TrackedFiles.Add(vm);
                }

                _logger.LogInformation($"Refreshed tracked files list: {TrackedFiles.Count} files");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error refreshing tracked files: {ex.Message}", ex);
                TrackedFilesMessage = $"Error: {ex.Message}";
            }
        }

        private async Task SyncFileAsync(TrackedFileViewModel? fileVm)
        {
            if (fileVm == null)
            {
                _logger.LogWarning("SyncFileAsync called with null file");
                return;
            }

            try
            {
                _logger.LogInformation($"Manually syncing file: {fileVm.FilePath} -> {fileVm.ViewName}");
                StatusMessage = $"Syncing {fileVm.FileName}...";

                // Get the tracked file info to retrieve import/render options
                var watchedFiles = _fileWatcher.GetWatchedFiles();
                var watchedFile = watchedFiles.FirstOrDefault(w =>
                    w.FilePath == fileVm.FilePath && w.ViewName == fileVm.ViewName);

                if (watchedFile == null)
                {
                    throw new InvalidOperationException("File is no longer being tracked");
                }

                // Call ImportAndRenderAsync directly (just like re-import does)
                var view = await _importManager.ImportAndRenderAsync(
                    _uidoc.Document,
                    watchedFile.FilePath,
                    watchedFile.ViewName,
                    watchedFile.ImportConfig,
                    watchedFile.RenderOptions,
                    _uidoc);

                // Switch to the synced view
                _uidoc.ActiveView = view;

                StatusMessage = $"Successfully synced {fileVm.FileName}";
                _logger.LogInformation($"Successfully synced view: {fileVm.ViewName}");

                // Refresh the tracked files list
                RefreshTrackedFiles();

                Autodesk.Revit.UI.TaskDialog.Show("Sync Complete", $"View '{fileVm.ViewName}' has been synced with latest file changes.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error syncing file: {ex.Message}", ex);
                StatusMessage = $"Error syncing: {ex.Message}";
                Autodesk.Revit.UI.TaskDialog.Show("Sync Error", $"Failed to sync file:\n{ex.Message}");
            }
        }

        private void StopTracking(TrackedFileViewModel? fileVm)
        {
            if (fileVm == null)
            {
                _logger.LogWarning("StopTracking called with null file");
                return;
            }

            try
            {
                _logger.LogInformation($"Stopping tracking for: {fileVm.FilePath} -> {fileVm.ViewName}");
                _fileWatcher.StopWatching(fileVm.FilePath, fileVm.ViewName);
                RefreshTrackedFiles();
                StatusMessage = $"Stopped tracking {fileVm.FileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping tracking: {ex.Message}", ex);
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        #endregion
    }

    /// <summary>
    /// ViewModel for displaying tracked file information in the UI
    /// </summary>
    public class TrackedFileViewModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string Status { get; set; } = "Unknown";
        public System.Windows.Media.Brush StatusColor { get; set; } = System.Windows.Media.Brushes.Gray;
    }
}
