using System.Collections.Generic;
using LoBIM.Features.NBCReview.Models;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for NBC (National Building Code) compliance checking and rating calculations
    /// </summary>
    public interface INBCComplianceService
    {
        /// <summary>
        /// Calculates building code compliance summary for each orientation based on distance groups
        /// </summary>
        List<BuildingCodeComplianceSummary> CalculateBuildingCodeComplianceByOrientation(
            List<DistanceGroupSummary> distanceGroups,
            string buildingClassification);

        /// <summary>
        /// Gets the maximum allowed openings percentage based on limiting distance
        /// </summary>
        double GetMaxAllowedOpeningsPercent(double limitingDistanceFt, string buildingClassification);

        /// <summary>
        /// Gets the required fire resistance rating based on limiting distance
        /// </summary>
        string GetFireResistanceRating(double limitingDistanceFt, string buildingClassification);

        /// <summary>
        /// Gets the required construction type based on limiting distance
        /// </summary>
        string GetConstructionTypeRequired(double limitingDistanceFt, string buildingClassification);

        /// <summary>
        /// Gets the required cladding type based on limiting distance
        /// </summary>
        string GetCladdingTypeRequired(double limitingDistanceFt, string buildingClassification);
    }
}
