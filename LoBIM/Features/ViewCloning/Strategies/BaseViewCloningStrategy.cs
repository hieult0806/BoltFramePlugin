using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Services;
using System;

namespace LoBIM.Features.ViewCloning.Strategies
{
    /// <summary>
    /// Base class for view cloning strategies, providing common functionality
    /// </summary>
    public abstract class BaseViewCloningStrategy : IViewCloningStrategy
    {
        protected readonly ILoggingService _logger;

        protected BaseViewCloningStrategy(ILoggingService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract bool CanHandle(ViewType viewType);

        public abstract Autodesk.Revit.DB.View CloneView(
            Document hostDoc,
            Autodesk.Revit.DB.View sourceView,
            Document linkedDoc,
            RevitLinkInstance linkInstance,
            string namePrefix,
            ViewPositioningMode positioningMode);

        /// <summary>
        /// Gets the appropriate transform based on the positioning mode
        /// </summary>
        protected Transform GetLinkTransform(RevitLinkInstance linkInstance, ViewPositioningMode positioningMode)
        {
            try
            {
                _logger.LogInformation($"Getting link transform for mode: {positioningMode}");

                switch (positioningMode)
                {
                    case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                    case ViewPositioningMode.BySharedCoordinates:
                        // For shared coordinates, the linked file uses the same coordinate system
                        // No transformation needed - views are already positioned correctly
                        _logger.LogInformation($"Shared coordinates mode - Using Identity transform (no adjustment)");
                        _logger.LogInformation($"Linked file and host file share the same coordinate system");
                        return Transform.Identity;

                    case ViewPositioningMode.InternalOriginToInternalOrigin:
                    default:
                        // Use the link's placement transform based on how it was placed in the host
                        // This accounts for any offset/rotation applied when the link was inserted
                        var transform = linkInstance.GetTransform();
                        _logger.LogInformation($"InternalOrigin mode - Using link placement transform");
                        _logger.LogInformation($"Link transform - Origin: {transform.Origin}, Rotation: {transform.BasisX}, {transform.BasisY}, {transform.BasisZ}");
                        return transform;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting link transform for mode {positioningMode}: {ex.Message}", ex);
                return linkInstance.GetTransform(); // Fallback to default
            }
        }

        /// <summary>
        /// Applies a name to the cloned view, handling naming conflicts and prohibited characters
        /// </summary>
        protected void ApplyViewName(Autodesk.Revit.DB.View view, string sourceViewName, string namePrefix)
        {
            // Sanitize the source view name by removing prohibited characters
            // Revit prohibits: \ : { } [ ] | ; < > ? ` ~
            string sanitizedName = SanitizeViewName(sourceViewName);

            string newName = string.IsNullOrEmpty(namePrefix)
                ? $"{sanitizedName} (Cloned)"
                : $"{namePrefix}_{sanitizedName}";

            try
            {
                view.Name = newName;
                _logger.LogInformation($"Set view name to: {newName}");
            }
            catch (Exception ex)
            {
                // If name conflict, append timestamp
                string fallbackName = $"{newName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                view.Name = fallbackName;
                _logger.LogWarning($"Name conflict, using fallback name: {fallbackName}. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes prohibited characters from view names
        /// Revit prohibits: \ : { } [ ] | ; < > ? ` ~
        /// </summary>
        private string SanitizeViewName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Replace prohibited characters with empty string or safe alternatives
            char[] prohibitedChars = new[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };

            foreach (char c in prohibitedChars)
            {
                name = name.Replace(c.ToString(), "");
            }

            // Trim any extra spaces that might result from removal
            return name.Trim();
        }

        /// <summary>
        /// Copies scale from source view to cloned view
        /// </summary>
        protected void CopyScale(Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View clonedView)
        {
            if (sourceView.Scale > 0)
            {
                try
                {
                    clonedView.Scale = sourceView.Scale;
                    _logger.LogInformation($"Set scale to {sourceView.Scale} for cloned view");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not set scale: {ex.Message}");
                }
            }
        }
    }
}
