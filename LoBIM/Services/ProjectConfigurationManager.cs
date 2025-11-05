using LoBIM.Models;
using Newtonsoft.Json;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LoBIM.Services
{
    internal class ProjectConfigurationManager : IProjectConfigurationManager
    {
        private readonly IPluginConfigurationManager _pluginConfigManager;
        private readonly IFileService _fileService;

        public ProjectConfigurationManager(
            IPluginConfigurationManager pluginConfigManager,
            IFileService fileService)
        {
            _pluginConfigManager = pluginConfigManager;
            _fileService = fileService;
        }

        public ProjectConfigurationModel LoadProjectConfiguration(Guid projectId)
        {
            var pluginConfig = _pluginConfigManager.LoadPluginConfiguration();
            string projectConfigPath = GetProjectConfigPath(pluginConfig.WorkingFolderPath, projectId);

            if (!_fileService.Exists(projectConfigPath))
                return GetDefaultProjectConfiguration(projectId);

            var json = _fileService.ReadAllText(projectConfigPath);
            return JsonConvert.DeserializeObject<ProjectConfigurationModel>(json)
                   ?? GetDefaultProjectConfiguration(projectId);
        }

        public void SaveProjectConfiguration(ProjectConfigurationModel config)
        {
            var pluginConfig = _pluginConfigManager.LoadPluginConfiguration();
            string projectConfigPath = GetProjectConfigPath(pluginConfig.WorkingFolderPath, config.ProjectId);

            var json = JsonConvert.SerializeObject(config);
            _fileService.WriteAllText(projectConfigPath, json);
        }

        public void ResetProjectConfigurationToDefault(Guid projectId)
        {
            var defaultConfig = GetDefaultProjectConfiguration(projectId);
            SaveProjectConfiguration(defaultConfig);
        }

        private string GetProjectConfigPath(string workingFolderPath, Guid projectId)
        {
            Directory.CreateDirectory(workingFolderPath);
            return Path.Combine(workingFolderPath, $"project-{projectId}.json");
        }

        private ProjectConfigurationModel GetDefaultProjectConfiguration(Guid projectId)
        {
            return new ProjectConfigurationModel
            {
                ProjectId = projectId,
                EnableAdvancedFeatures = false,
                DefaultFrameSize = 10,
                // Initialize other default settings
            };
        }
    }
}