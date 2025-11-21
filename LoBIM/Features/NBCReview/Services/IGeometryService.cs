using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for geometric calculations - polygon checks, ray casting, etc.
    /// </summary>
    public interface IGeometryService
    {
        /// <summary>
        /// Checks if a point is inside a polygon using ray casting algorithm
        /// </summary>
        bool IsPointInsidePolygon(XYZ point, List<XYZ> polygon);

        /// <summary>
        /// Gets the boundary curve loop from a property line element
        /// </summary>
        /// <param name="linkTransform">Optional transform for property lines from linked files</param>
        CurveLoop GetPropertyLineBoundary(Element propertyLine, Transform? linkTransform = null);
    }
}
