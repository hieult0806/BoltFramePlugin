using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
using LoBIM.Features.ViewCloning.Strategies;
using LoBIM.Services;
using LoBIM.Services.Parameters;
using LoBIM.ViewModels;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.ViewCloning.ViewModels
{
    public class ViewCloningWindowVM : BaseViewModel
    {
        private readonly IViewCloningService _viewCloningService;
        private readonly ILoggingService _logger;
        private readonly ExternalEvent _cloneViewsEvent;
        private readonly LoBIM.Features.ViewCloning.EventHandlers.CloneViewsEventHandler _cloneViewsHandler;
        private readonly ExternalEvent _openViewEvent;
        private readonly LoBIM.Features.ViewCloning.EventHandlers.OpenViewEventHandler _openViewHandler;
        private readonly ExternalEvent _highlightViewEvent;
        private readonly LoBIM.Features.ViewCloning.EventHandlers.HighlightViewEventHandler _highlightViewHandler;

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
                OnPropertyChanged(nameof(AvailableViews));
            }
        }

        public bool HasSelectedLink => SelectedLinkedFile != null;

        private LinkedViewInfo? _selectedView;
        public LinkedViewInfo? SelectedView
        {
            get => _selectedView;
            set
            {
                _selectedView = value;
                OnPropertyChanged(nameof(SelectedView));
                OnPropertyChanged(nameof(CanShowInProjectBrowser));

                // Auto-highlight cloned view in Project Browser on single click
                if (_selectedView != null && _selectedView.IsCloned && _selectedView.ClonedViewId != null)
                {
                    HighlightViewInProjectBrowser();
                }
            }
        }

        public bool CanShowInProjectBrowser => SelectedView != null && SelectedView.IsCloned && SelectedView.ClonedViewId != null;

        public ObservableCollection<LinkedViewInfo> AvailableViews
        {
            get
            {
                if (SelectedLinkedFile == null)
                    return new ObservableCollection<LinkedViewInfo>();

                return new ObservableCollection<LinkedViewInfo>(SelectedLinkedFile.Views);
            }
        }

        private string _namePrefix = "";
        public string NamePrefix
        {
            get => _namePrefix;
            set
            {
                _namePrefix = value;
                OnPropertyChanged(nameof(NamePrefix));
            }
        }

        private ViewPositioningMode _selectedPositioningMode = ViewPositioningMode.InternalOriginToInternalOrigin;
        public ViewPositioningMode SelectedPositioningMode
        {
            get => _selectedPositioningMode;
            set
            {
                _selectedPositioningMode = value;
                OnPropertyChanged(nameof(SelectedPositioningMode));
            }
        }

        public Array PositioningModes => Enum.GetValues(typeof(ViewPositioningMode));

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

        // Commands
        public ICommand RefreshLinkedFilesCommand { get; }
        public ICommand SelectAllViewsCommand { get; }
        public ICommand DeselectAllViewsCommand { get; }
        public ICommand CloneSelectedViewsCommand { get; }
        public ICommand LogSectionMarkerPositionCommand { get; }
        public ICommand ShowInProjectBrowserCommand { get; }
        public ICommand CloseCommand { get; }

        public ViewCloningWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _viewCloningService = DIContainerService.Container.GetInstance<IViewCloningService>();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();

            _linkedFiles = new ObservableCollection<LinkedFileInfo>();

            // Initialize ExternalEvent for cloning views
            _cloneViewsHandler = new LoBIM.Features.ViewCloning.EventHandlers.CloneViewsEventHandler();
            _cloneViewsEvent = ExternalEvent.Create(_cloneViewsHandler);

            // Initialize ExternalEvent for opening views
            _openViewHandler = new LoBIM.Features.ViewCloning.EventHandlers.OpenViewEventHandler();
            _openViewEvent = ExternalEvent.Create(_openViewHandler);

            // Initialize ExternalEvent for highlighting views in Project Browser
            _highlightViewHandler = new LoBIM.Features.ViewCloning.EventHandlers.HighlightViewEventHandler(_logger);
            _highlightViewEvent = ExternalEvent.Create(_highlightViewHandler);

            // Initialize commands
            RefreshLinkedFilesCommand = new RelayCommand(RefreshLinkedFiles);
            SelectAllViewsCommand = new RelayCommand(SelectAllViews);
            DeselectAllViewsCommand = new RelayCommand(DeselectAllViews);
            CloneSelectedViewsCommand = new RelayCommand(CloneSelectedViews, CanCloneViews);
            LogSectionMarkerPositionCommand = new RelayCommand(LogSectionMarkerPosition);
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
                _logger.LogInformation("Ensuring view source tracking parameters exist...");

                var parameterService = DIContainerService.Container.GetInstance<IProjectParameterService>();

                // Ensure view source tracking parameters
                var viewStrategy = new PlanViewCloningStrategy(_logger, parameterService);
                viewStrategy.EnsureSourceTrackingParameters(_document.Document);

                _logger.LogInformation("View source tracking parameters ready");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not ensure view source tracking parameters: {ex.Message}");
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

        private void SelectAllViews(object parameter)
        {
            if (SelectedLinkedFile == null) return;

            foreach (var view in SelectedLinkedFile.Views)
            {
                view.IsSelected = true;
            }

            StatusMessage = $"Selected all {SelectedLinkedFile.Views.Count} views";
        }

        private void DeselectAllViews(object parameter)
        {
            if (SelectedLinkedFile == null) return;

            foreach (var view in SelectedLinkedFile.Views)
            {
                view.IsSelected = false;
            }

            StatusMessage = "Deselected all views";
        }

        private bool CanCloneViews(object parameter)
        {
            return SelectedLinkedFile != null &&
                   SelectedLinkedFile.Views.Any(v => v.IsSelected && !v.IsCloned);
        }

        private void CloneSelectedViews(object parameter)
        {
            try
            {
                if (SelectedLinkedFile == null) return;

                var selectedViews = SelectedLinkedFile.Views
                    .Where(v => v.IsSelected && !v.IsCloned)
                    .ToList();

                if (selectedViews.Count == 0)
                {
                    TaskDialog.Show("No Views Selected", "Please select at least one view to clone.");
                    return;
                }

                IsLoading = true;
                StatusMessage = $"Cloning {selectedViews.Count} view(s)...";

                // Use ExternalEvent to execute cloning in Revit API context
                _cloneViewsHandler.SetParameters(selectedViews, NamePrefix, SelectedPositioningMode, OnCloningCompleted);
                _cloneViewsEvent.Raise();

                _logger.LogInformation($"Initiated cloning of {selectedViews.Count} views with prefix '{NamePrefix}'");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initiating view cloning: {ex.Message}", ex);
                StatusMessage = $"Error: {ex.Message}";
                TaskDialog.Show("Error", $"Failed to initiate view cloning: {ex.Message}");
                IsLoading = false;
            }
        }

        private void ShowInProjectBrowser(object parameter)
        {
            try
            {
                if (SelectedView == null || !SelectedView.IsCloned || SelectedView.ClonedViewId == null)
                {
                    StatusMessage = "Please select a cloned view to show in Project Browser";
                    return;
                }

                // Use ExternalEvent to open the view
                _openViewHandler.SetParameters(_document, SelectedView.ClonedViewId, OnOpenViewCompleted);
                _openViewEvent.Raise();

                StatusMessage = "Opening view...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error showing view: {ex.Message}";
                _logger.LogError($"Error showing view: {ex.Message}", ex);
            }
        }

        private void OnOpenViewCompleted(bool success, string viewName)
        {
            if (success)
            {
                StatusMessage = $"Opened view '{viewName}'";
                _logger.LogInformation($"Opened view '{viewName}' (Source: {SelectedView?.SourceFileName} > {SelectedView?.SourceViewName})");
            }
            else
            {
                StatusMessage = $"Error opening view: {viewName}";
                _logger.LogError($"Error opening view: {viewName}");
            }
        }

        /// <summary>
        /// Highlights the selected view in the Project Browser without opening it
        /// Triggered automatically when a cloned view is selected (single click)
        /// </summary>
        private void HighlightViewInProjectBrowser()
        {
            try
            {
                if (SelectedView == null || !SelectedView.IsCloned || SelectedView.ClonedViewId == null)
                {
                    return;
                }

                // Use ExternalEvent to highlight the view
                _highlightViewHandler.SetParameters(_document, SelectedView.ClonedViewId, OnHighlightViewCompleted);
                _highlightViewEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error highlighting view: {ex.Message}", ex);
            }
        }

        private void OnHighlightViewCompleted(bool success, string viewName)
        {
            if (success)
            {
                _logger.LogInformation($"Highlighted view '{viewName}' in Project Browser");
            }
            else
            {
                _logger.LogError($"Error highlighting view: {viewName}");
            }
        }

        /// <summary>
        /// Opens the selected view as the active view
        /// Triggered by double-click on DataGrid row
        /// </summary>
        public void OpenSelectedView()
        {
            ShowInProjectBrowser(null);
        }

        private void LogSectionMarkerPosition(object parameter)
        {
            try
            {
                var doc = _document.Document;

                Autodesk.Revit.DB.View view = null;

                // First, try to use the active view if it's a section
                if (_document.ActiveView is Autodesk.Revit.DB.View activeView)
                {
                    _logger.LogInformation($"Using active view: {activeView.Name}");
                    view = activeView;
                }
                else
                {
                    // Try to get from selection
                    var selection = _document.Selection;
                    if (selection.GetElementIds().Count > 0)
                    {
                        var selectedId = selection.GetElementIds().FirstOrDefault();
                        if (selectedId != null)
                        {
                            var element = doc.GetElement(selectedId);
                            _logger.LogInformation($"Selected Element Type: {element?.GetType().Name}");
                            _logger.LogInformation($"Selected Element Category: {element?.Category?.Name}");
                            _logger.LogInformation($"Selected Element Id: {element?.Id.Value}");

                            view = element as Autodesk.Revit.DB.View;
                        }
                    }
                }

                if (view != null)
                {
                    _logger.LogInformation($"=== VIEW INFORMATION ===");
                    _logger.LogInformation($"View Type: {view.GetType().Name}");
                    _logger.LogInformation($"View Category: {view.Category?.Name}");
                    _logger.LogInformation($"View IsCallout: {view.IsCallout}");
                    _logger.LogInformation($"View Name: {view.Name}");
                    _logger.LogInformation($"View Id: {view.Id.Value}");
                    _logger.LogInformation($"Origin: {view.Origin}");
                    _logger.LogInformation($"Direction: {view.ViewDirection}");
                    _logger.LogInformation($"Up Direction: {view.UpDirection}");
                    _logger.LogInformation($"Right Direction: {view.RightDirection}");

                    var cropBox = view.CropBox;
                    if (cropBox != null)
                    {
                        _logger.LogInformation($"=== CROPBOX INFORMATION ===");
                        _logger.LogInformation($"CropBox Origin: {cropBox.Transform.Origin}");
                        _logger.LogInformation($"CropBox BasisX: {cropBox.Transform.BasisX}");
                        _logger.LogInformation($"CropBox BasisY: {cropBox.Transform.BasisY}");
                        _logger.LogInformation($"CropBox BasisZ: {cropBox.Transform.BasisZ}");
                        _logger.LogInformation($"CropBox Scale: {cropBox.Transform.Scale}");
                        _logger.LogInformation($"CropBox Min: {cropBox.Min}");
                        _logger.LogInformation($"CropBox Max: {cropBox.Max}");
                    }

                    StatusMessage = $"Logged position info for section: {view.Name}";
                    _logger.LogInformation($"Logged position information for section '{view.Name}'. Check the log file for details.");
                }
                else
                {
                    _logger.LogWarning($"Could not find a view. Please open a view first.");
                    StatusMessage = "No active view found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error logging section marker position: {ex.Message}", ex);
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private void OnCloningCompleted(List<ElementId> clonedViewIds)
        {
            try
            {
                var totalCount = SelectedLinkedFile?.Views.Count(v => v.IsSelected && !v.IsCloned) ?? 0;
                var successCount = clonedViewIds?.Count ?? 0;

                // Update status message with result
                if (successCount > 0)
                {
                    StatusMessage = $"Successfully cloned {successCount} of {totalCount} view(s) from {SelectedLinkedFile?.FileName}. Opened last cloned view.";

                    // Reload linked files to refresh source tracking info
                    RefreshLinkedFiles(null);
                }
                else
                {
                    StatusMessage = "No views were cloned. Check the log for details.";
                }

                _logger.LogInformation($"Cloning completed: {successCount} views cloned");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in cloning completion handler: {ex.Message}", ex);
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Close(object parameter)
        {
            OnRequestClose(EventArgs.Empty);
        }
    }
}
