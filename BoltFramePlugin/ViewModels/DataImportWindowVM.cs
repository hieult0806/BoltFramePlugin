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
        private double _columnWidth = 1.5;
        private double _rowHeight = 0.15;
        private double _textHeight = 0.0104; // Calculated based on scale
        private double _viewScale = 96; // Default: 1/8" = 1'-0"
        private double _paperTextHeight = 0.125; // 1/8" on paper
        private bool _drawGridLines = true;
        private bool _fillHeaderBackground = true;
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
            set
            {
                _columnWidth = value;
                OnPropertyChanged(nameof(ColumnWidth));
            }
        }

        public double RowHeight
        {
            get => _rowHeight;
            set
            {
                _rowHeight = value;
                OnPropertyChanged(nameof(RowHeight));
            }
        }

        public double TextHeight
        {
            get => _textHeight;
            set
            {
                _textHeight = value;
                OnPropertyChanged(nameof(TextHeight));
            }
        }

        public double ViewScale
        {
            get => _viewScale;
            set
            {
                _viewScale = value;
                OnPropertyChanged(nameof(ViewScale));
                // Recalculate text height when scale changes
                CalculateTextHeight();
            }
        }

        public double PaperTextHeight
        {
            get => _paperTextHeight;
            set
            {
                _paperTextHeight = value;
                OnPropertyChanged(nameof(PaperTextHeight));
                // Recalculate text height when paper size changes
                CalculateTextHeight();
            }
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
        /// Calculate text height in Revit units (feet) based on view scale and paper text height
        /// Formula: TextHeight (feet) = PaperTextHeight (inches) * ViewScale / 12
        /// Example: For 1/8" text at 1/8"=1'-0" scale (96): 0.125 * 96 / 12 = 1" = 0.0833 feet
        /// </summary>
        private void CalculateTextHeight()
        {
            // Convert: paper inches * scale / 12 = model feet
            _textHeight = (_paperTextHeight * _viewScale) / 12.0;
            OnPropertyChanged(nameof(TextHeight));
            _logger.LogInformation($"Text height calculated: {_textHeight:F4} feet (Paper: {_paperTextHeight}\", Scale: 1:{_viewScale})");
        }

        #endregion
    }
}
