using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;
using LoBIM.Features.Framing.Models;
using Serilog.Core;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.Framing.Strategies
{
    /// <summary>
    /// Strategy for generating floor framing on multiple floors with flexible grid configurations.
    /// </summary>
    public class MultiFloorFramingStrategy : IFramingStrategy
    {
        /// <summary>
        /// Service for interacting with Revit API.
        /// </summary>
        private IRevitService _revitService;
        public IRevitService RevitService { get => _revitService; set => _revitService = value; }

        /// <summary>
        /// Data model containing framing parameters for multiple floors.
        /// </summary>
        public MultiFloorFrameGenerateModel Model { get; set; }

        IFrameGenerateModel IFramingStrategy.Model
        {
            get => Model;
            set => Model = value as MultiFloorFrameGenerateModel ?? throw new ArgumentException("Invalid model type for MultiFloorFramingStrategy.");
        }

        private const double MinBeamLength = 0.3; // Minimum allowable beam length (~1 foot)
        private readonly ILoggingService _logger;

        // Track generated beams for summary
        private Dictionary<string, List<double>> _generatedBeams = new Dictionary<string, List<double>>();
        private List<ElementId> _allGeneratedBeamIds = new List<ElementId>();
        public FramingSummaryModel Summary { get; private set; }
        public Group CreatedGroup { get; private set; }

        /// <summary>
        /// Initializes a new instance of MultiFloorFramingStrategy.
        /// </summary>
        /// <param name="revitService">Service for Revit API interactions.</param>
        /// <param name="model">Data model for framing.</param>
        public MultiFloorFramingStrategy(IRevitService revitService, IFrameGenerateModel model)
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
            _revitService = revitService ?? throw new ArgumentNullException(nameof(revitService));
            Model = model as MultiFloorFrameGenerateModel ?? throw new ArgumentException("Invalid model type for MultiFloorFramingStrategy.");
        }

        /// <summary>
        /// Starts the framing generation process for multiple floors.
        /// </summary>
        public void StartGenerate()
        {
            try
            {
                _logger.LogInformation("MultiFloorFramingStrategy.StartGenerate called");

                // Initialize tracking
                _generatedBeams.Clear();
                _allGeneratedBeamIds.Clear();

                Document doc = _revitService.UiDoc.Document;
                IEnumerable<Element> floors = Model.TargetElements;

                _logger.LogInformation($"Target elements count: {floors?.Count() ?? 0}");
                _logger.LogInformation($"Grid configs count: {Model.GridConfigs?.Count ?? 0}");

                if (floors == null || !floors.Any())
                {
                    _logger.LogInformation("No target floor elements provided for framing.");
                    TaskDialog.Show("Warning", "No target floor elements provided for framing.");
                    return;
                }

                // Log grid configuration details
                foreach (var config in Model.GridConfigs)
                {
                    _logger.LogInformation($"Grid Config - Orientation: {config.Orientation}, Spacing: {config.Spacing}, FamilySymbol: {config.FamilySymbol?.Name ?? "NULL"}");
                }

                // Activate all family symbols once if not active
                using (Transaction tx = new Transaction(doc, "Activate Family Symbols"))
                {
                    tx.Start();
                    foreach (var config in Model.GridConfigs)
                    {
                        if (config.FamilySymbol != null && !config.FamilySymbol.IsActive)
                        {
                            _logger.LogInformation($"Activating family symbol: {config.FamilySymbol.Name}");
                            config.FamilySymbol.Activate();
                            doc.Regenerate();
                        }
                    }
                    tx.Commit();
                }

                _logger.LogInformation("Family symbols activated");

                // Iterate over each floor and apply framing
                int floorCount = 0;
                foreach (Element floorElement in floors)
                {
                    floorCount++;
                    _logger.LogInformation($"Processing floor #{floorCount}, ID: {floorElement.Id}");

                    Floor floor = floorElement as Floor;
                    if (floor == null)
                    {
                        _logger.LogError("One of the target elements is not a Floor.", new InvalidCastException());
                        continue;
                    }

                    Face bestFlatFace = GetBestFlatSurface(floor);
                    const double offsetDistance = 0.15; // Define the offset distance

                    if (bestFlatFace != null)
                    {
                        _logger.LogInformation($"Found flat surface for floor {floor.Id}, applying framing...");
                        ApplyFramingToFloor(doc, floor, bestFlatFace, offsetDistance);
                        _logger.LogInformation($"Framing applied to floor {floor.Id}");
                    }
                    else
                    {
                        _logger.LogInformation($"No suitable flat surface found for floor ID: {floor.Id}");
                        TaskDialog.Show("Warning", $"No suitable flat surface found for floor ID: {floor.Id}");
                    }
                }

                _logger.LogInformation($"Framing generation completed for {floorCount} floors.");

                // Create group if requested
                if (Model.CreateGroup && _allGeneratedBeamIds.Count > 0)
                {
                    CreateBeamGroup(doc);
                }

                // Generate summary
                GenerateSummary();
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred during multi-floor framing generation.", ex);
                TaskDialog.Show("Error", $"Error in MultiFloorFramingStrategy: {ex.Message}\n\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Generates summary statistics from collected beam data
        /// </summary>
        private void GenerateSummary()
        {
            Summary = new FramingSummaryModel
            {
                IsGrouped = Model.CreateGroup && CreatedGroup != null,
                GroupName = CreatedGroup?.GroupType.Name ?? Model.GroupName
            };

            foreach (var kvp in _generatedBeams)
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

            _logger.LogInformation($"Summary generated with {Summary.Items.Count} beam types");
        }

        /// <summary>
        /// Creates a group containing all generated beams
        /// </summary>
        private void CreateBeamGroup(Document doc)
        {
            try
            {
                using (Transaction tx = new Transaction(doc, "Create Framing Group"))
                {
                    tx.Start();

                    CreatedGroup = doc.Create.NewGroup(_allGeneratedBeamIds);
                    CreatedGroup.GroupType.Name = Model.GroupName;

                    tx.Commit();

                    _logger.LogInformation($"Created group '{Model.GroupName}' with {_allGeneratedBeamIds.Count} beams");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating beam group: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Applies framing to a single floor based on the best flat face.
        /// </summary>
        private void ApplyFramingToFloor(Document doc, Floor floor, Face bestFlatFace, double offsetDistance)
        {
            try
            {
                // Retrieve the level for this floor
                Level level = GetLevelForFloor(floor);

                // Only create boundary beams if BoundaryConfig is enabled
                if (Model.BoundaryConfig != null)
                {
                    _logger.LogInformation($"Creating boundary beams for floor {floor.Id}");

                    using (Transaction tx = new Transaction(doc, $"Create Boundary Beams for Floor {floor.Id}"))
                    {
                        tx.Start();

                        int boundaryBeamCount = 0;
                        int edgeLoopCount = bestFlatFace.EdgeLoops.Size;

                        _logger.LogInformation($"Floor has {edgeLoopCount} edge loops (outer + inner voids)");

                        // Process ALL edge loops (outer boundary + any interior voids)
                        for (int i = 0; i < edgeLoopCount; i++)
                        {
                            EdgeArray edgeArray = bestFlatFace.EdgeLoops.get_Item(i);
                            string loopType = i == 0 ? "outer" : "inner";
                            _logger.LogInformation($"Processing {loopType} edge loop {i} with {edgeArray.Size} edges");

                            foreach (Edge edge in edgeArray)
                            {
                                Curve edgeCurve = edge.AsCurve();

                                // Place boundary beam layers based on BoundaryLayers config
                                int layerCount = Model.BoundaryConfig.BoundaryLayers;
                                _logger.LogInformation($"Creating {layerCount} boundary layer(s) for edge");

                                // Layer 1: Edge beam (always created if BoundaryLayers >= 1)
                                if (layerCount >= 1)
                                {
                                    NewFamilyInstance(doc, edgeCurve, Model.BoundaryConfig.FamilySymbol, level, Model.BoundaryConfig);
                                    boundaryBeamCount++;
                                }

                                // Additional layers: Create offset beams
                                for (int layer = 1; layer < layerCount; layer++)
                                {
                                    double currentOffset = offsetDistance * layer;
                                    Curve offsetCurve = CreateOffsetCurve(edgeCurve, bestFlatFace, currentOffset, Model.BoundaryConfig);
                                    if (offsetCurve != null)
                                    {
                                        NewFamilyInstance(doc, offsetCurve, Model.BoundaryConfig.FamilySymbol, level, Model.BoundaryConfig);
                                        boundaryBeamCount++;
                                    }
                                }
                            }
                        }

                        _logger.LogInformation($"Created {boundaryBeamCount} boundary beams");
                        tx.Commit();
                    }
                }
                else
                {
                    _logger.LogInformation("Boundary beam generation is disabled");
                }

                // Create the framing grid after Beams are placed
                CreateFramingGrid(doc, bestFlatFace, floor, level);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error framing floor ID: {floor.Id}", ex);
            }
        }

        /// <summary>
        /// Creates a new family instance (beam or joist) in the document based on the grid configuration.
        /// </summary>
        private void NewFamilyInstance(Document doc, Curve curve, FamilySymbol symbol, Level level, GridConfig config, string beamType = "Boundary")
        {
            if (symbol == null)
            {
                _logger.LogError("FamilySymbol is null. Cannot create FamilyInstance.", new ArgumentNullException());
                return;
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            // Apply Z_Offset to align with floor surface (convert from mm to feet)
            double zOffsetFeet = config.ZOffset / 304.8;
            XYZ translation = new XYZ(0, 0, zOffsetFeet);
            Curve offsetCurve = curve.CreateTransformed(Transform.CreateTranslation(translation));

            // Create the family instance with Z-offset applied
            FamilyInstance instance = doc.Create.NewFamilyInstance(offsetCurve, symbol, level, config.StructuralType);
            if (instance != null)
            {
                _logger.LogInformation($"Created {beamType} beam with ID: {instance.Id}, Z-offset: {config.ZOffset}mm");

                // Track beam ID for grouping
                _allGeneratedBeamIds.Add(instance.Id);

                // Track beam for summary
                string key = $"{beamType}|{symbol.FamilyName}|{symbol.Name}";
                if (!_generatedBeams.ContainsKey(key))
                {
                    _generatedBeams[key] = new List<double>();
                }
                _generatedBeams[key].Add(offsetCurve.Length);
            }
            else
            {
                _logger.LogError("Failed to create FamilyInstance.", new InvalidOperationException());
            }
        }

        /// <summary>
        /// Creates an offset curve based on the original curve and face normal.
        /// </summary>
        private Curve CreateOffsetCurve(Curve originalCurve, Face face, double offsetDistance, GridConfig config)
        {
            if (originalCurve is Line line)
            {
                return CreateOffsetLine(line, face, offsetDistance, config);
            }
            else
            {
                return CreateOffsetCurveInPlane(originalCurve, face, offsetDistance, config);
            }
        }

        /// <summary>
        /// Creates an offset line perpendicular to the original line and in the plane of the face.
        /// </summary>
        private Curve CreateOffsetLine(Line line, Face face, double offsetDistance, GridConfig config)
        {
            XYZ faceNormal = face.ComputeNormal(new UV(0, 0));

            // Compute the offset direction based on grid orientation
            XYZ offsetDirection = config.Orientation == GridOrientation.Horizontal
                ? faceNormal.CrossProduct(line.Direction).Normalize()
                : faceNormal.CrossProduct(line.Direction).Normalize(); // Modify if vertical grids have different offset logic

            // Offset the start and end points
            XYZ offsetStart = line.GetEndPoint(0) + offsetDirection * offsetDistance;
            XYZ offsetEnd = line.GetEndPoint(1) + offsetDirection * offsetDistance;

            return Line.CreateBound(offsetStart, offsetEnd);
        }

        /// <summary>
        /// Creates an offset curve for non-linear curves.
        /// </summary>
        private Curve CreateOffsetCurveInPlane(Curve curve, Face face, double offsetDistance, GridConfig config)
        {
            IList<XYZ> pointsOnCurve = curve.Tessellate();
            if (pointsOnCurve == null || pointsOnCurve.Count < 2)
                return null;

            List<XYZ> offsetPoints = new List<XYZ>();
            XYZ faceNormal = face.ComputeNormal(new UV(0, 0));

            for (int i = 0; i < pointsOnCurve.Count; i++)
            {
                XYZ tangent;
                if (i == 0)
                {
                    tangent = (pointsOnCurve[i + 1] - pointsOnCurve[i]).Normalize();
                }
                else if (i == pointsOnCurve.Count - 1)
                {
                    tangent = (pointsOnCurve[i] - pointsOnCurve[i - 1]).Normalize();
                }
                else
                {
                    tangent = (pointsOnCurve[i + 1] - pointsOnCurve[i - 1]).Normalize();
                }

                XYZ offsetDirection = faceNormal.CrossProduct(tangent).Normalize();
                XYZ offsetPoint = pointsOnCurve[i] + offsetDirection * offsetDistance;
                offsetPoints.Add(offsetPoint);
            }

            if (offsetPoints.Count >= 2)
            {
                return HermiteSpline.Create(offsetPoints, false);
            }

            return null;
        }

        /// <summary>
        /// Retrieves the level associated with a given floor.
        /// </summary>
        private Level GetLevelForFloor(Floor floor)
        {
            if (Model.FloorLevels.TryGetValue(floor.Id, out Level level))
            {
                return level;
            }

            // Fallback to default level if not found
            _logger.LogInformation($"Using default level for floor ID: {floor.Id}");
            return level;
        }

        /// <summary>
        /// Trims a curve with the given face solid and returns valid segments.
        /// </summary>
        private List<Curve> TrimCurveWithFace(Curve gridLine, Face face)
        {
            List<Curve> trimmedCurves = new List<Curve>();

            // Create a thin solid from the face
            List<CurveLoop> faceLoops = face.GetEdgesAsCurveLoops().ToList();
            Solid faceSolid = GeometryCreationUtilities.CreateExtrusionGeometry(faceLoops, face.ComputeNormal(new UV()), 0.1);

            // Intersect the grid line with the solid
            SolidCurveIntersectionOptions options = new SolidCurveIntersectionOptions();
            SolidCurveIntersection intersection = faceSolid.IntersectWithCurve(gridLine, options);

            if (intersection == null || intersection.SegmentCount == 0)
            {
                _logger.LogInformation("No intersection between grid line and face solid.");
                return trimmedCurves;
            }

            for (int i = 0; i < intersection.SegmentCount; i++)
            {
                Curve trimmedCurve = intersection.GetCurveSegment(i);
                if (trimmedCurve != null && trimmedCurve.Length > MinBeamLength)
                {
                    trimmedCurves.Add(trimmedCurve);
                }
            }

            return trimmedCurves;
        }

        /// <summary>
        /// Creates a framing grid by placing Beams and joists based on the flat face.
        /// </summary>
        private void CreateFramingGrid(Document doc, Face bestFlatFace, Floor floor, Level level)
        {
            if (bestFlatFace == null)
            {
                _logger.LogError("No flat surface found for framing.", new ArgumentNullException(nameof(bestFlatFace)));
                return;
            }

            using (Transaction tx = new Transaction(doc, $"Create Framing Grid for Floor {floor.Id}"))
            {
                tx.Start();

                List<ElementId> gridElements = new List<ElementId>();
                BoundingBoxUV faceBounds = bestFlatFace.GetBoundingBox();
                UV min = faceBounds.Min;
                UV max = faceBounds.Max;

                _logger.LogInformation($"Face bounds - U: [{min.U}, {max.U}], V: [{min.V}, {max.V}]");

                foreach (var config in Model.GridConfigs)
            {
                // Spacing is already in feet (Revit internal units)
                double spacingInFeet = config.Spacing;

                _logger.LogInformation($"Processing {config.Orientation} grid with spacing {spacingInFeet} ft");
                _logger.LogInformation($"U range: {max.U - min.U}, V range: {max.V - min.V}");

                if (config.Orientation == GridOrientation.Horizontal)
                {
                    // Horizontal grid lines (Beams)
                    _logger.LogInformation($"Creating horizontal beams with spacing={spacingInFeet}ft");
                    int beamCount = 0;
                    int iterationCount = 0;
                    int maxIterations = 1000; // Safety limit

                    for (double u = min.U; u <= max.U && iterationCount < maxIterations; u += spacingInFeet, iterationCount++)
                    {
                        beamCount++;
                        _logger.LogInformation($"Beam iteration {beamCount}: u={u}");

                        XYZ startPoint = bestFlatFace.Evaluate(new UV(u, min.V - 10)); // Extend beyond min.V
                        XYZ endPoint = bestFlatFace.Evaluate(new UV(u, max.V + 10));   // Extend beyond max.V

                        _logger.LogInformation($"Beam line: ({startPoint.X}, {startPoint.Y}, {startPoint.Z}) to ({endPoint.X}, {endPoint.Y}, {endPoint.Z})");

                        Line gridLine = Line.CreateBound(startPoint, endPoint);
                        List<Curve> trimmedCurves = TrimCurveWithFace(gridLine, bestFlatFace);

                        _logger.LogInformation($"Trimmed curves count: {trimmedCurves.Count}");

                        foreach (Curve trimmedCurve in trimmedCurves)
                        {
                            _logger.LogInformation($"Trimmed curve length: {trimmedCurve.Length} ft (min: {MinBeamLength} ft)");

                            if (trimmedCurve.Length > MinBeamLength)
                            {
                                // Apply Z_Offset (convert from mm to feet)
                                double zOffsetFeet = config.ZOffset / 304.8;
                                XYZ translation = new XYZ(0, 0, zOffsetFeet);
                                Curve offsetCurve = trimmedCurve.CreateTransformed(Transform.CreateTranslation(translation));

                                // Place the beam
                                _logger.LogInformation($"Creating beam with symbol: {config.FamilySymbol.Name}, level: {level.Name}, Z-offset: {config.ZOffset}mm ({zOffsetFeet}ft), structural type: {config.StructuralType}");
                                FamilyInstance beamInstance = doc.Create.NewFamilyInstance(offsetCurve, config.FamilySymbol, level, config.StructuralType);
                                if (beamInstance != null)
                                {
                                    gridElements.Add(beamInstance.Id);
                                    _allGeneratedBeamIds.Add(beamInstance.Id); // Track for grouping
                                    _logger.LogInformation($"✓ Successfully placed {config.Orientation} beam ID: {beamInstance.Id}");

                                    // Track beam for summary
                                    string key = $"Horizontal|{config.FamilySymbol.FamilyName}|{config.FamilySymbol.Name}";
                                    if (!_generatedBeams.ContainsKey(key))
                                    {
                                        _generatedBeams[key] = new List<double>();
                                    }
                                    _generatedBeams[key].Add(offsetCurve.Length);
                                }
                                else
                                {
                                    _logger.LogError("Failed to create beam - NewFamilyInstance returned null", null);
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"Skipping beam - too short ({trimmedCurve.Length} < {MinBeamLength})");
                            }
                        }
                    }
                    _logger.LogInformation($"Created {beamCount} horizontal beams");
                }
                else if (config.Orientation == GridOrientation.Vertical)
                {
                    // Vertical grid lines (joists)
                    for (double v = min.V; v <= max.V; v += config.Spacing)
                    {
                        XYZ startPoint = bestFlatFace.Evaluate(new UV(min.U - 10, v));
                        XYZ endPoint = bestFlatFace.Evaluate(new UV(max.U + 10, v));

                        Line gridLine = Line.CreateBound(startPoint, endPoint);
                        List<Curve> trimmedCurves = TrimCurveWithFace(gridLine, bestFlatFace);

                        foreach (Curve trimmedCurve in trimmedCurves)
                        {
                            if (trimmedCurve.Length > MinBeamLength)
                            {
                                // Apply Z_Offset (convert from mm to feet)
                                double zOffsetFeet = config.ZOffset / 304.8;
                                XYZ translation = new XYZ(0, 0, zOffsetFeet);
                                Curve offsetCurve = trimmedCurve.CreateTransformed(Transform.CreateTranslation(translation));

                                _logger.LogInformation($"Creating joist with symbol: {config.FamilySymbol.Name}, level: {level.Name}, Z-offset: {config.ZOffset}mm ({zOffsetFeet}ft)");

                                // Place the joist
                                FamilyInstance joistInstance = doc.Create.NewFamilyInstance(offsetCurve, config.FamilySymbol, level, config.StructuralType);
                                if (joistInstance != null)
                                {
                                    gridElements.Add(joistInstance.Id);
                                    _allGeneratedBeamIds.Add(joistInstance.Id); // Track for grouping
                                    _logger.LogInformation($"✓ Successfully placed {config.Orientation} joist ID: {joistInstance.Id}");

                                    // Track joist for summary
                                    string key = $"Vertical|{config.FamilySymbol.FamilyName}|{config.FamilySymbol.Name}";
                                    if (!_generatedBeams.ContainsKey(key))
                                    {
                                        _generatedBeams[key] = new List<double>();
                                    }
                                    _generatedBeams[key].Add(offsetCurve.Length);
                                }
                                else
                                {
                                    _logger.LogError("Failed to create joist - NewFamilyInstance returned null", null);
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"Skipping joist - too short ({trimmedCurve.Length} < {MinBeamLength})");
                            }
                        }
                    }
                }
            }

                _logger.LogInformation($"Created {gridElements.Count} grid elements for floor {floor.Id}");

                tx.Commit();

                // Optionally, create assemblies or perform further operations
                // Example: CreateAssembly(doc, gridElements, BuiltInCategory.OST_StructuralFraming);
            }
        }

        /// <summary>
        /// Retrieves the best flat surface (largest planar face) from the floor.
        /// </summary>
        /// <param name="floor">The floor element.</param>
        /// <returns>The best flat face or null if none found.</returns>
        private Face GetBestFlatSurface(Floor floor)
        {
            Options geomOptions = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geomElement = floor.get_Geometry(geomOptions);
            Face bestFlatFace = null;
            double largestArea = 0;

            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            double area = planarFace.Area;
                            if (area > largestArea)
                            {
                                largestArea = area;
                                bestFlatFace = planarFace;
                            }
                        }
                    }
                }
            }

            if (bestFlatFace != null)
            {
                _logger.LogInformation($"Best flat surface found with area: {largestArea} for floor ID: {floor.Id}");
            }
            else
            {
                _logger.LogInformation($"No flat surfaces found on the floor ID: {floor.Id}");
            }

            return bestFlatFace;
        }

        /// <summary>
        /// Creates an assembly from the provided elements.
        /// </summary>
        private void CreateAssembly(Document doc, List<ElementId> elementIds, BuiltInCategory category)
        {
            if (elementIds == null || elementIds.Count == 0)
            {
                _logger.LogInformation("No elements provided for assembly creation.");
                return;
            }

            using (Transaction tx = new Transaction(doc, "Create Assembly"))
            {
                tx.Start();

                // Create the assembly
                AssemblyInstance assembly = AssemblyInstance.Create(doc, elementIds, new ElementId(category));

                tx.Commit();
            }

            _logger.LogInformation($"Assembly created with {elementIds.Count} elements.");
        }
    }
}
