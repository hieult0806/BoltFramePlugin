using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB;
using BoltFramePlugin.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using System.Windows.Controls;
using System.Drawing.Design;

namespace BoltFramePlugin.Features.Framing.Strategies
{
    public class FloorFrameGenerateModel : IFrameGenerateModel
    {
        public Element TargetElement { get; set; }
        public Level Level { get; set; }
        public FamilySymbol BeamSymbol { get; set; }
        public FamilySymbol ColSymbol { get; set; }
        public double Spacing { get; set; }
        public double Z_Offset { get; set; }

        public double JoistSpacing { get; set; }
        public double BeamSpacing { get; set; }
    }
    public class FloorFramingStrategy : IFramingStrategy
    {
        public IRevitService RevitService { get; set; }
        public FloorFrameGenerateModel Model { get; set; }
        IFrameGenerateModel IFramingStrategy.Model { get => Model; set => Model = (FloorFrameGenerateModel)value; }

        public FloorFramingStrategy(IRevitService revitService, IFrameGenerateModel model)
        {
            RevitService = revitService;

            if (model is FloorFrameGenerateModel floorModel)
            {
                Model = floorModel;
            }
            else
            {
                throw new ArgumentException("Invalid model type for FloorFramingStrategy.");
            }
        }

        public void StartGenerate()
        {
            try
            {
                Document doc = (RevitService as RevitService).Document;
                FamilySymbol beamSymbol = Model.BeamSymbol;
                FamilySymbol colSymbol = Model.ColSymbol;
                Level level = Model.Level;
                Floor floor = Model.TargetElement as Floor;

                // Get the best flat surface
                Face bestFlatFace = GetBestFlatSurface(floor);
                // Define the offset distance
                double offsetDistance = 0.15;

                if (bestFlatFace != null)
                {
                    using (Transaction tx = new Transaction(floor.Document, "Frame Flat Surface"))
                    {
                        tx.Start();
                        if (!beamSymbol.IsActive)
                        {
                            doc.Regenerate();
                            beamSymbol.Activate();
                            doc.Regenerate();
                        }
                        // Loop through the edges of the best flat surface
                        EdgeArray edgeArray = bestFlatFace.EdgeLoops.get_Item(0);

                        foreach (Edge edge in edgeArray)
                        {
                            // Get the curve of the edge
                            Curve edgeCurve = edge.AsCurve();
                            // Place a beam along the edge
                            doc.Create.NewFamilyInstance(edgeCurve, beamSymbol, level, StructuralType.Beam);

                            // Compute the offset curve
                            Curve offsetCurve = CreateOffsetCurve(edgeCurve, bestFlatFace, offsetDistance);
                            doc.Create.NewFamilyInstance(offsetCurve, beamSymbol, level, StructuralType.Beam);
                        }

                        tx.Commit();
                        CreateFramingGrid(bestFlatFace, floor, beamSymbol, level);
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("StartGenerate", ex.Message);
            }
        }

        private Curve CreateOffsetCurve(Curve edgeCurve, Face face, double offsetDistance)
        {
            // Get the face normal (assuming planar face)
            XYZ faceNormal = face.ComputeNormal(new UV(0, 0)); // Normal of the face

            // For line curves
            if (edgeCurve is Line line)
            {
                // Get the direction of the line
                XYZ lineDirection = line.Direction;

                // Compute the offset direction (perpendicular to edge and in plane of face)
                XYZ offsetDirection = faceNormal.CrossProduct(lineDirection).Normalize();

                // Offset the start and end points
                XYZ offsetStartPoint = line.GetEndPoint(0) + offsetDirection * offsetDistance;
                XYZ offsetEndPoint = line.GetEndPoint(1) + offsetDirection * offsetDistance;

                // Create the offset line
                return Line.CreateBound(offsetStartPoint, offsetEndPoint);
            }
            else
            {
                // For other types of curves (e.g., arcs)
                // Offset the curve using the method below
                return OffsetCurveInPlane(edgeCurve, face, offsetDistance);
            }
        }

        private Curve OffsetCurveInPlane(Curve curve, Face face, double offsetDistance)
        {
            // Get a set of points along the curve
            IList<XYZ> pointsOnCurve = curve.Tessellate();

            List<XYZ> offsetPoints = new List<XYZ>();

            // Compute the face normal (assuming planar face)
            XYZ faceNormal = face.ComputeNormal(new UV(0, 0));

            for (int i = 0; i < pointsOnCurve.Count; i++)
            {
                // Approximate the tangent at this point
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

                // Compute the offset direction
                XYZ offsetDirection = faceNormal.CrossProduct(tangent).Normalize();

                // Offset the point
                XYZ offsetPoint = pointsOnCurve[i] + offsetDirection * offsetDistance;

                offsetPoints.Add(offsetPoint);
            }

            // Create a Hermite spline from the offset points
            if (offsetPoints.Count >= 2)
            {
                return HermiteSpline.Create(offsetPoints, false);
            }
            else
            {
                return null;
            }
        }

        private const double MIN_BEAM_LENGTH = 0.3; // Minimum allowable beam length (~1 foot)

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
                // For debugging
                //TaskDialog.Show("Debug", "No intersection between grid line and face solid.");
                return trimmedCurves;
            }

            for (int i = 0; i < intersection.SegmentCount; i++)
            {
                Curve trimmedCurve = intersection.GetCurveSegment(i);
                if (trimmedCurve.Length > MIN_BEAM_LENGTH)
                {
                    trimmedCurves.Add(trimmedCurve);
                }
            }

            return trimmedCurves;
        }

        public void CreateFramingGrid(Face bestFlatFace, Floor floor, FamilySymbol beamSymbol, Level level)
        {
            if (bestFlatFace == null)
            {
                TaskDialog.Show("Error", "No flat surface found for framing.");
                return;
            }

            List<ElementId> elements = new List<ElementId>();

            BoundingBoxUV faceBounds = bestFlatFace.GetBoundingBox();
            UV min = faceBounds.Min;
            UV max = faceBounds.Max;

            // Get the edge loops of the face
            EdgeArrayArray edgeLoops = bestFlatFace.EdgeLoops;

            using (Transaction tx = new Transaction(floor.Document, "Create Framing Grid"))
            {
                tx.Start();
                if (!beamSymbol.IsActive)
                {
                    beamSymbol.Activate();
                    (RevitService as RevitService).Document.Regenerate();
                }
                // Horizontal grid lines (along U-axis)
                for (double u = min.U; u <= max.U; u += Model.BeamSpacing)
                {
                    // Create a line that extends beyond the face boundary
                    XYZ startPoint = bestFlatFace.Evaluate(new UV(u, min.V - 10)); // Extend beyond min.V
                    XYZ endPoint = bestFlatFace.Evaluate(new UV(u, max.V + 10));   // Extend beyond max.V

                    Line gridLine = Line.CreateBound(startPoint, endPoint);

                    // Trim the line to fit within the face boundary
                    List<Curve> trimmedCurves = TrimCurveWithFace(gridLine, bestFlatFace);

                    foreach (Curve trimmedCurve in trimmedCurves)
                    {
                        if (trimmedCurve.Length > MIN_BEAM_LENGTH)
                        {
                            var e = floor.Document.Create.NewFamilyInstance(trimmedCurve, beamSymbol, level, StructuralType.Beam);
                            elements.Add(e.Id);
                        }
                    }
                }

                // Vertical grid lines (along V-axis)
                for (double v = min.V; v <= max.V; v += Model.JoistSpacing)
                {
                    // Create a line that extends beyond the face boundary
                    XYZ startPoint = bestFlatFace.Evaluate(new UV(min.U - 10, v));
                    XYZ endPoint = bestFlatFace.Evaluate(new UV(max.U + 10, v));

                    Line gridLine = Line.CreateBound(startPoint, endPoint);

                    // Trim the line to fit within the face boundary
                    List<Curve> trimmedCurves = TrimCurveWithFace(gridLine, bestFlatFace);

                    foreach (Curve trimmedCurve in trimmedCurves)
                    {
                        if (trimmedCurve.Length > MIN_BEAM_LENGTH)
                        {
                            // Apply Z_Offset
                            XYZ translation = new XYZ(0, 0, Model.Z_Offset);
                            Curve offsetCurve = trimmedCurve.CreateTransformed(Transform.CreateTranslation(translation));

                            var e = floor.Document.Create.NewFamilyInstance(offsetCurve, beamSymbol, level, StructuralType.Beam);
                            elements.Add(e.Id);
                        }
                    }
                }
                tx.Commit();
            }

            CreateAssembly(floor.Document, elements, BuiltInCategory.OST_StructuralFraming);

        }

        public Face GetBestFlatSurface(Floor floor)
        {
            // Get the geometry options
            Options geomOptions = new Options();
            geomOptions.ComputeReferences = true;
            geomOptions.IncludeNonVisibleObjects = false;

            // Get the floor's geometry element
            GeometryElement geomElement = floor.get_Geometry(geomOptions);

            // Initialize variables to track the best flat face
            Face bestFlatFace = null;
            double largestArea = 0;

            // Loop through the geometry and find the largest flat surface
            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        // Check if the face is planar
                        PlanarFace planarFace = face as PlanarFace;
                        if (planarFace != null)
                        {
                            // Get the area of the planar face
                            double area = planarFace.Area;

                            // If the area is larger than the current largest area, update the best face
                            if (area > largestArea)
                            {
                                largestArea = area;
                                bestFlatFace = planarFace;
                            }
                        }
                    }
                }
            }

            //if (bestFlatFace != null)
            //{
            //    TaskDialog.Show("Flat Surface Found", $"Best flat surface area: {largestArea}");
            //}
            //else
            //{
            //    TaskDialog.Show("Error", "No flat surfaces found on the floor.");
            //}

            return bestFlatFace;
        }

        public void CreateAssembly(Document doc, List<ElementId> elementIds, BuiltInCategory category)
        {
            using (Transaction tx = new Transaction(doc, "Create Assembly"))
            {
                tx.Start();

                // Create the assembly
                AssemblyInstance assembly = AssemblyInstance.Create(doc, elementIds, new ElementId(category));

                tx.Commit();
            }
        }
    }
}
