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

        public LimitingDistanceWindowVM(UIDocument uidoc) : base(uidoc)
        {
            _revitService = DIContainerService.Container.GetInstance<IRevitServiceFactory>().Create(uidoc);
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();

            _perimeterWalls = new ObservableCollection<WallInfo>();
            _referenceLines = new ObservableCollection<ReferenceLineInfo>();
            _distanceGroups = new ObservableCollection<DistanceGroupSummary>();

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

            _logger.LogInformation("LimitingDistanceWindowVM initialized.");

            // Set logger for WallInfo static logging
            WallInfo.SetLogger(_logger);

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
                // Check current selection
                var selectedIds = _document?.Selection?.GetElementIds();
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
                    // Create a CurveLoop from the curves
                    return CurveLoop.Create(curves);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error extracting property line boundary", ex);
                return null;
            }
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
                TaskDialog.Show("Success", $"Highlighted {wallIds.Count} perimeter walls in 3D view.");
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

                    // Get all walls matching this orientation and distance range
                    var wallsToHighlight = PerimeterWalls
                        .Where(w => w.Orientation != null &&
                                   w.LimitingDistance.HasValue &&
                                   GetOrientationDescription(w.Orientation) == group.Orientation &&
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
                    TaskDialog.Show("Success", $"Highlighted {wallsToHighlight.Count} walls in {group.Orientation} - {group.DistanceRange} in 3D view.");
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
            try
            {
                _logger.LogInformation("Initiating reference line detection via ExternalEvent...");

                // Set parameters for the event handler with callback
                _detectReferenceLinesHandler.SetParameters(
                    _document,
                    PerimeterWalls,
                    ReferenceLines,
                    RayLengthLimit,
                    () => {
                        // This callback runs after the external event completes
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            new Action(() => CalculateDistanceGroups()),
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

        private void CalculateDistanceGroups()
        {
            try
            {
                _logger.LogInformation("Calculating distance groups by orientation and distance...");
                _logger.LogInformation($"Total perimeter walls: {PerimeterWalls.Count}");
                _logger.LogInformation($"Walls with limiting distance: {PerimeterWalls.Count(w => w.LimitingDistance.HasValue)}");
                _logger.LogInformation($"Walls with orientation: {PerimeterWalls.Count(w => w.Orientation != null)}");

                var groups = new List<DistanceGroupSummary>();

                // Group walls by orientation first
                var wallsWithData = PerimeterWalls
                    .Where(w => w.LimitingDistance.HasValue && w.Orientation != null)
                    .ToList();

                _logger.LogInformation($"Walls with both limiting distance and orientation: {wallsWithData.Count}");

                var orientationGroups = wallsWithData
                    .GroupBy(w => GetOrientationDescription(w.Orientation))
                    .ToList();

                // Distance ranges based on building code tables (converted to feet from meters)
                // Table 3.2.3.1.-D and 3.2.3.1.-E show ranges: 0, 1.2, 1.5, 2.0, 2.5, 3, 4, 5, 6, 7, 8, 9+ meters
                var ranges = new[]
                {
                    new { Min = 0.0, Max = 3.937, Label = "0-1.2m (0-3.9ft)" },      // 0-1.2m
                    new { Min = 3.937, Max = 4.921, Label = "1.2-1.5m (3.9-4.9ft)" }, // 1.2-1.5m
                    new { Min = 4.921, Max = 6.562, Label = "1.5-2m (4.9-6.6ft)" },   // 1.5-2m
                    new { Min = 6.562, Max = 8.202, Label = "2-2.5m (6.6-8.2ft)" },   // 2-2.5m
                    new { Min = 8.202, Max = 9.843, Label = "2.5-3m (8.2-9.8ft)" },   // 2.5-3m
                    new { Min = 9.843, Max = 13.123, Label = "3-4m (9.8-13.1ft)" },   // 3-4m
                    new { Min = 13.123, Max = 16.404, Label = "4-5m (13.1-16.4ft)" }, // 4-5m
                    new { Min = 16.404, Max = 19.685, Label = "5-6m (16.4-19.7ft)" }, // 5-6m
                    new { Min = 19.685, Max = 22.966, Label = "6-7m (19.7-23.0ft)" }, // 6-7m
                    new { Min = 22.966, Max = 26.247, Label = "7-8m (23.0-26.2ft)" }, // 7-8m
                    new { Min = 26.247, Max = 29.528, Label = "8-9m (26.2-29.5ft)" }, // 8-9m
                    new { Min = 29.528, Max = double.MaxValue, Label = "9m+ (29.5ft+)" } // 9m+
                };

                foreach (var orientationGroup in orientationGroups)
                {
                    var orientation = orientationGroup.Key;

                    foreach (var range in ranges)
                    {
                        var wallsInRange = orientationGroup
                            .Where(w => w.LimitingDistance.Value >= range.Min &&
                                       w.LimitingDistance.Value < range.Max)
                            .ToList();

                        if (wallsInRange.Any())
                        {
                            var group = new DistanceGroupSummary
                            {
                                Orientation = orientation,
                                DistanceRange = range.Label,
                                MinDistance = range.Min,
                                MaxDistance = range.Max,
                                WallCount = wallsInRange.Count,
                                TotalGrossArea = wallsInRange.Sum(w => w.GrossArea),
                                TotalOpeningsArea = wallsInRange.Sum(w => w.OpeningsArea)
                            };

                            groups.Add(group);
                            _logger.LogInformation($"Group {orientation} - {range.Label}: {group.WallCount} walls, Gross: {group.TotalGrossArea:F2} ft²");
                        }
                    }
                }

                // Update the observable collection
                DistanceGroups.Clear();
                foreach (var group in groups.OrderBy(g => g.Orientation).ThenBy(g => g.MinDistance))
                {
                    DistanceGroups.Add(group);
                }

                _logger.LogInformation($"Distance groups calculated: {DistanceGroups.Count} groups across {orientationGroups.Count} orientations");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating distance groups", ex);
            }
        }

        private string GetOrientationDescription(XYZ? orientation)
        {
            if (orientation == null) return "Unknown";

            var normal = new XYZ(orientation.X, orientation.Y, 0).Normalize();
            var absX = Math.Abs(normal.X);
            var absY = Math.Abs(normal.Y);

            if (absY > absX)
            {
                return normal.Y > 0 ? "North" : "South";
            }
            else
            {
                return normal.X > 0 ? "East" : "West";
            }
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

                // Set parameters for the event handler
                _createProjectionsHandler.SetParameters(_document, PerimeterWalls.ToList(), ReferenceLines.ToList());

                // Raise the external event
                _createProjectionsEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error initiating wall projections creation", ex);
                TaskDialog.Show("Error", $"Error creating wall projections: {ex.Message}");
            }
        }

        // Keep the CastRayAndFindIntersection method for the event handler to use
        private void DetectReferenceLinesOld(object parameter)
        {
            try
            {
                _logger.LogInformation("Detecting reference lines by ray casting from exterior walls...");

                ReferenceLines.Clear();
                var doc = _document.Document;
                var activeView = _document.ActiveView;

                // Check if active view is a floor plan
                if (activeView == null || activeView.ViewType != ViewType.FloorPlan)
                {
                    TaskDialog.Show("Error", "Please activate a Floor Plan view before detecting reference lines.");
                    return;
                }

                // Collect all potential reference elements
                // 1. Get all Road_LD instances
                var roadCenterlines = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol?.Family?.Name == "Road_LD")
                    .ToList();

                // 2. Get all property lines (from OST_SiteProperty category, not segments)
                var propertyLines = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_SiteProperty)
                    .WhereElementIsNotElementType()
                    .ToList();

                _logger.LogInformation($"Property lines found (OST_SiteProperty): {propertyLines.Count}");

                // Log property line types
                foreach (var pl in propertyLines.Take(3))
                {
                    _logger.LogInformation($"Property line {pl.Id.Value} type: {pl.GetType().Name}, category: {pl.Category?.Name ?? "null"}");
                }

                // 3. Get all other perimeter walls (for imaginary lines)
                var allPerimeterWalls = PerimeterWalls.Select(w => w.Wall).ToList();

                // Extract curves from property lines (each property line may have multiple curves)
                var propertyLineCurves = new List<(Element element, Curve curve)>();

                foreach (var pl in propertyLines)
                {
                    var options = new Options
                    {
                        ComputeReferences = false,
                        DetailLevel = ViewDetailLevel.Fine
                    };

                    var geom = pl.get_Geometry(options);
                    if (geom != null)
                    {
                        int curveCount = 0;
                        foreach (var geomObj in geom)
                        {
                            if (geomObj is Curve curve)
                            {
                                propertyLineCurves.Add((pl, curve));
                                curveCount++;
                            }
                            else if (geomObj is GeometryInstance geomInst)
                            {
                                var instGeom = geomInst.GetInstanceGeometry();
                                foreach (var instObj in instGeom)
                                {
                                    if (instObj is Curve instCurve)
                                    {
                                        propertyLineCurves.Add((pl, instCurve));
                                        curveCount++;
                                    }
                                }
                            }
                        }

                        if (curveCount > 0)
                        {
                            _logger.LogInformation($"Property Line {pl.Id.Value}: Extracted {curveCount} curves");
                        }
                        else
                        {
                            _logger.LogWarning($"Property Line {pl.Id.Value}: No curves found in geometry");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Property Line {pl.Id.Value}: get_Geometry returned null");
                    }
                }

                _logger.LogInformation($"Total property line curves extracted: {propertyLineCurves.Count}");
                _logger.LogInformation($"Found {roadCenterlines.Count} Road CLs, {propertyLineCurves.Count} Property Line Segments, {allPerimeterWalls.Count} Perimeter Walls");

                // Process each perimeter wall and cast rays
                var createdImaginaryLines = new HashSet<string>();
                int imaginaryLineCount = 0;
                int propertyLineCount = 0;
                int roadCLCount = 0;

                // Store ray information for drawing
                var raysToDrawn = new List<(XYZ start, XYZ end, bool hit)>();

                foreach (var wallInfo in PerimeterWalls)
                {
                    var wall = wallInfo.Wall;
                    var locationCurve = wall.Location as LocationCurve;
                    if (locationCurve == null) continue;

                    var wallCurve = locationCurve.Curve;
                    var midpoint = wallCurve.Evaluate(0.5, true);

                    // Get wall normal direction (perpendicular outward)
                    var wallDirection = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();
                    var normal = new XYZ(-wallDirection.Y, wallDirection.X, 0); // Rotate 90 degrees in XY plane

                    // Create ray end point (ray length limit in meters, convert to feet)
                    var rayEndPoint = midpoint + (normal * (_rayLengthLimit * 3.28084)); // meters to feet

                    // Log ray information for debugging
                    _logger.LogInformation($"Wall {wall.Id.Value}: Ray from ({midpoint.X:F2}, {midpoint.Y:F2}) to ({rayEndPoint.X:F2}, {rayEndPoint.Y:F2})");

                    // Cast ray and find intersections
                    var hitResult = CastRayAndFindIntersection(wall, midpoint, rayEndPoint, allPerimeterWalls, propertyLineCurves, roadCenterlines);

                    // Store ray for drawing (whether it hit or not)
                    bool hasHit = hitResult != null;
                    raysToDrawn.Add((midpoint, rayEndPoint, hasHit));

                    if (hitResult != null)
                    {
                        // Check if we already created this reference line
                        var key = $"{hitResult.LineType}_{hitResult.ElementId.Value}";
                        if (!createdImaginaryLines.Contains(key))
                        {
                            ReferenceLines.Add(hitResult);
                            createdImaginaryLines.Add(key);

                            switch (hitResult.LineType)
                            {
                                case ReferenceLineType.ImaginaryLine:
                                    imaginaryLineCount++;
                                    break;
                                case ReferenceLineType.PropertyLine_NonStreetEdge:
                                case ReferenceLineType.PropertyLine_StreetEdge:
                                    propertyLineCount++;
                                    break;
                                case ReferenceLineType.RoadCenterline:
                                    roadCLCount++;
                                    break;
                            }

                            _logger.LogInformation($"Wall {wall.Id.Value} → {hitResult.LineTypeFormatted} at distance {hitResult.Name}");
                        }
                    }
                }

                // Draw rays as Detail Lines in the active floor plan view
                using (Transaction trans = new Transaction(doc, "Draw Reference Line Rays"))
                {
                    trans.Start();
                    try
                    {
                        int rayCount = 0;
                        foreach (var (start, end, hit) in raysToDrawn)
                        {
                            // Create 2D points (Z=0 for floor plan)
                            var start2D = new XYZ(start.X, start.Y, 0);
                            var end2D = new XYZ(end.X, end.Y, 0);

                            // Create line
                            var line = Line.CreateBound(start2D, end2D);

                            // Create detail line in the active view
                            var detailLine = doc.Create.NewDetailCurve(activeView, line);
                            rayCount++;
                        }

                        _logger.LogInformation($"Created {rayCount} detail lines for rays in view {activeView.Name}");
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error creating detail lines: {ex.Message}", ex);
                        trans.RollBack();
                    }
                }

                _logger.LogInformation($"Total reference lines detected: {ReferenceLines.Count}");
                _logger.LogInformation($"Breakdown - Imaginary Lines: {imaginaryLineCount}, Property Lines: {propertyLineCount}, Road CLs: {roadCLCount}");

                TaskDialog.Show("Reference Lines Detected",
                    $"Detected {ReferenceLines.Count} reference lines:\n" +
                    $"- Imaginary Lines (between walls): {imaginaryLineCount}\n" +
                    $"- Property Line Segments: {propertyLineCount}\n" +
                    $"- Road Centerlines: {roadCLCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error detecting reference lines", ex);
                TaskDialog.Show("Error", $"Error detecting reference lines: {ex.Message}");
            }
        }

        private ReferenceLineInfo? CastRayAndFindIntersection(
            Wall sourceWall,
            XYZ rayStart,
            XYZ rayEnd,
            List<Wall> allWalls,
            List<(Element element, Curve curve)> propertyLineCurves,
            List<FamilyInstance> roadCenterlines)
        {
            try
            {
                var rayLine = Line.CreateBound(rayStart, rayEnd);
                var ray2D = Line.CreateBound(new XYZ(rayStart.X, rayStart.Y, 0), new XYZ(rayEnd.X, rayEnd.Y, 0));

                double closestDistance = double.MaxValue;
                ReferenceLineInfo? closestHit = null;

                // 1. Check intersection with other walls first (highest priority)
                foreach (var wall in allWalls)
                {
                    if (wall.Id == sourceWall.Id) continue; // Skip self

                    var wallLocationCurve = wall.Location as LocationCurve;
                    if (wallLocationCurve == null) continue;

                    var wallCurve = wallLocationCurve.Curve;
                    var wallCurve2D = Line.CreateBound(
                        new XYZ(wallCurve.GetEndPoint(0).X, wallCurve.GetEndPoint(0).Y, 0),
                        new XYZ(wallCurve.GetEndPoint(1).X, wallCurve.GetEndPoint(1).Y, 0));

                    var result = ray2D.Intersect(wallCurve2D, out IntersectionResultArray results);
                    if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
                    {
                        var intersection = results.get_Item(0);
                        var distance = rayStart.DistanceTo(intersection.XYZPoint);

                        if (distance < closestDistance && distance > 0.01) // Ignore very small distances
                        {
                            closestDistance = distance;

                            // Create imaginary line at midpoint between the two walls
                            var midpointBetweenWalls = (rayStart + intersection.XYZPoint) / 2.0;
                            var imaginaryLine = Line.CreateBound(rayStart, intersection.XYZPoint);

                            closestHit = new ReferenceLineInfo
                            {
                                Element = wall,
                                ElementId = wall.Id,
                                LineType = ReferenceLineType.ImaginaryLine,
                                Curve = imaginaryLine,
                                Name = $"Imaginary Line (Wall {sourceWall.Id.Value} ↔ Wall {wall.Id.Value})",
                                IsStreetEdge = false
                            };
                        }
                    }
                }

                // If we hit a wall, return immediately (highest priority)
                if (closestHit != null)
                    return closestHit;

                // 2. Check intersection with property lines
                foreach (var (propLine, curve) in propertyLineCurves)
                {
                    // Curve is already extracted from the tuple
                    if (curve == null) continue;

                    // Project curve to 2D (XY plane at Z=0)
                    Curve curve2D = null;
                    if (curve is Line line)
                    {
                        curve2D = Line.CreateBound(
                            new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, 0),
                            new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, 0));
                    }
                    else if (curve is Arc arc)
                    {
                        // Project arc to 2D
                        var start2D = new XYZ(arc.GetEndPoint(0).X, arc.GetEndPoint(0).Y, 0);
                        var end2D = new XYZ(arc.GetEndPoint(1).X, arc.GetEndPoint(1).Y, 0);
                        var mid = arc.Evaluate(0.5, true);
                        var mid2D = new XYZ(mid.X, mid.Y, 0);

                        try
                        {
                            curve2D = Arc.Create(start2D, end2D, mid2D);
                        }
                        catch
                        {
                            // If arc creation fails, use line approximation
                            curve2D = Line.CreateBound(start2D, end2D);
                        }
                    }
                    else
                    {
                        // For other curve types, approximate with line from start to end
                        curve2D = Line.CreateBound(
                            new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0),
                            new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0));
                    }

                    if (curve2D == null) continue;

                    var result = ray2D.Intersect(curve2D, out IntersectionResultArray results);
                    if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
                    {
                        var intersection = results.get_Item(0);
                        var distance = rayStart.DistanceTo(intersection.XYZPoint);

                        _logger.LogInformation($"Property line {propLine.Id.Value} hit at distance {distance} ft");

                        if (distance < closestDistance && distance > 0.01)
                        {
                            closestDistance = distance;
                            closestHit = new ReferenceLineInfo
                            {
                                Element = propLine,
                                ElementId = propLine.Id,
                                LineType = ReferenceLineType.PropertyLine_NonStreetEdge,
                                Curve = curve,
                                Name = $"Property Line - {propLine.Id.Value}",
                                IsStreetEdge = false
                            };
                        }
                    }
                }

                // 3. Check intersection with road centerlines (check after property lines to determine if street edge)
                var roadHit = false;
                foreach (var road in roadCenterlines)
                {
                    var roadLocationCurve = road.Location as LocationCurve;
                    if (roadLocationCurve == null) continue;

                    var roadCurve = roadLocationCurve.Curve;
                    var roadCurve2D = Line.CreateBound(
                        new XYZ(roadCurve.GetEndPoint(0).X, roadCurve.GetEndPoint(0).Y, 0),
                        new XYZ(roadCurve.GetEndPoint(1).X, roadCurve.GetEndPoint(1).Y, 0));

                    var result = ray2D.Intersect(roadCurve2D, out IntersectionResultArray results);
                    if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
                    {
                        var intersection = results.get_Item(0);
                        var distance = rayStart.DistanceTo(intersection.XYZPoint);

                        // If we hit a property line first, then road → use Road CL as governing line
                        if (closestHit != null && closestHit.LineType == ReferenceLineType.PropertyLine_NonStreetEdge)
                        {
                            // Mark property line as street edge
                            closestHit.LineType = ReferenceLineType.PropertyLine_StreetEdge;
                            closestHit.IsStreetEdge = true;
                            roadHit = true;
                        }
                        // If road is closest → use road
                        else if (distance < closestDistance && distance > 0.01)
                        {
                            closestDistance = distance;
                            closestHit = new ReferenceLineInfo
                            {
                                Element = road,
                                ElementId = road.Id,
                                LineType = ReferenceLineType.RoadCenterline,
                                Curve = roadCurve,
                                Name = $"Road CL - {road.Id.Value}",
                                IsStreetEdge = false
                            };
                        }
                    }
                }

                return closestHit;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ray casting for wall {sourceWall.Id.Value}", ex);
                return null;
            }
        }

        private void ShowLogs(object parameter)
        {
            var logWindowVM = new LogWindowVM();
            var windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
            windowManager.Open(logWindowVM);
        }

        private void Close(object parameter)
        {
            _logger.LogInformation("Closing Limiting Distance window.");

            // Set closing flag to prevent OnIdling from accessing disposed objects
            _isClosing = true;

            // Unsubscribe from events
            try
            {
                if (_document?.Application != null)
                {
                    _document.Application.Idling -= OnIdling;
                    _logger.LogInformation("Unsubscribed from Idling event.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error unsubscribing from Idling event", ex);
            }

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
    }
}
