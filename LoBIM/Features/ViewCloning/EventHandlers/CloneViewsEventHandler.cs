using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
using LoBIM.Services;

namespace LoBIM.Features.ViewCloning.EventHandlers
{
    /// <summary>
    /// External event handler for cloning views from linked files
    /// </summary>
    public class CloneViewsEventHandler : IExternalEventHandler
    {
        private List<LinkedViewInfo> _viewsToClone;
        private string _namePrefix;
        private ViewPositioningMode _positioningMode;
        private Action<int> _onCompleted;
        private readonly IViewCloningService _viewCloningService;
        private readonly ILoggingService _logger;

        public CloneViewsEventHandler()
        {
            _viewCloningService = DIContainerService.Container.GetInstance<IViewCloningService>();
            _logger = DIContainerService.Container.GetInstance<ILoggingService>();
        }

        /// <summary>
        /// Set parameters for the cloning operation
        /// </summary>
        public void SetParameters(List<LinkedViewInfo> viewsToClone, string namePrefix, ViewPositioningMode positioningMode, Action<int> onCompleted)
        {
            _viewsToClone = viewsToClone;
            _namePrefix = namePrefix;
            _positioningMode = positioningMode;
            _onCompleted = onCompleted;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;

                _logger.LogInformation($"Starting to clone {_viewsToClone?.Count ?? 0} views with prefix '{_namePrefix}'");

                if (_viewsToClone == null || _viewsToClone.Count == 0)
                {
                    _logger.LogWarning("No views to clone");
                    _onCompleted?.Invoke(0);
                    return;
                }

                // Clone the views (transaction is handled inside the service)
                int successCount = _viewCloningService.CloneViews(doc, _viewsToClone, _namePrefix, _positioningMode);

                _logger.LogInformation($"Successfully cloned {successCount} out of {_viewsToClone.Count} views");

                // Call the completion callback
                _onCompleted?.Invoke(successCount);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CloneViewsEventHandler: {ex.Message}", ex);
                _onCompleted?.Invoke(0);
            }
        }

        public string GetName()
        {
            return "Clone Views Event Handler";
        }
    }
}
