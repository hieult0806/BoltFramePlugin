using Autodesk.Revit.DB;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.Factories
{
    public class FramingStrategyFactory
    {
        public static IFramingStrategy GetFramingStrategy(IRevitService service, IFrameGenerateModel model)
        {
            if (model is MultiFloorFrameGenerateModel)
            {
                return new MultiFloorFramingStrategy(service, model);
            }
            else if (model is FloorFrameGenerateModel)
            {
                return new FloorFramingStrategy(service, model);
            }
            else if (model is WallFrameGenerateModel)
            {
                return new WallFramingStrategy(service, model);
            }
            else
            {
                throw new ArgumentException("Unsupported element type for framing.");
            }
        }
    }
}
