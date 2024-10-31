using Autodesk.Revit.DB;
using BoltFramePlugin.Services;
using Serilog.Core;
namespace BoltFramePlugin.FramingStrategies
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
                Document doc = _revitService.UiDoc.Document;
                IEnumerable<Element> floors = Model.TargetElements;

                if (floors == null || !floors.Any())
                {
                    _logger.LogInformation("No target floor elements provided for framing.");
                    return;
                }

                // Activate all family symbols once if not active
                using (Transaction tx = new Transaction(doc, "Activate Family Symbols"))
                {
                    tx.Start();
                    foreach (var config in Model.GridConfigs)
                    {
                        if (config.FamilySymbol != null && !config.FamilySymbol.IsActive)
                        {
                            config.FamilySymbol.Activate();
                            doc.Regenerate();
                        }
                    }
                    tx.Commit();
                }

                // Iterate over each floor and apply framing
                foreach (Element floorElement in floors)
                {
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
                        ApplyFramingToFloor(doc, floor, bestFlatFace, offsetDistance);
                    }
                    else
                    {
                        _logger.LogInformation($"No suitable flat surface found for floor ID: {floor.Id}");
                    }
                }

                _logger.LogInformation("Framing generation completed for all floors.");
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred during multi-floor framing generation.", ex);
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

                using (Transaction tx = new Transaction(doc, $"Frame Floor ID: {floor.Id}"))
                {
                    tx.Start();

                    EdgeArray edgeArray = bestFlatFace.EdgeLoops.get_Item(0);

                    foreach (Edge edge in edgeArray)
                    {
                        Curve edgeCurve = edge.AsCurve();

                        foreach (var config in Model.GridConfigs)
                        {
                            // Place a grid element along the edge based on its configuration
                            NewFamilyInstance(doc, edgeCurve, config.FamilySymbol, level, config);

                            // Compute the offset curve if needed
                            Curve offsetCurve = CreateOffsetCurve(edgeCurve, bestFlatFace, offsetDistance, config);
                            if (offsetCurve != null)
                            {
                                NewFamilyInstance(doc, offsetCurve, config.FamilySymbol, level, config);
                            }
                        }
                    }

                    tx.Commit();
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
        private void NewFamilyInstance(Document doc, Curve curve, FamilySymbol symbol, Level level, GridConfig config)
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

            // Create the family instance
            FamilyInstance instance = doc.Create.NewFamilyInstance(curve, symbol, level, config.StructuralType);
            if (instance != null)
            {
                _logger.LogInformation($"Created {config.Orientation} grid element with ID: {instance.Id}");
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

            List<ElementId> gridElements = new List<ElementId>();
            BoundingBoxUV faceBounds = bestFlatFace.GetBoundingBox();
            UV min = faceBounds.Min;
            UV max = faceBounds.Max;

            foreach (var config in Model.GridConfigs)
            {
                if (config.Orientation == GridOrientation.Horizontal)
                {
                    // Horizontal grid lines (Beams)
                    for (double u = min.U; u <= max.U; u += config.Spacing)
                    {
                        XYZ startPoint = bestFlatFace.Evaluate(new UV(u, min.V - 10)); // Extend beyond min.V
                        XYZ endPoint = bestFlatFace.Evaluate(new UV(u, max.V + 10));   // Extend beyond max.V

                        Line gridLine = Line.CreateBound(startPoint, endPoint);
                        List<Curve> trimmedCurves = TrimCurveWithFace(gridLine, bestFlatFace);

                        foreach (Curve trimmedCurve in trimmedCurves)
                        {
                            if (trimmedCurve.Length > MinBeamLength)
                            {
                                // Place the beam
                                FamilyInstance beamInstance = doc.Create.NewFamilyInstance(trimmedCurve, config.FamilySymbol, level, config.StructuralType);
                                if (beamInstance != null)
                                {
                                    gridElements.Add(beamInstance.Id);
                                    _logger.LogInformation($"Placed {config.Orientation} grid element ID: {beamInstance.Id}");
                                }
                            }
                        }
                    }
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
                                // Apply Z_Offset
                                XYZ translation = new XYZ(0, 0, config.ZOffset);
                                Curve offsetCurve = trimmedCurve.CreateTransformed(Transform.CreateTranslation(translation));

                                // Place the joist
                                FamilyInstance joistInstance = doc.Create.NewFamilyInstance(offsetCurve, config.FamilySymbol, level, config.StructuralType);
                                if (joistInstance != null)
                                {
                                    gridElements.Add(joistInstance.Id);
                                    _logger.LogInformation($"Placed {config.Orientation} grid element ID: {joistInstance.Id}");
                                }
                            }
                        }
                    }
                }
            }

            // Optionally, create assemblies or perform further operations
            // Example: CreateAssembly(doc, gridElements, BuiltInCategory.OST_StructuralFraming);
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
