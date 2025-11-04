using BoltFramePlugin.Features.LimitingDistance.Models;

namespace BoltFramePlugin.Features.LimitingDistance.Services
{
    /// <summary>
    /// Service for loading and accessing NBC (National Building Code) configuration
    /// </summary>
    public interface INBCConfigurationService
    {
        /// <summary>
        /// Get the loaded NBC configuration
        /// </summary>
        NBCConfiguration Configuration { get; }

        /// <summary>
        /// Get NBC requirements for a specific building classification and limiting distance
        /// </summary>
        /// <param name="classificationKey">Building classification (e.g., "GroupD", "GroupA")</param>
        /// <param name="limitingDistanceMeters">Actual limiting distance in meters</param>
        /// <returns>Applicable NBC requirement or null if not found</returns>
        NBCRequirement? GetRequirement(string classificationKey, double limitingDistanceMeters);

        /// <summary>
        /// Get distance range containing the specified distance
        /// </summary>
        /// <param name="distanceMeters">Distance in meters</param>
        /// <returns>Distance range or null if not found</returns>
        DistanceRange? GetDistanceRange(double distanceMeters);

        /// <summary>
        /// Get color for a specific distance range
        /// </summary>
        /// <param name="distanceMeters">Distance in meters</param>
        /// <returns>RGB color tuple</returns>
        (byte Red, byte Green, byte Blue) GetColorForDistance(double distanceMeters);

        /// <summary>
        /// Get building classification group
        /// </summary>
        /// <param name="classificationKey">Classification key (e.g., "GroupD")</param>
        /// <returns>Building classification group or null</returns>
        BuildingClassificationGroup? GetClassificationGroup(string classificationKey);

        /// <summary>
        /// Reload configuration from file
        /// </summary>
        void ReloadConfiguration();
    }
}
