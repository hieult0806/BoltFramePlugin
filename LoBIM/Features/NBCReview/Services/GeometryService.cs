using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Services;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for geometric calculations - polygon checks, ray casting, etc.
    /// </summary>
    public class GeometryService : IGeometryService
    {
        private readonly ILoggingService _logger;

        public GeometryService(ILoggingService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        /// <summary>
        /// Checks if a point is inside a polygon using ray casting algorithm
        /// </summary>
        public bool IsPointInsidePolygon(XYZ point, List<XYZ> polygon)
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

        /// <summary>
        /// Gets the boundary curve loop from a property line element
        /// </summary>
        /// <param name="linkTransform">Optional transform for property lines from linked files</param>
        public CurveLoop GetPropertyLineBoundary(Element propertyLine, Transform? linkTransform = null)
        {
            if (propertyLine == null)
                throw new ArgumentNullException(nameof(propertyLine));

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
                {
                    _logger.LogError("Property line has no geometry");
                    return null;
                }

                var curves = new List<Curve>();

                foreach (var geomObj in geometryElement)
                {
                    if (geomObj is Curve curve)
                    {
                        // Apply transform if this is from a linked file
                        var transformedCurve = linkTransform != null ? curve.CreateTransformed(linkTransform) : curve;
                        curves.Add(transformedCurve);
                    }
                    else if (geomObj is GeometryInstance geomInstance)
                    {
                        var instanceGeometry = geomInstance.GetInstanceGeometry();
                        foreach (var instObj in instanceGeometry)
                        {
                            if (instObj is Curve instCurve)
                            {
                                // Apply transform if this is from a linked file
                                var transformedCurve = linkTransform != null ? instCurve.CreateTransformed(linkTransform) : instCurve;
                                curves.Add(transformedCurve);
                            }
                        }
                    }
                }

                _logger.LogInformation($"Extracted {curves.Count} curves from property line{(linkTransform != null ? " (linked)" : "")}");

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

                _logger.LogError("Could not create curve loop from property line");
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
                    _logger.LogWarning($"Could not find contiguous curve. {remainingCurves.Count} curves remaining.");

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
    }
}
