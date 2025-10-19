using Autodesk.Revit.UI;
using BoltFramePlugin.Models.DataImport;
using BoltFramePlugin.Services;
using BoltFramePlugin.Services.DataImport;
using BoltFramePlugin.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BoltFramePlugin.ViewModels
{
    public class DataImportWindowVM : BaseViewModel
    {
        private readonly ILoggingService _logger;
        private readonly DataImportManager _importManager;
        private readonly UIDocument _uidoc;

        private string _filePath = string.Empty;
        private bool _hasHeaders = true;
        private char _csvDelimiter = ',';
        private bool _skipEmptyRows = true;
        private bool _trimWhitespace = true;
        private string _viewName = string.Empty;
        private double _columnWidth = 1.5; // 1.5 feet in model space (at scale 4, appears as 4.5" on paper)
        private double _rowHeight = 0.167; // 0.167 feet in model space (at scale 4, appears as 0.5" on paper)
        private double _borderOffset = 0.0208; // 0.0208 feet in model space (at scale 4, appears as 5/64" on paper)
        private double _textHeight = 0.0208; // Calculated from paper text height
        private double _textOffsetX = 0; // Text horizontal offset in feet
        private double _textOffsetY = 0; // Text vertical offset in feet
        private double _viewScale = 4; // Default: 3" = 1'-0" (1:4 scale)
        private double _paperTextHeight = 0.25; // 1/4" Arial on paper
        private bool _drawGridLines = true;
        private bool _fillHeaderBackground = false; // Transparent background
        private bool _autoSizeColumns = false;
        private TextAlignment _textAlignment = TextAlignment.Left;
        private ImportedTableData? _previewData = null;
        private bool _isImporting = false;
        private string _statusMessage = "Ready";

        public DataImportWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _uidoc = uidoc;
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();

            var renderService = new RevitTableRenderService(_logger);
            _importManager = new DataImportManager(renderService, _logger);

            // Initialize commands
            SelectFileCommand = new RelayCommand(SelectFile);
            PreviewDataCommand = new RelayCommand(async param => await PreviewDataAsync(), param => CanPreview);
            ImportAndRenderCommand = new RelayCommand(async param => await ImportAndRenderAsync(), param => CanImportAndRender);
            CloseCommand = new RelayCommand(Close);

            // Calculate initial dimensions based on default scale
            RecalculateDimensionsFromScale();

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

        #endregion

        #region Commands

        public ICommand SelectFileCommand { get; }
        public ICommand PreviewDataCommand { get; }
        public ICommand ImportAndRenderCommand { get; }
        public ICommand CloseCommand { get; }

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
                    renderOptions);

                StatusMessage = $"Successfully created view: {ViewName}";
                _logger.LogInformation($"Successfully imported and rendered to view: {ViewName}");

                // Open the created view (no transaction needed - setting ActiveView doesn't modify the document)
                _uidoc.ActiveView = view;

                Autodesk.Revit.UI.TaskDialog.Show("Success", $"Table successfully imported to view:\n{ViewName}");

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
            // Paper sizes in inches
            const double paperTextSize = 0.25;      // 1/4" text
            const double paperColumnWidth = 4.5;    // 4.5" column width
            const double paperRowHeight = 0.5;      // 1/2" row height
            const double paperBorderOffset = 0.0625; // 1/16" border offset

            // Calculate model space dimensions
            // Model dimension (ft) = Paper size (inches) × ViewScale / 12
            // This accounts for the 1:N scale ratio
            _columnWidth = paperColumnWidth * _viewScale / 12.0;
            _rowHeight = paperRowHeight * _viewScale / 12.0;
            _borderOffset = paperBorderOffset * _viewScale / 12.0;

            // Text offset calculation based on measured values
            // Reference measurements:
            // Scale 4:  Move Right 69/256" (0.02246 ft), Move Up 95/128" (0.06185 ft)
            // Scale 36: Move Right 3" (0.25 ft), Move Up 6.25" (0.5208 ft)
            //
            // Using linear interpolation: Offset = A + B × ViewScale
            //
            // For X: 0.02246 = A + B×4, and 0.25 = A + B×36
            // Solving: B = (0.25 - 0.02246)/(36-4) = 0.22754/32 = 0.007110625
            //          A = 0.02246 - (0.007110625×4) = 0.02246 - 0.028442 = -0.005982
            // Therefore: OffsetX = -0.005982 + 0.007110625 × ViewScale
            //
            // For Y: 0.06185 = A + B×4, and 0.5208 = A + B×36
            // Solving: B = (0.5208 - 0.06185)/(36-4) = 0.45895/32 = 0.01434219
            //          A = 0.06185 - (0.01434219×4) = 0.06185 - 0.05737 = 0.00448
            // Therefore: OffsetY = 0.00448 + 0.01434219 × ViewScale

            const double offsetXIntercept = -0.005982;
            const double offsetXSlope = 0.007110625;
            const double offsetYIntercept = 0.00448;
            const double offsetYSlope = 0.01434219;

            _textOffsetX = offsetXIntercept + (offsetXSlope * _viewScale);
            _textOffsetY = offsetYIntercept + (offsetYSlope * _viewScale);

            // Text size is paper size (not affected by view scale in drafting views)
            const double minTextHeight = 3.0 / 256.0 / 12.0;
            const double maxTextHeight = (16.0 + 73.0 / 256.0) / 12.0;
            double calculatedTextHeight = paperTextSize / 12.0;
            _textHeight = Math.Max(minTextHeight, Math.Min(maxTextHeight, calculatedTextHeight));

            // Notify property changes
            OnPropertyChanged(nameof(ColumnWidth));
            OnPropertyChanged(nameof(RowHeight));
            OnPropertyChanged(nameof(BorderOffset));
            OnPropertyChanged(nameof(TextHeight));
            OnPropertyChanged(nameof(TextOffsetX));
            OnPropertyChanged(nameof(TextOffsetY));

            _logger.LogInformation($"Dimensions recalculated for scale 1:{_viewScale} - Column: {_columnWidth:F3}ft, Row: {_rowHeight:F3}ft, Border: {_borderOffset:F4}ft, Text: {_textHeight:F4}ft, TextOffset: ({_textOffsetX:F3}, {_textOffsetY:F3})");
        }

        #endregion
    }
}
