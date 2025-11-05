using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.UI;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
using LoBIM.Services;
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
        public ICommand CloseCommand { get; }

        public ViewCloningWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _viewCloningService = DIContainerService.Container.GetInstance<IViewCloningService>();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();

            _linkedFiles = new ObservableCollection<LinkedFileInfo>();

            // Initialize ExternalEvent for cloning views
            _cloneViewsHandler = new LoBIM.Features.ViewCloning.EventHandlers.CloneViewsEventHandler();
            _cloneViewsEvent = ExternalEvent.Create(_cloneViewsHandler);

            // Initialize commands
            RefreshLinkedFilesCommand = new RelayCommand(RefreshLinkedFiles);
            SelectAllViewsCommand = new RelayCommand(SelectAllViews);
            DeselectAllViewsCommand = new RelayCommand(DeselectAllViews);
            CloneSelectedViewsCommand = new RelayCommand(CloneSelectedViews, CanCloneViews);
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
                _cloneViewsHandler.SetParameters(selectedViews, NamePrefix, OnCloningCompleted);
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

        private void OnCloningCompleted(int successCount)
        {
            try
            {
                var totalCount = SelectedLinkedFile?.Views.Count(v => v.IsSelected && !v.IsCloned) ?? 0;

                StatusMessage = $"Successfully cloned {successCount} out of {totalCount} views";

                // Refresh the UI
                OnPropertyChanged(nameof(AvailableViews));

                if (successCount > 0)
                {
                    TaskDialog.Show("Success",
                        $"Successfully cloned {successCount} view(s) from {SelectedLinkedFile?.FileName}");
                }
                else
                {
                    TaskDialog.Show("Warning",
                        "No views were cloned. Check the log for details.");
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
