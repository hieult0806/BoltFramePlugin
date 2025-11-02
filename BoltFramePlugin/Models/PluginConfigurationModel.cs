using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoltFramePlugin.Features.LimitingDistance.Models;

namespace BoltFramePlugin.Models
{
    public class PluginConfigurationModel
    {
        public int ConfigVersion { get; set; } = 1;
        public string WorkingFolderPath { get; set; }
        public bool EnableLogging { get; set; }
        public string LogLevel { get; set; }

        // Limiting Distance Plugin Settings
        public string DefaultBuildingClassification { get; set; } = "Type IIA";
        public double DefaultRayLengthLimit { get; set; } = 100.0; // feet
        public bool AutoCreateArrows { get; set; } = false; // Automatically create arrows when detecting reference lines

        // Opening Limits Tables Data
        public OpeningLimitsTableData OpeningLimitsData { get; set; } = null; // null means use defaults

        // UI Settings
        public string UITheme { get; set; } = "Light";
        public WindowPosition LimitingDistanceWindowPosition { get; set; } = new WindowPosition();

        // Recent Projects
        public List<string> RecentProjects { get; set; } = new List<string>();
    }

    /// <summary>
    /// Stores window position and size
    /// </summary>
    public class WindowPosition
    {
        public double X { get; set; } = 100;
        public double Y { get; set; } = 100;
        public double Width { get; set; } = 1000;
        public double Height { get; set; } = 700;
        public bool IsMaximized { get; set; } = false;
    }
}
