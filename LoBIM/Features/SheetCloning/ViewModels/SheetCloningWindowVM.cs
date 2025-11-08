using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.SheetCloning.EventHandlers;
using LoBIM.Features.SheetCloning.Models;
using LoBIM.Features.SheetCloning.Services;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
using LoBIM.Services;
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

        private ObservableCollection<LinkedFileInfo> _linkedFiles;
        public ObservableCollection<LinkedFileInfo> LinkedFiles
        {
            get => _linkedFiles;
            set
            {
                _linkedFiles = value;
                OnPropertyChanged(nameof(LinkedFiles));
                OnPropertyChanged(nameof(HasLinkedFiles));
            }
        }

        public bool HasLinkedFiles => LinkedFiles?.Count > 0;

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
            }
        }

        public bool HasSheets => AvailableSheets?.Count > 0;

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
        public ICommand CloseCommand { get; }

        private List<LinkedSheetInfo> _allSheets;

        public SheetCloningWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _sheetCloningService = DIContainerService.Container.GetInstance<ISheetCloningService>();
            _viewCloningService = DIContainerService.Container.GetInstance<IViewCloningService>();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();

            _linkedFiles = new ObservableCollection<LinkedFileInfo>();
            _availableSheets = new ObservableCollection<LinkedSheetInfo>();
            _allSheets = new List<LinkedSheetInfo>();

            // Initialize ExternalEvent for cloning sheets
            _cloneSheetsHandler = new CloneSheetsEventHandler(_logger, _sheetCloningService);
            _cloneSheetsEvent = ExternalEvent.Create(_cloneSheetsHandler);

            // Initialize commands
            RefreshLinkedFilesCommand = new RelayCommand(RefreshLinkedFiles);
            SelectAllSheetsCommand = new RelayCommand(SelectAllSheets);
            DeselectAllSheetsCommand = new RelayCommand(DeselectAllSheets);
            CloneSelectedSheetsCommand = new RelayCommand(CloneSelectedSheets, CanCloneSheets);
            CloseCommand = new RelayCommand(Close);

            // Load linked files on startup
            RefreshLinkedFiles(null);
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
                    SelectedLinkedFile.LinkedDocument,
                    SelectedLinkedFile.FileName);

                _allSheets = sheets;
                AvailableSheets = new ObservableCollection<LinkedSheetInfo>(sheets);

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

                // Deselect cloned sheets
                foreach (var sheet in AvailableSheets.Where(s => s.IsSelected))
                {
                    sheet.IsSelected = false;
                }
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
