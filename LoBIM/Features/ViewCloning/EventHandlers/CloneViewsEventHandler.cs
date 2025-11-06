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
        private Action<List<ElementId>> _onCompleted;
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
        public void SetParameters(List<LinkedViewInfo> viewsToClone, string namePrefix, ViewPositioningMode positioningMode, Action<List<ElementId>> onCompleted)
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
                    _onCompleted?.Invoke(new List<ElementId>());
                    return;
                }

                // Clone the views (transaction is handled inside the service)
                var clonedViewIds = _viewCloningService.CloneViews(doc, _viewsToClone, _namePrefix, _positioningMode);

                _logger.LogInformation($"Successfully cloned {clonedViewIds.Count} out of {_viewsToClone.Count} views");

                // Open the last cloned view if any were cloned
                if (clonedViewIds.Count > 0)
                {
                    try
                    {
                        var lastClonedViewId = clonedViewIds[clonedViewIds.Count - 1];
                        app.ActiveUIDocument.ActiveView = doc.GetElement(lastClonedViewId) as Autodesk.Revit.DB.View;
                        _logger.LogInformation($"Opened cloned view with ID: {lastClonedViewId}");
                    }
                    catch (Exception openEx)
                    {
                        _logger.LogWarning($"Could not open cloned view: {openEx.Message}");
                    }
                }

                // Call the completion callback
                _onCompleted?.Invoke(clonedViewIds);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CloneViewsEventHandler: {ex.Message}", ex);
                _onCompleted?.Invoke(new List<ElementId>());
            }
        }

        public string GetName()
        {
            return "Clone Views Event Handler";
        }
    }
}
