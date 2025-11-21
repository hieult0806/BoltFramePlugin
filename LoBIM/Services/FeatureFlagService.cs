using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LoBIM.Models;

namespace LoBIM.Services
{
    /// <summary>
    /// Service for managing feature flags
    /// </summary>
    public interface IFeatureFlagService
    {
        /// <summary>
        /// Gets all enabled features
        /// </summary>
        List<FeatureFlagModel> GetEnabledFeatures();

        /// <summary>
        /// Checks if a specific feature is enabled
        /// </summary>
        bool IsFeatureEnabled(string featureName);

        /// <summary>
        /// Gets a specific feature configuration
        /// </summary>
        FeatureFlagModel? GetFeature(string featureName);
    }

    /// <summary>
    /// Implementation of feature flag service
    /// </summary>
    public class FeatureFlagService : IFeatureFlagService
    {
        private readonly ILoggingService _logger;
        private FeatureFlagsConfiguration _configuration;

        public FeatureFlagService(ILoggingService logger)
        {
            _logger = logger;
            _configuration = LoadConfiguration();
        }

        public List<FeatureFlagModel> GetEnabledFeatures()
        {
            return _configuration.Features
                .Where(f => f.Value.Enabled)
                .Select(f => f.Value)
                .ToList();
        }

        public bool IsFeatureEnabled(string featureName)
        {
            if (_configuration.Features.TryGetValue(featureName, out var feature))
            {
                return feature.Enabled;
            }
            return false;
        }

        public FeatureFlagModel? GetFeature(string featureName)
        {
            if (_configuration.Features.TryGetValue(featureName, out var feature))
            {
                return feature;
            }
            return null;
        }

        private FeatureFlagsConfiguration LoadConfiguration()
        {
            try
            {
                // Get the assembly location
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
                string configPath = Path.Combine(assemblyDirectory, "Resources", "Config", "FeatureFlags.json");

                _logger.LogInformation($"Loading feature flags from: {configPath}");

                if (!File.Exists(configPath))
                {
                    _logger.LogWarning($"Feature flags configuration not found at {configPath}. Using default configuration.");
                    return GetDefaultConfiguration();
                }

                string jsonContent = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<FeatureFlagsConfiguration>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null || config.Features == null || config.Features.Count == 0)
                {
                    _logger.LogWarning("Feature flags configuration is empty. Using default configuration.");
                    return GetDefaultConfiguration();
                }

                _logger.LogInformation($"Loaded {config.Features.Count} feature flags. Enabled: {config.Features.Count(f => f.Value.Enabled)}");
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading feature flags configuration: {ex.Message}", ex);
                return GetDefaultConfiguration();
            }
        }

        private FeatureFlagsConfiguration GetDefaultConfiguration()
        {
            // Return default configuration with all features enabled
            return new FeatureFlagsConfiguration
            {
                Features = new Dictionary<string, FeatureFlagModel>
                {
                    { "BoltFrame", new FeatureFlagModel { Enabled = true, Name = "Bolt Frame" } },
                    { "SwitchViewPanel", new FeatureFlagModel { Enabled = true, Name = "View Plans" } },
                    { "NBCReview", new FeatureFlagModel { Enabled = true, Name = "NBC Review" } },
                    { "DataImport", new FeatureFlagModel { Enabled = true, Name = "Import Table" } },
                    { "SheetManagement", new FeatureFlagModel { Enabled = true, Name = "Manage Sheets" } },
                    { "ViewCloning", new FeatureFlagModel { Enabled = true, Name = "Clone Views" } },
                    { "OpenLogs", new FeatureFlagModel { Enabled = true, Name = "Open Logs" } }
                }
            };
        }
    }
}
