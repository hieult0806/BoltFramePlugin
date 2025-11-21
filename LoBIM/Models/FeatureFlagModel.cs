using System.Collections.Generic;

namespace LoBIM.Models
{
    /// <summary>
    /// Represents a feature flag configuration
    /// </summary>
    public class FeatureFlagModel
    {
        /// <summary>
        /// Whether the feature is enabled and should appear in the ribbon
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Display name of the button in the ribbon
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Unique button identifier
        /// </summary>
        public string ButtonName { get; set; } = string.Empty;

        /// <summary>
        /// Full class name of the command
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// Short tooltip text
        /// </summary>
        public string Tooltip { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description
        /// </summary>
        public string LongDescription { get; set; } = string.Empty;

        /// <summary>
        /// Icon file name (without extension)
        /// </summary>
        public string? IconName { get; set; }
    }

    /// <summary>
    /// Root configuration for all feature flags
    /// </summary>
    public class FeatureFlagsConfiguration
    {
        /// <summary>
        /// Dictionary of feature flags keyed by feature name
        /// </summary>
        public Dictionary<string, FeatureFlagModel> Features { get; set; } = new Dictionary<string, FeatureFlagModel>();
    }
}
