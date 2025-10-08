using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Factories;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.Models;
using BoltFramePlugin.Services;
using BoltFramePlugin.ViewModels;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.EventHandlers
{
    internal class GenerateFrameEventHandler : IExternalEventHandler
    {
        private IRevitService _service;
        private IFrameGenerateModel _frameGenerateModel;
        private readonly ILoggingService _logger;

        public GenerateFrameEventHandler()
        {
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
        }

        public void SetParameters(IRevitService service, IFrameGenerateModel model)
        {
            _service = service;
            _frameGenerateModel = model;
            _logger.LogInformation($"Parameters set - Model type: {model?.GetType().Name}");
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _logger.LogInformation("GenerateFrameEventHandler.Execute started");

                if (_service == null || _frameGenerateModel == null)
                {
                    _logger.LogError("Service or Model is null in Execute");
                    TaskDialog.Show("Error", "Internal error: Service or Model is null");
                    return;
                }

                _logger.LogInformation($"Getting framing strategy for model type: {_frameGenerateModel.GetType().Name}");
                var framingStrategy = FramingStrategyFactory.GetFramingStrategy(_service, _frameGenerateModel);

                _logger.LogInformation($"Framing strategy created: {framingStrategy.GetType().Name}");
                _logger.LogInformation("Starting frame generation...");

                framingStrategy.StartGenerate();

                _logger.LogInformation("Frame generation completed");

                // Show summary if available (MultiFloorFramingStrategy has Summary property)
                if (framingStrategy is MultiFloorFramingStrategy multiFloorStrategy &&
                    multiFloorStrategy.Summary != null &&
                    multiFloorStrategy.Summary.Items.Count > 0)
                {
                    var windowManager = DIContainerService.Container.GetInstance<IWindowManager>();
                    var summaryVM = new FramingSummaryVM(_service.UiDoc, multiFloorStrategy.Summary);
                    windowManager.Open(summaryVM);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GenerateFrameEventHandler.Execute: {ex.Message}", ex);
                TaskDialog.Show("Error", $"Error generating frames: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
            }
        }

        public string GetName()
        {
            return "Place Beam External Event";
        }
    }
}