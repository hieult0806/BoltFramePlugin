using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.Helpers
{
    public static class ConfigurationManager
    {
        private static string ConfigDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Resources.Strings.Strings.CompanyName,
            Resources.Strings.Strings.PluginName);

        private static string DefaultConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

        public static string ConfigurationFilePath { get; private set; } = DefaultConfigFilePath;

        public static void LoadConfigurationFilePath()
        {
            string settingsFilePath = Path.Combine(ConfigDirectory, "settings.txt");
            if (File.Exists(settingsFilePath))
            {
                ConfigurationFilePath = File.ReadAllText(settingsFilePath);
            }
            else
            {
                ConfigurationFilePath = DefaultConfigFilePath; // Default path
            }
        }

        public static void SaveConfigurationFilePath(string newPath)
        {
            ConfigurationFilePath = newPath;

            // Ensure the config directory exists
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            // Save the path to a settings file
            string settingsFilePath = Path.Combine(ConfigDirectory, "settings.txt");
            File.WriteAllText(settingsFilePath, newPath);
        }
    }
}
