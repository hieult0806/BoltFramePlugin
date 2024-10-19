using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.Models
{
    public class PluginConfigurationModel
    {
        public int ConfigVersion { get; set; } = 1;
        public string WorkingFolderPath { get; set; }
        public bool EnableLogging { get; set; }
        public string LogLevel { get; set; }
    }
}
