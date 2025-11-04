using System;
using System.IO;
using System.Text.Json;
using BoltFramePlugin.Features.LimitingDistance.Models;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.Features.LimitingDistance.Services
{
    /// <summary>
    /// Service for loading and accessing NBC (National Building Code) configuration
    /// Loads from Resources/Config/NBCRequirements.json
    /// </summary>
    public class NBCConfigurationService : INBCConfigurationService
    {
        private readonly ILoggingService _logger;
        private NBCConfiguration _configuration;
        private const string ConfigFileName = "NBCRequirements.json";

        public NBCConfiguration Configuration => _configuration;

        public NBCConfigurationService(ILoggingService logger)
        {
            _logger = logger;
            _configuration = LoadConfiguration();
        }

        /// <summary>
        /// Load NBC configuration from embedded resource
        /// </summary>
        private NBCConfiguration LoadConfiguration()
        {
            try
            {
                // Try to load from Resources/Config folder relative to the assembly
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                var configPath = Path.Combine(assemblyDirectory, "Resources", "Config", ConfigFileName);

                if (!File.Exists(configPath))
                {
                    _logger.LogError($"NBC configuration file not found at: {configPath}");
                    return CreateDefaultConfiguration();
                }

                var jsonContent = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var config = JsonSerializer.Deserialize<NBCConfiguration>(jsonContent, options);

                if (config == null)
                {
                    _logger.LogError("Failed to deserialize NBC configuration, using default");
                    return CreateDefaultConfiguration();
                }

                _logger.LogInformation($"Loaded NBC configuration: {config.CodeReference} v{config.Version}");
                _logger.LogInformation($"  - {config.DistanceRanges.Count} distance ranges");
                _logger.LogInformation($"  - {config.BuildingClassifications.Count} building classifications");

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading NBC configuration: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Create a minimal default configuration if file loading fails
        /// </summary>
        private NBCConfiguration CreateDefaultConfiguration()
        {
            _logger.LogWarning("Creating default NBC configuration");

            var config = new NBCConfiguration
            {
                CodeReference = "NBC 2020 (Default)",
                Version = "1.0",
                Description = "Default fallback configuration"
            };

            // Add basic distance ranges
            config.DistanceRanges.Add(new DistanceRange
            {
                RangeId = "0-1.2m",
                MinMeters = 0.0,
                MaxMeters = 1.2,
                MinFeet = 0.0,
                MaxFeet = 3.937,
                Label = "0-1.2m (0-3.9ft)",
                ColorIndex = 0,
                ColorRGB = new RGBColor { R = 0, G = 255, B = 0 }
            });

            // Add Group D (Residential) as default
            var groupD = new BuildingClassificationGroup
            {
                Name = "Group D - Residential",
                Description = "Default residential classification"
            };

            groupD.Requirements.Add(new NBCRequirement
            {
                LimitingDistanceMeters = 1.2,
                MaxUnprotectedOpeningPercent = 0,
                FireResistanceRating = "45min",
                ConstructionType = "Combustible",
                CladdingType = "Combustible"
            });

            groupD.Requirements.Add(new NBCRequirement
            {
                LimitingDistanceMeters = 2.0,
                MaxUnprotectedOpeningPercent = 10,
                FireResistanceRating = "None",
                ConstructionType = "Combustible",
                CladdingType = "Combustible"
            });

            config.BuildingClassifications.Add("GroupD", groupD);

            return config;
        }

        public NBCRequirement? GetRequirement(string classificationKey, double limitingDistanceMeters)
        {
            return _configuration.GetApplicableRequirement(classificationKey, limitingDistanceMeters);
        }

        public DistanceRange? GetDistanceRange(double distanceMeters)
        {
            return _configuration.GetDistanceRangeByMeters(distanceMeters);
        }

        public (byte Red, byte Green, byte Blue) GetColorForDistance(double distanceMeters)
        {
            var range = GetDistanceRange(distanceMeters);
            if (range != null)
            {
                return range.ColorRGB.ToTuple();
            }

            // Default to red if no range found
            return (255, 0, 0);
        }

        public BuildingClassificationGroup? GetClassificationGroup(string classificationKey)
        {
            return _configuration.GetClassification(classificationKey);
        }

        public void ReloadConfiguration()
        {
            _logger.LogInformation("Reloading NBC configuration");
            _configuration = LoadConfiguration();
        }
    }
}
