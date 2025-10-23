using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoltFramePlugin.Models;

namespace BoltFramePlugin.Services
{
    public interface IProjectConfigurationManager
    {
        ProjectConfigurationModel LoadProjectConfiguration(Guid projectId);
        void SaveProjectConfiguration(ProjectConfigurationModel config);
        void ResetProjectConfigurationToDefault(Guid projectId);
    }
}
