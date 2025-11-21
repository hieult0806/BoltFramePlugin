using System.Collections.Generic;
using Autodesk.Revit.DB;
using LoBIM.Features.NBCReview.Models;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for analyzing walls - distance calculations, area calculations, opening analysis
    /// </summary>
    public interface IWallAnalysisService
    {
        /// <summary>
        /// Finds all perimeter walls inside the property line boundary
        /// </summary>
        /// <param name="propertyLineTransform">Optional transform for property lines from linked files</param>
        List<WallInfo> FindPerimeterWalls(Document doc, Element propertyLine, double rayLengthLimit, Transform? propertyLineTransform = null);

        /// <summary>
        /// Checks if a wall is inside the property line boundary
        /// </summary>
        /// <param name="linkTransform">Optional transform for walls from linked files</param>
        bool IsWallInsidePropertyLine(Wall wall, CurveLoop propertyLineBoundary, Transform? linkTransform = null);

        /// <summary>
        /// Calculates the gross area of a wall
        /// </summary>
        double CalculateWallGrossArea(Wall wall);

        /// <summary>
        /// Calculates the total area of openings in a wall (doors, windows)
        /// </summary>
        double CalculateWallOpeningsArea(Wall wall);

        /// <summary>
        /// Calculates distance groups from walls with regions based on distance ranges
        /// </summary>
        List<DistanceGroupSummary> CalculateDistanceGroupsFromRegions(
            List<WallInfo> wallsWithRegions,
            List<FilledRegionTypeDefinition> distanceRanges);
    }
}
