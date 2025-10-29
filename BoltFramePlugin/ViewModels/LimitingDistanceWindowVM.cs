using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BoltFramePlugin.Services;
using BoltFramePlugin.Filters;
using BoltFramePlugin.EventHandlers;
using BoltFramePlugin.Models.LimitingDistance;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.ViewModels
{
    public class LimitingDistanceWindowVM : BaseViewModel
    {
        private IRevitService _revitService;
        private ILoggingService _logger;
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

        public List<string> BuildingClassifications { get; } = new List<string>
        {
            "Part 3: Commercial",
            "Part 9: Residential"
        };

        // Occupant Groups for dropdown
        public List<string> OccupantGroups { get; } = new List<string>
        {
            "Group A",
            "Group B",
            "Group C",
            "Group D",
            "Group E",
            "Group F"
        };

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

        public LimitingDistanceWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _revitService = DIContainerService.Container.GetInstance<IRevitServiceFactory>().Create(uidoc);
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _extensibleStorage = DIContainerService.Container.GetInstance<IExtensibleStorageService>();
            _pluginConfig = DIContainerService.Container.GetInstance<IPluginConfigurationManager>();

            _perimeterWalls = new ObservableCollection<WallInfo>();
            _referenceLines = new ObservableCollection<ReferenceLineInfo>();
            _distanceGroups = new ObservableCollection<DistanceGroupSummary>();
            _buildingCodeCompliance = new ObservableCollection<BuildingCodeComplianceSummary>();
            _createdViews = new ObservableCollection<ViewInfo>();

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

                // Use the PropertyLineSelectionFilter to only allow property lines
                var filter = new PropertyLineSelectionFilter();
                var reference = _document.Selection.PickObject(
                    ObjectType.Element,
                    filter,
                    "Select a property line");

                if (reference != null)
                {
                    var element = _document.Document.GetElement(reference);

                    SelectedPropertyLine = element;
                    _logger.LogInformation($"Property line selected: {element.Id.Value}");

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

                // Get the property line boundary curve loop
                var propertyLineBoundary = GetPropertyLineBoundary(_selectedPropertyLine);
                if (propertyLineBoundary == null)
                {
                    TaskDialog.Show("Error", "Could not extract boundary from the selected property line.");
                    _logger.LogError("Failed to extract property line boundary.");
                    return;
                }

                // Get all walls in the document
                var walls = new FilteredElementCollector(_document.Document)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .ToList();

                _logger.LogInformation($"Total walls found: {walls.Count}");

                // Filter walls that have LD_IsPerimeter parameter set to true AND are inside the property line
                foreach (var wall in walls)
                {
                    var parameter = wall.LookupParameter("LD_IsPerimeter");

                    if (parameter != null && parameter.AsInteger() == 1)
                    {
                        // Check if wall is inside the property line boundary
                        if (IsWallInsidePropertyLine(wall, propertyLineBoundary))
                        {
                            // Calculate areas
                            var grossArea = CalculateWallGrossArea(wall);
                            var openingsArea = CalculateWallOpeningsArea(wall);

                            var wallInfo = new WallInfo
                            {
                                Wall = wall,
                                ElementId = wall.Id,
                                Name = wall.Name,
                                Length = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0,
                                GrossArea = grossArea,
                                OpeningsArea = openingsArea,
                                Orientation = wall.Orientation // Set wall orientation (normal vector)
                            };

                            // Subscribe to parameter update events
                            wallInfo.ParameterUpdateRequested += WallInfo_ParameterUpdateRequested;

                            PerimeterWalls.Add(wallInfo);
                            _logger.LogInformation($"Perimeter wall found inside property line: {wall.Name} (ID: {wall.Id.Value}) - Gross: {grossArea:F2} ft², Openings: {openingsArea:F2} ft², Net: {wallInfo.NetArea:F2} ft²");
                        }
                    }
                }

                _logger.LogInformation($"Total perimeter walls found inside property line: {PerimeterWalls.Count}");

                if (PerimeterWalls.Count == 0)
                {
                    TaskDialog.Show("No Perimeter Walls", "No walls found with LD_IsPerimeter parameter set to true inside the selected property line.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error finding perimeter walls", ex);
                TaskDialog.Show("Error", $"Error finding perimeter walls: {ex.Message}");
            }
        }

        private CurveLoop? GetPropertyLineBoundary(Element propertyLine)
        {
            try
            {
                // Try to get the geometry from the property line
                var options = new Options
                {
                    ComputeReferences = false,
                    DetailLevel = ViewDetailLevel.Fine
                };

                var geometryElement = propertyLine.get_Geometry(options);
                if (geometryElement == null)
                    return null;

                var curves = new List<Curve>();

                foreach (var geomObj in geometryElement)
                {
                    if (geomObj is Curve curve)
                    {
                        curves.Add(curve);
                    }
                    else if (geomObj is GeometryInstance geomInstance)
                    {
                        var instanceGeometry = geomInstance.GetInstanceGeometry();
                        foreach (var instObj in instanceGeometry)
                        {
                            if (instObj is Curve instCurve)
                            {
                                curves.Add(instCurve);
                            }
                        }
                    }
                }

                if (curves.Count > 0)
                {
                    // Sort curves to make them contiguous
                    var sortedCurves = SortCurvesContiguous(curves);

                    if (sortedCurves != null && sortedCurves.Count > 0)
                    {
                        // Create a CurveLoop from the sorted curves
                        return CurveLoop.Create(sortedCurves);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error extracting property line boundary", ex);
                return null;
            }
        }

        private List<Curve> SortCurvesContiguous(List<Curve> curves)
        {
            if (curves == null || curves.Count == 0)
                return curves;

            var sortedCurves = new List<Curve>();
            var remainingCurves = new List<Curve>(curves);

            // Start with the first curve
            sortedCurves.Add(remainingCurves[0]);
            remainingCurves.RemoveAt(0);

            double tolerance = 0.001; // 1mm tolerance for endpoint matching

            // Keep adding curves until all are sorted or we can't find a match
            while (remainingCurves.Count > 0)
            {
                var lastCurve = sortedCurves[sortedCurves.Count - 1];
                var lastEndPoint = lastCurve.GetEndPoint(1);

                bool foundMatch = false;

                for (int i = 0; i < remainingCurves.Count; i++)
                {
                    var candidate = remainingCurves[i];
                    var candidateStart = candidate.GetEndPoint(0);
                    var candidateEnd = candidate.GetEndPoint(1);

                    // Check if candidate starts where last curve ends
                    if (lastEndPoint.DistanceTo(candidateStart) < tolerance)
                    {
                        sortedCurves.Add(candidate);
                        remainingCurves.RemoveAt(i);
                        foundMatch = true;
                        break;
                    }
                    // Check if candidate is reversed (ends where last curve ends)
                    else if (lastEndPoint.DistanceTo(candidateEnd) < tolerance)
                    {
                        // Create a reversed curve
                        var reversedCurve = candidate.CreateReversed();
                        sortedCurves.Add(reversedCurve);
                        remainingCurves.RemoveAt(i);
                        foundMatch = true;
                        break;
                    }
                }

                // If no match found, the curves are not forming a closed loop
                if (!foundMatch)
                {
                    _logger.LogWarning($"Could not find contiguous curve. {remainingCurves.Count} curves remaining. Last endpoint: {lastEndPoint}");

                    // Try to find any curve close to the start point to close the loop
                    var firstCurve = sortedCurves[0];
                    var firstStartPoint = firstCurve.GetEndPoint(0);

                    if (lastEndPoint.DistanceTo(firstStartPoint) < tolerance)
                    {
                        // Loop is closed, we're done
                        _logger.LogInformation($"Curve loop closed successfully with {sortedCurves.Count} curves");
                        break;
                    }

                    // Cannot continue, return what we have
                    _logger.LogError($"Curves are not contiguous. Sorted {sortedCurves.Count} out of {curves.Count} curves");
                    return null;
                }
            }

            return sortedCurves;
        }

        private bool IsWallInsidePropertyLine(Wall wall, CurveLoop propertyLineBoundary)
        {
            try
            {
                // Get the wall location curve
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null)
                    return false;

                var wallCurve = locationCurve.Curve;

                // Get the midpoint of the wall
                var midpoint = wallCurve.Evaluate(0.5, true);

                // Extract polygon points from the curve loop
                var polygonPoints = new List<XYZ>();
                foreach (var curve in propertyLineBoundary)
                {
                    polygonPoints.Add(curve.GetEndPoint(0));
                }

                // Use ray casting algorithm for point-in-polygon test
                return IsPointInsidePolygon(midpoint, polygonPoints);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking if wall {wall.Id.Value} is inside property line", ex);
                return false;
            }
        }

        // Ray casting algorithm to check if a point is inside a polygon
        private bool IsPointInsidePolygon(XYZ point, List<XYZ> polygon)
        {
            bool inside = false;
            int count = polygon.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var xi = polygon[i].X;
                var yi = polygon[i].Y;
                var xj = polygon[j].X;
                var yj = polygon[j].Y;

                var intersect = ((yi > point.Y) != (yj > point.Y)) &&
                               (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        private bool CanHighlightWalls(object parameter)
        {
            return PerimeterWalls.Count > 0;
        }

        private void HighlightWalls(object parameter)
        {
            try
            {
                _logger.LogInformation("Highlighting perimeter walls in 3D view...");

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

                // Select the perimeter walls
                var wallIds = PerimeterWalls.Select(w => w.ElementId).ToList();
                _document.Selection.SetElementIds(wallIds);

                _logger.LogInformation($"Highlighted {wallIds.Count} perimeter walls in 3D view.");
                // TaskDialog.Show("Success", $"Highlighted {wallIds.Count} perimeter walls in 3D view.");
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

        private double CalculateWallGrossArea(Wall wall)
        {
            try
            {
                // Try to get the gross area from built-in parameter
                var areaParam = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null && areaParam.HasValue)
                {
                    return areaParam.AsDouble();
                }

                // Fallback: calculate from height and length
                var heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                var lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);

                if (heightParam != null && lengthParam != null)
                {
                    var height = heightParam.AsDouble();
                    var length = lengthParam.AsDouble();
                    return height * length;
                }

                return 0.0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating gross area for wall {wall.Id.Value}", ex);
                return 0.0;
            }
        }

        private double CalculateWallOpeningsArea(Wall wall)
        {
            try
            {
                double totalOpeningsArea = 0.0;

                // Get all inserts (windows, doors, etc.) hosted in this wall
                var insertIds = wall.FindInserts(true, true, true, true);

                foreach (var insertId in insertIds)
                {
                    var insert = _document.Document.GetElement(insertId);

                    if (insert is FamilyInstance familyInstance)
                    {
                        // Try to get the area from the instance
                        var areaParam = familyInstance.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                        if (areaParam != null && areaParam.HasValue)
                        {
                            totalOpeningsArea += areaParam.AsDouble();
                        }
                        else
                        {
                            // Fallback: calculate from width and height parameters
                            var widthParam = familyInstance.Symbol?.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
                            var heightParam = familyInstance.Symbol?.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM);

                            if (widthParam != null && heightParam != null)
                            {
                                var width = widthParam.AsDouble();
                                var height = heightParam.AsDouble();
                                totalOpeningsArea += width * height;
                            }
                        }
                    }
                }

                return totalOpeningsArea;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating openings area for wall {wall.Id.Value}", ex);
                return 0.0;
            }
        }

        private void HighlightWallIn3D(object parameter)
        {
            try
            {
                if (parameter is WallInfo wallInfo)
                {
                    _logger.LogInformation($"Highlighting wall {wallInfo.ElementId.Value} in 3D view...");

                    var doc = _document.Document;

                    // Find a 3D view
                    var view3D = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .Cast<View3D>()
                        .FirstOrDefault(v => !v.IsTemplate);

                    if (view3D == null)
                    {
                        TaskDialog.Show("No 3D View", "No 3D view found in the document.");
                        return;
                    }

                    // Set the active view to 3D
                    _document.ActiveView = view3D;

                    // Select the wall
                    _document.Selection.SetElementIds(new List<ElementId> { wallInfo.ElementId });

                    _logger.LogInformation($"Highlighted wall in 3D view.");
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
                    _logger.LogInformation($"Highlighting wall {wallInfo.ElementId.Value} in floor plan...");

                    var doc = _document.Document;
                    var wall = wallInfo.Wall;

                    // Get the wall's base level
                    var baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    if (baseLevelParam != null && baseLevelParam.AsElementId() != ElementId.InvalidElementId)
                    {
                        var level = doc.GetElement(baseLevelParam.AsElementId()) as Level;
                        if (level != null)
                        {
                            // Find the floor plan view for this level
                            var floorPlan = new FilteredElementCollector(doc)
                                .OfClass(typeof(ViewPlan))
                                .Cast<ViewPlan>()
                                .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate && v.GenLevel?.Id == level.Id);

                            if (floorPlan != null)
                            {
                                // Set the active view to floor plan
                                _document.ActiveView = floorPlan;

                                // Select the wall
                                _document.Selection.SetElementIds(new List<ElementId> { wallInfo.ElementId });

                                _logger.LogInformation($"Highlighted wall in floor plan {floorPlan.Name}.");
                                return;
                            }
                        }
                    }

                    TaskDialog.Show("No Floor Plan", "Could not find floor plan for this wall's level.");
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
                _logger.LogInformation("Calculating distance groups from walls with created regions...");
                _logger.LogInformation($"Walls with regions: {wallsWithRegions?.Count ?? 0}");

                var groups = new List<DistanceGroupSummary>();

                if (wallsWithRegions == null || wallsWithRegions.Count == 0)
                {
                    DistanceGroups.Clear();
                    _logger.LogWarning("No walls with regions to calculate distance groups");
                    return;
                }

                // Group walls by their assigned reference line
                var wallsByReferenceLine = wallsWithRegions
                    .Where(w => w.LimitingDistance.HasValue && w.ReferenceLine != null)
                    .GroupBy(w => w.ReferenceLine)
                    .OrderBy(g => g.Key.LineTypeFormatted)
                    .ThenBy(g => g.Key.Name)
                    .ToList();

                _logger.LogInformation($"Grouped into {wallsByReferenceLine.Count} reference line groups");

                // Distance ranges based on building code tables (converted to feet from meters)
                var ranges = new[]
                {
                    new { Min = 0.0, Max = 3.937, Label = "0-1.2m (0-3.9ft)" },
                    new { Min = 3.937, Max = 4.921, Label = "1.2-1.5m (3.9-4.9ft)" },
                    new { Min = 4.921, Max = 6.562, Label = "1.5-2m (4.9-6.6ft)" },
                    new { Min = 6.562, Max = 8.202, Label = "2-2.5m (6.6-8.2ft)" },
                    new { Min = 8.202, Max = 9.843, Label = "2.5-3m (8.2-9.8ft)" },
                    new { Min = 9.843, Max = 13.123, Label = "3-4m (9.8-13.1ft)" },
                    new { Min = 13.123, Max = 16.404, Label = "4-5m (13.1-16.4ft)" },
                    new { Min = 16.404, Max = 19.685, Label = "5-6m (16.4-19.7ft)" },
                    new { Min = 19.685, Max = 22.966, Label = "6-7m (19.7-23.0ft)" },
                    new { Min = 22.966, Max = 26.247, Label = "7-8m (23.0-26.2ft)" },
                    new { Min = 26.247, Max = 29.528, Label = "8-9m (26.2-29.5ft)" },
                    new { Min = 29.528, Max = double.MaxValue, Label = "9m+ (29.5ft+)" }
                };

                foreach (var refLineGroup in wallsByReferenceLine)
                {
                    var refLineName = refLineGroup.Key.Name;

                    foreach (var range in ranges)
                    {
                        // For the last range (9m+), handle infinity properly
                        var wallsInRange = refLineGroup
                            .Where(w => w.LimitingDistance.Value >= range.Min &&
                                       (w.LimitingDistance.Value < range.Max || range.Max == double.MaxValue))
                            .ToList();

                        if (wallsInRange.Any())
                        {
                            var group = new DistanceGroupSummary
                            {
                                Orientation = refLineName,
                                DistanceRange = range.Label,
                                MinDistance = range.Min,
                                MaxDistance = range.Max,
                                WallCount = wallsInRange.Count,
                                TotalGrossArea = wallsInRange.Sum(w => w.GrossArea),
                                TotalOpeningsArea = wallsInRange.Sum(w => w.OpeningsArea)
                            };

                            groups.Add(group);
                            _logger.LogInformation($"  {refLineName} - {range.Label}: {group.WallCount} walls, Gross: {group.TotalGrossArea:F2} ft²");
                        }
                    }
                }

                // Update the observable collection
                DistanceGroups.Clear();
                foreach (var group in groups.OrderBy(g => g.Orientation).ThenBy(g => g.MinDistance))
                {
                    DistanceGroups.Add(group);
                }

                _logger.LogInformation($"Distance groups calculated: {DistanceGroups.Count} groups");
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
                BuildingCodeCompliance.Clear();

                if (DistanceGroups == null || DistanceGroups.Count == 0)
                {
                    _logger.LogWarning("No distance groups available for compliance calculation");
                    return;
                }

                // Group by orientation (elevation)
                var groupedByOrientation = DistanceGroups
                    .GroupBy(g => g.Orientation)
                    .OrderBy(g => g.Key);

                foreach (var orientationGroup in groupedByOrientation)
                {
                    var elevation = orientationGroup.Key;

                    // Calculate total area for this elevation
                    var totalGrossArea = orientationGroup.Sum(g => g.TotalGrossArea);
                    var totalOpeningsArea = orientationGroup.Sum(g => g.TotalOpeningsArea);

                    // Get the minimum limiting distance for this elevation (most restrictive)
                    var minLimitingDistance = orientationGroup.Min(g => g.MinDistance);

                    // Calculate proposed unprotected openings percentage
                    var proposedOpeningsPercent = totalGrossArea > 0
                        ? (totalOpeningsArea / totalGrossArea * 100)
                        : 0;

                    // Determine maximum allowed openings based on limiting distance and building classification
                    var maxAllowedPercent = GetMaxAllowedOpeningsPercent(minLimitingDistance, BuildingClassification);
                    var frr = GetFireResistanceRating(minLimitingDistance, BuildingClassification);
                    var constructionType = GetConstructionTypeRequired(minLimitingDistance, BuildingClassification);
                    var claddingType = GetCladdingTypeRequired(minLimitingDistance, BuildingClassification);

                    // Convert from ft² to m² (1 ft² = 0.092903 m²)
                    var totalGrossAreaM2 = totalGrossArea * 0.092903;
                    var limitingDistanceM = minLimitingDistance * 0.3048; // ft to m

                    var compliance = new BuildingCodeComplianceSummary
                    {
                        OccupancyClassification = BuildingClassification,
                        Elevation = elevation,
                        ExposingBuildingFaceArea = totalGrossAreaM2,
                        LimitingDistance = limitingDistanceM,
                        MaxUnprotectedOpeningsPercent = maxAllowedPercent,
                        ProposedUnprotectedOpeningsPercent = proposedOpeningsPercent,
                        FireResistanceRating = frr,
                        ConstructionTypeRequired = constructionType,
                        CladdingTypeRequired = claddingType
                    };

                    BuildingCodeCompliance.Add(compliance);

                    _logger.LogInformation($"  {elevation}: Area={totalGrossAreaM2:F1}m², LD={limitingDistanceM:F1}m, Max={maxAllowedPercent}%, Proposed={proposedOpeningsPercent:F1}%, Status={compliance.ComplianceStatus}");
                }

                _logger.LogInformation($"Building code compliance calculated: {BuildingCodeCompliance.Count} elevations");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating building code compliance", ex);
            }
        }

        /// <summary>
        /// Determines maximum allowed unprotected openings percentage based on limiting distance
        /// Based on NBC (National Building Code) Table 3.2.3.1.D
        /// </summary>
        private double GetMaxAllowedOpeningsPercent(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048; // Convert ft to m

            // Simplified NBC Table 3.2.3.1.D logic
            // These values should be adjusted based on actual building code requirements
            if (limitingDistanceM <= 1.2) return 10;
            if (limitingDistanceM <= 1.5) return 15;
            if (limitingDistanceM <= 2.0) return 25;
            if (limitingDistanceM <= 2.5) return 40;
            if (limitingDistanceM <= 3.0) return 60;
            if (limitingDistanceM <= 4.0) return 80;
            if (limitingDistanceM <= 5.0) return 100;
            return 100; // No restriction for > 5m
        }

        /// <summary>
        /// Determines required fire resistance rating based on limiting distance
        /// </summary>
        private string GetFireResistanceRating(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048;

            if (limitingDistanceM <= 2.0) return "1 h";
            if (limitingDistanceM <= 3.0) return "2 h";
            if (limitingDistanceM <= 5.0) return "1 h";
            return "45 min";
        }

        /// <summary>
        /// Determines required construction type based on limiting distance
        /// </summary>
        private string GetConstructionTypeRequired(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048;

            if (limitingDistanceM <= 2.0) return "Combustible / Noncombustible";
            return "Noncombustible";
        }

        /// <summary>
        /// Determines required cladding type based on limiting distance
        /// </summary>
        private string GetCladdingTypeRequired(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048;

            if (limitingDistanceM <= 2.0) return "Combustible / Noncombustible";
            if (limitingDistanceM <= 5.0) return "Noncombustible";
            return "Noncombustible";
        }

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

            // Unsubscribe from events
            try
            {
                if (_document?.Application != null)
                {
                    _document.Application.Idling -= OnIdling;
                    _logger.LogInformation("Cleanup: Unsubscribed from Idling event.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Cleanup: Error unsubscribing from Idling event", ex);
            }
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
    }
}
