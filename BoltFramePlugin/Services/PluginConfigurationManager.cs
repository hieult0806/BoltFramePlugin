using BoltFramePlugin.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoltFramePlugin.Services
{
    internal class PluginConfigurationManager : IPluginConfigurationManager
    {
        private readonly string _pluginConfigPath;
        private readonly IFileService _fileService;

        public PluginConfigurationManager(IFileService fileService)
        {
            _fileService = fileService;
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Revit",
                "BoltFramePlugin");
            Directory.CreateDirectory(appDataPath);
            _pluginConfigPath = Path.Combine(appDataPath, "config.json");
        }

        public PluginConfigurationModel LoadPluginConfiguration()
        {
            if (!_fileService.Exists(_pluginConfigPath))
                return GetDefaultPluginConfiguration();

            var json = _fileService.ReadAllText(_pluginConfigPath);
            return JsonSerializer.Deserialize<PluginConfigurationModel>(json)
                   ?? GetDefaultPluginConfiguration();
        }

        public void SavePluginConfiguration(PluginConfigurationModel config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            _fileService.WriteAllText(_pluginConfigPath, json);
        }

        public void ResetPluginConfigurationToDefault()
        {
            var defaultConfig = GetDefaultPluginConfiguration();
            SavePluginConfiguration(defaultConfig);
        }

        private PluginConfigurationModel GetDefaultPluginConfiguration()
        {
            return new PluginConfigurationModel
            {
                WorkingFolderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Revit",
                    "BoltFramePlugin",
                    "Projects"),
                EnableLogging = true,
                LogLevel = "Info",
                DefaultBuildingClassification = "Type IIA",
                DefaultRayLengthLimit = 100.0,
                AutoCreateArrows = false,
                UITheme = "Light",
                LimitingDistanceWindowPosition = new WindowPosition(),
                RecentProjects = new List<string>()
            };
        }
    }
}
