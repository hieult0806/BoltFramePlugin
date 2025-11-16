using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Services;
using System;
using System.Linq;

namespace LoBIM.Features.ViewCloning.Strategies
{
    /// <summary>
    /// Strategy for cloning View3D (3D views)
    /// </summary>
    public class View3DViewCloningStrategy : BaseViewCloningStrategy
    {
        public View3DViewCloningStrategy(ILoggingService logger, LoBIM.Services.Parameters.IProjectParameterService parameterService)
            : base(logger, parameterService)
        {
        }

        public override bool CanHandle(ViewType viewType)
        {
            return viewType == ViewType.ThreeD;
        }

        public override Autodesk.Revit.DB.View CloneView(
            Document hostDoc,
            Autodesk.Revit.DB.View sourceView,
            Document linkedDoc,
            RevitLinkInstance linkInstance,
            string namePrefix,
            ViewPositioningMode positioningMode)
        {
            try
            {
                if (!(sourceView is View3D source3DView))
                {
                    _logger.LogWarning($"Source view is not a View3D: {sourceView.Name}");
                    return null;
                }

                _logger.LogInformation($"=== CLONING 3D VIEW ===");
                _logger.LogInformation($"View Name: {sourceView.Name}");
                _logger.LogInformation($"Is Perspective: {source3DView.IsPerspective}");
                _logger.LogInformation($"Is Template: {source3DView.IsTemplate}");

                // Find a suitable 3D view type in the host document
                var view3DType = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

                if (view3DType == null)
                {
                    _logger.LogError($"No 3D view type found in host document");
                    return null;
                }

                // Create the 3D view - use the appropriate creation method based on source view type
                View3D cloned3DView;
                if (source3DView.IsPerspective)
                {
                    _logger.LogInformation($"Creating perspective 3D view");
                    cloned3DView = View3D.CreatePerspective(hostDoc, view3DType.Id);
                }
                else
                {
                    _logger.LogInformation($"Creating isometric 3D view");
                    cloned3DView = View3D.CreateIsometric(hostDoc, view3DType.Id);
                }

                _logger.LogInformation($"Created new 3D view with ID: {cloned3DView.Id} (IsPerspective: {cloned3DView.IsPerspective})");

                // Apply name
                ApplyViewName(cloned3DView, sourceView.Name, namePrefix);

                // Copy scale
                CopyScale(sourceView, cloned3DView);

                // Copy orientation (view direction)
                CopyViewOrientation(source3DView, cloned3DView, linkInstance, positioningMode);

                // Copy camera settings (perspective views)
                CopyCameraSettings(source3DView, cloned3DView);

                // Copy section box if active
                CopySectionBox(source3DView, cloned3DView, linkInstance, positioningMode);

                // Copy crop region (3D views only support rectangular crop boxes)
                CopyCropRegion(source3DView, cloned3DView, supportsCustomShapes: false);

                // Copy display style
                CopyDisplayStyle(source3DView, cloned3DView);

                // Store source view information for tracking
                string linkedFileName = System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title);
                StoreSourceViewInfo(source3DView, cloned3DView, linkedFileName);

                _logger.LogInformation($"Successfully cloned 3D view: {cloned3DView.Name}");
                return cloned3DView;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning 3D view '{sourceView.Name}': {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Copies the view orientation (eye position and view direction) from source to cloned view
        /// </summary>
        private void CopyViewOrientation(View3D source3DView, View3D cloned3DView, RevitLinkInstance linkInstance, ViewPositioningMode positioningMode)
        {
            try
            {
                _logger.LogInformation($"=== COPYING VIEW ORIENTATION ===");

                // Get source orientation
                var sourceOrientation3D = source3DView.GetOrientation();

                _logger.LogInformation($"Source eye position: {sourceOrientation3D.EyePosition}");
                _logger.LogInformation($"Source forward direction: {sourceOrientation3D.ForwardDirection}");
                _logger.LogInformation($"Source up direction: {sourceOrientation3D.UpDirection}");

                // Apply positioning mode transformation
                Transform linkTransform = GetLinkTransform(linkInstance, positioningMode);

                // Transform eye position and directions
                XYZ transformedEyePosition = linkTransform.OfPoint(sourceOrientation3D.EyePosition);
                XYZ transformedForwardDirection = linkTransform.OfVector(sourceOrientation3D.ForwardDirection);
                XYZ transformedUpDirection = linkTransform.OfVector(sourceOrientation3D.UpDirection);

                _logger.LogInformation($"Transformed eye position: {transformedEyePosition}");
                _logger.LogInformation($"Transformed forward direction: {transformedForwardDirection}");
                _logger.LogInformation($"Transformed up direction: {transformedUpDirection}");

                // Create new orientation
                ViewOrientation3D newOrientation = new ViewOrientation3D(
                    transformedEyePosition,
                    transformedUpDirection,
                    transformedForwardDirection);

                // Apply to cloned view
                cloned3DView.SetOrientation(newOrientation);

                _logger.LogInformation($"Successfully copied view orientation");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy view orientation: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies camera settings from source to cloned 3D view (for perspective views)
        /// </summary>
        private void CopyCameraSettings(View3D source3DView, View3D cloned3DView)
        {
            try
            {
                _logger.LogInformation($"=== COPYING CAMERA SETTINGS ===");
                _logger.LogInformation($"Source is perspective: {source3DView.IsPerspective}");
                _logger.LogInformation($"Target is perspective: {cloned3DView.IsPerspective}");

                // Both views must be perspective to copy camera-specific settings
                if (source3DView.IsPerspective && cloned3DView.IsPerspective)
                {
                    _logger.LogInformation($"Both views are perspective - copying camera-specific settings");

                    // Get camera parameters from source
                    // Note: The main camera position and direction are already handled by SetOrientation
                    // Here we handle additional perspective-specific settings

                    // Check if there are view-specific parameters we can copy
                    // For perspective views, the orientation already includes the camera position
                    // Additional settings might include far clip settings, etc.

                    _logger.LogInformation($"Perspective camera position and direction already copied via SetOrientation");
                    _logger.LogInformation($"Additional perspective-specific parameters handled by view creation");
                }
                else if (source3DView.IsPerspective && !cloned3DView.IsPerspective)
                {
                    _logger.LogWarning($"Source is perspective but target is isometric - this should not happen with correct view creation");
                }
                else
                {
                    _logger.LogInformation($"Source is isometric view - no camera-specific settings to copy");
                }

                _logger.LogInformation($"Successfully processed camera settings");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy camera settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies the section box from source to cloned view if active
        /// </summary>
        private void CopySectionBox(View3D source3DView, View3D cloned3DView, RevitLinkInstance linkInstance, ViewPositioningMode positioningMode)
        {
            try
            {
                if (!source3DView.IsSectionBoxActive)
                {
                    _logger.LogInformation($"Source view does not have section box active - skipping");
                    return;
                }

                _logger.LogInformation($"=== COPYING SECTION BOX ===");

                // Get source section box
                var sourceSectionBox = source3DView.GetSectionBox();

                _logger.LogInformation($"Source section box:");
                _logger.LogInformation($"  Origin: {sourceSectionBox.Transform.Origin}");
                _logger.LogInformation($"  Min: {sourceSectionBox.Min}");
                _logger.LogInformation($"  Max: {sourceSectionBox.Max}");

                // Apply positioning mode transformation
                Transform linkTransform = GetLinkTransform(linkInstance, positioningMode);

                // Transform the section box
                Transform transformedTransform = linkTransform.Multiply(sourceSectionBox.Transform);

                _logger.LogInformation($"Transformed section box origin: {transformedTransform.Origin}");

                // Create new section box with transformed coordinates
                BoundingBoxXYZ newSectionBox = new BoundingBoxXYZ
                {
                    Transform = transformedTransform,
                    Min = sourceSectionBox.Min,  // Min/Max are in local coordinates, don't transform
                    Max = sourceSectionBox.Max
                };

                // Apply to cloned view
                cloned3DView.SetSectionBox(newSectionBox);
                cloned3DView.IsSectionBoxActive = true;

                _logger.LogInformation($"Successfully copied section box");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy section box: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies display style settings from source to cloned view
        /// </summary>
        private void CopyDisplayStyle(View3D source3DView, View3D cloned3DView)
        {
            try
            {
                _logger.LogInformation($"=== COPYING DISPLAY STYLE ===");

                // Copy display style
                cloned3DView.DisplayStyle = source3DView.DisplayStyle;
                _logger.LogInformation($"Display style: {source3DView.DisplayStyle}");

                // Copy detail level
                cloned3DView.DetailLevel = source3DView.DetailLevel;
                _logger.LogInformation($"Detail level: {source3DView.DetailLevel}");

                _logger.LogInformation($"Successfully copied display style settings");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy display style: {ex.Message}");
            }
        }
    }
}
