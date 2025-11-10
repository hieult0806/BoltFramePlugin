using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Services;
using System;
using System.Linq;

namespace LoBIM.Features.ViewCloning.Strategies
{
    /// <summary>
    /// Strategy for cloning ViewSection (includes both Section and Elevation views)
    /// Handles both regular views and callout views
    /// </summary>
    public class SectionViewCloningStrategy : BaseViewCloningStrategy
    {
        public SectionViewCloningStrategy(ILoggingService logger) : base(logger)
        {
        }

        public override bool CanHandle(ViewType viewType)
        {
            // This strategy handles ViewSection concrete type, which covers both:
            // - ViewType.Section
            // - ViewType.Elevation
            return viewType == ViewType.Section || viewType == ViewType.Elevation;
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
                if (!(sourceView is ViewSection sourceSection))
                {
                    _logger.LogWarning($"Source view is not a ViewSection: {sourceView.Name}");
                    return null;
                }

                _logger.LogInformation($"=== CLONING VIEW SECTION ===");
                _logger.LogInformation($"View Name: {sourceView.Name}");
                _logger.LogInformation($"ViewType: {sourceView.ViewType} (Section or Elevation)");
                _logger.LogInformation($"Is Callout: {sourceView.IsCallout}");

                // Get the crop box from the source view
                var sourceBoundingBox = sourceSection.CropBox;
                if (sourceBoundingBox == null)
                {
                    _logger.LogWarning($"Could not get crop box from view: {sourceView.Name}");
                    return null;
                }

                // Get link transform
                Transform linkTransform = GetLinkTransform(linkInstance, positioningMode);

                // Get source transform and direction vectors
                var srcTransform = sourceBoundingBox.Transform;
                var sourceOrigin = sourceSection.Origin;
                var sourceDirection = sourceSection.ViewDirection;
                var sourceUpDirection = sourceSection.UpDirection;
                var sourceRightDirection = sourceSection.RightDirection;

                _logger.LogInformation($"=== SOURCE VIEW DETAILS ===");
                _logger.LogInformation($"Source Origin: {sourceOrigin}");
                _logger.LogInformation($"Source Direction: {sourceDirection}");
                _logger.LogInformation($"Source Up: {sourceUpDirection}");
                _logger.LogInformation($"Source Right: {sourceRightDirection}");

                // Check if this is a callout - if so, need parent-relative positioning
                Transform finalTransform;
                if (sourceView.IsCallout)
                {
                    _logger.LogInformation($"=== CALLOUT DETECTED ===");
                    finalTransform = CalculateCalloutTransform(sourceSection, linkedDoc, sourceBoundingBox, linkTransform, positioningMode);
                    if (finalTransform == null)
                    {
                        _logger.LogWarning($"Failed to calculate callout transform");
                        return null;
                    }
                }
                else
                {
                    _logger.LogInformation($"=== REGULAR VIEW (NON-CALLOUT) ===");
                    finalTransform = CalculateRegularTransform(srcTransform, linkTransform, positioningMode);
                }

                // Create the section view (works for both sections and elevations)
                var newSection = CreateSectionView(hostDoc, sourceView.ViewType, sourceBoundingBox, finalTransform);

                if (newSection == null)
                {
                    return null;
                }

                // Copy properties
                CopyScale(sourceView, newSection);
                ApplyViewName(newSection, sourceView.Name, namePrefix);

                // NOTE: For section views, we do NOT call CopyCropRegion because:
                // - The crop box (bounding box) defines the section's position and orientation
                // - It was already set correctly during ViewSection.CreateSection()
                // - Copying the crop box would overwrite the position we carefully calculated
                // - Custom crop shapes will be copied separately if needed

                // However, we still need to copy annotation crop settings
                CopyAnnotationCrop(sourceView, newSection);

                // Store source view information for tracking
                string linkedFileName = System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title);
                StoreSourceViewInfo(sourceView, newSection, linkedFileName);

                _logger.LogInformation($"Successfully cloned view: {newSection.Name}");
                return newSection;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning view {sourceView.Name}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Calculates transform for a regular (non-callout) view
        /// </summary>
        private Transform CalculateRegularTransform(
            Transform srcTransform,
            Transform linkTransform,
            ViewPositioningMode positioningMode)
        {
            _logger.LogInformation($"=== APPLYING POSITIONING MODE: {positioningMode} ===");

            Transform finalTransform;

            switch (positioningMode)
            {
                case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                case ViewPositioningMode.BySharedCoordinates:
                    // For shared coordinates, use the view's coordinates as-is from the linked file
                    _logger.LogInformation($"Using shared coordinates - no link transform applied");
                    _logger.LogInformation($"View origin: {srcTransform.Origin}");
                    _logger.LogInformation($"View direction: BasisZ = {srcTransform.BasisZ}");

                    finalTransform = srcTransform;
                    break;

                case ViewPositioningMode.InternalOriginToInternalOrigin:
                default:
                    // Apply link transform to position view relative to link placement
                    _logger.LogInformation($"Applying link transform based on link placement mode");
                    _logger.LogInformation($"Link transform origin offset: {linkTransform.Origin}");
                    _logger.LogInformation($"View cropbox origin (in linked file): {srcTransform.Origin}");

                    // Standard transform composition for regular views
                    finalTransform = linkTransform.Multiply(srcTransform);

                    _logger.LogInformation($"Composed origin (in host): {finalTransform.Origin}");
                    break;
            }

            _logger.LogInformation($"=== FINAL TRANSFORM ===");
            _logger.LogInformation($"Final Origin: {finalTransform.Origin}");
            _logger.LogInformation($"Final BasisX: {finalTransform.BasisX}");
            _logger.LogInformation($"Final BasisY: {finalTransform.BasisY}");
            _logger.LogInformation($"Final BasisZ: {finalTransform.BasisZ}");

            return finalTransform;
        }

        /// <summary>
        /// Calculates transform for a callout view (parent-relative positioning)
        /// </summary>
        private Transform CalculateCalloutTransform(
            ViewSection calloutView,
            Document linkedDoc,
            BoundingBoxXYZ calloutBoundingBox,
            Transform linkTransform,
            ViewPositioningMode positioningMode)
        {
            _logger.LogInformation($"=== CALLOUT COORDINATE TRANSFORMATION ===");

            // Get parent view
            ElementId parentId = null;
            try
            {
                parentId = calloutView.GetCalloutParentId();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get callout parent ID: {ex.Message}");
                return null;
            }

            if (parentId == null || parentId == ElementId.InvalidElementId)
            {
                _logger.LogWarning($"No valid parent ID found for callout view");
                return null;
            }

            var parentView = linkedDoc.GetElement(parentId) as Autodesk.Revit.DB.View;
            if (parentView == null)
            {
                _logger.LogWarning($"Could not find parent view with ID: {parentId}");
                return null;
            }

            _logger.LogInformation($"=== PARENT VIEW INFORMATION ===");
            _logger.LogInformation($"Parent View Name: {parentView.Name}");
            _logger.LogInformation($"Parent View Type: {parentView.ViewType}");
            _logger.LogInformation($"Parent Origin: {parentView.Origin}");

            // Get parent crop box
            var parentCropBox = parentView.CropBox;
            if (parentCropBox == null)
            {
                _logger.LogWarning($"Parent view has no cropbox - cannot transform callout");
                return null;
            }

            var parentTransform = parentCropBox.Transform;
            var srcTransform = calloutBoundingBox.Transform;

            _logger.LogInformation($"Transforming callout POSITION through parent view coordinate system");
            _logger.LogInformation($"Callout cropbox origin (in parent coords): {srcTransform.Origin}");
            _logger.LogInformation($"Parent cropbox origin: {parentTransform.Origin}");

            // CRITICAL INSIGHT: For callouts, the cropbox origin is ALREADY in the parent's coordinate system
            // We should NOT transform it through the parent - it's already relative to the parent!
            // Just use the callout's cropbox origin directly (it's in world coordinates already)

            _logger.LogInformation($"=== ANALYSIS: CALLOUT VS PARENT ===");
            _logger.LogInformation($"Original callout origin: {calloutView.Origin}");
            _logger.LogInformation($"Parent view origin: {parentView.Origin}");
            _logger.LogInformation($"Are they the same? {calloutView.Origin.IsAlmostEqualTo(parentView.Origin)}");

            // The callout's origin should match the parent's origin (they share the same cutting plane)
            // So we should use the callout's cropbox origin directly
            XYZ calloutWorldOrigin = srcTransform.Origin;

            _logger.LogInformation($"Using callout cropbox origin directly: {calloutWorldOrigin}");

            // Now apply positioning mode
            _logger.LogInformation($"=== APPLYING POSITIONING MODE: {positioningMode} ===");

            Transform finalTransform;

            switch (positioningMode)
            {
                case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                case ViewPositioningMode.BySharedCoordinates:
                    // For shared coordinates, use the transformed world position as-is
                    _logger.LogInformation($"Using shared coordinates - no link transform applied");
                    _logger.LogInformation($"CALLOUT: Using transformed world position: {calloutWorldOrigin}");

                    // For callouts, use the transformed origin but keep the source direction vectors
                    finalTransform = Transform.Identity;
                    finalTransform.Origin = calloutWorldOrigin;
                    finalTransform.BasisX = srcTransform.BasisX;
                    finalTransform.BasisY = srcTransform.BasisY;
                    finalTransform.BasisZ = srcTransform.BasisZ;
                    break;

                case ViewPositioningMode.InternalOriginToInternalOrigin:
                default:
                    // Apply link transform to the callout world origin
                    _logger.LogInformation($"Applying link transform to callout");
                    _logger.LogInformation($"Link transform origin offset: {linkTransform.Origin}");
                    _logger.LogInformation($"Callout world origin (in linked file): {calloutWorldOrigin}");

                    // Transform the callout world origin through the link transform
                    XYZ transformedCalloutOrigin = linkTransform.OfPoint(calloutWorldOrigin);
                    _logger.LogInformation($"Transformed callout origin (in host): {transformedCalloutOrigin}");

                    // Create transform with the transformed callout position and source direction
                    finalTransform = Transform.Identity;
                    finalTransform.Origin = transformedCalloutOrigin;
                    finalTransform.BasisX = srcTransform.BasisX;
                    finalTransform.BasisY = srcTransform.BasisY;
                    finalTransform.BasisZ = srcTransform.BasisZ;
                    break;
            }

            _logger.LogInformation($"=== FINAL CALLOUT TRANSFORM ===");
            _logger.LogInformation($"Final Origin: {finalTransform.Origin}");
            _logger.LogInformation($"Final BasisX: {finalTransform.BasisX}");
            _logger.LogInformation($"Final BasisY: {finalTransform.BasisY}");
            _logger.LogInformation($"Final BasisZ: {finalTransform.BasisZ}");

            return finalTransform;
        }

        /// <summary>
        /// Creates the section view in the host document
        /// Works for both Section and Elevation view types
        /// </summary>
        private ViewSection CreateSectionView(
            Document hostDoc,
            ViewType viewType,
            BoundingBoxXYZ sourceBoundingBox,
            Transform finalTransform)
        {
            // IMPORTANT: Both Section and Elevation views are created using ViewSection.CreateSection()
            // which requires a ViewFamilyType with ViewFamily.Section
            // Elevations are NOT created with ViewFamily.Elevation - that's just for display purposes
            ViewFamily viewFamily = ViewFamily.Section;

            _logger.LogInformation($"View type: {viewType}, using ViewFamily: {viewFamily}");

            // Find matching view family type in host document
            var hostViewFamilyType = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == viewFamily);

            if (hostViewFamilyType == null)
            {
                _logger.LogWarning($"Could not find {viewFamily} view family type in host document");
                return null;
            }

            XYZ min = sourceBoundingBox.Min;
            XYZ max = sourceBoundingBox.Max;

            // Calculate actual world coordinates
            var worldMin = finalTransform.OfPoint(min);
            var worldMax = finalTransform.OfPoint(max);
            _logger.LogInformation($"=== VIEW BOX FINAL ===");
            _logger.LogInformation($"Min (relative): {min}");
            _logger.LogInformation($"Max (relative): {max}");
            _logger.LogInformation($"Min (world): {worldMin}");
            _logger.LogInformation($"Max (world): {worldMax}");

            // Calculate the dimensions
            double width = max.X - min.X;
            double height = max.Y - min.Y;
            double depth = max.Z - min.Z;

            _logger.LogInformation($"View dimensions - Width: {width}, Height: {height}, Depth: {depth}");

            // CRITICAL UNDERSTANDING:
            // 1. ViewSection.CreateSection() REVERSES the BasisZ direction
            // 2. Revit normalizes the bounding box: If Min != (0,0,0), Revit shifts the origin
            //
            // We must:
            // A. Pre-flip BasisZ (so Revit flips it back to correct direction)
            // B. Normalize Min/Max to (0,0,0)/(width,height,depth) ourselves
            // C. Keep the original cropbox transform origin (don't adjust it)

            _logger.LogInformation($"=== CREATING SECTION WITH REVIT API QUIRKS COMPENSATION ===");
            _logger.LogInformation($"Source Transform Origin: {finalTransform.Origin}");
            _logger.LogInformation($"Source BasisZ: {finalTransform.BasisZ}");
            _logger.LogInformation($"Source Min: {min}");
            _logger.LogInformation($"Source Max: {max}");

            // Normalize the bounding box to (0,0,0)/(width,height,depth)
            XYZ normalizedMin = new XYZ(0, 0, 0);
            XYZ normalizedMax = new XYZ(width, height, depth);

            // Create adjusted transform:
            // - Keep the SAME origin as source cropbox transform
            // - Flip BasisZ to compensate for Revit's reversal
            Transform adjustedTransform = Transform.CreateTranslation(finalTransform.Origin);
            adjustedTransform.BasisX = finalTransform.BasisX;
            adjustedTransform.BasisY = finalTransform.BasisY;
            adjustedTransform.BasisZ = -finalTransform.BasisZ;  // PRE-FLIP to compensate for Revit's reversal

            _logger.LogInformation($"=== ADJUSTED VALUES FOR REVIT API ===");
            _logger.LogInformation($"Adjusted Transform Origin (same as source): {adjustedTransform.Origin}");
            _logger.LogInformation($"Adjusted BasisZ (pre-flipped): {adjustedTransform.BasisZ}");
            _logger.LogInformation($"Normalized Min: {normalizedMin}");
            _logger.LogInformation($"Normalized Max: {normalizedMax}");

            // Create section box with NORMALIZED Min/Max and adjusted transform
            var sectionBox = new BoundingBoxXYZ
            {
                Transform = adjustedTransform,
                Min = normalizedMin,  // MUST be (0,0,0) to prevent Revit from shifting origin
                Max = normalizedMax   // MUST be (width,height,depth)
            };

            // Create the section view (works for both sections and elevations)
            var newSection = ViewSection.CreateSection(hostDoc, hostViewFamilyType.Id, sectionBox);

            // Log what was actually created
            _logger.LogInformation($"=== CREATED VIEW PROPERTIES ===");
            _logger.LogInformation($"Created Origin: {newSection.Origin}");
            _logger.LogInformation($"Created Direction: {newSection.ViewDirection}");
            _logger.LogInformation($"Created Up: {newSection.UpDirection}");
            _logger.LogInformation($"Created Right: {newSection.RightDirection}");
            _logger.LogInformation($"Created CropBox Origin: {newSection.CropBox.Transform.Origin}");
            _logger.LogInformation($"Created CropBox BasisZ: {newSection.CropBox.Transform.BasisZ}");
            _logger.LogInformation($"Created CropBox Min: {newSection.CropBox.Min}");
            _logger.LogInformation($"Created CropBox Max: {newSection.CropBox.Max}");

            // Attempt to restore the original cropbox Min/Max after creation
            try
            {
                _logger.LogInformation($"=== ATTEMPTING TO RESTORE ORIGINAL CROPBOX MIN/MAX ===");
                _logger.LogInformation($"Target Min: {min}");
                _logger.LogInformation($"Target Max: {max}");

                var currentCropBox = newSection.CropBox;
                currentCropBox.Min = min;
                currentCropBox.Max = max;
                newSection.CropBox = currentCropBox;

                _logger.LogInformation($"Successfully restored original cropbox Min/Max");
                _logger.LogInformation($"Final CropBox Min: {newSection.CropBox.Min}");
                _logger.LogInformation($"Final CropBox Max: {newSection.CropBox.Max}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not restore original cropbox Min/Max: {ex.Message}");
                _logger.LogInformation($"The section will use normalized cropbox coordinates (this is cosmetic only)");
            }

            return newSection;
        }
    }
}
