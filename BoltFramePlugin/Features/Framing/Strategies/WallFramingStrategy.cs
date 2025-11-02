using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Services;
using BoltFramePlugin.Features.Framing.Models;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.Features.Framing.Strategies
{
    /// <summary>
    /// Strategy for generating wall framing with top/bottom plates, columns, and studs.
    /// </summary>
    public class WallFramingStrategy : IFramingStrategy
    {
        private IRevitService _revitService;
        public IRevitService RevitService { get => _revitService; set => _revitService = value; }

        public WallFrameGenerateModel Model { get; set; }

        IFrameGenerateModel IFramingStrategy.Model
        {
            get => Model;
            set => Model = value as WallFrameGenerateModel ?? throw new ArgumentException("Invalid model type for WallFramingStrategy.");
        }

        private const double MinFramingLength = 0.1; // Minimum allowable framing length
        private readonly ILoggingService _logger;

        // Track generated members for summary
        private Dictionary<string, List<double>> _generatedMembers = new Dictionary<string, List<double>>();
        private List<ElementId> _allGeneratedMemberIds = new List<ElementId>();
        public FramingSummaryModel Summary { get; private set; }
        public Group CreatedGroup { get; private set; }

        public WallFramingStrategy(IRevitService revitService, IFrameGenerateModel model)
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _revitService = revitService ?? throw new ArgumentNullException(nameof(revitService));
            Model = model as WallFrameGenerateModel ?? throw new ArgumentException("Invalid model type for WallFramingStrategy.");
        }

        public void StartGenerate()
        {
            try
            {
                _logger.LogInformation("WallFramingStrategy.StartGenerate called");

                // Initialize tracking
                _generatedMembers.Clear();
                _allGeneratedMemberIds.Clear();

                Document doc = _revitService.UiDoc.Document;
                IEnumerable<Element> walls = Model.TargetElements;

                _logger.LogInformation($"Target wall elements count: {walls?.Count() ?? 0}");

                if (walls == null || !walls.Any())
                {
                    _logger.LogInformation("No target wall elements provided for framing.");
                    TaskDialog.Show("Warning", "No target wall elements provided for framing.");
                    return;
                }

                // Activate all family symbols once if not active
                using (Transaction tx = new Transaction(doc, "Activate Family Symbols"))
                {
                    tx.Start();

                    if (Model.TopPlateConfig?.FamilySymbol != null && !Model.TopPlateConfig.FamilySymbol.IsActive)
                    {
                        _logger.LogInformation($"Activating top plate symbol: {Model.TopPlateConfig.FamilySymbol.Name}");
                        Model.TopPlateConfig.FamilySymbol.Activate();
                    }

                    if (Model.BottomPlateConfig?.FamilySymbol != null && !Model.BottomPlateConfig.FamilySymbol.IsActive)
                    {
                        _logger.LogInformation($"Activating bottom plate symbol: {Model.BottomPlateConfig.FamilySymbol.Name}");
                        Model.BottomPlateConfig.FamilySymbol.Activate();
                    }

                    if (Model.ColumnConfig?.FamilySymbol != null && !Model.ColumnConfig.FamilySymbol.IsActive)
                    {
                        _logger.LogInformation($"Activating column symbol: {Model.ColumnConfig.FamilySymbol.Name}");
                        Model.ColumnConfig.FamilySymbol.Activate();
                    }

                    if (Model.StudConfig?.FamilySymbol != null && !Model.StudConfig.FamilySymbol.IsActive)
                    {
                        _logger.LogInformation($"Activating stud symbol: {Model.StudConfig.FamilySymbol.Name}");
                        Model.StudConfig.FamilySymbol.Activate();
                    }

                    doc.Regenerate();
                    tx.Commit();
                }

                _logger.LogInformation("Family symbols activated");

                // Iterate over each wall and apply framing
                int wallCount = 0;
                foreach (Element wallElement in walls)
                {
                    wallCount++;
                    _logger.LogInformation($"Processing wall #{wallCount}, ID: {wallElement.Id}");

                    Wall wall = wallElement as Wall;
                    if (wall == null)
                    {
                        _logger.LogError("One of the target elements is not a Wall.", new InvalidCastException());
                        continue;
                    }

                    ApplyFramingToWall(doc, wall);
                    _logger.LogInformation($"Framing applied to wall {wall.Id}");
                }

                _logger.LogInformation($"Framing generation completed for {wallCount} walls.");

                // Create group if requested
                if (Model.CreateGroup && _allGeneratedMemberIds.Count > 0)
                {
                    CreateFramingGroup(doc);
                }

                // Generate summary
                GenerateSummary();
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred during wall framing generation.", ex);
                TaskDialog.Show("Error", $"Error in WallFramingStrategy: {ex.Message}\n\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Applies framing to a single wall.
        /// </summary>
        private void ApplyFramingToWall(Document doc, Wall wall)
        {
            try
            {
                Level level = GetLevelForWall(wall);
                LocationCurve locationCurve = wall.Location as LocationCurve;

                if (locationCurve == null)
                {
                    _logger.LogError($"Wall {wall.Id} does not have a location curve.", new InvalidOperationException());
                    return;
                }

                Curve wallCurve = locationCurve.Curve;
                double wallHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 10.0;

                _logger.LogInformation($"Wall curve length: {wallCurve.Length} ft, height: {wallHeight} ft");

                using (Transaction tx = new Transaction(doc, $"Create Wall Framing for Wall {wall.Id}"))
                {
                    tx.Start();

                    // Create top plate (horizontal at top)
                    if (Model.TopPlateConfig != null)
                    {
                        CreateTopPlate(doc, wall, wallCurve, wallHeight, level);
                    }

                    // Create bottom plate (horizontal at bottom)
                    if (Model.BottomPlateConfig != null)
                    {
                        CreateBottomPlate(doc, wall, wallCurve, level);
                    }

                    // Create columns (vertical at wall ends)
                    if (Model.ColumnConfig != null)
                    {
                        CreateColumns(doc, wall, wallCurve, wallHeight, level);
                    }

                    // Create studs (vertical along wall)
                    if (Model.StudConfig != null)
                    {
                        CreateStuds(doc, wall, wallCurve, wallHeight, level);
                    }

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error framing wall ID: {wall.Id}", ex);
            }
        }

        /// <summary>
        /// Creates top plate along the wall.
        /// </summary>
        private void CreateTopPlate(Document doc, Wall wall, Curve wallCurve, double wallHeight, Level level)
        {
            try
            {
                _logger.LogInformation($"Creating top plate for wall {wall.Id}");

                // Apply Z-offset to position at top of wall
                double zOffsetFeet = (Model.TopPlateConfig.ZOffset / 304.8) + wallHeight;
                XYZ translation = new XYZ(0, 0, zOffsetFeet);
                Curve offsetCurve = wallCurve.CreateTransformed(Transform.CreateTranslation(translation));

                FamilyInstance instance = doc.Create.NewFamilyInstance(
                    offsetCurve,
                    Model.TopPlateConfig.FamilySymbol,
                    level,
                    Model.TopPlateConfig.StructuralType
                );

                if (instance != null)
                {
                    _logger.LogInformation($"Created top plate with ID: {instance.Id}");
                    _allGeneratedMemberIds.Add(instance.Id);
                    TrackMember("Top Plate", Model.TopPlateConfig.FamilySymbol, offsetCurve.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating top plate for wall {wall.Id}", ex);
            }
        }

        /// <summary>
        /// Creates bottom plate along the wall.
        /// </summary>
        private void CreateBottomPlate(Document doc, Wall wall, Curve wallCurve, Level level)
        {
            try
            {
                _logger.LogInformation($"Creating bottom plate for wall {wall.Id}");

                // Apply Z-offset to position at bottom of wall
                double zOffsetFeet = Model.BottomPlateConfig.ZOffset / 304.8;
                XYZ translation = new XYZ(0, 0, zOffsetFeet);
                Curve offsetCurve = wallCurve.CreateTransformed(Transform.CreateTranslation(translation));

                FamilyInstance instance = doc.Create.NewFamilyInstance(
                    offsetCurve,
                    Model.BottomPlateConfig.FamilySymbol,
                    level,
                    Model.BottomPlateConfig.StructuralType
                );

                if (instance != null)
                {
                    _logger.LogInformation($"Created bottom plate with ID: {instance.Id}");
                    _allGeneratedMemberIds.Add(instance.Id);
                    TrackMember("Bottom Plate", Model.BottomPlateConfig.FamilySymbol, offsetCurve.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating bottom plate for wall {wall.Id}", ex);
            }
        }

        /// <summary>
        /// Creates columns at wall ends.
        /// </summary>
        private void CreateColumns(Document doc, Wall wall, Curve wallCurve, double wallHeight, Level level)
        {
            try
            {
                _logger.LogInformation($"Creating columns for wall {wall.Id}");

                XYZ startPoint = wallCurve.GetEndPoint(0);
                XYZ endPoint = wallCurve.GetEndPoint(1);

                // Apply Z-offset from config
                double zOffsetFeet = Model.ColumnConfig.ZOffset / 304.8;

                // Create column at start
                XYZ columnStart = startPoint + new XYZ(0, 0, zOffsetFeet);
                XYZ columnEnd = startPoint + new XYZ(0, 0, zOffsetFeet + wallHeight);
                Line columnLine1 = Line.CreateBound(columnStart, columnEnd);

                FamilyInstance column1 = doc.Create.NewFamilyInstance(
                    columnLine1,
                    Model.ColumnConfig.FamilySymbol,
                    level,
                    Model.ColumnConfig.StructuralType
                );

                if (column1 != null)
                {
                    _logger.LogInformation($"Created start column with ID: {column1.Id}");
                    _allGeneratedMemberIds.Add(column1.Id);
                    TrackMember("Column", Model.ColumnConfig.FamilySymbol, columnLine1.Length);
                }

                // Create column at end
                XYZ columnStart2 = endPoint + new XYZ(0, 0, zOffsetFeet);
                XYZ columnEnd2 = endPoint + new XYZ(0, 0, zOffsetFeet + wallHeight);
                Line columnLine2 = Line.CreateBound(columnStart2, columnEnd2);

                FamilyInstance column2 = doc.Create.NewFamilyInstance(
                    columnLine2,
                    Model.ColumnConfig.FamilySymbol,
                    level,
                    Model.ColumnConfig.StructuralType
                );

                if (column2 != null)
                {
                    _logger.LogInformation($"Created end column with ID: {column2.Id}");
                    _allGeneratedMemberIds.Add(column2.Id);
                    TrackMember("Column", Model.ColumnConfig.FamilySymbol, columnLine2.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating columns for wall {wall.Id}", ex);
            }
        }

        /// <summary>
        /// Creates studs along the wall at specified spacing.
        /// </summary>
        private void CreateStuds(Document doc, Wall wall, Curve wallCurve, double wallHeight, Level level)
        {
            try
            {
                _logger.LogInformation($"Creating studs for wall {wall.Id} with spacing {Model.StudConfig.Spacing} ft");

                double spacingInFeet = Model.StudConfig.Spacing;
                double wallLength = wallCurve.Length;
                XYZ startPoint = wallCurve.GetEndPoint(0);
                XYZ wallDirection = (wallCurve.GetEndPoint(1) - startPoint).Normalize();
                double zOffsetFeet = Model.StudConfig.ZOffset / 304.8;

                int studCount = 0;

                // Place studs along wall length at specified spacing
                for (double distance = spacingInFeet; distance < wallLength; distance += spacingInFeet)
                {
                    XYZ studBase = startPoint + wallDirection * distance;
                    XYZ studBottom = studBase + new XYZ(0, 0, zOffsetFeet);
                    XYZ studTop = studBase + new XYZ(0, 0, zOffsetFeet + wallHeight);

                    Line studLine = Line.CreateBound(studBottom, studTop);

                    FamilyInstance stud = doc.Create.NewFamilyInstance(
                        studLine,
                        Model.StudConfig.FamilySymbol,
                        level,
                        Model.StudConfig.StructuralType
                    );

                    if (stud != null)
                    {
                        studCount++;
                        _allGeneratedMemberIds.Add(stud.Id);
                        TrackMember("Stud", Model.StudConfig.FamilySymbol, studLine.Length);
                    }
                }

                _logger.LogInformation($"Created {studCount} studs for wall {wall.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating studs for wall {wall.Id}", ex);
            }
        }

        /// <summary>
        /// Tracks a member for summary statistics.
        /// </summary>
        private void TrackMember(string memberType, FamilySymbol symbol, double length)
        {
            string key = $"{memberType}|{symbol.FamilyName}|{symbol.Name}";
            if (!_generatedMembers.ContainsKey(key))
            {
                _generatedMembers[key] = new List<double>();
            }
            _generatedMembers[key].Add(length);
        }

        /// <summary>
        /// Retrieves the level associated with a given wall.
        /// </summary>
        private Level GetLevelForWall(Wall wall)
        {
            if (Model.WallLevels.TryGetValue(wall.Id, out Level level))
            {
                return level;
            }

            // Fallback to wall's level parameter
            ElementId levelId = wall.LevelId;
            if (levelId != null && levelId != ElementId.InvalidElementId)
            {
                level = _revitService.UiDoc.Document.GetElement(levelId) as Level;
                _logger.LogInformation($"Using wall's level for wall ID: {wall.Id}");
                return level;
            }

            _logger.LogInformation($"No level found for wall ID: {wall.Id}");
            return null;
        }

        /// <summary>
        /// Generates summary statistics from collected member data.
        /// </summary>
        private void GenerateSummary()
        {
            Summary = new FramingSummaryModel
            {
                IsGrouped = Model.CreateGroup && CreatedGroup != null,
                GroupName = CreatedGroup?.GroupType.Name ?? Model.GroupName
            };

            foreach (var kvp in _generatedMembers)
            {
                if (kvp.Value.Count > 0)
                {
                    var item = new FramingSummaryItem
                    {
                        BeamType = kvp.Key.Split('|')[0],
                        FamilyName = kvp.Key.Split('|')[1],
                        TypeName = kvp.Key.Split('|')[2],
                        Count = kvp.Value.Count,
                        MinLength = kvp.Value.Min(),
                        MaxLength = kvp.Value.Max(),
                        AvgLength = kvp.Value.Average(),
                        TotalLength = kvp.Value.Sum()
                    };
                    Summary.Items.Add(item);
                }
            }

            _logger.LogInformation($"Summary generated with {Summary.Items.Count} member types");
        }

        /// <summary>
        /// Creates a group containing all generated framing members.
        /// </summary>
        private void CreateFramingGroup(Document doc)
        {
            try
            {
                using (Transaction tx = new Transaction(doc, "Create Wall Framing Group"))
                {
                    tx.Start();

                    CreatedGroup = doc.Create.NewGroup(_allGeneratedMemberIds);
                    CreatedGroup.GroupType.Name = Model.GroupName;

                    tx.Commit();

                    _logger.LogInformation($"Created group '{Model.GroupName}' with {_allGeneratedMemberIds.Count} members");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating framing group: {ex.Message}", ex);
            }
        }
    }
}
