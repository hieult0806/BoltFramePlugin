using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
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
        public SectionViewCloningStrategy(
            ILoggingService logger,
            LoBIM.Services.Parameters.IProjectParameterService parameterService,
            IViewTemplateTransferService viewTemplateService)
            : base(logger, parameterService, viewTemplateService)
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

                _logger.LogInformation($"");
                _logger.LogInformation($"╔══════════════════════════════════════════════════════════════════");
                _logger.LogInformation($"║ SOURCE VIEW DETAILS (from Linked Document)");
                _logger.LogInformation($"╠══════════════════════════════════════════════════════════════════");
                _logger.LogInformation($"║ View Name: {sourceView.Name}");
                _logger.LogInformation($"║ Source Origin:         ({sourceOrigin.X:F4}, {sourceOrigin.Y:F4}, {sourceOrigin.Z:F4})");
                _logger.LogInformation($"║ Source Direction:      ({sourceDirection.X:F4}, {sourceDirection.Y:F4}, {sourceDirection.Z:F4})");
                _logger.LogInformation($"║ Source Up:             ({sourceUpDirection.X:F4}, {sourceUpDirection.Y:F4}, {sourceUpDirection.Z:F4})");
                _logger.LogInformation($"║ Source Right:          ({sourceRightDirection.X:F4}, {sourceRightDirection.Y:F4}, {sourceRightDirection.Z:F4})");
                _logger.LogInformation($"║ CropBox Origin:        ({srcTransform.Origin.X:F4}, {srcTransform.Origin.Y:F4}, {srcTransform.Origin.Z:F4})");
                _logger.LogInformation($"║ CropBox BasisX:        ({srcTransform.BasisX.X:F4}, {srcTransform.BasisX.Y:F4}, {srcTransform.BasisX.Z:F4})");
                _logger.LogInformation($"║ CropBox BasisY:        ({srcTransform.BasisY.X:F4}, {srcTransform.BasisY.Y:F4}, {srcTransform.BasisY.Z:F4})");
                _logger.LogInformation($"║ CropBox BasisZ:        ({srcTransform.BasisZ.X:F4}, {srcTransform.BasisZ.Y:F4}, {srcTransform.BasisZ.Z:F4})");
                _logger.LogInformation($"║ CropBox Min:           ({sourceBoundingBox.Min.X:F4}, {sourceBoundingBox.Min.Y:F4}, {sourceBoundingBox.Min.Z:F4})");
                _logger.LogInformation($"║ CropBox Max:           ({sourceBoundingBox.Max.X:F4}, {sourceBoundingBox.Max.Y:F4}, {sourceBoundingBox.Max.Z:F4})");
                _logger.LogInformation($"╚══════════════════════════════════════════════════════════════════");
                _logger.LogInformation($"");

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

                // Store the SOURCE crop box dimensions BEFORE creating the view
                // The View Template can override crop settings, so we need to restore them after template application
                // We must store the SOURCE dimensions because CreateSectionView normalizes them to (0,0,0)/(width,height,depth)
                var sourceCropMin = sourceBoundingBox.Min;
                var sourceCropMax = sourceBoundingBox.Max;
                _logger.LogInformation($"=== STORING SOURCE CROP BOX (from linked view) ===");
                _logger.LogInformation($"Source Min: ({sourceCropMin.X:F6}, {sourceCropMin.Y:F6}, {sourceCropMin.Z:F6})");
                _logger.LogInformation($"Source Max: ({sourceCropMax.X:F6}, {sourceCropMax.Y:F6}, {sourceCropMax.Z:F6})");

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

                // Copy View Template if source has one (transfer if needed)
                CopyViewTemplate(hostDoc, sourceView, newSection, linkedDoc);

                // CRITICAL: Restore SOURCE crop box AND scale AFTER template application
                // The View Template may override both crop region and scale settings
                // We need to restore both to maintain the correct view extent and viewport size
                try
                {
                    _logger.LogInformation($"=== RESTORING SOURCE CROP BOX (after template) ===");
                    _logger.LogInformation($"Restoring to source dimensions:");
                    _logger.LogInformation($"  Min: ({sourceCropMin.X:F6}, {sourceCropMin.Y:F6}, {sourceCropMin.Z:F6})");
                    _logger.LogInformation($"  Max: ({sourceCropMax.X:F6}, {sourceCropMax.Y:F6}, {sourceCropMax.Z:F6})");

                    var currentCropBox = newSection.CropBox;
                    currentCropBox.Min = sourceCropMin;
                    currentCropBox.Max = sourceCropMax;
                    newSection.CropBox = currentCropBox;

                    _logger.LogInformation($"✓ Successfully restored SOURCE crop box after template application");
                    _logger.LogInformation($"  Final Min: ({newSection.CropBox.Min.X:F6}, {newSection.CropBox.Min.Y:F6}, {newSection.CropBox.Min.Z:F6})");
                    _logger.LogInformation($"  Final Max: ({newSection.CropBox.Max.X:F6}, {newSection.CropBox.Max.Y:F6}, {newSection.CropBox.Max.Z:F6})");

                    // Verify the dimensions match
                    var finalMin = newSection.CropBox.Min;
                    var finalMax = newSection.CropBox.Max;
                    var minMatch = Math.Abs(finalMin.X - sourceCropMin.X) < 0.001 &&
                                  Math.Abs(finalMin.Y - sourceCropMin.Y) < 0.001 &&
                                  Math.Abs(finalMin.Z - sourceCropMin.Z) < 0.001;
                    var maxMatch = Math.Abs(finalMax.X - sourceCropMax.X) < 0.001 &&
                                  Math.Abs(finalMax.Y - sourceCropMax.Y) < 0.001 &&
                                  Math.Abs(finalMax.Z - sourceCropMax.Z) < 0.001;

                    if (minMatch && maxMatch)
                    {
                        _logger.LogInformation($"✓✓✓ CROP BOX DIMENSIONS MATCH SOURCE EXACTLY");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ CROP BOX DIMENSIONS DO NOT MATCH SOURCE");
                        _logger.LogWarning($"  Min match: {minMatch}, Max match: {maxMatch}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠ Could not restore crop box after template: {ex.Message}");
                }

                // CRITICAL: Re-apply SOURCE scale AFTER template application
                // The View Template may override the scale, which affects viewport size on sheets
                try
                {
                    var sourceScale = sourceView.Scale;
                    var currentScale = newSection.Scale;

                    _logger.LogInformation($"=== RESTORING SOURCE SCALE (after template) ===");
                    _logger.LogInformation($"Source scale: {sourceScale}");
                    _logger.LogInformation($"Current scale (after template): {currentScale}");

                    if (sourceScale != currentScale)
                    {
                        _logger.LogWarning($"⚠️ View Template changed the scale from {sourceScale} to {currentScale}");
                        _logger.LogInformation($"Restoring source scale: {sourceScale}");
                        newSection.Scale = sourceScale;
                        _logger.LogInformation($"✓ Successfully restored source scale to: {newSection.Scale}");
                    }
                    else
                    {
                        _logger.LogInformation($"✓ Scale unchanged by template (already {sourceScale})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠ Could not restore scale after template: {ex.Message}");
                }

                // Final comparison logging to help identify position issues
                _logger.LogInformation($"");
                _logger.LogInformation($"╔══════════════════════════════════════════════════════════════════");
                _logger.LogInformation($"║ POSITION COMPARISON: Source vs Created");
                _logger.LogInformation($"╠══════════════════════════════════════════════════════════════════");
                _logger.LogInformation($"║ EXPECTED (Source in Linked File):                                ");
                _logger.LogInformation($"║   Origin:            ({sourceOrigin.X:F4}, {sourceOrigin.Y:F4}, {sourceOrigin.Z:F4})");
                _logger.LogInformation($"║   Direction:         ({sourceDirection.X:F4}, {sourceDirection.Y:F4}, {sourceDirection.Z:F4})");
                _logger.LogInformation($"║   CropBox Origin:    ({srcTransform.Origin.X:F4}, {srcTransform.Origin.Y:F4}, {srcTransform.Origin.Z:F4})");
                _logger.LogInformation($"║                                                                      ");
                _logger.LogInformation($"║ ACTUAL (Created in Host Document):                                ");
                _logger.LogInformation($"║   Origin:            ({newSection.Origin.X:F4}, {newSection.Origin.Y:F4}, {newSection.Origin.Z:F4})");
                _logger.LogInformation($"║   Direction:         ({newSection.ViewDirection.X:F4}, {newSection.ViewDirection.Y:F4}, {newSection.ViewDirection.Z:F4})");
                _logger.LogInformation($"║   CropBox Origin:    ({newSection.CropBox.Transform.Origin.X:F4}, {newSection.CropBox.Transform.Origin.Y:F4}, {newSection.CropBox.Transform.Origin.Z:F4})");
                _logger.LogInformation($"║                                                                      ");

                // Calculate differences
                var originDiff = new XYZ(
                    Math.Abs(newSection.Origin.X - sourceOrigin.X),
                    Math.Abs(newSection.Origin.Y - sourceOrigin.Y),
                    Math.Abs(newSection.Origin.Z - sourceOrigin.Z)
                );
                var cropBoxOriginDiff = new XYZ(
                    Math.Abs(newSection.CropBox.Transform.Origin.X - srcTransform.Origin.X),
                    Math.Abs(newSection.CropBox.Transform.Origin.Y - srcTransform.Origin.Y),
                    Math.Abs(newSection.CropBox.Transform.Origin.Z - srcTransform.Origin.Z)
                );

                _logger.LogInformation($"║ DIFFERENCES:                                                         ");
                _logger.LogInformation($"║   Origin Δ:          ({originDiff.X:F4}, {originDiff.Y:F4}, {originDiff.Z:F4})");
                _logger.LogInformation($"║   CropBox Origin Δ:  ({cropBoxOriginDiff.X:F4}, {cropBoxOriginDiff.Y:F4}, {cropBoxOriginDiff.Z:F4})");
                _logger.LogInformation($"║                                                                      ");

                // Check if link transform was applied
                if (linkTransform != null && !linkTransform.IsIdentity)
                {
                    _logger.LogInformation($"║ Link Transform Applied: YES                                          ");
                    _logger.LogInformation($"║   Link Offset:       ({linkTransform.Origin.X:F4}, {linkTransform.Origin.Y:F4}, {linkTransform.Origin.Z:F4})");

                    // Calculate what the expected position should be with link transform
                    var expectedOriginWithLink = linkTransform.OfPoint(sourceOrigin);
                    var expectedCropBoxOriginWithLink = linkTransform.OfPoint(srcTransform.Origin);

                    _logger.LogInformation($"║                                                                      ");
                    _logger.LogInformation($"║ EXPECTED WITH LINK TRANSFORM:                                       ");
                    _logger.LogInformation($"║   Expected Origin:   ({expectedOriginWithLink.X:F4}, {expectedOriginWithLink.Y:F4}, {expectedOriginWithLink.Z:F4})");
                    _logger.LogInformation($"║   Expected CropBox:  ({expectedCropBoxOriginWithLink.X:F4}, {expectedCropBoxOriginWithLink.Y:F4}, {expectedCropBoxOriginWithLink.Z:F4})");

                    var adjustedOriginDiff = new XYZ(
                        Math.Abs(newSection.Origin.X - expectedOriginWithLink.X),
                        Math.Abs(newSection.Origin.Y - expectedOriginWithLink.Y),
                        Math.Abs(newSection.Origin.Z - expectedOriginWithLink.Z)
                    );
                    _logger.LogInformation($"║   Δ from expected:   ({adjustedOriginDiff.X:F4}, {adjustedOriginDiff.Y:F4}, {adjustedOriginDiff.Z:F4})");
                }
                else
                {
                    _logger.LogInformation($"║ Link Transform Applied: NO (Identity transform)                      ");
                }

                _logger.LogInformation($"╚══════════════════════════════════════════════════════════════════");
                _logger.LogInformation($"");

                _logger.LogInformation($"✓ Successfully cloned view: {newSection.Name}");
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
            _logger.LogInformation($"");
            _logger.LogInformation($"▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓");
            _logger.LogInformation($"▓▓▓ APPLYING POSITIONING MODE: {positioningMode}");
            _logger.LogInformation($"▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓");
            _logger.LogInformation($"");

            Transform finalTransform;

            switch (positioningMode)
            {
                case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                case ViewPositioningMode.BySharedCoordinates:
                    // For shared coordinates, use the view's coordinates as-is from the linked file
                    _logger.LogInformation($"⚠️⚠️⚠️ USING SHARED COORDINATES MODE - NO LINK TRANSFORM APPLIED ⚠️⚠️⚠️");
                    _logger.LogInformation($"View origin: {srcTransform.Origin}");
                    _logger.LogInformation($"View direction: BasisZ = {srcTransform.BasisZ}");

                    finalTransform = srcTransform;
                    break;

                case ViewPositioningMode.InternalOriginToInternalOrigin:
                default:
                    // Apply link transform to position view relative to link placement
                    _logger.LogInformation($"✓✓✓ USING INTERNAL ORIGIN MODE - APPLYING LINK TRANSFORM ✓✓✓");
                    _logger.LogInformation($"Link transform origin offset: {linkTransform.Origin}");
                    _logger.LogInformation($"View cropbox origin (in linked file): {srcTransform.Origin}");

                    // Log link transform details
                    _logger.LogInformation($"");
                    _logger.LogInformation($"Link Transform Details:");
                    _logger.LogInformation($"  Origin: ({linkTransform.Origin.X:F4}, {linkTransform.Origin.Y:F4}, {linkTransform.Origin.Z:F4})");
                    _logger.LogInformation($"  BasisX: ({linkTransform.BasisX.X:F4}, {linkTransform.BasisX.Y:F4}, {linkTransform.BasisX.Z:F4})");
                    _logger.LogInformation($"  BasisY: ({linkTransform.BasisY.X:F4}, {linkTransform.BasisY.Y:F4}, {linkTransform.BasisY.Z:F4})");
                    _logger.LogInformation($"  BasisZ: ({linkTransform.BasisZ.X:F4}, {linkTransform.BasisZ.Y:F4}, {linkTransform.BasisZ.Z:F4})");
                    _logger.LogInformation($"  IsIdentity: {linkTransform.IsIdentity}");

                    _logger.LogInformation($"");
                    _logger.LogInformation($"Source Transform Details:");
                    _logger.LogInformation($"  Origin: ({srcTransform.Origin.X:F4}, {srcTransform.Origin.Y:F4}, {srcTransform.Origin.Z:F4})");
                    _logger.LogInformation($"  BasisX: ({srcTransform.BasisX.X:F4}, {srcTransform.BasisX.Y:F4}, {srcTransform.BasisX.Z:F4})");
                    _logger.LogInformation($"  BasisY: ({srcTransform.BasisY.X:F4}, {srcTransform.BasisY.Y:F4}, {srcTransform.BasisY.Z:F4})");
                    _logger.LogInformation($"  BasisZ: ({srcTransform.BasisZ.X:F4}, {srcTransform.BasisZ.Y:F4}, {srcTransform.BasisZ.Z:F4})");

                    // CRITICAL FIX: For section views, we need to transform the cropbox transform
                    // into the host document's coordinate system by applying the link transform
                    // to both the origin (position) and the basis vectors (orientation)
                    //
                    // NOTE: We do NOT use linkTransform.Multiply(srcTransform) because that's for
                    // composing transforms, not for transforming a view's position/orientation.
                    //
                    // Instead, we need to:
                    // 1. Transform the origin point using OfPoint()
                    // 2. Transform the orientation vectors using OfVector()

                    finalTransform = Transform.Identity;
                    finalTransform.Origin = linkTransform.OfPoint(srcTransform.Origin);
                    finalTransform.BasisX = linkTransform.OfVector(srcTransform.BasisX);
                    finalTransform.BasisY = linkTransform.OfVector(srcTransform.BasisY);
                    finalTransform.BasisZ = linkTransform.OfVector(srcTransform.BasisZ);

                    _logger.LogInformation($"");
                    _logger.LogInformation($"After Transforming CropBox Transform:");
                    _logger.LogInformation($"  Transformed Origin: ({finalTransform.Origin.X:F4}, {finalTransform.Origin.Y:F4}, {finalTransform.Origin.Z:F4})");
                    _logger.LogInformation($"  Transformed BasisX: ({finalTransform.BasisX.X:F4}, {finalTransform.BasisX.Y:F4}, {finalTransform.BasisX.Z:F4})");
                    _logger.LogInformation($"  Transformed BasisY: ({finalTransform.BasisY.X:F4}, {finalTransform.BasisY.Y:F4}, {finalTransform.BasisY.Z:F4})");
                    _logger.LogInformation($"  Transformed BasisZ: ({finalTransform.BasisZ.X:F4}, {finalTransform.BasisZ.Y:F4}, {finalTransform.BasisZ.Z:F4})");

                    _logger.LogInformation($"Composed origin (in host): {finalTransform.Origin}");
                    break;
            }

            _logger.LogInformation($"");
            _logger.LogInformation($"╔══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"║ FINAL TRANSFORM (Regular View)");
            _logger.LogInformation($"╠══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"║ Final Origin:          ({finalTransform.Origin.X:F4}, {finalTransform.Origin.Y:F4}, {finalTransform.Origin.Z:F4})");
            _logger.LogInformation($"║ Final BasisX:          ({finalTransform.BasisX.X:F4}, {finalTransform.BasisX.Y:F4}, {finalTransform.BasisX.Z:F4})");
            _logger.LogInformation($"║ Final BasisY:          ({finalTransform.BasisY.X:F4}, {finalTransform.BasisY.Y:F4}, {finalTransform.BasisY.Z:F4})");
            _logger.LogInformation($"║ Final BasisZ:          ({finalTransform.BasisZ.X:F4}, {finalTransform.BasisZ.Y:F4}, {finalTransform.BasisZ.Z:F4})");
            _logger.LogInformation($"╚══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"");

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

            _logger.LogInformation($"");
            _logger.LogInformation($"╔══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"║ FINAL TRANSFORM (Callout View)");
            _logger.LogInformation($"╠══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"║ Final Origin:          ({finalTransform.Origin.X:F4}, {finalTransform.Origin.Y:F4}, {finalTransform.Origin.Z:F4})");
            _logger.LogInformation($"║ Final BasisX:          ({finalTransform.BasisX.X:F4}, {finalTransform.BasisX.Y:F4}, {finalTransform.BasisX.Z:F4})");
            _logger.LogInformation($"║ Final BasisY:          ({finalTransform.BasisY.X:F4}, {finalTransform.BasisY.Y:F4}, {finalTransform.BasisY.Z:F4})");
            _logger.LogInformation($"║ Final BasisZ:          ({finalTransform.BasisZ.X:F4}, {finalTransform.BasisZ.Y:F4}, {finalTransform.BasisZ.Z:F4})");
            _logger.LogInformation($"╚══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"");

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
            // - Use the full composed transform from finalTransform (includes link transform!)
            // - Flip BasisZ to compensate for Revit's reversal
            // IMPORTANT: Cannot use Transform.CreateTranslation() as it discards rotation information!
            // We need to preserve the full transform composition from linkTransform.Multiply(srcTransform)
            Transform adjustedTransform = Transform.Identity;
            adjustedTransform.Origin = finalTransform.Origin;      // Use composed origin
            adjustedTransform.BasisX = finalTransform.BasisX;      // Use composed BasisX
            adjustedTransform.BasisY = finalTransform.BasisY;      // Use composed BasisY
            adjustedTransform.BasisZ = -finalTransform.BasisZ;     // PRE-FLIP to compensate for Revit's reversal

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
            var createdCropBox = newSection.CropBox;
            _logger.LogInformation($"");
            _logger.LogInformation($"╔══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"║ CREATED VIEW PROPERTIES (in Host Document)");
            _logger.LogInformation($"╠══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"║ Created Origin:        ({newSection.Origin.X:F4}, {newSection.Origin.Y:F4}, {newSection.Origin.Z:F4})");
            _logger.LogInformation($"║ Created Direction:     ({newSection.ViewDirection.X:F4}, {newSection.ViewDirection.Y:F4}, {newSection.ViewDirection.Z:F4})");
            _logger.LogInformation($"║ Created Up:            ({newSection.UpDirection.X:F4}, {newSection.UpDirection.Y:F4}, {newSection.UpDirection.Z:F4})");
            _logger.LogInformation($"║ Created Right:         ({newSection.RightDirection.X:F4}, {newSection.RightDirection.Y:F4}, {newSection.RightDirection.Z:F4})");
            _logger.LogInformation($"║ CropBox Origin:        ({createdCropBox.Transform.Origin.X:F4}, {createdCropBox.Transform.Origin.Y:F4}, {createdCropBox.Transform.Origin.Z:F4})");
            _logger.LogInformation($"║ CropBox BasisX:        ({createdCropBox.Transform.BasisX.X:F4}, {createdCropBox.Transform.BasisX.Y:F4}, {createdCropBox.Transform.BasisX.Z:F4})");
            _logger.LogInformation($"║ CropBox BasisY:        ({createdCropBox.Transform.BasisY.X:F4}, {createdCropBox.Transform.BasisY.Y:F4}, {createdCropBox.Transform.BasisY.Z:F4})");
            _logger.LogInformation($"║ CropBox BasisZ:        ({createdCropBox.Transform.BasisZ.X:F4}, {createdCropBox.Transform.BasisZ.Y:F4}, {createdCropBox.Transform.BasisZ.Z:F4})");
            _logger.LogInformation($"║ CropBox Min:           ({createdCropBox.Min.X:F4}, {createdCropBox.Min.Y:F4}, {createdCropBox.Min.Z:F4})");
            _logger.LogInformation($"║ CropBox Max:           ({createdCropBox.Max.X:F4}, {createdCropBox.Max.Y:F4}, {createdCropBox.Max.Z:F4})");
            _logger.LogInformation($"╚══════════════════════════════════════════════════════════════════");
            _logger.LogInformation($"");

            // NOTE: Crop box restoration has been moved to AFTER template application
            // in the CloneView method, because View Templates can override crop settings.
            // Restoring here (before template) would be overwritten by the template.

            return newSection;
        }
    }
}
