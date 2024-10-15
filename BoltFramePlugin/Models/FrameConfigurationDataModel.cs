using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.Models
{
    public class FrameConfigurationDataModel
    {
        public string ConfigurationName { get; set; }
        public Dictionary<string, object> Attributes { get; set; }

        public FrameConfigurationDataModel()
        {
            ConfigurationName = "new-config";
            Attributes = new Dictionary<string, object>
                {
                    { "Stud Spacing", 16.0 },
                    { "Column Type", "null" },
                    { "Beam Type", "null" },
                    { "Top Plate Count", 2 },
                    { "Bottom Plate Count", 1 },
                    { "Stud Corner Count", 4 },
                    { "Stud Start", StudStart.Left },
                    { "Z-Offset", 1 },
                    { "Joist Spacing", 3 },
                    { "Beam Spacing", 3 }
                };
        }
        public FrameConfigurationDataModel(string configurationName, Dictionary<string, object> attributes)
        {
            ConfigurationName = configurationName;
            Attributes = attributes;
        }
    }
}
