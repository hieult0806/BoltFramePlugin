using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Services;

namespace LoBIM.Features.ViewCloning.Services
{
    /// <summary>
    /// Service for detecting and cloning views from linked Revit files
    /// </summary>
    public class ViewCloningService : IViewCloningService
    {
        private readonly ILoggingService _logger;

        public ViewCloningService(ILoggingService logger)
        {
            _logger = logger;
        }

        public List<LinkedFileInfo> GetLinkedFiles(Document doc)
        {
            var linkedFiles = new List<LinkedFileInfo>();

            try
            {
                // Find all RevitLinkInstance elements
                var linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(link => link.GetLinkDocument() != null)
                    .ToList();

                foreach (var linkInstance in linkInstances)
                {
                    try
                    {
                        var linkedDoc = linkInstance.GetLinkDocument();
                        if (linkedDoc == null) continue;

                        var linkInfo = new LinkedFileInfo
                        {
                            LinkInstance = linkInstance,
                            LinkedDocument = linkedDoc,
                            FileName = linkedDoc.Title,
                            FilePath = linkedDoc.PathName,
                            LinkId = linkInstance.Id
                        };

                        // Get views from the linked document
                        linkInfo.Views = GetViewsFromLinkedFile(linkedDoc, linkInfo);

                        linkedFiles.Add(linkInfo);

                        _logger.LogInformation($"Found linked file: {linkInfo.FileName} with {linkInfo.ViewCount} views");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing linked file: {ex.Message}", ex);
                    }
                }

                _logger.LogInformation($"Total linked files found: {linkedFiles.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting linked files: {ex.Message}", ex);
            }

            return linkedFiles;
        }

        public List<LinkedViewInfo> GetViewsFromLinkedFile(Document linkedDoc, LinkedFileInfo parentLink)
        {
            var views = new List<LinkedViewInfo>();

            try
            {
                // Get all views from the linked document (excluding templates and sheets)
                var viewCollector = new FilteredElementCollector(linkedDoc)
                    .OfClass(typeof(Autodesk.Revit.DB.View))
                    .Cast<Autodesk.Revit.DB.View>()
                    .Where(v => !v.IsTemplate &&
                                v.ViewType != ViewType.DrawingSheet &&
                                v.ViewType != ViewType.ProjectBrowser &&
                                v.ViewType != ViewType.SystemBrowser &&
                                v.CanBePrinted) // Only printable views
                    .ToList();

                foreach (var view in viewCollector)
                {
                    try
                    {
                        var viewInfo = new LinkedViewInfo
                        {
                            View = view,
                            ParentLink = parentLink,
                            ViewName = view.Name,
                            ViewType = view.ViewType.ToString(),
                            ViewId = view.Id,
                            Scale = view.Scale,
                            IsSelected = false,
                            IsCloned = false
                        };

                        // Get level name if applicable
                        if (view.GenLevel != null)
                        {
                            viewInfo.LevelName = view.GenLevel.Name;
                        }

                        views.Add(viewInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error processing view {view.Name}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"Found {views.Count} views in linked file {linkedDoc.Title}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting views from linked file: {ex.Message}", ex);
            }

            return views;
        }

        public ElementId? CloneView(Document hostDoc, LinkedViewInfo viewInfo, string namePrefix = "", ViewPositioningMode positioningMode = ViewPositioningMode.InternalOriginToInternalOrigin)
        {
            try
            {
                var sourceView = viewInfo.View;
                var linkedDoc = viewInfo.ParentLink.LinkedDocument;
                var linkInstance = viewInfo.ParentLink.LinkInstance;

                _logger.LogInformation($"Cloning view: {sourceView.Name} from {linkedDoc.Title} using positioning mode: {positioningMode}");

                // Get the link transform based on the selected positioning mode
                Transform linkTransform = GetLinkTransform(linkInstance, positioningMode);

                // Create a view of the same type in the host document
                Autodesk.Revit.DB.View newView = null;

                switch (sourceView.ViewType)
                {
                    case ViewType.FloorPlan:
                    case ViewType.CeilingPlan:
                    case ViewType.EngineeringPlan:
                        newView = ClonePlanView(hostDoc, sourceView, namePrefix);
                        break;

                    case ViewType.Section:
                        newView = CloneSectionView(hostDoc, sourceView, namePrefix, linkTransform, positioningMode);
                        break;

                    case ViewType.Elevation:
                        newView = CloneElevationView(hostDoc, sourceView, namePrefix, linkTransform, positioningMode);
                        break;

                    case ViewType.ThreeD:
                        newView = Clone3DView(hostDoc, sourceView, namePrefix);
                        break;

                    case ViewType.DraftingView:
                    case ViewType.Detail:
                        newView = CloneDraftingView(hostDoc, sourceView, namePrefix);
                        break;

                    default:
                        _logger.LogWarning($"View type {sourceView.ViewType} not supported for cloning");
                        return null;
                }

                if (newView != null)
                {
                    // Copy view properties
                    CopyViewProperties(sourceView, newView);

                    viewInfo.IsCloned = true;
                    viewInfo.ClonedViewId = newView.Id;

                    _logger.LogInformation($"Successfully cloned view: {newView.Name} (ID: {newView.Id.Value})");
                    return newView.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning view {viewInfo.ViewName}: {ex.Message}", ex);
            }

            return null;
        }

        public int CloneViews(Document hostDoc, List<LinkedViewInfo> viewsToClone, string namePrefix = "", ViewPositioningMode positioningMode = ViewPositioningMode.InternalOriginToInternalOrigin)
        {
            int successCount = 0;

            using (var transaction = new Transaction(hostDoc, "Clone Views from Linked Files"))
            {
                transaction.Start();

                foreach (var viewInfo in viewsToClone)
                {
                    var result = CloneView(hostDoc, viewInfo, namePrefix, positioningMode);
                    if (result != null)
                    {
                        successCount++;
                    }
                }

                transaction.Commit();
            }

            _logger.LogInformation($"Cloned {successCount} out of {viewsToClone.Count} views");
            return successCount;
        }

        private Autodesk.Revit.DB.View ClonePlanView(Document hostDoc, Autodesk.Revit.DB.View sourceView, string namePrefix)
        {
            // Find a matching level in the host document
            var sourceLevel = sourceView.GenLevel;
            if (sourceLevel == null) return null;

            // Try to find a level with the same name in the host
            var targetLevel = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name == sourceLevel.Name);

            if (targetLevel == null)
            {
                _logger.LogWarning($"Level {sourceLevel.Name} not found in host document");
                return null;
            }

            // Get the view family type ID
            var viewFamilyTypeId = sourceView.GetTypeId();
            var viewFamilyType = sourceView.Document.GetElement(viewFamilyTypeId) as ViewFamilyType;

            // Find matching view family type in host document
            var hostViewFamilyType = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == viewFamilyType.ViewFamily);

            if (hostViewFamilyType == null) return null;

            // Create the view
            Autodesk.Revit.DB.View newView = ViewPlan.Create(hostDoc, hostViewFamilyType.Id, targetLevel.Id);

            // Set the view name
            string newName = string.IsNullOrEmpty(namePrefix)
                ? $"{sourceView.Name} (Cloned)"
                : $"{namePrefix}_{sourceView.Name}";

            try
            {
                newView.Name = newName;
            }
            catch
            {
                // If name already exists, add timestamp
                newView.Name = $"{newName}_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            return newView;
        }

        private Autodesk.Revit.DB.View CloneSectionView(Document hostDoc, Autodesk.Revit.DB.View sourceView, string namePrefix, Transform linkTransform, ViewPositioningMode positioningMode)
        {
            try
            {
                if (!(sourceView is ViewSection sourceSection))
                {
                    _logger.LogWarning($"Source view is not a ViewSection: {sourceView.Name}");
                    return null;
                }

                // Get the crop box from the source view
                var sourceBoundingBox = sourceSection.CropBox;
                if (sourceBoundingBox == null)
                {
                    _logger.LogWarning($"Could not get crop box from section view: {sourceView.Name}");
                    return null;
                }

                // Get the view direction and origin from the linked file
                var sourceOrigin = sourceSection.Origin;
                var sourceDirection = sourceSection.ViewDirection;
                var sourceUpDirection = sourceSection.UpDirection;
                var sourceRightDirection = sourceSection.RightDirection;

                _logger.LogInformation($"=== SOURCE SECTION VIEW DETAILS ===");
                _logger.LogInformation($"Source Origin: {sourceOrigin}");
                _logger.LogInformation($"Source Direction: {sourceDirection}");
                _logger.LogInformation($"Source Up: {sourceUpDirection}");
                _logger.LogInformation($"Source Right: {sourceRightDirection}");

                var srcTransform = sourceBoundingBox.Transform;
                _logger.LogInformation($"=== SOURCE CROPBOX TRANSFORM ===");
                _logger.LogInformation($"CropBox Origin: {srcTransform.Origin}");
                _logger.LogInformation($"CropBox BasisX: {srcTransform.BasisX}");
                _logger.LogInformation($"CropBox BasisY: {srcTransform.BasisY}");
                _logger.LogInformation($"CropBox BasisZ: {srcTransform.BasisZ}");
                _logger.LogInformation($"CropBox Scale: {srcTransform.Scale}");
                _logger.LogInformation($"CropBox Min: {sourceBoundingBox.Min}");
                _logger.LogInformation($"CropBox Max: {sourceBoundingBox.Max}");

                _logger.LogInformation($"=== LINK TRANSFORM ===");
                _logger.LogInformation($"Link Origin: {linkTransform.Origin}");
                _logger.LogInformation($"Link BasisX: {linkTransform.BasisX}");
                _logger.LogInformation($"Link BasisY: {linkTransform.BasisY}");
                _logger.LogInformation($"Link BasisZ: {linkTransform.BasisZ}");
                _logger.LogInformation($"Link Scale: {linkTransform.Scale}");

                // Find matching view family type in host document
                var hostViewFamilyType = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Section);

                if (hostViewFamilyType == null)
                {
                    _logger.LogWarning($"Could not find Section view family type in host document");
                    return null;
                }

                // Determine the final transform based on positioning mode
                Transform finalTransform;
                XYZ min = sourceBoundingBox.Min;
                XYZ max = sourceBoundingBox.Max;

                _logger.LogInformation($"=== APPLYING POSITIONING MODE: {positioningMode} ===");

                switch (positioningMode)
                {
                    case ViewPositioningMode.TestIdentity:
                        _logger.LogInformation($"TEST: Using Identity transform (origin at 0,0,0)");
                        finalTransform = Transform.Identity;
                        break;

                    case ViewPositioningMode.TestSourceTransformOnly:
                        _logger.LogInformation($"TEST: Using source cropbox transform ONLY (no link transform)");
                        _logger.LogInformation($"Source transform origin: {srcTransform.Origin}");
                        finalTransform = srcTransform;
                        break;

                    case ViewPositioningMode.TestAbsoluteWorldCoordinates:
                        _logger.LogInformation($"TEST: Creating section using absolute world coordinates");
                        // Transform the source section origin to host document coordinates
                        var transformedOrigin = linkTransform.OfPoint(sourceOrigin);
                        _logger.LogInformation($"Source section origin: {sourceOrigin}");
                        _logger.LogInformation($"Transformed section origin: {transformedOrigin}");

                        // Transform the direction vectors
                        var transformedDirection = linkTransform.OfVector(sourceDirection);
                        var transformedUp = linkTransform.OfVector(sourceUpDirection);
                        var transformedRight = linkTransform.OfVector(sourceRightDirection);

                        _logger.LogInformation($"Transformed direction: {transformedDirection}");
                        _logger.LogInformation($"Transformed up: {transformedUp}");
                        _logger.LogInformation($"Transformed right: {transformedRight}");

                        // Create a new transform at the transformed origin with transformed basis vectors
                        finalTransform = Transform.Identity;
                        finalTransform.Origin = transformedOrigin;
                        finalTransform.BasisX = transformedRight;
                        finalTransform.BasisY = transformedUp;
                        finalTransform.BasisZ = transformedDirection;
                        break;

                    case ViewPositioningMode.TestReconstructFromOrigin:
                        _logger.LogInformation($"TEST: Reconstructing section from Origin (no cropbox transform)");
                        // For ProjectBasePointToProjectBasePoint, the section Origin is already in shared coordinates
                        // Just use it directly without any transformation
                        _logger.LogInformation($"Using section origin directly: {sourceOrigin}");
                        _logger.LogInformation($"Using section direction directly: {sourceDirection}");

                        // Create transform using the section's origin and direction vectors directly
                        finalTransform = Transform.Identity;
                        finalTransform.Origin = sourceOrigin;  // Already in shared coordinates
                        finalTransform.BasisX = sourceRightDirection;
                        finalTransform.BasisY = sourceUpDirection;
                        finalTransform.BasisZ = sourceDirection;
                        break;

                    case ViewPositioningMode.TestLinkOriginOnly:
                        _logger.LogInformation($"TEST: Using source transform PLUS link origin offset");
                        // Take source transform and add link origin offset
                        var translatedOrigin = srcTransform.Origin + linkTransform.Origin;
                        _logger.LogInformation($"Source origin: {srcTransform.Origin}");
                        _logger.LogInformation($"Link origin: {linkTransform.Origin}");
                        _logger.LogInformation($"Combined origin: {translatedOrigin}");
                        // Create new transform with combined origin but source rotation
                        finalTransform = Transform.CreateTranslation(translatedOrigin - srcTransform.Origin).Multiply(srcTransform);
                        break;

                    case ViewPositioningMode.TestInverseLinkTransform:
                        _logger.LogInformation($"TEST: Using INVERSE of link transform");
                        var inverseLinkTransform = linkTransform.Inverse;
                        finalTransform = inverseLinkTransform.Multiply(srcTransform);
                        _logger.LogInformation($"Inverse link origin: {inverseLinkTransform.Origin}");
                        break;

                    case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                    case ViewPositioningMode.BySharedCoordinates:
                    case ViewPositioningMode.NoTransform:
                        _logger.LogInformation($"Using source transform as-is (shared coordinates)");
                        finalTransform = srcTransform;
                        break;

                    case ViewPositioningMode.InternalOriginToInternalOrigin:
                    case ViewPositioningMode.CenterToCenter:
                    case ViewPositioningMode.OriginToLastPlaced:
                    default:
                        _logger.LogInformation($"Composing link transform with source transform");
                        finalTransform = linkTransform.Multiply(srcTransform);
                        break;
                }

                _logger.LogInformation($"=== FINAL TRANSFORM ===");
                _logger.LogInformation($"Final Origin: {finalTransform.Origin}");
                _logger.LogInformation($"Final BasisX: {finalTransform.BasisX}");
                _logger.LogInformation($"Final BasisY: {finalTransform.BasisY}");
                _logger.LogInformation($"Final BasisZ: {finalTransform.BasisZ}");
                _logger.LogInformation($"Final Scale: {finalTransform.Scale}");

                // Calculate actual world coordinates
                var worldMin = finalTransform.OfPoint(min);
                var worldMax = finalTransform.OfPoint(max);
                _logger.LogInformation($"=== SECTION BOX FINAL ===");
                _logger.LogInformation($"Min (relative): {min}");
                _logger.LogInformation($"Max (relative): {max}");
                _logger.LogInformation($"Min (world): {worldMin}");
                _logger.LogInformation($"Max (world): {worldMax}");

                // Create the section box
                var sectionBox = new BoundingBoxXYZ
                {
                    Transform = finalTransform,
                    Min = min,
                    Max = max
                };

                // Create the section view
                var newSection = ViewSection.CreateSection(hostDoc, hostViewFamilyType.Id, sectionBox);

                // Copy the scale immediately after creation
                if (sourceView.Scale > 0)
                {
                    try
                    {
                        newSection.Scale = sourceView.Scale;
                        _logger.LogInformation($"Set scale to {sourceView.Scale} for cloned section view");
                    }
                    catch (Exception scaleEx)
                    {
                        _logger.LogWarning($"Could not set scale: {scaleEx.Message}");
                    }
                }

                // Set the view name
                string newName = string.IsNullOrEmpty(namePrefix)
                    ? $"{sourceView.Name} (Cloned)"
                    : $"{namePrefix}_{sourceView.Name}";

                try
                {
                    newSection.Name = newName;
                }
                catch
                {
                    newSection.Name = $"{newName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                _logger.LogInformation($"Successfully cloned section view: {newSection.Name}");
                return newSection;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning section view {sourceView.Name}: {ex.Message}", ex);
                return null;
            }
        }

        private Autodesk.Revit.DB.View CloneElevationView(Document hostDoc, Autodesk.Revit.DB.View sourceView, string namePrefix, Transform linkTransform, ViewPositioningMode positioningMode)
        {
            try
            {
                if (!(sourceView is ViewSection sourceElevation))
                {
                    _logger.LogWarning($"Source view is not an elevation: {sourceView.Name}");
                    return null;
                }

                // Get the crop box from the source view
                var sourceBoundingBox = sourceElevation.CropBox;
                if (sourceBoundingBox == null)
                {
                    _logger.LogWarning($"Could not get crop box from elevation view: {sourceView.Name}");
                    return null;
                }

                // Get the view properties for TestAbsoluteWorldCoordinates mode
                var sourceOrigin = sourceElevation.Origin;
                var sourceDirection = sourceElevation.ViewDirection;
                var sourceUpDirection = sourceElevation.UpDirection;
                var sourceRightDirection = sourceElevation.RightDirection;

                // Find matching view family type in host document
                var hostViewFamilyType = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Elevation);

                if (hostViewFamilyType == null)
                {
                    _logger.LogWarning($"Could not find Elevation view family type in host document");
                    return null;
                }

                // Determine the final transform based on positioning mode
                Transform finalTransform;
                var srcTransform = sourceBoundingBox.Transform;
                XYZ min = sourceBoundingBox.Min;
                XYZ max = sourceBoundingBox.Max;

                _logger.LogInformation($"=== APPLYING POSITIONING MODE FOR ELEVATION: {positioningMode} ===");

                switch (positioningMode)
                {
                    case ViewPositioningMode.TestIdentity:
                        _logger.LogInformation($"TEST: Using Identity transform (origin at 0,0,0)");
                        finalTransform = Transform.Identity;
                        break;

                    case ViewPositioningMode.TestSourceTransformOnly:
                        _logger.LogInformation($"TEST: Using source cropbox transform ONLY (no link transform)");
                        _logger.LogInformation($"Source transform origin: {srcTransform.Origin}");
                        finalTransform = srcTransform;
                        break;

                    case ViewPositioningMode.TestAbsoluteWorldCoordinates:
                        _logger.LogInformation($"TEST: Creating elevation using absolute world coordinates");
                        var transformedOriginElev = linkTransform.OfPoint(sourceOrigin);
                        var transformedDirectionElev = linkTransform.OfVector(sourceDirection);
                        var transformedUpElev = linkTransform.OfVector(sourceUpDirection);
                        var transformedRightElev = linkTransform.OfVector(sourceRightDirection);

                        _logger.LogInformation($"Source elevation origin: {sourceOrigin}");
                        _logger.LogInformation($"Transformed elevation origin: {transformedOriginElev}");

                        finalTransform = Transform.Identity;
                        finalTransform.Origin = transformedOriginElev;
                        finalTransform.BasisX = transformedRightElev;
                        finalTransform.BasisY = transformedUpElev;
                        finalTransform.BasisZ = transformedDirectionElev;
                        break;

                    case ViewPositioningMode.TestReconstructFromOrigin:
                        _logger.LogInformation($"TEST: Reconstructing elevation from Origin (no cropbox transform)");
                        _logger.LogInformation($"Using elevation origin directly: {sourceOrigin}");

                        finalTransform = Transform.Identity;
                        finalTransform.Origin = sourceOrigin;  // Already in shared coordinates
                        finalTransform.BasisX = sourceRightDirection;
                        finalTransform.BasisY = sourceUpDirection;
                        finalTransform.BasisZ = sourceDirection;
                        break;

                    case ViewPositioningMode.TestLinkOriginOnly:
                        _logger.LogInformation($"TEST: Using source transform PLUS link origin offset");
                        var translatedOrigin = srcTransform.Origin + linkTransform.Origin;
                        _logger.LogInformation($"Source origin: {srcTransform.Origin}");
                        _logger.LogInformation($"Link origin: {linkTransform.Origin}");
                        _logger.LogInformation($"Combined origin: {translatedOrigin}");
                        finalTransform = Transform.CreateTranslation(translatedOrigin - srcTransform.Origin).Multiply(srcTransform);
                        break;

                    case ViewPositioningMode.TestInverseLinkTransform:
                        _logger.LogInformation($"TEST: Using INVERSE of link transform");
                        var inverseLinkTransform = linkTransform.Inverse;
                        finalTransform = inverseLinkTransform.Multiply(srcTransform);
                        _logger.LogInformation($"Inverse link origin: {inverseLinkTransform.Origin}");
                        break;

                    case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                    case ViewPositioningMode.BySharedCoordinates:
                    case ViewPositioningMode.NoTransform:
                        _logger.LogInformation($"Using source transform as-is (shared coordinates)");
                        finalTransform = srcTransform;
                        break;

                    case ViewPositioningMode.InternalOriginToInternalOrigin:
                    case ViewPositioningMode.CenterToCenter:
                    case ViewPositioningMode.OriginToLastPlaced:
                    default:
                        _logger.LogInformation($"Composing link transform with source transform");
                        finalTransform = linkTransform.Multiply(srcTransform);
                        break;
                }

                _logger.LogInformation($"Elevation final transform - Origin: {finalTransform.Origin}");

                // Create the elevation box
                var elevationBox = new BoundingBoxXYZ
                {
                    Transform = finalTransform,
                    Min = min,
                    Max = max
                };

                // Create the elevation view using the section creation method
                // (Elevations are technically sections in Revit API)
                var newElevation = ViewSection.CreateSection(hostDoc, hostViewFamilyType.Id, elevationBox);

                // Copy the scale immediately after creation
                if (sourceView.Scale > 0)
                {
                    try
                    {
                        newElevation.Scale = sourceView.Scale;
                        _logger.LogInformation($"Set scale to {sourceView.Scale} for cloned elevation view");
                    }
                    catch (Exception scaleEx)
                    {
                        _logger.LogWarning($"Could not set scale: {scaleEx.Message}");
                    }
                }

                // Set the view name
                string newName = string.IsNullOrEmpty(namePrefix)
                    ? $"{sourceView.Name} (Cloned)"
                    : $"{namePrefix}_{sourceView.Name}";

                try
                {
                    newElevation.Name = newName;
                }
                catch
                {
                    newElevation.Name = $"{newName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                _logger.LogInformation($"Successfully cloned elevation view: {newElevation.Name}");
                return newElevation;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning elevation view {sourceView.Name}: {ex.Message}", ex);
                return null;
            }
        }

        private Autodesk.Revit.DB.View Clone3DView(Document hostDoc, Autodesk.Revit.DB.View sourceView, string namePrefix)
        {
            // Get the 3D view type
            var view3DType = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

            if (view3DType == null) return null;

            // Create 3D view
            var new3DView = View3D.CreateIsometric(hostDoc, view3DType.Id);

            // Set the view name
            string newName = string.IsNullOrEmpty(namePrefix)
                ? $"{sourceView.Name} (Cloned)"
                : $"{namePrefix}_{sourceView.Name}";

            try
            {
                new3DView.Name = newName;
            }
            catch
            {
                new3DView.Name = $"{newName}_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            return new3DView;
        }

        private Autodesk.Revit.DB.View CloneDraftingView(Document hostDoc, Autodesk.Revit.DB.View sourceView, string namePrefix)
        {
            try
            {
                // Get the drafting view type
                var draftingViewType = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

                if (draftingViewType == null)
                {
                    _logger.LogWarning($"Could not find Drafting view family type in host document");
                    return null;
                }

                // Create drafting view
                var newDraftingView = ViewDrafting.Create(hostDoc, draftingViewType.Id);

                // Set the view name
                string newName = string.IsNullOrEmpty(namePrefix)
                    ? $"{sourceView.Name} (Cloned)"
                    : $"{namePrefix}_{sourceView.Name}";

                try
                {
                    newDraftingView.Name = newName;
                }
                catch
                {
                    newDraftingView.Name = $"{newName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                _logger.LogInformation($"Successfully cloned drafting view: {newDraftingView.Name}");
                return newDraftingView;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning drafting view {sourceView.Name}: {ex.Message}", ex);
                return null;
            }
        }

        private void CopyViewProperties(Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View targetView)
        {
            try
            {
                // Copy scale
                if (sourceView.Scale > 0)
                {
                    targetView.Scale = sourceView.Scale;
                }

                // Copy detail level
                targetView.DetailLevel = sourceView.DetailLevel;

                // Copy display style (if applicable for 3D views)
                if (sourceView is View3D sourceView3D && targetView is View3D targetView3D)
                {
                    targetView3D.DisplayStyle = sourceView3D.DisplayStyle;
                }

                _logger.LogInformation($"Copied view properties from {sourceView.Name} to {targetView.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error copying some view properties: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the appropriate transform based on the positioning mode
        /// </summary>
        private Transform GetLinkTransform(RevitLinkInstance linkInstance, ViewPositioningMode positioningMode)
        {
            try
            {
                _logger.LogInformation($"Getting link transform for mode: {positioningMode}");

                switch (positioningMode)
                {
                    case ViewPositioningMode.InternalOriginToInternalOrigin:
                        // This is the default - uses the link's placement transform
                        var transform1 = linkInstance.GetTransform();
                        _logger.LogInformation($"InternalOrigin transform - Origin: {transform1.Origin}, Scale: {transform1.Scale}");
                        return transform1;

                    case ViewPositioningMode.CenterToCenter:
                        // Get the center to center transform
                        var transform2 = linkInstance.GetTotalTransform();
                        _logger.LogInformation($"CenterToCenter transform - Origin: {transform2.Origin}, Scale: {transform2.Scale}");
                        return transform2;

                    case ViewPositioningMode.BySharedCoordinates:
                        // For shared coordinates, don't transform at all
                        // The views are already in the same coordinate system
                        _logger.LogInformation($"BySharedCoordinates - Using Identity transform");
                        return Transform.Identity;

                    case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                        // For Project Base Point, we need to account for the difference
                        // between the link's project base point and the host's project base point
                        // GetTotalTransform includes the effect of shared coordinates
                        var transform4 = linkInstance.GetTotalTransform();
                        _logger.LogInformation($"ProjectBasePoint transform - Origin: {transform4.Origin}, Scale: {transform4.Scale}");
                        return transform4;

                    case ViewPositioningMode.OriginToLastPlaced:
                        // Use the placement transform (same as Internal Origin)
                        var transform5 = linkInstance.GetTransform();
                        _logger.LogInformation($"OriginToLastPlaced transform - Origin: {transform5.Origin}, Scale: {transform5.Scale}");
                        return transform5;

                    case ViewPositioningMode.NoTransform:
                        // Don't apply any transformation - use the exact coordinates from the section view
                        _logger.LogInformation($"NoTransform - Using Identity transform (no coordinate adjustment)");
                        return Transform.Identity;

                    case ViewPositioningMode.TestIdentity:
                    case ViewPositioningMode.TestLinkOriginOnly:
                    case ViewPositioningMode.TestInverseLinkTransform:
                    case ViewPositioningMode.TestSourceTransformOnly:
                    case ViewPositioningMode.TestAbsoluteWorldCoordinates:
                    case ViewPositioningMode.TestReconstructFromOrigin:
                        // For test modes, return the actual link transform
                        // The test logic is applied in CloneSectionView
                        var testTransform = linkInstance.GetTransform();
                        _logger.LogInformation($"Test mode - returning link transform for processing");
                        return testTransform;

                    default:
                        _logger.LogWarning($"Unknown positioning mode: {positioningMode}, using default");
                        return linkInstance.GetTransform();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting link transform for mode {positioningMode}: {ex.Message}", ex);
                return linkInstance.GetTransform(); // Fallback to default
            }
        }
    }
}
