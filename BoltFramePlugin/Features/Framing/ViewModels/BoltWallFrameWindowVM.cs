using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Features.Framing.EventHandlers;
using BoltFramePlugin.Features.Framing.Models;
using BoltFramePlugin.Features.Framing.Strategies;
using BoltFramePlugin.Services;
using BoltFramePlugin.ViewModels;
using BoltFramePlugin.ViewModels.UserControls;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.Features.Framing.ViewModels
{
    internal class BoltWallFrameWindowVM : BaseViewModel
    {
        private ExternalEvent _externalGenerateEvent;
        private GenerateFrameEventHandler _generateEventHandler;

        // Commands
        public ICommand CloseCommand { get; }
        public ICommand GenerateFrameCommand { get; }
        public ICommand OpenConfigurationWindowCommand { get; }

        private IRevitService _revitService;

        private InputWallFrameConfigurationVM _topPlateConfiguration;
        public InputWallFrameConfigurationVM TopPlateConfiguration
        {
            get => _topPlateConfiguration;
            set
            {
                _topPlateConfiguration = value;
                OnPropertyChanged(nameof(TopPlateConfiguration));
            }
        }

        private InputWallFrameConfigurationVM _bottomPlateConfiguration;
        public InputWallFrameConfigurationVM BottomPlateConfiguration
        {
            get => _bottomPlateConfiguration;
            set
            {
                _bottomPlateConfiguration = value;
                OnPropertyChanged(nameof(BottomPlateConfiguration));
            }
        }

        private InputWallFrameConfigurationVM _columnConfiguration;
        public InputWallFrameConfigurationVM ColumnConfiguration
        {
            get => _columnConfiguration;
            set
            {
                _columnConfiguration = value;
                OnPropertyChanged(nameof(ColumnConfiguration));
            }
        }

        private InputWallFrameConfigurationVM _studConfiguration;
        public InputWallFrameConfigurationVM StudConfiguration
        {
            get => _studConfiguration;
            set
            {
                _studConfiguration = value;
                OnPropertyChanged(nameof(StudConfiguration));
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

        private string _groupName = "Wall Framing";
        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged(nameof(GroupName));
            }
        }

        public BoltWallFrameWindowVM(UIDocument document) : base(document)
        {
            _revitService = DIContainerService.Container.GetInstance<IRevitServiceFactory>().Create(_document);

            _generateEventHandler = new GenerateFrameEventHandler();
            _externalGenerateEvent = ExternalEvent.Create(_generateEventHandler);

            // Initialize commands
            CloseCommand = new RelayCommand(OnClose);
            GenerateFrameCommand = new RelayCommand(GenerateFrame);
            OpenConfigurationWindowCommand = new RelayCommand(OpenConfigurationWindow);

            TopPlateConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Top Plate");
            BottomPlateConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Bottom Plate");
            ColumnConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Column");
            StudConfiguration = new InputWallFrameConfigurationVM(_revitService, _windowManager, "Stud");

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
                logger.LogInformation("GenerateFrame button clicked (Wall Framing)");

                // Get selected elements from Revit selection
                var selectionService = new RevitSelectionService(_revitService.UiDoc);
                var selectedElements = selectionService.GetSelectedElements();

                logger.LogInformation($"Found {selectedElements?.Count ?? 0} selected elements");

                if (selectedElements == null || !selectedElements.Any())
                {
                    TaskDialog.Show("BoltFrame", "No elements selected. Please select wall elements to generate framing.");
                    return;
                }

                // Filter only walls
                var walls = selectedElements.Where(e => e is Wall).ToList();

                if (!walls.Any())
                {
                    TaskDialog.Show("BoltFrame", "No wall elements found in selection. Please select walls.");
                    return;
                }

                logger.LogInformation($"Found {walls.Count} wall elements");

                // Build wall level dictionary
                var wallLevels = new Dictionary<ElementId, Level>();
                foreach (Wall wall in walls.Cast<Wall>())
                {
                    ElementId levelId = wall.LevelId;
                    if (levelId != null && levelId != ElementId.InvalidElementId)
                    {
                        Level level = _revitService.UiDoc.Document.GetElement(levelId) as Level;
                        if (level != null)
                        {
                            wallLevels[wall.Id] = level;
                        }
                    }
                }

                // Create wall frame model
                var model = new WallFrameGenerateModel
                {
                    TargetElements = walls,
                    WallLevels = wallLevels,
                    CreateGroup = CreateGroup,
                    GroupName = GroupName
                };

                // Add configurations based on what's enabled
                if (TopPlateConfiguration.IsEnabled && TopPlateConfiguration.Config.FamilySymbol != null)
                {
                    model.TopPlateConfig = TopPlateConfiguration.Config;
                    logger.LogInformation($"Top plate enabled with spacing: {TopPlateConfiguration.Config.Spacing}");
                }

                if (BottomPlateConfiguration.IsEnabled && BottomPlateConfiguration.Config.FamilySymbol != null)
                {
                    model.BottomPlateConfig = BottomPlateConfiguration.Config;
                    logger.LogInformation($"Bottom plate enabled with spacing: {BottomPlateConfiguration.Config.Spacing}");
                }

                if (ColumnConfiguration.IsEnabled && ColumnConfiguration.Config.FamilySymbol != null)
                {
                    model.ColumnConfig = ColumnConfiguration.Config;
                    logger.LogInformation($"Column enabled with spacing: {ColumnConfiguration.Config.Spacing}");
                }

                if (StudConfiguration.IsEnabled && StudConfiguration.Config.FamilySymbol != null)
                {
                    model.StudConfig = StudConfiguration.Config;
                    logger.LogInformation($"Stud enabled with spacing: {StudConfiguration.Config.Spacing}");
                }

                // Set parameters and raise the external event
                _generateEventHandler.SetParameters(_revitService, model);
                _externalGenerateEvent.Raise();

                logger.LogInformation("External event raised for wall framing generation");
            }
            catch (Exception ex)
            {
                var logger = DIContainerService.Container.GetInstance<ILoggingService>();
                logger.LogError("Error in GenerateFrame", ex);
                TaskDialog.Show("Error", $"An error occurred: {ex.Message}");
            }
        }

        private void OpenConfigurationWindow(object parameter)
        {
            // Open configuration window
            TaskDialog.Show("Configuration", "Configuration window not yet implemented.");
        }
    }
}
