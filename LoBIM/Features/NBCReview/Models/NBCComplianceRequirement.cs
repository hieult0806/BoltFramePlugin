using System.Collections.Generic;
using System.Linq;

namespace LoBIM.Features.NBCReview.Models
{
    /// <summary>
    /// EBF = Exposing Building Face
    /// Details for a specific limiting distance requirement
    /// </summary>
    public class EBFDetails
    {
        /// <summary>
        /// Limiting Distance in meters (e.g., 3.5, 4.5, 6, 7, etc.)
        /// </summary>
        public double LimitingDistanceM { get; set; }

        /// <summary>
        /// Area of building exposing face in square meters (sqm)
        /// </summary>
        public int AreaOfBuildingExposingFaceSqm { get; set; }

        /// <summary>
        /// Maximum Allowance by NBC in percentage (%)
        /// </summary>
        public string MaximumAllowanceByNBC { get; set; } = string.Empty;

        /// <summary>
        /// Proposed opening in percentage (%)
        /// </summary>
        public string ProposedOpening { get; set; } = string.Empty;

        /// <summary>
        /// FRR (Fire Resistance Rating) - e.g., "1H", "45m", "2H"
        /// </summary>
        public string FRR { get; set; } = string.Empty;

        /// <summary>
        /// Type of Construction Required
        /// </summary>
        public string TypeOfConstructionRequired { get; set; } = string.Empty;

        /// <summary>
        /// Type of Cladding Required
        /// </summary>
        public string TypeOfCladdingRequired { get; set; } = string.Empty;

        /// <summary>
        /// Whether this requirement is met/compliant based on actual measurements
        /// </summary>
        public bool IsCompliant { get; set; }

        /// <summary>
        /// Additional notes or comments
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Building classification group (A, C, D, E)
    /// Contains multiple EBF details for different limiting distances
    /// </summary>
    public class BuildingClassification
    {
        /// <summary>
        /// Classification group name (e.g., "A", "C", "D", "E")
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// List of EBF details for this classification
        /// Each entry represents different limiting distance requirements
        /// </summary>
        public List<EBFDetails> EBFDetailsList { get; set; } = new List<EBFDetails>();

        /// <summary>
        /// Find EBF details for a specific limiting distance
        /// </summary>
        public EBFDetails? FindByLimitingDistance(double limitingDistance)
        {
            return EBFDetailsList.FirstOrDefault(ebf =>
                Math.Abs(ebf.LimitingDistanceM - limitingDistance) < 0.01);
        }

        /// <summary>
        /// Find the applicable EBF details for a measured limiting distance
        /// Returns the requirement with the closest limiting distance that doesn't exceed the measured value
        /// </summary>
        public EBFDetails? GetApplicableRequirement(double measuredLimitingDistance)
        {
            return EBFDetailsList
                .Where(ebf => ebf.LimitingDistanceM <= measuredLimitingDistance)
                .OrderByDescending(ebf => ebf.LimitingDistanceM)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Orientation group (1, 2, 3, etc.)
    /// Contains multiple building classifications
    /// </summary>
    public class Orientation
    {
        /// <summary>
        /// Orientation ID/number (1, 2, 3, etc.)
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// List of building classifications for this orientation
        /// </summary>
        public List<BuildingClassification> Classifications { get; set; } = new List<BuildingClassification>();

        /// <summary>
        /// Find a classification by group name
        /// </summary>
        public BuildingClassification? FindClassification(string groupName)
        {
            return Classifications.FirstOrDefault(c =>
                c.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get applicable EBF details for a specific classification and measured distance
        /// </summary>
        public EBFDetails? GetApplicableRequirement(string classificationName, double measuredLimitingDistance)
        {
            var classification = FindClassification(classificationName);
            return classification?.GetApplicableRequirement(measuredLimitingDistance);
        }
    }

    /// <summary>
    /// Root collection of NBC compliance requirements organized by orientation
    /// Represents the complete LimitingDistance_Temple.xlsx structure
    /// </summary>
    public class NBCComplianceTable
    {
        /// <summary>
        /// List of all orientations (1, 2, 3, etc.)
        /// Each orientation contains multiple building classifications
        /// </summary>
        public List<Orientation> Orientations { get; set; } = [];

        /// <summary>
        /// Template version or NBC code reference
        /// </summary>
        public string CodeReference { get; set; } = "NBC 2020";

        /// <summary>
        /// Project-specific information
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// Date when the compliance check was performed
        /// </summary>
        public System.DateTime CheckDate { get; set; } = System.DateTime.Now;

        /// <summary>
        /// Find an orientation by ID
        /// </summary>
        public Orientation? FindOrientation(int orientationId)
        {
            return Orientations.FirstOrDefault(o => o.ID == orientationId);
        }

        /// <summary>
        /// Get applicable EBF details for a specific orientation, classification, and measured distance
        /// </summary>
        public EBFDetails? GetApplicableRequirement(
            int orientationId,
            string classificationName,
            double measuredLimitingDistance)
        {
            var orientation = FindOrientation(orientationId);
            return orientation?.GetApplicableRequirement(classificationName, measuredLimitingDistance);
        }

        /// <summary>
        /// Get all EBF details across all orientations and classifications
        /// Useful for populating UI or generating reports
        /// </summary>
        public List<(int OrientationId, string Classification, EBFDetails Details)> GetAllEBFDetails()
        {
            var allDetails = new List<(int, string, EBFDetails)>();

            foreach (var orientation in Orientations)
            {
                foreach (var classification in orientation.Classifications)
                {
                    foreach (var ebf in classification.EBFDetailsList)
                    {
                        allDetails.Add((orientation.ID, classification.GroupName, ebf));
                    }
                }
            }

            return allDetails;
        }
    }

    /// <summary>
    /// Service for loading NBC compliance requirements from Excel template
    /// </summary>
    public class NBCComplianceTemplateLoader
    {
        private readonly string _templatePath;

        public NBCComplianceTemplateLoader(string templatePath)
        {
            _templatePath = templatePath;
        }

        /// <summary>
        /// Load compliance requirements from the Excel template
        /// This will read the LimitingDistance_Temple.xlsx file
        /// </summary>
        public NBCComplianceTable LoadFromTemplate()
        {
            // TODO: Implement using ExcelImportService
            // This will be implemented next
            throw new System.NotImplementedException();
        }
    }
}
