using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.Factories
{
    public class FramingStrategyFactory
    {
        public static IFramingStrategy GetFramingStrategy(IRevitService service, IFrameGenerateModel model)
        {
            Element element = model.TargetElement;
            if (element is Floor)
            {
                return new FloorFramingStrategy(service, model);
            }
            else
            {
                throw new ArgumentException("Unsupported element type for framing.");
            }
        }
    }
}
