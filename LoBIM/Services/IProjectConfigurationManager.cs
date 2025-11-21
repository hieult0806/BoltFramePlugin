using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LoBIM.Models;

namespace LoBIM.Services
{
    public interface IProjectConfigurationManager
    {
        ProjectConfigurationModel LoadProjectConfiguration(Guid projectId);
        void SaveProjectConfiguration(ProjectConfigurationModel config);
        void ResetProjectConfigurationToDefault(Guid projectId);
    }
}
