using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LoBIM.EventHandlers;
using LoBIM.Features.NBCReview.EventHandlers;
using LoBIM.Features.NBCReview.Models;
using LoBIM.Features.NBCReview.Services;
using LoBIM.Filters;
using LoBIM.Services;
using LoBIM.Services.Parameters;
using LoBIM.ViewModels;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.NBCReview.ViewModels
{
    public class NBCReviewWindowVM : BaseViewModel
    {
        private IRevitService _revitService;
        private ILoggingService _logger;
        private INBCConfigurationService _nbcConfig;
        private IWallAnalysisService _wallAnalysisService;
        private INBCComplianceService _nbcComplianceService;
        private INBCInitializationService _nbcInitializationService;
        private IViewNavigationService _viewNavigationService;
        public ILoggingService Logger => _logger; // Expose for code-behind
        private ExternalEvent _createArrowsEvent;
        private CreateArrowsEventHandler _createArrowsHandler;
        private ExternalEvent _detectReferenceLinesEvent;
        private DetectReferenceLinesEventHandler _detectReferenceLinesHandler;
        private ExternalEvent _createProjectionsEvent;
        private CreateWallProjectionsEventHandler _createProjectionsHandler;
        private ExternalEvent _updateWallParameterEvent;
        private UpdateWallParameterEventHandler _updateWallParameterHandler;
        private ExternalEvent _deleteViewEvent;
        private DeleteViewEventHandler _deleteViewHandler;
        private ExternalEvent _initializeProjectParametersEvent;
        private InitializeProjectParametersEventHandler _initializeProjectParametersHandler;
        private ExternalEvent _loadFamiliesEvent;
        private LoadFamiliesEventHandler _loadFamiliesHandler;
        private ExternalEvent _initializeFilledRegionTypesEvent;
        private InitializeFilledRegionTypesEventHandler _initializeFilledRegionTypesHandler;
        private ExternalEvent _cleanupDuplicateFilledRegionTypesEvent;
        private CleanupDuplicateFilledRegionTypesEventHandler _cleanupDuplicateFilledRegionTypesHandler;

        // Configuration cache
        private List<FilledRegionTypeDefinition> _filledRegionTypeRanges = new List<FilledRegionTypeDefinition>();

        // Commands
        public ICommand SelectPropertyLineCommand { get; }
        public ICommand HighlightWallsCommand { get; }
        public ICommand CreateArrowsCommand { get; }
        public ICommand HighlightWallIn3DCommand { get; }
        public ICommand HighlightDistanceGroupCommand { get; }
        public ICommand HighlightWallInFloorPlanCommand { get; }
        public ICommand DetectReferenceLinesCommand { get; }
        public ICommand CreateWallProjectionsCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ShowLogsCommand { get; }
        public ICommand CheckCeilingWallConnectionsCommand { get; }
        public ICommand CheckWallCeilingConnectionsCommand { get; }
        public ICommand FindPerimeterWallsWithoutTopCeilingCommand { get; }
        public ICommand DebugWallCeilingTrimmingCommand { get; }
        public ICommand OpenViewCommand { get; }
        public ICommand DeleteViewCommand { get; }
        public ICommand DeleteAllViewsCommand { get; }
        public ICommand InitializeProjectParametersCommand { get; }
        public ICommand LoadRequiredFamiliesCommand { get; }
        public ICommand BrowseCustomFamilyCommand { get; }
        public ICommand InitializeFilledRegionTypesCommand { get; }
        public ICommand CleanupDuplicateFilledRegionTypesCommand { get; }
        public ICommand InitializeAllCommand { get; }

        // Properties
        private Element? _selectedPropertyLine;

        public Element? SelectedPropertyLine
        {
            get => _selectedPropertyLine;
            set
            {
                _selectedPropertyLine = value;
                OnPropertyChanged(nameof(SelectedPropertyLine));
                OnPropertyChanged(nameof(PropertyLineInfo));
                OnPropertyChanged(nameof(IsPropertyLineSelected));
            }
        }

        public string PropertyLineInfo
        {
            get
            {
                if (_selectedPropertyLine == null)
                    return "No property line selected";

                return $"Selected: {_selectedPropertyLine.Name} (ID: {_selectedPropertyLine.Id.Value})";
            }
        }

        public bool IsPropertyLineSelected => _selectedPropertyLine != null;

        private ObservableCollection<WallInfo> _perimeterWalls;
        public ObservableCollection<WallInfo> PerimeterWalls
        {
            get => _perimeterWalls;
            set
            {
                _perimeterWalls = value;
                OnPropertyChanged(nameof(PerimeterWalls));
                OnPropertyChanged(nameof(WallCount));
            }
        }

        public string WallCount => $"Perimeter Walls: {_perimeterWalls?.Count ?? 0}";

        private WallInfo? _selectedWall;
        public WallInfo? SelectedWall
        {
            get => _selectedWall;
            set
            {
                _selectedWall = value;
                OnPropertyChanged(nameof(SelectedWall));
            }
        }

        // Building Classification
        private string _buildingClassification = "Part 3: Commercial";
        public string BuildingClassification
        {
            get => _buildingClassification;
            set
            {
                _buildingClassification = value;
                OnPropertyChanged(nameof(BuildingClassification));
                _logger.LogInformation($"Building classification changed to: {value}");
            }
        }

        // Building classifications and occupant groups loaded from NBC configuration
        public List<string> BuildingClassifications => _nbcConfig.Configuration.GetBuildingCodePartNames();
        public List<string> OccupantGroups => _nbcConfig.Configuration.GetOccupantGroupCodes();

        // Ray Casting Configuration
        private double _rayLengthLimit = 500.0; // meters, configurable
        public double RayLengthLimit
        {
            get => _rayLengthLimit;
            set
            {
                _rayLengthLimit = value;
                OnPropertyChanged(nameof(RayLengthLimit));
            }
        }

        // Auto Create Arrows Configuration
        private bool _autoCreateArrows = false;
        private bool _isLoadingConfig = false; // Flag to prevent saving during initialization

        public bool AutoCreateArrows
        {
            get => _autoCreateArrows;
            set
            {
                if (_autoCreateArrows == value) return; // No change, skip

                _autoCreateArrows = value;
                OnPropertyChanged(nameof(AutoCreateArrows));

                // Only save to configuration if not currently loading
                if (!_isLoadingConfig)
                {
                    SaveAutoCreateArrowsSetting(value);
                    _logger.LogInformation($"Auto create arrows changed to: {value}");
                }
            }
        }

        // Reference Lines
        private ObservableCollection<ReferenceLineInfo> _referenceLines;
        public ObservableCollection<ReferenceLineInfo> ReferenceLines
        {
            get => _referenceLines;
            set
            {
                _referenceLines = value;
                OnPropertyChanged(nameof(ReferenceLines));
                OnPropertyChanged(nameof(ReferenceLineCount));
            }
        }

        public string ReferenceLineCount => $"Reference Lines: {_referenceLines?.Count ?? 0}";

        private string _debugOutput = "Debug information will appear here...";
        public string DebugOutput
        {
            get => _debugOutput;
            set
            {
                _debugOutput = value;
                OnPropertyChanged(nameof(DebugOutput));
            }
        }

        private ObservableCollection<DistanceGroupSummary> _distanceGroups;
        public ObservableCollection<DistanceGroupSummary> DistanceGroups
        {
            get => _distanceGroups;
            set
            {
                _distanceGroups = value;
                OnPropertyChanged(nameof(DistanceGroups));
            }
        }

        private ObservableCollection<BuildingCodeComplianceSummary> _buildingCodeCompliance;
        public ObservableCollection<BuildingCodeComplianceSummary> BuildingCodeCompliance
        {
            get => _buildingCodeCompliance;
            set
            {
                _buildingCodeCompliance = value;
                OnPropertyChanged(nameof(BuildingCodeCompliance));
            }
        }

        // Project Setup Collections
        private ObservableCollection<ProjectParameterInfo> _requiredProjectParameters;
        public ObservableCollection<ProjectParameterInfo> RequiredProjectParameters
        {
            get => _requiredProjectParameters;
            set
            {
                _requiredProjectParameters = value;
                OnPropertyChanged(nameof(RequiredProjectParameters));
            }
        }

        private ObservableCollection<RequiredFamilyInfo> _requiredFamilies;
        public ObservableCollection<RequiredFamilyInfo> RequiredFamilies
        {
            get => _requiredFamilies;
            set
            {
                _requiredFamilies = value;
                OnPropertyChanged(nameof(RequiredFamilies));
            }
        }

        private ObservableCollection<FilledRegionTypeInfo> _requiredFilledRegionTypes;
        public ObservableCollection<FilledRegionTypeInfo> RequiredFilledRegionTypes
        {
            get => _requiredFilledRegionTypes;
            set
            {
                _requiredFilledRegionTypes = value;
                OnPropertyChanged(nameof(RequiredFilledRegionTypes));
            }
        }

        // Created Views Tracking
        private ObservableCollection<ViewInfo> _createdViews;
        public ObservableCollection<ViewInfo> CreatedViews
        {
            get => _createdViews;
            set
            {
                _createdViews = value;
                OnPropertyChanged(nameof(CreatedViews));
                OnPropertyChanged(nameof(CreatedViewsCount));
            }
        }

        public string CreatedViewsCount => $"Created Views: {_createdViews?.Count ?? 0}";

        private ViewInfo? _selectedView;
        public ViewInfo? SelectedView
        {
            get => _selectedView;
            set
            {
                _selectedView = value;
                OnPropertyChanged(nameof(SelectedView));
                OnPropertyChanged(nameof(IsViewSelected));
            }
        }

        public bool IsViewSelected => _selectedView != null;

        private readonly IExtensibleStorageService _extensibleStorage;
        private readonly IPluginConfigurationManager _pluginConfig;

        public NBCReviewWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _revitService = DIContainerService.Container.GetInstance<IRevitServiceFactory>().Create(uidoc);
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _nbcConfig = DIContainerService.Container.GetInstance<INBCConfigurationService>();
            _extensibleStorage = DIContainerService.Container.GetInstance<IExtensibleStorageService>();
            _pluginConfig = DIContainerService.Container.GetInstance<IPluginConfigurationManager>();
            _wallAnalysisService = DIContainerService.Container.GetInstance<IWallAnalysisService>();
            _nbcComplianceService = DIContainerService.Container.GetInstance<INBCComplianceService>();
            _nbcInitializationService = DIContainerService.Container.GetInstance<INBCInitializationService>();
            _viewNavigationService = DIContainerService.Container.GetInstance<IViewNavigationService>();

            _perimeterWalls = new ObservableCollection<WallInfo>();
            _referenceLines = new ObservableCollection<ReferenceLineInfo>();
            _distanceGroups = new ObservableCollection<DistanceGroupSummary>();
            _buildingCodeCompliance = new ObservableCollection<BuildingCodeComplianceSummary>();
            _createdViews = new ObservableCollection<ViewInfo>();
            _requiredProjectParameters = new ObservableCollection<ProjectParameterInfo>();
            _requiredFamilies = new ObservableCollection<RequiredFamilyInfo>();
            _requiredFilledRegionTypes = new ObservableCollection<FilledRegionTypeInfo>();

            // Initialize project setup data
            InitializeProjectSetupData();

            // Initialize ExternalEvent for arrow creation
            _createArrowsHandler = new CreateArrowsEventHandler();
            _createArrowsEvent = ExternalEvent.Create(_createArrowsHandler);

            // Initialize ExternalEvent for detecting reference lines
            _detectReferenceLinesHandler = new DetectReferenceLinesEventHandler();
            _detectReferenceLinesEvent = ExternalEvent.Create(_detectReferenceLinesHandler);

            // Initialize ExternalEvent for creating wall projections
            _createProjectionsHandler = new CreateWallProjectionsEventHandler();
            _createProjectionsEvent = ExternalEvent.Create(_createProjectionsHandler);

            // Initialize ExternalEvent for updating wall parameters
            _updateWallParameterHandler = new UpdateWallParameterEventHandler(_logger);
            _updateWallParameterEvent = ExternalEvent.Create(_updateWallParameterHandler);

            // Initialize ExternalEvent for deleting views
            _deleteViewHandler = new DeleteViewEventHandler();
            _deleteViewEvent = ExternalEvent.Create(_deleteViewHandler);

            // Initialize ExternalEvent for initializing project parameters
            var parameterService = DIContainerService.Container.GetInstance<IProjectParameterService>();
            _initializeProjectParametersHandler = new InitializeProjectParametersEventHandler(_logger, parameterService);
            _initializeProjectParametersEvent = ExternalEvent.Create(_initializeProjectParametersHandler);

            // Initialize ExternalEvent for loading families
            _loadFamiliesHandler = new LoadFamiliesEventHandler(_logger);
            _loadFamiliesEvent = ExternalEvent.Create(_loadFamiliesHandler);

            // Initialize ExternalEvent for initializing filled region types
            _initializeFilledRegionTypesHandler = new InitializeFilledRegionTypesEventHandler(_logger);
            _initializeFilledRegionTypesEvent = ExternalEvent.Create(_initializeFilledRegionTypesHandler);

            // Initialize ExternalEvent for cleaning up duplicate filled region types
            _cleanupDuplicateFilledRegionTypesHandler = new CleanupDuplicateFilledRegionTypesEventHandler(_logger);
            _cleanupDuplicateFilledRegionTypesEvent = ExternalEvent.Create(_cleanupDuplicateFilledRegionTypesHandler);

            // Initialize commands
            SelectPropertyLineCommand = new RelayCommand(SelectPropertyLine);
            HighlightWallsCommand = new RelayCommand(HighlightWalls, CanHighlightWalls);
            CreateArrowsCommand = new RelayCommand(CreateArrows, CanCreateArrows);
            HighlightWallIn3DCommand = new RelayCommand(HighlightWallIn3D);
            HighlightDistanceGroupCommand = new RelayCommand(HighlightDistanceGroup);
            HighlightWallInFloorPlanCommand = new RelayCommand(HighlightWallInFloorPlan);
            DetectReferenceLinesCommand = new RelayCommand(DetectReferenceLines);
            CreateWallProjectionsCommand = new RelayCommand(CreateWallProjections);
            CloseCommand = new RelayCommand(Close);
            ShowLogsCommand = new RelayCommand(ShowLogs);
            CheckCeilingWallConnectionsCommand = new RelayCommand(CheckCeilingWallConnections);
            CheckWallCeilingConnectionsCommand = new RelayCommand(CheckWallCeilingConnections);
            FindPerimeterWallsWithoutTopCeilingCommand = new RelayCommand(FindPerimeterWallsWithoutTopCeiling);
            DebugWallCeilingTrimmingCommand = new RelayCommand(DebugWallCeilingTrimming);
            OpenViewCommand = new RelayCommand(OpenView, CanOpenView);
            DeleteViewCommand = new RelayCommand(DeleteView, CanDeleteView);
            DeleteAllViewsCommand = new RelayCommand(DeleteAllViews, CanDeleteAllViews);
            InitializeProjectParametersCommand = new RelayCommand(InitializeProjectParameters);
            LoadRequiredFamiliesCommand = new RelayCommand(LoadRequiredFamilies);
            BrowseCustomFamilyCommand = new RelayCommand(BrowseCustomFamily);
            InitializeFilledRegionTypesCommand = new RelayCommand(InitializeFilledRegionTypes);
            CleanupDuplicateFilledRegionTypesCommand = new RelayCommand(CleanupDuplicateFilledRegionTypes);
            InitializeAllCommand = new RelayCommand(InitializeAll);

            _logger.LogInformation("LimitingDistanceWindowVM initialized.");

            // Set logger for WallInfo static logging
            WallInfo.SetLogger(_logger);

            // Load plugin configuration defaults
            LoadPluginDefaults();

            // Load saved data from extensible storage
            LoadSavedData();

            // Subscribe to Idling event to monitor selection changes
            _document.Application.Idling += OnIdling;

            // Check if property line is pre-selected
            CheckPreSelectedPropertyLine();
        }

        private ElementId? _lastSelectedElementId = null;

        private bool _isClosing = false;

        private void OnIdling(object? sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            // Don't process if window is closing
            if (_isClosing)
                return;

            try
            {
                // Defensive check: ensure document is still valid
                if (_document == null || _document.Document == null || _document.Document.IsValidObject == false)
                {
                    _logger?.LogWarning("OnIdling: Document is null or invalid, unsubscribing from Idling event.");
                    _isClosing = true;
                    return;
                }

                // Check current selection
                var selectedIds = _document.Selection?.GetElementIds();
                if (selectedIds == null)
                    return;

                if (selectedIds.Count == 1)
                {
                    var elementId = selectedIds.First();

                    // Only process if selection changed
                    if (_lastSelectedElementId == null || _lastSelectedElementId.Value != elementId.Value)
                    {
                        _lastSelectedElementId = elementId;
                        var element = _document.Document.GetElement(elementId);

                        // Check if selected element is a wall in our perimeter walls list
                        if (element is Wall)
                        {
                            var wallInfo = _perimeterWalls?.FirstOrDefault(w => w.ElementId.Value == elementId.Value);
                            if (wallInfo != null && SelectedWall != wallInfo)
                            {
                                SelectedWall = wallInfo;
                                _logger.LogInformation($"Selected wall in table: {wallInfo.Name}");
                            }
                        }
                    }
                }
                else if (selectedIds.Count == 0 && _lastSelectedElementId != null)
                {
                    _lastSelectedElementId = null;
                    SelectedWall = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in OnIdling", ex);
                // If there's an error, set closing flag to prevent further issues
                _isClosing = true;
            }
        }

        private void CheckPreSelectedPropertyLine()
        {
            try
            {
                var selection = _document.Selection;
                var selectedIds = selection.GetElementIds();

                if (selectedIds.Count == 0)
                {
                    _logger.LogInformation("No elements pre-selected.");
                    return;
                }

                // Check if any selected element is a property line
                foreach (var id in selectedIds)
                {
                    var element = _document.Document.GetElement(id);

                    // Check if it's a property line (category: Property Lines, OST_SiteProperty)
                    if (element?.Category?.BuiltInCategory == BuiltInCategory.OST_SiteProperty)
                    {
                        SelectedPropertyLine = element;
                        _logger.LogInformation($"Pre-selected property line detected: {element.Id.Value}");

                        // Automatically find perimeter walls
                        FindPerimeterWalls();

                        // Automatically detect reference lines after finding perimeter walls
                        // Note: shouldAutoCreateArrows = false for automatic detection when property line is pre-selected
                        if (PerimeterWalls.Count > 0)
                        {
                            _logger.LogInformation("Auto-detecting reference lines for pre-selected property line...");
                            DetectReferenceLinesInternal(shouldAutoCreateArrows: false);
                        }

                        break; // Use the first property line found
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error checking pre-selected property line", ex);
            }
        }

        private void SelectPropertyLine(object parameter)
        {
            try
            {
                _logger.LogInformation("Selecting property line...");

                // Clear any existing selection first
                _document.Selection.SetElementIds(new List<ElementId>());

                // Use the PropertyLineSelectionFilter to only allow property lines from host document
                var filter = new PropertyLineSelectionFilter();

                // Select property line from host document only
                var reference = _document.Selection.PickObject(
                    ObjectType.Element,
                    filter,
                    "Select a property line");

                if (reference != null)
                {
                    var element = _document.Document.GetElement(reference);
                    _logger.LogInformation($"Property line selected: {element.Id.Value}");

                    SelectedPropertyLine = element;

                    // Automatically find perimeter walls
                    FindPerimeterWalls();

                    // Automatically detect reference lines after finding perimeter walls
                    // Note: shouldAutoCreateArrows = false for automatic detection during property line selection
                    if (PerimeterWalls.Count > 0)
                    {
                        _logger.LogInformation("Auto-detecting reference lines after property line selection...");
                        DetectReferenceLinesInternal(shouldAutoCreateArrows: false);
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                _logger.LogInformation("Property line selection cancelled by user.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error selecting property line", ex);
                TaskDialog.Show("Error", $"Error selecting property line: {ex.Message}");
            }
        }

        private void FindPerimeterWalls()
        {
            try
            {
                _logger.LogInformation("Finding perimeter walls inside property line boundaries...");

                PerimeterWalls.Clear();

                if (_selectedPropertyLine == null)
                {
                    _logger.LogWarning("No property line selected.");
                    return;
                }

                // Use the WallAnalysisService to find perimeter walls
                var rayLengthLimit = RayLengthLimit; // Use the configured ray length limit
                var wallInfos = _wallAnalysisService.FindPerimeterWalls(_document.Document, _selectedPropertyLine, rayLengthLimit);

                // Add walls to the ObservableCollection and subscribe to events
                foreach (var wallInfo in wallInfos)
                {
                    // Subscribe to parameter update events
                    wallInfo.ParameterUpdateRequested += WallInfo_ParameterUpdateRequested;
                    PerimeterWalls.Add(wallInfo);
                }

                _logger.LogInformation($"Total perimeter walls found inside property line: {PerimeterWalls.Count}");

                if (PerimeterWalls.Count == 0)
                {
                    TaskDialog.Show("No Perimeter Walls", "No walls found with IsPerimeter parameter set to true inside the selected property line.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error finding perimeter walls", ex);
                TaskDialog.Show("Error", $"Error finding perimeter walls: {ex.Message}");
            }
        }


        private bool CanHighlightWalls(object parameter)
        {
            return PerimeterWalls.Count > 0;
        }

        private void HighlightWalls(object parameter)
        {
            try
            {
                // Separate host walls from linked walls
                var hostWalls = PerimeterWalls.Where(w => !w.IsFromLinkedDocument).Select(w => w.ElementId).ToList();
                var linkedWalls = PerimeterWalls.Where(w => w.IsFromLinkedDocument).ToList();

                if (linkedWalls.Count > 0)
                {
                    _logger.LogWarning($"Found {linkedWalls.Count} walls from linked files that cannot be highlighted in selection");
                    var message = $"Found {hostWalls.Count} walls in host document and {linkedWalls.Count} walls in linked files.\n\n" +
                                  $"Note: Walls from linked files cannot be selected in Revit, only host document walls will be highlighted.";
                    TaskDialog.Show("Linked Walls Detected", message);
                }

                if (hostWalls.Count > 0)
                {
                    _viewNavigationService.HighlightWallsIn3DView(_document, hostWalls);
                }
                else
                {
                    TaskDialog.Show("No Host Walls", "All perimeter walls are from linked files and cannot be selected in the host document.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error highlighting walls", ex);
                TaskDialog.Show("Error", $"Error highlighting walls: {ex.Message}");
            }
        }

        private bool CanCreateArrows(object parameter)
        {
            return PerimeterWalls.Count > 0;
        }

        private void CreateArrows(object parameter)
        {
            try
            {
                // Log with stack trace to see where this is being called from
                var stackTrace = new System.Diagnostics.StackTrace(true);
                _logger.LogInformation($"CreateArrows called! Stack trace:\n{stackTrace}");
                _logger.LogInformation("Initiating arrow creation for perimeter walls...");

                // Set parameters for the event handler
                _createArrowsHandler.SetParameters(_document, PerimeterWalls.ToList());

                // Raise the external event
                _createArrowsEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error initiating arrow creation", ex);
                TaskDialog.Show("Error", $"Error creating arrows: {ex.Message}");
            }
        }


        private void HighlightWallIn3D(object parameter)
        {
            try
            {
                if (parameter is WallInfo wallInfo)
                {
                    if (wallInfo.IsFromLinkedDocument)
                    {
                        _logger.LogWarning($"Cannot select wall {wallInfo.ElementId.Value} - it's from a linked file");
                        TaskDialog.Show("Linked Wall",
                            $"Wall '{wallInfo.Name}' is from a linked Revit file and cannot be selected in the host document.\n\n" +
                            "Only walls in the host document can be highlighted.");
                        return;
                    }

                    _viewNavigationService.HighlightWallIn3DView(_document, wallInfo.ElementId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error highlighting wall in 3D", ex);
                TaskDialog.Show("Error", $"Error highlighting wall: {ex.Message}");
            }
        }

        private void HighlightWallInFloorPlan(object parameter)
        {
            try
            {
                if (parameter is WallInfo wallInfo)
                {
                    if (wallInfo.IsFromLinkedDocument)
                    {
                        _logger.LogWarning($"Cannot select wall {wallInfo.ElementId.Value} - it's from a linked file");
                        TaskDialog.Show("Linked Wall",
                            $"Wall '{wallInfo.Name}' is from a linked Revit file and cannot be selected in the host document.\n\n" +
                            "Only walls in the host document can be highlighted.");
                        return;
                    }

                    _viewNavigationService.HighlightWallInFloorPlan(_document, wallInfo.Wall);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error highlighting wall in floor plan", ex);
                TaskDialog.Show("Error", $"Error highlighting wall: {ex.Message}");
            }
        }

        private void HighlightDistanceGroup(object parameter)
        {
            try
            {
                if (parameter is DistanceGroupSummary group)
                {
                    _logger.LogInformation($"Highlighting walls in distance group: {group.Orientation} - {group.DistanceRange}");

                    // Get all walls matching this reference line and distance range
                    // group.Orientation now contains the reference line name
                    var wallsToHighlight = PerimeterWalls
                        .Where(w => w.ReferenceLine != null &&
                                   w.LimitingDistance.HasValue &&
                                   w.ReferenceLine.Name == group.Orientation &&
                                   w.LimitingDistance.Value >= group.MinDistance &&
                                   w.LimitingDistance.Value < group.MaxDistance)
                        .Select(w => w.ElementId)
                        .ToList();

                    if (wallsToHighlight.Count == 0)
                    {
                        _logger.LogWarning("No walls found matching the selected distance group.");
                        TaskDialog.Show("No Walls Found", "No walls found in the selected distance group.");
                        return;
                    }

                    var doc = _document.Document;

                    // Find or create a 3D view
                    var view3D = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .Cast<View3D>()
                        .FirstOrDefault(v => !v.IsTemplate);

                    if (view3D == null)
                    {
                        TaskDialog.Show("No 3D View", "No 3D view found in the document.");
                        _logger.LogWarning("No 3D view found.");
                        return;
                    }

                    // Set the active view to 3D
                    _document.ActiveView = view3D;

                    // Select the walls in the group
                    _document.Selection.SetElementIds(wallsToHighlight);

                    _logger.LogInformation($"Highlighted {wallsToHighlight.Count} walls in distance group in 3D view.");
                    // TaskDialog.Show("Success", $"Highlighted {wallsToHighlight.Count} walls in {group.Orientation} - {group.DistanceRange} in 3D view.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error highlighting distance group", ex);
                TaskDialog.Show("Error", $"Error highlighting distance group: {ex.Message}");
            }
        }

        private void WallInfo_ParameterUpdateRequested(object? sender, ParameterUpdateEventArgs e)
        {
            if (sender is WallInfo wallInfo)
            {
                _logger.LogInformation($"Parameter update requested for wall {wallInfo.ElementId?.Value}: {e.ParameterName} = {e.ParameterValue}");

                // Set parameters for the event handler
                _updateWallParameterHandler.SetParameters(wallInfo.Wall, e.ParameterName, e.ParameterValue);

                // Raise the external event
                _updateWallParameterEvent.Raise();
            }
        }

        private void DetectReferenceLines(object parameter)
        {
            DetectReferenceLinesInternal(shouldAutoCreateArrows: true);
        }

        private void DetectReferenceLinesInternal(bool shouldAutoCreateArrows)
        {
            try
            {
                _logger.LogInformation($"Initiating reference line detection via ExternalEvent... shouldAutoCreateArrows={shouldAutoCreateArrows}, AutoCreateArrows={AutoCreateArrows}");

                // Determine if we should draw distance measurement arrows
                // Only draw if shouldAutoCreateArrows is true AND AutoCreateArrows is enabled
                bool shouldDrawDistanceArrows = shouldAutoCreateArrows && AutoCreateArrows;
                _logger.LogInformation($"shouldDrawDistanceArrows={shouldDrawDistanceArrows}");

                // Set parameters for the event handler with callback
                _detectReferenceLinesHandler.SetParameters(
                    _document,
                    PerimeterWalls,
                    ReferenceLines,
                    RayLengthLimit,
                    shouldDrawDistanceArrows,
                    () =>
                    {
                        // This callback runs after the external event completes
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                // Distance Groups are calculated AFTER wall projections are created, not after detection

                                // Auto-create WALL ORIENTATION arrows if enabled AND if this detection should trigger auto-creation
                                _logger.LogInformation($"Wall orientation arrow creation check: shouldAutoCreateArrows={shouldAutoCreateArrows}, AutoCreateArrows={AutoCreateArrows}, ReferenceLines.Count={ReferenceLines.Count}");

                                if (shouldAutoCreateArrows && AutoCreateArrows && ReferenceLines.Count > 0)
                                {
                                    _logger.LogInformation("Auto-creating wall orientation arrows after reference line detection...");
                                    CreateArrows(null);
                                }
                                else
                                {
                                    _logger.LogInformation("Skipping wall orientation arrow creation (one or more conditions not met)");
                                }
                            }),
                            System.Windows.Threading.DispatcherPriority.Normal);
                    });

                // Raise the external event
                _detectReferenceLinesEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error initiating reference line detection", ex);
                TaskDialog.Show("Error", $"Error initiating reference line detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates distance groups based on walls with created regions
        /// Groups by: Linked Reference Line → Distance Group
        /// </summary>
        /// <param name="wallsWithRegions">List of walls that successfully had regions created</param>
        public void CalculateDistanceGroupsFromRegions(List<WallInfo> wallsWithRegions)
        {
            try
            {
                DistanceGroups.Clear();

                // Check if distance ranges are loaded
                if (_filledRegionTypeRanges == null || _filledRegionTypeRanges.Count == 0)
                {
                    _logger.LogWarning("No filled region type ranges loaded from JSON");
                    TaskDialog.Show("Warning", "No distance ranges loaded from configuration, using default ranges.");
                    return;
                }

                // Use the wall analysis service to calculate distance groups
                var distanceGroupsList = _wallAnalysisService.CalculateDistanceGroupsFromRegions(
                    wallsWithRegions,
                    _filledRegionTypeRanges);

                // Replace the collection
                DistanceGroups = new ObservableCollection<DistanceGroupSummary>(distanceGroupsList);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating distance groups from regions", ex);
            }
        }

        /// <summary>
        /// Calculates building code compliance based on distance groups and building classification
        /// Determines maximum allowed openings percentage and compares with actual openings
        /// </summary>
        public void CalculateBuildingCodeCompliance()
        {
            try
            {
                _logger.LogInformation("Calculating building code compliance...");

                // Use the compliance service to calculate compliance by orientation
                var complianceSummaries = _nbcComplianceService.CalculateBuildingCodeComplianceByOrientation(
                    DistanceGroups.ToList(),
                    BuildingClassification);

                // Replace the collection
                BuildingCodeCompliance = new ObservableCollection<BuildingCodeComplianceSummary>(complianceSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating building code compliance", ex);
            }
        }

        /// <summary>
        /// Determines maximum allowed unprotected openings percentage based on limiting distance
        /// Uses NBCConfigurationService to load requirements from NBCRequirements.json
        /// </summary>

        private void CreateWallProjections(object parameter)
        {
            try
            {
                _logger.LogInformation("Initiating wall projections creation via ExternalEvent...");

                if (PerimeterWalls.Count == 0)
                {
                    TaskDialog.Show("Error", "No perimeter walls found. Please detect reference lines first.");
                    return;
                }

                if (ReferenceLines.Count == 0)
                {
                    TaskDialog.Show("Error", "No reference lines detected. Please run 'Detect Reference Lines' first.");
                    return;
                }

                // Set parameters for the event handler with view tracking callback
                _createProjectionsHandler.SetParameters(
                    _document,
                    PerimeterWalls.ToList(),
                    ReferenceLines.ToList(),
                    null,
                    AddCreatedView,
                    (wallsWithRegions) =>
                    {
                        // This callback runs after projections are created
                        // Update Distance Groups based on walls that actually got regions
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                CalculateDistanceGroupsFromRegions(wallsWithRegions);
                                CalculateBuildingCodeCompliance();
                            }));
                    });

                // Raise the external event
                _createProjectionsEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error initiating wall projections creation", ex);
                TaskDialog.Show("Error", $"Error creating wall projections: {ex.Message}");
            }
        }
        private void ShowLogs(object parameter)
        {
            var logWindowVM = new LogWindowVM();
            var windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
            windowManager.Open(logWindowVM);
        }

        /// <summary>
        /// Public cleanup method to ensure proper resource disposal when window closes
        /// </summary>
        public void Cleanup()
        {
            _logger.LogInformation("Cleanup: Limiting Distance window cleanup initiated.");

            // Save data before closing
            try
            {
                //TODO: Uncomment and implement data saving if needed
                // SaveData();
                _logger.LogInformation("Cleanup: Data saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Cleanup: Error saving data", ex);
            }

            // Set closing flag to prevent OnIdling from accessing disposed objects
            _isClosing = true;

            // Note: We don't unsubscribe from Idling event here because:
            // 1. The OnIdling handler checks _isClosing flag and returns early
            // 2. Unsubscribing outside Revit API context throws "Invalid call to Revit API" exception
            // 3. The event handler is lightweight and won't cause issues if called after cleanup
            _logger.LogInformation("Cleanup: Set closing flag, Idling event handler will exit early.");
        }

        private void Close(object parameter)
        {
            _logger.LogInformation("Close command: Closing Limiting Distance window.");

            // Cleanup is now handled by Window_Closing event
            Cleanup();

            DialogResult = false;
            OnRequestClose(EventArgs.Empty);
        }

        private void CheckCeilingWallConnections(object parameter)
        {
            try
            {
                _logger.LogInformation("CheckCeilingWallConnections command executed.");
                DebugOutput = "Starting ceiling-wall connection analysis...\n";

                // Prompt user to select a ceiling
                var reference = _document.Selection.PickObject(
                    ObjectType.Element,
                    new CeilingSelectionFilter(),
                    "Select a ceiling to analyze wall connections");

                if (reference == null)
                {
                    DebugOutput += "No ceiling selected.\n";
                    return;
                }

                var ceiling = _document.Document.GetElement(reference.ElementId) as Ceiling;
                if (ceiling == null)
                {
                    DebugOutput += "Selected element is not a ceiling.\n";
                    return;
                }

                DebugOutput += $"Analyzing ceiling: {ceiling.Name} (ID: {ceiling.Id.Value})\n\n";

                // Use the CeilingWallAnalyzer helper
                var relationships = Helpers.CeilingWallAnalyzer.GetConnectedWalls(ceiling, _document.Document);

                if (relationships.Count == 0)
                {
                    DebugOutput += "No walls connected to this ceiling.\n";
                }
                else
                {
                    DebugOutput += $"Found {relationships.Count} wall(s) connected to ceiling:\n\n";

                    foreach (var relationship in relationships)
                    {
                        DebugOutput += $"Wall ID: {relationship.Wall.Id.Value}\n";
                        DebugOutput += $"  Name: {relationship.Wall.Name}\n";
                        DebugOutput += $"  Joined: {(relationship.IsJoined ? "YES" : "No")}\n";
                        DebugOutput += $"  Vertical: {relationship.VerticalRelationship}\n";
                        DebugOutput += $"  Horizontal: {relationship.HorizontalRelationship}\n";
                        DebugOutput += $"  Wall Base: {relationship.WallBaseElevation:F2} ft\n";
                        DebugOutput += $"  Wall Top: {relationship.WallTopElevation:F2} ft\n";
                        DebugOutput += $"  Ceiling: {relationship.CeilingElevation:F2} ft\n";
                        DebugOutput += "\n";
                    }

                    // Highlight the connected walls in the view
                    var wallIds = relationships.Select(r => r.Wall.Id).ToList();
                    _document.Selection.SetElementIds(wallIds);
                    DebugOutput += $"Highlighted {wallIds.Count} connected wall(s) in the view.\n";
                }

                _logger.LogInformation($"Ceiling-wall analysis complete. Found {relationships.Count} connections.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                DebugOutput += "Operation cancelled by user.\n";
                _logger.LogInformation("Ceiling selection cancelled.");
            }
            catch (Exception ex)
            {
                DebugOutput += $"Error: {ex.Message}\n";
                _logger.LogError("Error checking ceiling-wall connections", ex);
            }
        }

        private void CheckWallCeilingConnections(object parameter)
        {
            try
            {
                _logger.LogInformation("CheckWallCeilingConnections command executed.");
                DebugOutput = "Starting wall-ceiling connection analysis...\n";

                // Prompt user to select a wall
                var reference = _document.Selection.PickObject(
                    ObjectType.Element,
                    new WallSelectionFilter(),
                    "Select a wall to analyze ceiling connections");

                if (reference == null)
                {
                    DebugOutput += "No wall selected.\n";
                    return;
                }

                var wall = _document.Document.GetElement(reference.ElementId) as Wall;
                if (wall == null)
                {
                    DebugOutput += "Selected element is not a wall.\n";
                    return;
                }

                DebugOutput += $"Analyzing wall: {wall.Name} (ID: {wall.Id.Value})\n\n";

                // Use the CeilingWallAnalyzer helper
                var relationships = Helpers.CeilingWallAnalyzer.GetConnectedCeilings(wall, _document.Document);

                if (relationships.Count == 0)
                {
                    DebugOutput += "No ceilings connected to this wall.\n";
                }
                else
                {
                    DebugOutput += $"Found {relationships.Count} ceiling(s) connected to wall:\n\n";

                    foreach (var relationship in relationships)
                    {
                        DebugOutput += $"Ceiling ID: {relationship.Ceiling.Id.Value}\n";
                        DebugOutput += $"  Name: {relationship.Ceiling.Name}\n";
                        DebugOutput += $"  Joined: {(relationship.IsJoined ? "YES" : "No")}\n";
                        DebugOutput += $"  Vertical: {relationship.VerticalRelationship}\n";
                        DebugOutput += $"  Horizontal: {relationship.HorizontalRelationship}\n";
                        DebugOutput += $"  Wall Base: {relationship.WallBaseElevation:F2} ft\n";
                        DebugOutput += $"  Wall Top: {relationship.WallTopElevation:F2} ft\n";
                        DebugOutput += $"  Ceiling: {relationship.CeilingElevation:F2} ft\n";
                        DebugOutput += "\n";
                    }

                    // Highlight the connected ceilings in the view
                    var ceilingIds = relationships.Select(r => r.Ceiling.Id).ToList();
                    _document.Selection.SetElementIds(ceilingIds);
                    DebugOutput += $"Highlighted {ceilingIds.Count} connected ceiling(s) in the view.\n";
                }

                _logger.LogInformation($"Wall-ceiling analysis complete. Found {relationships.Count} connections.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                DebugOutput += "Operation cancelled by user.\n";
                _logger.LogInformation("Wall selection cancelled.");
            }
            catch (Exception ex)
            {
                DebugOutput += $"Error: {ex.Message}\n";
                _logger.LogError("Error checking wall-ceiling connections", ex);
            }
        }

        private void FindPerimeterWallsWithoutTopCeiling(object parameter)
        {
            try
            {
                _logger.LogInformation("FindPerimeterWallsWithoutTopCeiling command executed.");
                DebugOutput = "Finding perimeter walls without top-most ceiling connections...\n\n";

                // Use the CeilingWallAnalyzer helper
                var walls = Helpers.CeilingWallAnalyzer.GetPerimeterWallsWithoutTopMostCeiling(_document.Document);

                if (walls.Count == 0)
                {
                    DebugOutput += "No perimeter walls found without top-most ceiling connections.\n";
                    DebugOutput += "\nThis means:\n";
                    DebugOutput += "- All perimeter walls are properly connected to top-most ceilings, OR\n";
                    DebugOutput += "- No walls are marked as perimeter/exterior walls, OR\n";
                    DebugOutput += "- No ceilings have the 'IsTopMost' parameter checked\n";
                }
                else
                {
                    DebugOutput += $"Found {walls.Count} perimeter wall(s) WITHOUT top-most ceiling connections:\n\n";

                    foreach (var wall in walls)
                    {
                        DebugOutput += $"Wall ID: {wall.Id.Value}\n";
                        DebugOutput += $"  Name: {wall.Name}\n";
                        DebugOutput += $"  Type: {wall.WallType?.Name ?? "Unknown"}\n";

                        // Get wall function
                        var wallFunctionParam = wall.get_Parameter(BuiltInParameter.FUNCTION_PARAM);
                        if (wallFunctionParam != null)
                        {
                            var wallFunction = (WallFunction)wallFunctionParam.AsInteger();
                            DebugOutput += $"  Function: {wallFunction}\n";
                        }

                        // Get wall height
                        var wallHeightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                        if (wallHeightParam != null)
                        {
                            DebugOutput += $"  Height: {wallHeightParam.AsDouble():F2} ft\n";
                        }

                        DebugOutput += "\n";
                    }

                    // Highlight the walls in the view
                    var wallIds = walls.Select(w => w.Id).ToList();
                    _document.Selection.SetElementIds(wallIds);
                    DebugOutput += $"Highlighted {wallIds.Count} perimeter wall(s) in the view.\n\n";

                    DebugOutput += "ACTION REQUIRED:\n";
                    DebugOutput += "These perimeter walls should be connected to a ceiling with 'IsTopMost' parameter checked.\n";
                }

                _logger.LogInformation($"Perimeter walls analysis complete. Found {walls.Count} walls without top-most ceiling.");
            }
            catch (Exception ex)
            {
                DebugOutput += $"Error: {ex.Message}\n";
                _logger.LogError("Error finding perimeter walls without top ceiling", ex);
            }
        }

        private void DebugWallCeilingTrimming(object parameter)
        {
            try
            {
                _logger.LogInformation("DebugWallCeilingTrimming command executed.");
                DebugOutput = "Analyzing wall-ceiling trimming for current walls...\n\n";

                if (_perimeterWalls == null || _perimeterWalls.Count == 0)
                {
                    DebugOutput += "No walls loaded. Please run 'Highlight Walls' first.\n";
                    return;
                }

                int totalWalls = 0;
                int wallsWithCeilings = 0;
                int wallsWithTopMostCeilings = 0;
                int wallsTrimmed = 0;
                int wallsNotTrimmed = 0;

                foreach (var wallInfo in _perimeterWalls)
                {
                    totalWalls++;
                    var wall = _document.Document.GetElement(wallInfo.ElementId) as Wall;
                    if (wall == null) continue;

                    var connectedCeilings = Helpers.CeilingWallAnalyzer.GetConnectedCeilings(wall, _document.Document);

                    if (connectedCeilings.Count > 0)
                    {
                        wallsWithCeilings++;
                        DebugOutput += $"Wall {wallInfo.ElementId.Value} ({wallInfo.Name}):\n";
                        DebugOutput += $"  {connectedCeilings.Count} connected ceiling(s)\n";

                        foreach (var rel in connectedCeilings)
                        {
                            var isTopMostParam = rel.Ceiling.LookupParameter("IsTopMost");
                            var isTopMost = isTopMostParam != null && isTopMostParam.StorageType == StorageType.Integer && isTopMostParam.AsInteger() == 1;

                            DebugOutput += $"    Ceiling {rel.Ceiling.Id.Value}: IsJoined={rel.IsJoined}, ";
                            DebugOutput += $"IsTopMost={isTopMost}\n";
                            DebugOutput += $"      Ceiling elev: {rel.CeilingElevation:F2} ft, ";
                            DebugOutput += $"Wall top: {rel.WallTopElevation:F2} ft\n";

                            if (isTopMost)
                            {
                                wallsWithTopMostCeilings++;
                                if (rel.CeilingElevation < rel.WallTopElevation)
                                {
                                    DebugOutput += $"      ** SHOULD TRIM ** (ceiling below wall top)\n";
                                    wallsTrimmed++;
                                }
                                else
                                {
                                    DebugOutput += $"      ** WILL NOT TRIM ** (ceiling at/above wall top)\n";
                                    wallsNotTrimmed++;
                                }
                            }
                        }
                        DebugOutput += "\n";
                    }
                }

                DebugOutput += $"SUMMARY:\n";
                DebugOutput += $"  Total walls: {totalWalls}\n";
                DebugOutput += $"  Walls with connected ceilings: {wallsWithCeilings}\n";
                DebugOutput += $"  Walls with top-most ceilings: {wallsWithTopMostCeilings}\n";
                DebugOutput += $"  Should trim: {wallsTrimmed}\n";
                DebugOutput += $"  Won't trim (ceiling too high): {wallsNotTrimmed}\n";

                _logger.LogInformation($"Wall-ceiling trimming debug complete.");
            }
            catch (Exception ex)
            {
                DebugOutput += $"Error: {ex.Message}\n";
                _logger.LogError("Error debugging wall-ceiling trimming", ex);
            }
        }

        #region View Tracking Commands

        private bool CanOpenView(object parameter) => SelectedView != null && SelectedView.View != null;

        private void OpenView(object parameter)
        {
            try
            {
                if (SelectedView?.View == null)
                {
                    _logger.LogWarning("OpenView: No view selected or view is null");
                    return;
                }

                _document.ActiveView = SelectedView.View;
                _logger.LogInformation($"Opened view: {SelectedView.ViewName}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error opening view: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to open view: {ex.Message}");
            }
        }

        private bool CanDeleteView(object parameter) => SelectedView != null;

        private void DeleteView(object parameter)
        {
            try
            {
                if (SelectedView == null)
                {
                    _logger.LogWarning("DeleteView: No view selected");
                    return;
                }

                var viewToDelete = SelectedView;

                // Set parameters for the delete event handler
                _deleteViewHandler.SetParameters(
                    new List<ElementId> { viewToDelete.View.Id },
                    () =>
                    {
                        // This callback runs after deletion is complete
                        CreatedViews.Remove(viewToDelete);
                        _logger.LogInformation($"Deleted view: {viewToDelete.ViewName}");
                    });

                // Raise the external event
                _deleteViewEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initiating view deletion: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to delete view: {ex.Message}");
            }
        }

        private bool CanDeleteAllViews(object parameter) => CreatedViews?.Count > 0;

        private void DeleteAllViews(object parameter)
        {
            try
            {
                if (CreatedViews == null || CreatedViews.Count == 0)
                {
                    _logger.LogWarning("DeleteAllViews: No views to delete");
                    return;
                }

                var result = TaskDialog.Show("Confirm Delete",
                    $"Are you sure you want to delete all {CreatedViews.Count} created views?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (result != TaskDialogResult.Yes)
                    return;

                var viewIds = CreatedViews.Select(v => v.View.Id).ToList();
                var count = CreatedViews.Count;

                // Set parameters for the delete event handler
                _deleteViewHandler.SetParameters(
                    viewIds,
                    () =>
                    {
                        // This callback runs after deletion is complete
                        CreatedViews.Clear();
                        _logger.LogInformation($"Deleted all {count} views");
                    });

                // Raise the external event
                _deleteViewEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initiating deletion of all views: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to delete all views: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a created view to the tracking list
        /// </summary>
        public void AddCreatedView(ViewSection view, int wallsCount)
        {
            try
            {
                var viewInfo = new ViewInfo(view, wallsCount);
                CreatedViews.Add(viewInfo);
                _logger.LogInformation($"Added view to tracking: {view.Name} with {wallsCount} walls");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding created view to tracking: {ex.Message}", ex);
            }
        }

        #endregion

        #region Data Persistence

        /// <summary>
        /// Loads plugin default settings from configuration
        /// </summary>
        private void LoadPluginDefaults()
        {
            try
            {
                _isLoadingConfig = true; // Prevent saving during load

                var config = _pluginConfig.LoadPluginConfiguration();

                // Set defaults from plugin configuration
                if (string.IsNullOrEmpty(BuildingClassification))
                {
                    BuildingClassification = config.DefaultBuildingClassification;
                }

                if (RayLengthLimit == 0)
                {
                    RayLengthLimit = config.DefaultRayLengthLimit;
                }

                // Load auto create arrows setting using property (won't trigger save due to flag)
                AutoCreateArrows = config.AutoCreateArrows;

                _logger.LogInformation($"Loaded plugin defaults: Classification={BuildingClassification}, RayLength={RayLengthLimit}, AutoCreateArrows={AutoCreateArrows}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading plugin defaults: {ex.Message}", ex);
            }
            finally
            {
                _isLoadingConfig = false; // Re-enable saving
            }
        }

        /// <summary>
        /// Loads saved data from extensible storage
        /// </summary>
        private void LoadSavedData()
        {
            try
            {
                var data = _extensibleStorage.LoadLimitingDistanceData(_document.Document);

                if (data == null || string.IsNullOrEmpty(data.PropertyLineId))
                {
                    _logger.LogInformation("No saved data found in document.");
                    return;
                }

                _logger.LogInformation($"Loading saved data from extensible storage...");

                // Load building classification and ray length if saved
                if (!string.IsNullOrEmpty(data.BuildingClassification))
                {
                    BuildingClassification = data.BuildingClassification;
                }

                if (data.RayLengthLimit > 0)
                {
                    RayLengthLimit = data.RayLengthLimit;
                }

                // Load property line
                if (!string.IsNullOrEmpty(data.PropertyLineId))
                {
                    try
                    {
                        var element = _document.Document.GetElement(data.PropertyLineId);
                        if (element != null)
                        {
                            SelectedPropertyLine = element;
                            _logger.LogInformation($"Restored property line: {element.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not restore property line: {ex.Message}");
                    }
                }

                // Reload perimeter walls and reference lines if property line is set
                if (_selectedPropertyLine != null)
                {
                    FindPerimeterWalls();

                    if (PerimeterWalls.Count > 0)
                    {
                        // Don't auto-create arrows when loading saved data
                        DetectReferenceLinesInternal(shouldAutoCreateArrows: false);
                    }
                }

                _logger.LogInformation($"Loaded saved data: {data.PerimeterWallIds.Count} walls, {data.ReferenceLineIds.Count} ref lines, {data.CreatedViewIds.Count} views");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading saved data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves current data to extensible storage
        /// </summary>
        public void SaveData()
        {
            try
            {
                var propertyLineId = _selectedPropertyLine?.UniqueId ?? string.Empty;
                var perimeterWallIds = _perimeterWalls.Select(w => w.Wall.UniqueId).ToList();
                var referenceLineIds = _referenceLines.Select(r => r.Element.UniqueId).ToList();
                var createdViewIds = _createdViews.Where(v => v.View != null).Select(v => v.View.UniqueId).ToList();

                // Build wall distances dictionary
                var wallDistances = new Dictionary<string, double>();
                foreach (var wall in _perimeterWalls)
                {
                    if (wall.LimitingDistance.HasValue && wall.LimitingDistance.Value > 0)
                    {
                        wallDistances[wall.Wall.UniqueId] = wall.LimitingDistance.Value;
                    }
                }

                _extensibleStorage.SaveLimitingDistanceData(
                    _document.Document,
                    propertyLineId,
                    perimeterWallIds,
                    BuildingClassification,
                    RayLengthLimit,
                    referenceLineIds,
                    createdViewIds,
                    wallDistances);

                _logger.LogInformation($"Saved data to extensible storage: {perimeterWallIds.Count} walls, {referenceLineIds.Count} ref lines");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clears all saved data from extensible storage
        /// </summary>
        public void ClearSavedData()
        {
            try
            {
                _extensibleStorage.ClearLimitingDistanceData(_document.Document);
                _logger.LogInformation("Cleared all saved data from extensible storage.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error clearing saved data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves the AutoCreateArrows setting to plugin configuration
        /// </summary>
        private void SaveAutoCreateArrowsSetting(bool value)
        {
            try
            {
                var config = _pluginConfig.LoadPluginConfiguration();
                config.AutoCreateArrows = value;
                _pluginConfig.SavePluginConfiguration(config);
                _logger.LogInformation($"Saved AutoCreateArrows setting: {value}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving AutoCreateArrows setting: {ex.Message}", ex);
            }
        }

        #endregion

        #region Project Setup Methods

        /// <summary>
        /// Initializes the project setup data with required parameters, families, and filled region types
        /// </summary>
        private void InitializeProjectSetupData()
        {
            // Load required project parameters from JSON configuration
            LoadProjectParametersFromJson();

            // Scan and load required families from Resources/Families folder
            LoadFamilyListFromFolder();

            // Load required filled region types from JSON configuration
            LoadFilledRegionTypesFromJson();
        }

        /// <summary>
        /// Load project parameter definitions from JSON configuration file and populate the DataGrid
        /// </summary>
        private void LoadProjectParametersFromJson()
        {
            try
            {
                // Get the configuration file path
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
                var configPath = System.IO.Path.Combine(assemblyDir!, "Resources", "Config", "ProjectParameters.json");

                if (!System.IO.File.Exists(configPath))
                {
                    _logger.LogWarning($"Configuration file not found: {configPath}. Using default parameters.");

                    // Fallback to default parameters
                    RequiredProjectParameters.Add(new ProjectParameterInfo
                    {
                        Name = "IsPerimeter",
                        ParameterType = "Yes/No",
                        GroupName = "Identity Data",
                        Status = "Not Checked"
                    });
                    return;
                }

                // Read and parse JSON configuration
                var jsonContent = System.IO.File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ProjectParameterConfiguration>(jsonContent, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null || config.ProjectParameters == null || config.ProjectParameters.Count == 0)
                {
                    _logger.LogWarning("Failed to parse configuration or no parameters defined.");
                    return;
                }

                // Get existing parameters from the Revit document
                var doc = GetUIDocument()?.Document;
                var existingParameters = new HashSet<string>();

                if (doc != null)
                {
                    var paramElements = new FilteredElementCollector(doc)
                        .OfClass(typeof(ParameterElement))
                        .Cast<ParameterElement>()
                        .ToList();

                    foreach (var paramElem in paramElements)
                    {
                        existingParameters.Add(paramElem.Name);
                    }

                    _logger.LogInformation($"Found {existingParameters.Count} parameters already in document");
                }

                // Populate the RequiredProjectParameters collection from JSON
                int alreadyExistsCount = 0;
                foreach (var paramDef in config.ProjectParameters)
                {
                    bool exists = existingParameters.Contains(paramDef.Name);
                    if (exists) alreadyExistsCount++;

                    RequiredProjectParameters.Add(new ProjectParameterInfo
                    {
                        Name = paramDef.Name,
                        ParameterType = paramDef.ParameterType,
                        GroupName = paramDef.GroupName,
                        Status = exists ? "Already Exists" : "Not Checked"
                    });
                }

                _logger.LogInformation($"Loaded {RequiredProjectParameters.Count} parameter definitions from JSON configuration. {alreadyExistsCount} already exist in document.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading project parameters from JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Scan the Families folder and populate the RequiredFamilies collection
        /// </summary>
        private void LoadFamilyListFromFolder()
        {
            try
            {
                // Get the families folder path
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
                var familiesPath = System.IO.Path.Combine(assemblyDir!, "Resources", "Families");

                if (!System.IO.Directory.Exists(familiesPath))
                {
                    _logger.LogWarning($"Families folder not found: {familiesPath}. No families to display.");
                    return;
                }

                // Get all .rfa files in the directory, excluding Revit backup files (.0001.rfa, .0002.rfa, etc.)
                var allFamilyFiles = System.IO.Directory.GetFiles(familiesPath, "*.rfa");
                var familyFiles = allFamilyFiles.Where(f => !System.Text.RegularExpressions.Regex.IsMatch(
                    System.IO.Path.GetFileName(f),
                    @"\.\d{4}\.rfa$")).ToArray();

                if (familyFiles.Length == 0)
                {
                    _logger.LogWarning($"No family files found in: {familiesPath}");
                    return;
                }

                _logger.LogInformation($"Filtered {allFamilyFiles.Length - familyFiles.Length} backup files.");

                // Get currently loaded families from the Revit document
                var doc = GetUIDocument()?.Document;
                var loadedFamilies = new System.Collections.Generic.HashSet<string>();

                if (doc != null)
                {
                    var families = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                        .OfClass(typeof(Autodesk.Revit.DB.Family))
                        .Cast<Autodesk.Revit.DB.Family>()
                        .ToList();

                    foreach (var family in families)
                    {
                        loadedFamilies.Add(family.Name);
                    }

                    _logger.LogInformation($"Found {loadedFamilies.Count} families already loaded in document");
                }

                // Populate the RequiredFamilies collection
                foreach (var familyPath in familyFiles)
                {
                    var familyName = System.IO.Path.GetFileNameWithoutExtension(familyPath);

                    // Check if family is already loaded
                    bool isLoaded = loadedFamilies.Contains(familyName);
                    string status = isLoaded ? "Already Loaded" : "Not Loaded";
                    string category = "To Be Determined";

                    // If loaded, try to get the actual category
                    if (isLoaded && doc != null)
                    {
                        var family = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                            .OfClass(typeof(Autodesk.Revit.DB.Family))
                            .Cast<Autodesk.Revit.DB.Family>()
                            .FirstOrDefault(f => f.Name == familyName);

                        if (family != null && family.FamilyCategory != null)
                        {
                            category = family.FamilyCategory.Name;
                        }
                    }

                    RequiredFamilies.Add(new RequiredFamilyInfo
                    {
                        Name = familyName,
                        Category = category,
                        Purpose = "Loaded from Resources/Families",
                        FilePath = familyPath,
                        Status = status
                    });
                }

                _logger.LogInformation($"Found {RequiredFamilies.Count} family files in Resources/Families folder. {loadedFamilies.Count} already loaded.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading family list from folder: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load filled region type definitions from JSON configuration file and populate the DataGrid
        /// </summary>
        private void LoadFilledRegionTypesFromJson()
        {
            try
            {
                // Get the configuration file path
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
                var configPath = System.IO.Path.Combine(assemblyDir!, "Resources", "Config", "FilledRegionTypes.json");

                if (!System.IO.File.Exists(configPath))
                {
                    _logger.LogWarning($"Configuration file not found: {configPath}. Using default filled region types.");

                    // Fallback to default filled region types
                    RequiredFilledRegionTypes.Add(new FilledRegionTypeInfo
                    {
                        Name = "NBC_0-1m",
                        PatternName = "Solid fill",
                        ColorDescription = "Red (255, 0, 0)",
                        ColorR = 255,
                        ColorG = 0,
                        ColorB = 0,
                        Status = "Not Checked"
                    });
                    return;
                }

                // Read and parse JSON configuration
                var jsonContent = System.IO.File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<FilledRegionTypeConfiguration>(jsonContent, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null || config.FilledRegionTypes == null || config.FilledRegionTypes.Count == 0)
                {
                    _logger.LogWarning("Failed to parse configuration or no filled region types defined.");
                    return;
                }

                // Store the configuration for use in distance group calculations
                _filledRegionTypeRanges = config.FilledRegionTypes;

                // Get existing filled region types from the Revit document
                var doc = GetUIDocument()?.Document;
                var existingFilledRegionTypes = new HashSet<string>();

                if (doc != null)
                {
                    var filledRegionTypes = new FilteredElementCollector(doc)
                        .OfClass(typeof(FilledRegionType))
                        .Cast<FilledRegionType>()
                        .ToList();

                    foreach (var frt in filledRegionTypes)
                    {
                        existingFilledRegionTypes.Add(frt.Name);
                    }

                    _logger.LogInformation($"Found {existingFilledRegionTypes.Count} filled region types already in document");
                }

                // Populate the RequiredFilledRegionTypes collection from JSON
                int alreadyExistsCount = 0;
                foreach (var regionDef in config.FilledRegionTypes)
                {
                    bool exists = existingFilledRegionTypes.Contains(regionDef.Name);
                    if (exists) alreadyExistsCount++;

                    RequiredFilledRegionTypes.Add(new FilledRegionTypeInfo
                    {
                        Name = regionDef.Name,
                        PatternName = regionDef.PatternName,
                        ColorDescription = regionDef.ColorDescription,
                        ColorR = regionDef.ColorR,
                        ColorG = regionDef.ColorG,
                        ColorB = regionDef.ColorB,
                        Status = exists ? "Already Exists" : "Not Checked"
                    });
                }

                _logger.LogInformation($"Loaded {RequiredFilledRegionTypes.Count} filled region type definitions from JSON configuration. {alreadyExistsCount} already exist in document.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading filled region types from JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Initialize project parameters from JSON configuration
        /// </summary>
        private void InitializeProjectParameters(object parameter)
        {
            try
            {
                _logger.LogInformation("Initializing project parameters from JSON configuration...");

                // Get the configuration file path
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
                var configPath = System.IO.Path.Combine(assemblyDir!, "Resources", "Config", "ProjectParameters.json");

                if (!System.IO.File.Exists(configPath))
                {
                    TaskDialog.Show("Error", $"Configuration file not found:\n{configPath}\n\nPlease ensure the ProjectParameters.json file exists in the Resources/Config folder.");
                    _logger.LogError($"Configuration file not found: {configPath}");
                    return;
                }

                // Set parameters for the event handler
                _initializeProjectParametersHandler.SetParameters(
                    GetUIDocument(),
                    configPath,
                    (results) =>
                    {
                        // Update the RequiredProjectParameters collection with results
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            RequiredProjectParameters.Clear();
                            foreach (var result in results)
                            {
                                RequiredProjectParameters.Add(result);
                            }
                        });
                    });

                // Raise the external event
                _initializeProjectParametersEvent.Raise();

                _logger.LogInformation("Project parameters initialization event raised.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing project parameters: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to initialize project parameters:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Load required families from Resources/Families folder
        /// </summary>
        private void LoadRequiredFamilies(object parameter)
        {
            try
            {
                _logger.LogInformation("Loading required families from Resources/Families folder...");

                // Get the families folder path
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
                var familiesPath = System.IO.Path.Combine(assemblyDir!, "Resources", "Families");

                if (!System.IO.Directory.Exists(familiesPath))
                {
                    TaskDialog.Show("Error", $"Families folder not found:\n{familiesPath}\n\nPlease ensure family files (.rfa) are in the Resources/Families folder.");
                    _logger.LogError($"Families folder not found: {familiesPath}");
                    return;
                }

                // Set parameters for the event handler
                _loadFamiliesHandler.SetParameters(
                    GetUIDocument(),
                    familiesPath,
                    (results) =>
                    {
                        // Update the RequiredFamilies collection with results
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            RequiredFamilies.Clear();
                            foreach (var result in results)
                            {
                                RequiredFamilies.Add(result);
                            }
                        });
                    });

                // Raise the external event
                _loadFamiliesEvent.Raise();

                _logger.LogInformation("Family loading event raised.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading required families: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to load required families:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Browse and load a custom family
        /// </summary>
        private void BrowseCustomFamily(object parameter)
        {
            try
            {
                _logger.LogInformation("Browsing for custom family...");

                // TODO: Implement custom family browser
                TaskDialog.Show("Browse Family", "Custom family browser is not yet implemented.\n\nThis feature will allow you to browse and load custom families from your file system.");

                _logger.LogInformation("Custom family browser opened.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error browsing custom family: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to browse custom family:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Initialize filled region types for distance groups from configuration
        /// </summary>
        private void InitializeFilledRegionTypes(object parameter)
        {
            try
            {
                _logger.LogInformation("Initializing filled region types from configuration...");

                // Check if we have loaded the configuration
                if (_filledRegionTypeRanges == null || _filledRegionTypeRanges.Count == 0)
                {
                    TaskDialog.Show("Error", "No filled region type configuration loaded.\n\nPlease ensure the FilledRegionTypes.json file exists in the Resources/Config folder.");
                    _logger.LogError("No filled region type configuration loaded.");
                    return;
                }

                // Set parameters for the event handler
                _initializeFilledRegionTypesHandler.SetParameters(
                    GetUIDocument(),
                    _filledRegionTypeRanges,
                    (results) =>
                    {
                        // Update the RequiredFilledRegionTypes collection with results
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            RequiredFilledRegionTypes.Clear();
                            foreach (var result in results)
                            {
                                RequiredFilledRegionTypes.Add(result);
                            }
                        });
                    });

                // Raise the external event
                _initializeFilledRegionTypesEvent.Raise();

                _logger.LogInformation("Filled region types initialization event raised.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing filled region types: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to initialize filled region types:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Clean up duplicate filled region types from the project
        /// </summary>
        private void CleanupDuplicateFilledRegionTypes(object parameter)
        {
            try
            {
                _logger.LogInformation("Cleaning up duplicate filled region types...");

                // Set parameters for the event handler
                _cleanupDuplicateFilledRegionTypesHandler.SetParameters(
                    GetUIDocument(),
                    (result) =>
                    {
                        // Refresh the filled region types list after cleanup
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            LoadFilledRegionTypesFromJson();
                        });
                    });

                // Raise the external event
                _cleanupDuplicateFilledRegionTypesEvent.Raise();

                _logger.LogInformation("Cleanup duplicates event raised.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cleaning up duplicates: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to clean up duplicates:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Initialize all project components (parameters, families, filled region types)
        /// </summary>
        private void InitializeAll(object parameter)
        {
            try
            {
                _logger.LogInformation("Initializing all project components...");

                InitializeProjectParameters(parameter);
                LoadRequiredFamilies(parameter);
                InitializeFilledRegionTypes(parameter);

                TaskDialog.Show("Project Setup", "All project components have been initialized.\n\nPlease check the status of each component in the respective sections.");

                _logger.LogInformation("All project components initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing all project components: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Failed to initialize all project components:\n{ex.Message}");
            }
        }

        #endregion
    }
}
