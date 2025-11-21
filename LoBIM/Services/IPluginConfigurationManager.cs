using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LoBIM.Models;

namespace LoBIM.Services
{
    public interface IPluginConfigurationManager
    {
        PluginConfigurationModel LoadPluginConfiguration();
        void SavePluginConfiguration(PluginConfigurationModel config);
        void ResetPluginConfigurationToDefault();
    }
}
