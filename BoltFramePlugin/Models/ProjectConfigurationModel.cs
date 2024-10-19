using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.Models
{
    internal class ProjectConfigurationModel
    {
        public Guid ProjectId { get; set; } // Unique identifier for the Revit project
        public bool EnableAdvancedFeatures { get; set; }
        public int DefaultFrameSize { get; set; }
    }
}
