using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Factories;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.Models;
using BoltFramePlugin.Services;

namespace BoltFramePlugin.EventHandlers
{
    internal class GenerateFrameEventHandler : IExternalEventHandler
    {
        private IRevitService _service;
        private IFrameGenerateModel _frameGenerateModel;
        public void SetParameters(IRevitService service, IFrameGenerateModel model)
        {
            _service = service;
            _frameGenerateModel = model;
        }

        public void Execute(UIApplication app)
        {
            var framingStrategy = FramingStrategyFactory.GetFramingStrategy(_service, _frameGenerateModel);
            framingStrategy.StartGenerate();
        }

        public string GetName()
        {
            return "Place Beam External Event";
        }
    }
}