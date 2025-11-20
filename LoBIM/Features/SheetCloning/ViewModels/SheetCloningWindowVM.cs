using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.SheetCloning.EventHandlers;
using LoBIM.Features.SheetCloning.Helpers;
using LoBIM.Features.SheetCloning.Models;
using LoBIM.Features.SheetCloning.Services;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
using LoBIM.Features.ViewCloning.Strategies;
using LoBIM.Services;
using LoBIM.Services.Parameters;
using LoBIM.ViewModels;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.SheetCloning.ViewModels
{
    public class SheetCloningWindowVM : BaseViewModel
    {
        private readonly ISheetCloningService _sheetCloningService;
        private readonly IViewCloningService _viewCloningService;
        private readonly ILoggingService _logger;
        private readonly ExternalEvent _cloneSheetsEvent;
        private readonly CloneSheetsEventHandler _cloneSheetsHandler;
        private readonly ExternalEvent _openSheetEvent;
        private readonly OpenSheetEventHandler _openSheetHandler;
        private readonly ExternalEvent _highlightSheetEvent;
        private readonly HighlightSheetEventHandler _highlightSheetHandler;

        private ObservableCollection<LinkedFileInfo> _linkedFiles;
        public ObservableCollection<LinkedFileInfo> LinkedFiles
        {
            get => _linkedFiles;
            set
            {
                _linkedFiles = value;
                OnPropertyChanged(nameof(LinkedFiles));
                OnPropertyChanged(nameof(HasLinkedFiles));
                OnPropertyChanged(nameof(HasNoLinkedFiles));
            }
        }

        public bool HasLinkedFiles => LinkedFiles?.Count > 0;
        public bool HasNoLinkedFiles => !HasLinkedFiles;

        private LinkedFileInfo? _selectedLinkedFile;
        public LinkedFileInfo? SelectedLinkedFile
        {
            get => _selectedLinkedFile;
            set
            {
                _selectedLinkedFile = value;
                OnPropertyChanged(nameof(SelectedLinkedFile));
                OnPropertyChanged(nameof(HasSelectedLink));
                LoadSheetsFromSelectedFile();
            }
        }

        public bool HasSelectedLink => SelectedLinkedFile != null;

        private ObservableCollection<LinkedSheetInfo> _availableSheets;
        public ObservableCollection<LinkedSheetInfo> AvailableSheets
        {
            get => _availableSheets;
            set
            {
                _availableSheets = value;
                OnPropertyChanged(nameof(AvailableSheets));
                OnPropertyChanged(nameof(HasSheets));
                OnPropertyChanged(nameof(HasNoSheets));
            }
        }

        public bool HasSheets => AvailableSheets?.Count > 0;
        public bool HasNoSheets => !HasSheets;

        private LinkedSheetInfo? _selectedSheet;
        public LinkedSheetInfo? SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                _selectedSheet = value;
                OnPropertyChanged(nameof(SelectedSheet));
                OnPropertyChanged(nameof(CanShowInProjectBrowser));

                // Auto-highlight cloned sheet in Project Browser on single click
                if (_selectedSheet != null && _selectedSheet.IsCloned && _selectedSheet.ClonedSheetId != null)
                {
                    HighlightSheetInProjectBrowser();
                }
            }
        }

        public bool CanShowInProjectBrowser => SelectedSheet != null && SelectedSheet.IsCloned && SelectedSheet.ClonedSheetId != null;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                FilterSheets();
            }
        }

        // Commands
        public ICommand RefreshLinkedFilesCommand { get; }
        public ICommand SelectAllSheetsCommand { get; }
        public ICommand DeselectAllSheetsCommand { get; }
        public ICommand CloneSelectedSheetsCommand { get; }
        public ICommand ShowInProjectBrowserCommand { get; }
        public ICommand CloseCommand { get; }

        private List<LinkedSheetInfo> _allSheets;

        public SheetCloningWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _sheetCloningService = DIContainerService.Container.GetInstance<ISheetCloningService>();
            _viewCloningService = DIContainerService.Container.GetInstance<IViewCloningService>();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            var parameterService = DIContainerService.Container.GetInstance<IProjectParameterService>();

            _linkedFiles = new ObservableCollection<LinkedFileInfo>();
            _availableSheets = new ObservableCollection<LinkedSheetInfo>();
            _allSheets = new List<LinkedSheetInfo>();

            // Initialize ExternalEvent for cloning sheets
            _cloneSheetsHandler = new CloneSheetsEventHandler(_logger, _sheetCloningService, parameterService);
            _cloneSheetsEvent = ExternalEvent.Create(_cloneSheetsHandler);

            // Initialize ExternalEvent for opening sheets
            _openSheetHandler = new OpenSheetEventHandler();
            _openSheetEvent = ExternalEvent.Create(_openSheetHandler);

            // Initialize ExternalEvent for highlighting sheets in Project Browser
            _highlightSheetHandler = new HighlightSheetEventHandler(_logger);
            _highlightSheetEvent = ExternalEvent.Create(_highlightSheetHandler);

            // Initialize commands
            RefreshLinkedFilesCommand = new RelayCommand(RefreshLinkedFiles);
            SelectAllSheetsCommand = new RelayCommand(SelectAllSheets);
            DeselectAllSheetsCommand = new RelayCommand(DeselectAllSheets);
            CloneSelectedSheetsCommand = new RelayCommand(CloneSelectedSheets, CanCloneSheets);
            ShowInProjectBrowserCommand = new RelayCommand(ShowInProjectBrowser, param => CanShowInProjectBrowser);
            CloseCommand = new RelayCommand(Close);

            // Ensure source tracking parameters exist when window opens
            EnsureSourceTrackingParameters();

            // Load linked files on startup
            RefreshLinkedFiles(null);
        }

        /// <summary>
        /// Ensures source tracking parameters exist in the document
        /// This needs to be called outside of any transaction
        /// </summary>
        private void EnsureSourceTrackingParameters()
        {
            try
            {
                _logger.LogInformation("Ensuring source tracking parameters exist...");

                var parameterService = DIContainerService.Container.GetInstance<IProjectParameterService>();
                var viewTemplateService = DIContainerService.Container.GetInstance<LoBIM.Features.ViewCloning.Services.IViewTemplateTransferService>();

                // Ensure view source tracking parameters
                var viewStrategy = new PlanViewCloningStrategy(_logger, parameterService, viewTemplateService);
                viewStrategy.EnsureSourceTrackingParameters(_document.Document);

                // Ensure sheet source tracking parameters
                var sheetTrackingHelper = new SheetSourceTrackingHelper(_logger, parameterService);
                sheetTrackingHelper.EnsureSourceTrackingParameters(_document.Document);

                _logger.LogInformation("Source tracking parameters ready");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not ensure source tracking parameters: {ex.Message}");
            }
        }

        private void RefreshLinkedFiles(object parameter)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading linked files...";

                var doc = _document.Document;
                var linkedFiles = _viewCloningService.GetLinkedFiles(doc);

                LinkedFiles = new ObservableCollection<LinkedFileInfo>(linkedFiles);

                if (LinkedFiles.Count > 0)
                {
                    SelectedLinkedFile = LinkedFiles[0];
                    StatusMessage = $"Found {LinkedFiles.Count} linked file(s)";
                }
                else
                {
                    StatusMessage = "No linked files found in the document";
                }

                _logger.LogInformation($"Loaded {LinkedFiles.Count} linked files");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error refreshing linked files: {ex.Message}", ex);
                StatusMessage = $"Error: {ex.Message}";
                TaskDialog.Show("Error", $"Failed to load linked files: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadSheetsFromSelectedFile()
        {
            if (SelectedLinkedFile == null)
            {
                AvailableSheets = new ObservableCollection<LinkedSheetInfo>();
                _allSheets.Clear();
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading sheets from {SelectedLinkedFile.FileName}...";

                var sheets = _sheetCloningService.GetSheetsFromLinkedFile(
                    _document.Document,
                    SelectedLinkedFile.LinkedDocument,
                    SelectedLinkedFile.FileName);

                _allSheets = sheets;
                AvailableSheets = new ObservableCollection<LinkedSheetInfo>(sheets);

                // Debug: Log sheet info
                foreach (var sheet in sheets)
                {
                    _logger.LogInformation($"Sheet: {sheet.SheetNumber} - IsCloned: {sheet.IsCloned}, StatusText: '{sheet.StatusText}', SourceTrackingText: '{sheet.SourceTrackingText}'");
                }

                StatusMessage = $"Found {sheets.Count} sheet(s) in {SelectedLinkedFile.FileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading sheets: {ex.Message}", ex);
                StatusMessage = $"Error: {ex.Message}";
                TaskDialog.Show("Error", $"Failed to load sheets: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterSheets()
        {
            if (_allSheets == null || _allSheets.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                AvailableSheets = new ObservableCollection<LinkedSheetInfo>(_allSheets);
            }
            else
            {
                var filtered = _allSheets.Where(s =>
                    s.SheetNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.SheetName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                AvailableSheets = new ObservableCollection<LinkedSheetInfo>(filtered);
            }
        }

        private void SelectAllSheets(object parameter)
        {
            if (AvailableSheets == null) return;

            foreach (var sheet in AvailableSheets)
            {
                sheet.IsSelected = true;
            }

            StatusMessage = $"Selected all {AvailableSheets.Count} sheet(s)";
        }

        private void DeselectAllSheets(object parameter)
        {
            if (AvailableSheets == null) return;

            foreach (var sheet in AvailableSheets)
            {
                sheet.IsSelected = false;
            }

            StatusMessage = "Deselected all sheets";
        }

        private bool CanCloneSheets(object parameter)
        {
            return AvailableSheets != null && AvailableSheets.Any(s => s.IsSelected);
        }

        private void ShowInProjectBrowser(object parameter)
        {
            try
            {
                if (SelectedSheet == null || !SelectedSheet.IsCloned || SelectedSheet.ClonedSheetId == null)
                {
                    StatusMessage = "Please select a cloned sheet to show in Project Browser";
                    return;
                }

                // Use ExternalEvent to open the sheet
                _openSheetHandler.SetParameters(_document, SelectedSheet.ClonedSheetId, OnOpenSheetCompleted);
                _openSheetEvent.Raise();

                StatusMessage = "Opening sheet...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error showing sheet: {ex.Message}";
                _logger.LogError($"Error showing sheet: {ex.Message}", ex);
            }
        }

        private void OnOpenSheetCompleted(bool success, string sheetNumber)
        {
            if (success)
            {
                StatusMessage = $"Opened sheet '{sheetNumber}'";
                _logger.LogInformation($"Opened sheet '{sheetNumber}' (Source: {SelectedSheet?.SourceTrackingFileName} > {SelectedSheet?.SourceTrackingSheetName})");
            }
            else
            {
                StatusMessage = $"Error opening sheet: {sheetNumber}";
                _logger.LogError($"Error opening sheet: {sheetNumber}");
            }
        }

        /// <summary>
        /// Highlights the selected sheet in the Project Browser without opening it
        /// Triggered automatically when a cloned sheet is selected (single click)
        /// </summary>
        private void HighlightSheetInProjectBrowser()
        {
            try
            {
                if (SelectedSheet == null || !SelectedSheet.IsCloned || SelectedSheet.ClonedSheetId == null)
                {
                    return;
                }

                // Use ExternalEvent to highlight the sheet
                _highlightSheetHandler.SetParameters(_document, SelectedSheet.ClonedSheetId, OnHighlightSheetCompleted);
                _highlightSheetEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error highlighting sheet: {ex.Message}", ex);
            }
        }

        private void OnHighlightSheetCompleted(bool success, string sheetNumber)
        {
            if (success)
            {
                _logger.LogInformation($"Highlighted sheet '{sheetNumber}' in Project Browser");
            }
            else
            {
                _logger.LogError($"Error highlighting sheet: {sheetNumber}");
            }
        }

        /// <summary>
        /// Opens the selected sheet as the active view
        /// Triggered by double-click on DataGrid row
        /// </summary>
        public void OpenSelectedSheet()
        {
            ShowInProjectBrowser(null);
        }

        private void CloneSelectedSheets(object parameter)
        {
            try
            {
                var selectedSheets = AvailableSheets.Where(s => s.IsSelected).ToList();

                if (selectedSheets.Count == 0)
                {
                    TaskDialog.Show("No Sheets Selected", "Please select at least one sheet to clone.");
                    return;
                }

                IsLoading = true;
                StatusMessage = $"Cloning {selectedSheets.Count} sheet(s)...";

                // Build dictionary of linked documents
                var linkedDocs = new Dictionary<string, Document>();
                foreach (var file in LinkedFiles)
                {
                    linkedDocs[file.FileName] = file.LinkedDocument;
                }

                // Use ExternalEvent to execute cloning in Revit API context
                _cloneSheetsHandler.SetParameters(_document, selectedSheets, linkedDocs, OnCloningCompleted);
                _cloneSheetsEvent.Raise();

                _logger.LogInformation($"Initiated cloning of {selectedSheets.Count} sheets");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initiating sheet cloning: {ex.Message}", ex);
                StatusMessage = $"Error: {ex.Message}";
                TaskDialog.Show("Error", $"Failed to initiate sheet cloning: {ex.Message}");
                IsLoading = false;
            }
        }

        private void OnCloningCompleted(int successCount)
        {
            IsLoading = false;

            if (successCount > 0)
            {
                StatusMessage = $"Successfully cloned {successCount} sheet(s)";
                TaskDialog.Show("Success", $"Successfully cloned {successCount} sheet(s).");

                // Reload sheets from the selected file to refresh source tracking info
                LoadSheetsFromSelectedFile();
            }
            else
            {
                StatusMessage = "No sheets were cloned";
                TaskDialog.Show("Error", "Failed to clone sheets. Check the log for details.");
            }

            _logger.LogInformation($"Sheet cloning completed: {successCount} sheets cloned");
        }

        private void Close(object parameter)
        {
            OnRequestClose(EventArgs.Empty);
        }
    }
}
