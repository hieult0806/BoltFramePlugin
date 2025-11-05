using Autodesk.Revit.UI;
using LoBIM.Features.SheetManagement.EventHandlers;
using LoBIM.Features.SheetManagement.Models;
using LoBIM.Features.SheetManagement.Services;
using LoBIM.Services;
using LoBIM.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace LoBIM.Features.SheetManagement.ViewModels
{
    public class SheetManagementWindowVM : BaseViewModel
    {
        private readonly ILoggingService _logger;
        private readonly ISheetManagementService _sheetService;

        // External events
        private readonly RenumberSheetsEventHandler _renumberEventHandler;
        private readonly ExternalEvent _renumberExternalEvent;

        // Observable collections
        private ObservableCollection<SheetItemModel> _sheets;
        private ObservableCollection<SheetItemModel> _filteredSheets;
        private ObservableCollection<string> _availableParameters;

        // Filter properties
        private string _selectedParameter;
        private string _filterText;

        // Renumbering properties
        private string _prefix;
        private int _startNumber;
        private int _increment;
        private int _paddingDigits;

        // UI state
        private bool _isLoading;
        private string _statusMessage;

        public SheetManagementWindowVM(UIDocument document) : base(document)
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _sheetService = DIContainerService.Container.GetInstance<ISheetManagementService>();

            // Initialize event handlers
            _renumberEventHandler = new RenumberSheetsEventHandler(_logger, _sheetService);
            _renumberExternalEvent = ExternalEvent.Create(_renumberEventHandler);

            // Initialize collections
            Sheets = new ObservableCollection<SheetItemModel>();
            FilteredSheets = new ObservableCollection<SheetItemModel>();
            AvailableParameters = new ObservableCollection<string>();

            // Initialize properties
            Prefix = "A0.";
            StartNumber = 1;
            Increment = 1;
            PaddingDigits = 2;

            // Initialize commands
            LoadSheetsCommand = new RelayCommand(ExecuteLoadSheets);
            ApplyFilterCommand = new RelayCommand(ExecuteApplyFilter);
            ClearFilterCommand = new RelayCommand(ExecuteClearFilter);
            RenumberSheetsCommand = new RelayCommand(ExecuteRenumberSheets, CanExecuteRenumberSheets);
            PreviewRenumberingCommand = new RelayCommand(ExecutePreviewRenumbering, CanExecuteRenumberSheets);
            MoveSheetUpCommand = new RelayCommand(ExecuteMoveSheetUp, CanExecuteMoveSheetUp);
            MoveSheetDownCommand = new RelayCommand(ExecuteMoveSheetDown, CanExecuteMoveSheetDown);
            InsertSheetCommand = new RelayCommand(ExecuteInsertSheet);

            // Load initial data
            LoadSheets();
        }

        #region Properties

        public ObservableCollection<SheetItemModel> Sheets
        {
            get => _sheets;
            set
            {
                _sheets = value;
                OnPropertyChanged(nameof(Sheets));
            }
        }

        public ObservableCollection<SheetItemModel> FilteredSheets
        {
            get => _filteredSheets;
            set
            {
                _filteredSheets = value;
                OnPropertyChanged(nameof(FilteredSheets));
            }
        }

        public ObservableCollection<string> AvailableParameters
        {
            get => _availableParameters;
            set
            {
                _availableParameters = value;
                OnPropertyChanged(nameof(AvailableParameters));
            }
        }

        public string SelectedParameter
        {
            get => _selectedParameter;
            set
            {
                _selectedParameter = value;
                OnPropertyChanged(nameof(SelectedParameter));
                ExecuteApplyFilter(null);
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                OnPropertyChanged(nameof(FilterText));
                ExecuteApplyFilter(null);
            }
        }

        public string Prefix
        {
            get => _prefix;
            set
            {
                _prefix = value;
                OnPropertyChanged(nameof(Prefix));
            }
        }

        public int StartNumber
        {
            get => _startNumber;
            set
            {
                _startNumber = value;
                OnPropertyChanged(nameof(StartNumber));
            }
        }

        public int Increment
        {
            get => _increment;
            set
            {
                _increment = value;
                OnPropertyChanged(nameof(Increment));
            }
        }

        public int PaddingDigits
        {
            get => _paddingDigits;
            set
            {
                _paddingDigits = value;
                OnPropertyChanged(nameof(PaddingDigits));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
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

        public ICommand LoadSheetsCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand RenumberSheetsCommand { get; }
        public ICommand PreviewRenumberingCommand { get; }
        public ICommand MoveSheetUpCommand { get; }
        public ICommand MoveSheetDownCommand { get; }
        public ICommand InsertSheetCommand { get; }

        #endregion

        #region Command Implementations

        private void ExecuteLoadSheets(object parameter)
        {
            LoadSheets();
        }

        private void LoadSheets()
        {
            try
            {
                StatusMessage = "Loading sheets...";

                var document = _document.Document;
                var sheetList = _sheetService.GetAllSheets(document);

                Sheets.Clear();
                foreach (var sheet in sheetList)
                {
                    Sheets.Add(sheet);
                }

                // Load available parameters
                var parameters = _sheetService.GetAvailableSheetParameters(document);
                AvailableParameters.Clear();
                AvailableParameters.Add("All"); // Default option
                foreach (var param in parameters)
                {
                    AvailableParameters.Add(param);
                }

                SelectedParameter = "All";
                ExecuteApplyFilter(null);

                StatusMessage = $"Loaded {Sheets.Count} sheets";
                _logger.LogInformation($"Loaded {Sheets.Count} sheets");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading sheets: {ex.Message}";
                _logger.LogError($"Error loading sheets: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"Error loading sheets: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteApplyFilter(object parameter)
        {
            try
            {
                FilteredSheets.Clear();

                if (string.IsNullOrWhiteSpace(FilterText) && (SelectedParameter == "All" || string.IsNullOrWhiteSpace(SelectedParameter)))
                {
                    // No filter applied, show all sheets
                    foreach (var sheet in Sheets)
                    {
                        FilteredSheets.Add(sheet);
                    }
                }
                else
                {
                    // Apply filter
                    string paramName = SelectedParameter == "All" ? null : SelectedParameter;
                    foreach (var sheet in Sheets)
                    {
                        if (sheet.MatchesFilter(paramName, FilterText))
                        {
                            FilteredSheets.Add(sheet);
                        }
                    }
                }

                StatusMessage = $"Showing {FilteredSheets.Count} of {Sheets.Count} sheets";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying filter: {ex.Message}", ex);
            }
        }

        private void ExecuteClearFilter(object parameter)
        {
            FilterText = string.Empty;
            SelectedParameter = "All";
            ExecuteApplyFilter(null);
        }

        private bool CanExecuteRenumberSheets(object parameter)
        {
            return FilteredSheets != null && FilteredSheets.Count > 0 && !IsLoading;
        }

        private void ExecuteRenumberSheets(object parameter)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"This will renumber {FilteredSheets.Count} sheets starting from {Prefix}{StartNumber.ToString().PadLeft(PaddingDigits, '0')}.\n\nDo you want to continue?",
                    "Confirm Renumbering",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                StatusMessage = "Renumbering sheets...";
                _logger.LogInformation("Starting renumbering operation");

                var model = CreateRenumberingModel();
                _logger.LogInformation($"Created renumbering model with {model.Sheets.Count} sheets");

                _renumberEventHandler.SetParameters(model, OnRenumberingCompleted);
                _logger.LogInformation("Set parameters on event handler");

                // Set loading state BEFORE raising the event
                IsLoading = true;
                _logger.LogInformation("IsLoading set to true");

                var raiseResult = _renumberExternalEvent.Raise();
                _logger.LogInformation($"External event Raise() returned: {raiseResult}");

                // If the event couldn't be raised, reset and show error
                if (raiseResult != ExternalEventRequest.Accepted)
                {
                    IsLoading = false;
                    StatusMessage = "Could not start renumbering operation";
                    _logger.LogError($"External event raise failed with result: {raiseResult}");
                    System.Windows.MessageBox.Show($"Could not start renumbering. The operation was {raiseResult}.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error starting renumbering: {ex.Message}";
                _logger.LogError($"Error starting renumbering: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        private void ExecutePreviewRenumbering(object parameter)
        {
            try
            {
                var model = CreateRenumberingModel();
                var preview = _sheetService.PreviewRenumbering(model);

                var previewText = "Preview of new sheet numbers:\n\n";
                foreach (var sheet in FilteredSheets)
                {
                    if (preview.TryGetValue(sheet.ElementId, out string newNumber))
                    {
                        previewText += $"{sheet.SheetNumber} → {newNumber}\n";
                    }
                }

                System.Windows.MessageBox.Show(previewText, "Renumbering Preview", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating preview: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"Error creating preview: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private bool CanExecuteMoveSheetUp(object parameter)
        {
            if (parameter is SheetItemModel sheet)
            {
                int index = FilteredSheets.IndexOf(sheet);
                return index > 0;
            }
            return false;
        }

        private void ExecuteMoveSheetUp(object parameter)
        {
            if (parameter is SheetItemModel sheet)
            {
                int index = FilteredSheets.IndexOf(sheet);
                if (index > 0)
                {
                    FilteredSheets.Move(index, index - 1);
                    UpdateDisplayOrders();
                }
            }
        }

        private bool CanExecuteMoveSheetDown(object parameter)
        {
            if (parameter is SheetItemModel sheet)
            {
                int index = FilteredSheets.IndexOf(sheet);
                return index < FilteredSheets.Count - 1;
            }
            return false;
        }

        private void ExecuteMoveSheetDown(object parameter)
        {
            if (parameter is SheetItemModel sheet)
            {
                int index = FilteredSheets.IndexOf(sheet);
                if (index < FilteredSheets.Count - 1)
                {
                    FilteredSheets.Move(index, index + 1);
                    UpdateDisplayOrders();
                }
            }
        }

        private void ExecuteInsertSheet(object parameter)
        {
            System.Windows.MessageBox.Show("Insert sheet functionality - to be implemented based on your requirements", "Information", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        #endregion

        #region Helper Methods

        private SheetRenumberingModel CreateRenumberingModel()
        {
            var model = new SheetRenumberingModel
            {
                Prefix = Prefix,
                StartNumber = StartNumber,
                Increment = Increment,
                PaddingDigits = PaddingDigits,
                Sheets = new List<SheetRenumberingItem>()
            };

            for (int i = 0; i < FilteredSheets.Count; i++)
            {
                var sheet = FilteredSheets[i];
                int newNumber = StartNumber + (i * Increment);
                string paddedNumber = newNumber.ToString().PadLeft(PaddingDigits, '0');
                string fullSheetNumber = $"{Prefix}{paddedNumber}";

                model.Sheets.Add(new SheetRenumberingItem
                {
                    ElementId = sheet.ElementId,
                    NewSheetNumber = fullSheetNumber,
                    CurrentSheetNumber = sheet.SheetNumber,
                    Order = i
                });
            }

            return model;
        }

        private void OnRenumberingCompleted(bool success)
        {
            // External events run on the Revit UI thread
            try
            {
                _logger.LogInformation($"OnRenumberingCompleted called with success={success}");

                // Hide loading overlay
                IsLoading = false;
                _logger.LogInformation("IsLoading set to false");

                if (success)
                {
                    StatusMessage = "Renumbering completed successfully";
                    // Reload sheets to see new numbers
                    LoadSheets();
                    System.Windows.MessageBox.Show(
                        "Sheets renumbered successfully!",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Renumbering failed";
                    System.Windows.MessageBox.Show(
                        "Failed to renumber sheets. Check the log for details.",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }

                _logger.LogInformation("OnRenumberingCompleted completed successfully");
            }
            catch (Exception ex)
            {
                IsLoading = false;
                _logger.LogError($"Error in OnRenumberingCompleted: {ex.Message}", ex);
            }
        }

        private void UpdateDisplayOrders()
        {
            for (int i = 0; i < FilteredSheets.Count; i++)
            {
                FilteredSheets[i].DisplayOrder = i;
            }
        }

        /// <summary>
        /// Handle drag-drop reordering
        /// </summary>
        public void MoveSheet(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= FilteredSheets.Count ||
                newIndex < 0 || newIndex >= FilteredSheets.Count ||
                oldIndex == newIndex)
                return;

            FilteredSheets.Move(oldIndex, newIndex);
            UpdateDisplayOrders();
        }

        #endregion
    }
}
