using System;
using System.Collections.Generic;
using System.Linq;
using LoBIM.Features.NBCReview.Models;
using LoBIM.Services;

namespace LoBIM.Features.NBCReview.Services
{
    /// <summary>
    /// Service for NBC (National Building Code) compliance checking and rating calculations
    /// </summary>
    public class NBCComplianceService : INBCComplianceService
    {
        private readonly ILoggingService _logger;
        private readonly INBCConfigurationService _nbcConfig;

        public NBCComplianceService(ILoggingService logger, INBCConfigurationService nbcConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _nbcConfig = nbcConfig ?? throw new ArgumentNullException(nameof(nbcConfig));
        }

        public List<BuildingCodeComplianceSummary> CalculateBuildingCodeComplianceByOrientation(
            List<DistanceGroupSummary> distanceGroups,
            string buildingClassification)
        {
            var complianceSummaries = new List<BuildingCodeComplianceSummary>();

            if (distanceGroups == null || distanceGroups.Count == 0)
            {
                _logger.LogWarning("No distance groups available for compliance calculation");
                return complianceSummaries;
            }

            // Group by orientation and calculate compliance for each
            var groupedByOrientation = distanceGroups
                .GroupBy(g => g.Orientation)
                .OrderBy(g => g.Key);

            foreach (var orientationGroup in groupedByOrientation)
            {
                var orientation = orientationGroup.Key;

                // Calculate total area for this orientation
                var totalGrossArea = orientationGroup.Sum(g => g.TotalGrossArea);
                var totalOpeningsArea = orientationGroup.Sum(g => g.TotalOpeningsArea);

                // Get the minimum limiting distance for this orientation (most restrictive)
                var minLimitingDistance = orientationGroup.Min(g => g.MinDistance);

                // Calculate proposed unprotected openings percentage
                var proposedOpeningsPercent = totalGrossArea > 0
                    ? (totalOpeningsArea / totalGrossArea * 100)
                    : 0;

                // Determine maximum allowed openings based on limiting distance and building classification
                var maxAllowedPercent = GetMaxAllowedOpeningsPercent(minLimitingDistance, buildingClassification);
                var frr = GetFireResistanceRating(minLimitingDistance, buildingClassification);
                var constructionType = GetConstructionTypeRequired(minLimitingDistance, buildingClassification);
                var claddingType = GetCladdingTypeRequired(minLimitingDistance, buildingClassification);

                // Convert from ft² to m² (1 ft² = 0.092903 m²)
                var totalGrossAreaM2 = totalGrossArea * 0.092903;
                var limitingDistanceM = minLimitingDistance * 0.3048; // ft to m

                var compliance = new BuildingCodeComplianceSummary
                {
                    OccupancyClassification = buildingClassification,
                    Orientation = orientation,
                    ExposingBuildingFaceArea = totalGrossAreaM2,
                    LimitingDistance = limitingDistanceM,
                    MaxUnprotectedOpeningsPercent = maxAllowedPercent,
                    ProposedUnprotectedOpeningsPercent = proposedOpeningsPercent,
                    FireResistanceRating = frr,
                    ConstructionTypeRequired = constructionType,
                    CladdingTypeRequired = claddingType
                };

                complianceSummaries.Add(compliance);

                _logger.LogInformation($"  {orientation}: Area={totalGrossAreaM2:F1}m², LD={limitingDistanceM:F1}m, Max={maxAllowedPercent}%, Proposed={proposedOpeningsPercent:F1}%, Status={compliance.ComplianceStatus}");
            }

            _logger.LogInformation($"Building code compliance calculated: {complianceSummaries.Count} orientations");

            return complianceSummaries;
        }

        public double GetMaxAllowedOpeningsPercent(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048; // Convert ft to m

            // Default to Group D if classification not specified or not found
            string classificationKey = "GroupD";

            // Map building classification to NBC group key if needed
            if (!string.IsNullOrEmpty(buildingClassification))
            {
                // Try to map the classification string to a group key
                if (buildingClassification.Contains("A", StringComparison.OrdinalIgnoreCase))
                    classificationKey = "GroupA";
                else if (buildingClassification.Contains("C", StringComparison.OrdinalIgnoreCase))
                    classificationKey = "GroupC";
                else if (buildingClassification.Contains("D", StringComparison.OrdinalIgnoreCase))
                    classificationKey = "GroupD";
                else if (buildingClassification.Contains("E", StringComparison.OrdinalIgnoreCase))
                    classificationKey = "GroupE";
            }

            var requirement = _nbcConfig.GetRequirement(classificationKey, limitingDistanceM);

            if (requirement != null)
            {
                return requirement.MaxUnprotectedOpeningPercent;
            }

            // Fallback: no restriction if no requirement found
            _logger.LogWarning($"No NBC requirement found for {classificationKey} at {limitingDistanceM}m, returning 100%");
            return 100;
        }

        public string GetFireResistanceRating(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048;

            if (limitingDistanceM <= 2.0) return "1 h";
            if (limitingDistanceM <= 3.0) return "2 h";
            if (limitingDistanceM <= 5.0) return "1 h";
            return "45 min";
        }

        public string GetConstructionTypeRequired(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048;

            if (limitingDistanceM <= 2.0) return "Combustible / Noncombustible";
            return "Noncombustible";
        }

        public string GetCladdingTypeRequired(double limitingDistanceFt, string buildingClassification)
        {
            var limitingDistanceM = limitingDistanceFt * 0.3048;

            if (limitingDistanceM <= 2.0) return "Combustible / Noncombustible";
            if (limitingDistanceM <= 5.0) return "Noncombustible";
            return "Noncombustible";
        }
    }
}
