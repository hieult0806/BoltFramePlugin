using System.Collections.ObjectModel;
using System.ComponentModel;
using BoltFramePlugin.Models;
using System.Windows.Input;
using BoltFramePlugin.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.EventHandlers;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.ViewModels.Framing;
using BoltFramePlugin.ViewModels.UserControls;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.ViewModels
{
    internal class BoltFrameMainWindowVM : BaseViewModel
    {
        private ExternalEvent _externalGenerateEvent;
        private GenerateFrameEventHandler _generateEventHandler;

        private ExternalEvent _externalCreateBeamPocketEvent;
        private CreateBeamPocketEventHandler _createBeamPocketEventHandler;

        // ObservableCollection for attributes
        public ObservableCollection<AttributeItem> Attributes { get; set; }

        // Commands
        public ICommand CloseCommand { get; }
        public ICommand GenerateFrameCommand { get; }
        public ICommand OpenConfigurationWindowCommand { get; }
        public ICommand CreateBeamPocketCommand { get; }

        private IRevitService _revitService;

        private InputWallFrameConfigurationVM _horizontalFrameConfiguration;
        public InputWallFrameConfigurationVM HorizontalFrameConfiguration
        {
            get => _horizontalFrameConfiguration;
            set
            {
                _horizontalFrameConfiguration = value;
                OnPropertyChanged(nameof(HorizontalFrameConfiguration));
            }
        }

        private InputWallFrameConfigurationVM _verticalFrameConfiguration;
        public InputWallFrameConfigurationVM VerticalFrameConfiguration
        {
            get => _verticalFrameConfiguration;
            set
            {
                _verticalFrameConfiguration = value;
                OnPropertyChanged(nameof(VerticalFrameConfiguration));
            }
        }

        private InputWallFrameConfigurationVM _boundaryFrameConfiguration;
        public InputWallFrameConfigurationVM BoundaryFrameConfiguration
        {
            get => _boundaryFrameConfiguration;
            set
            {
                _boundaryFrameConfiguration = value;
                OnPropertyChanged(nameof(BoundaryFrameConfiguration));
            }
        }

        private TreeViewUcVM _treeViewUcVM;
        public TreeViewUcVM TreeViewUcVM { get => _treeViewUcVM; set { _treeViewUcVM = value; OnPropertyChanged(nameof(TreeViewUcVM)); } }

        private bool _createGroup = true;
        public bool CreateGroup
        {
            get => _createGroup;
            set
            {
                _createGroup = value;
                OnPropertyChanged(nameof(CreateGroup));
            }
        }

        private string _groupName = "Floor Framing";
        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged(nameof(GroupName));
            }
        }

        public BoltFrameMainWindowVM(UIDocument document) : base(document)
        {
            _revitService = DIContainerService.Container.GetInstance<IRevitServiceFactory>().Create(_document);

            _generateEventHandler = new GenerateFrameEventHandler();
            _externalGenerateEvent = ExternalEvent.Create(_generateEventHandler);

            _createBeamPocketEventHandler = new CreateBeamPocketEventHandler();
            _externalCreateBeamPocketEvent = ExternalEvent.Create(_createBeamPocketEventHandler);

            // Initialize commands
            CloseCommand = new RelayCommand(OnClose);
            GenerateFrameCommand = new RelayCommand(GenerateFrame);
            OpenConfigurationWindowCommand = new RelayCommand(OpenConfigurationWindow);
            CreateBeamPocketCommand = new RelayCommand(CreateBeamPocket);

            HorizontalFrameConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Horizontal Beam");
            VerticalFrameConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Vertical Beam");
            BoundaryFrameConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Boundary Beam");
            TreeViewUcVM = new TreeViewUcVM(_revitService.UiDoc);
            TreeViewUcVM.PropertyChanged += TreeViewUcVM_PropertyChanged;
        }

        private void TreeViewUcVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {

        }

        private void OnClose(object parameter)
        {
            DialogResult = false;
            OnRequestClose(EventArgs.Empty);
        }

        private void GenerateFrame(object parameter)
        {
            try
            {
                var logger = DIContainerService.Container.GetInstance<ILoggingService>();
                logger.LogInformation("GenerateFrame button clicked");

                // Get selected elements from Revit selection
                var selectionService = new RevitSelectionService(_revitService.UiDoc);
                var selectedElements = selectionService.GetSelectedElements();

                logger.LogInformation($"Found {selectedElements?.Count ?? 0} selected elements");

                if (selectedElements == null || !selectedElements.Any())
                {
                    TaskDialog.Show("BoltFrame", "No elements selected. Please select floor elements to generate framing.");
                    return;
                }

                // Create grid configurations from horizontal and vertical settings
                var gridConfigs = new List<GridConfig>();

                // Add horizontal beam configuration if enabled and spacing is set
                if (HorizontalFrameConfiguration.IsEnabled &&
                    HorizontalFrameConfiguration.Config.Spacing > 0 &&
                    HorizontalFrameConfiguration.ElementPreview?.Symbol != null)
                {
                    HorizontalFrameConfiguration.Config.Orientation = GridOrientation.Horizontal;
                    HorizontalFrameConfiguration.Config.FamilySymbol = HorizontalFrameConfiguration.ElementPreview.Symbol;
                    gridConfigs.Add(HorizontalFrameConfiguration.Config);
                    logger.LogInformation("Horizontal beam configuration added");
                }

                // Add vertical beam configuration if enabled and spacing is set
                if (VerticalFrameConfiguration.IsEnabled &&
                    VerticalFrameConfiguration.Config.Spacing > 0 &&
                    VerticalFrameConfiguration.ElementPreview?.Symbol != null)
                {
                    VerticalFrameConfiguration.Config.Orientation = GridOrientation.Vertical;
                    VerticalFrameConfiguration.Config.FamilySymbol = VerticalFrameConfiguration.ElementPreview.Symbol;
                    gridConfigs.Add(VerticalFrameConfiguration.Config);
                    logger.LogInformation("Vertical beam configuration added");
                }

                // Prepare boundary beam configuration (if enabled)
                GridConfig boundaryConfig = null;
                if (BoundaryFrameConfiguration.IsEnabled &&
                    BoundaryFrameConfiguration.ElementPreview?.Symbol != null)
                {
                    BoundaryFrameConfiguration.Config.Orientation = GridOrientation.Horizontal; // Doesn't matter for boundary
                    BoundaryFrameConfiguration.Config.FamilySymbol = BoundaryFrameConfiguration.ElementPreview.Symbol;
                    boundaryConfig = BoundaryFrameConfiguration.Config;
                    logger.LogInformation("Boundary beam configuration added");
                }

                if (gridConfigs.Count == 0 && boundaryConfig == null)
                {
                    TaskDialog.Show("BoltFrame", "Please enable and configure at least one beam type (boundary, horizontal, or vertical).");
                    return;
                }

                logger.LogInformation($"Grid configurations created: {gridConfigs.Count}, Boundary: {(boundaryConfig != null ? "Yes" : "No")}");

                // Create model for multi-floor framing
                var model = new MultiFloorFrameGenerateModel
                {
                    TargetElements = selectedElements,
                    GridConfigs = gridConfigs,
                    BoundaryConfig = boundaryConfig,
                    CreateGroup = CreateGroup,
                    GroupName = string.IsNullOrWhiteSpace(GroupName) ? "Floor Framing" : GroupName
                };

                // Set floor levels
                foreach (var element in selectedElements)
                {
                    if (element is Floor floor)
                    {
                        var level = _revitService.UiDoc.Document.GetElement(floor.LevelId) as Level;
                        if (level != null)
                        {
                            model.FloorLevels[element.Id] = level;
                        }
                    }
                }

                logger.LogInformation($"Floor levels set: {model.FloorLevels.Count}");
                logger.LogInformation("Raising external event for frame generation");

                // Execute frame generation via external event
                _generateEventHandler.SetParameters(_revitService, model);
                _externalGenerateEvent.Raise();

                logger.LogInformation("External event raised successfully");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("BoltFrame Error", $"Error generating frames: {ex.Message}");
            }
        }

        private void OpenConfigurationWindow(object parameter)
        {
            _windowManager.OpenDialog(new ConfigurationWindowVM(_document));
        }

        private void CreateBeamPocket(object parameter)
        {
            _createBeamPocketEventHandler.SetParameters(_revitService);
            _externalCreateBeamPocketEvent.Raise();
        }
    }
}
