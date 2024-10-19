using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.FramingStrategies
{
    public interface IFramingStrategy
    {
        IRevitService RevitService { get; set; }
        IFrameGenerateModel Model { get; set; }
        void StartGenerate();
    }

    public interface IFrameGenerateModel
    {
        double Spacing { get; set; }
    }
}
