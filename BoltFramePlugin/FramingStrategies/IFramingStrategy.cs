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
        Element TargetElement { get; set; }
        FamilySymbol BeamSymbol { get; set; }
        FamilySymbol ColSymbol { get; set; }
        Level Level { get; set; }
        double Spacing { get; set; }
    }
}
