using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BoltFramePlugin.Helpers
{
    public class PluginConfiguration
    {
        public string ConfigurationFolderUrl { get; set; } = @"C:\";
    }

    public static class ConfigurationHandler
    {
        public static PluginConfiguration LoadPluginConfiguration()
        {
            string configFilePath = ConfigurationManager.ConfigurationFilePath;

            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                PluginConfiguration config = JsonConvert.DeserializeObject<PluginConfiguration>(json);
                return config;
            }
            else
            {
                // Return default configuration
                return new PluginConfiguration();
            }
        }

        public static void SavePluginConfiguration(PluginConfiguration config)
        {
            string configFilePath = ConfigurationManager.ConfigurationFilePath;

            // Ensure the directory exists
            string directory = Path.GetDirectoryName(configFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFilePath, json);
        }
    }
}
