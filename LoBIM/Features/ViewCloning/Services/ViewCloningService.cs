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

        public ElementId? CloneView(Document hostDoc, LinkedViewInfo viewInfo, string namePrefix = "")
        {
            try
            {
                var sourceView = viewInfo.View;
                var linkedDoc = viewInfo.ParentLink.LinkedDocument;

                _logger.LogInformation($"Cloning view: {sourceView.Name} from {linkedDoc.Title}");

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
                        newView = CloneSectionView(hostDoc, sourceView, namePrefix);
                        break;

                    case ViewType.Elevation:
                        newView = CloneElevationView(hostDoc, sourceView, namePrefix);
                        break;

                    case ViewType.ThreeD:
                        newView = Clone3DView(hostDoc, sourceView, namePrefix);
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

        public int CloneViews(Document hostDoc, List<LinkedViewInfo> viewsToClone, string namePrefix = "")
        {
            int successCount = 0;

            using (var transaction = new Transaction(hostDoc, "Clone Views from Linked Files"))
            {
                transaction.Start();

                foreach (var viewInfo in viewsToClone)
                {
                    var result = CloneView(hostDoc, viewInfo, namePrefix);
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

        private Autodesk.Revit.DB.View CloneSectionView(Document hostDoc, Autodesk.Revit.DB.View sourceView, string namePrefix)
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

                // Get the view direction and origin
                var sourceOrigin = sourceSection.Origin;
                var sourceDirection = sourceSection.ViewDirection;
                var sourceUpDirection = sourceSection.UpDirection;
                var sourceRightDirection = sourceSection.RightDirection;

                // Find matching view family type in host document
                var viewFamilyTypeId = sourceView.GetTypeId();
                var viewFamilyType = sourceView.Document.GetElement(viewFamilyTypeId) as ViewFamilyType;

                var hostViewFamilyType = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Section);

                if (hostViewFamilyType == null)
                {
                    _logger.LogWarning($"Could not find Section view family type in host document");
                    return null;
                }

                // Create a bounding box for the section in the host document
                var transform = Transform.Identity;
                transform.Origin = sourceOrigin;
                transform.BasisX = sourceRightDirection;
                transform.BasisY = sourceUpDirection;
                transform.BasisZ = sourceDirection;

                var min = sourceBoundingBox.Min;
                var max = sourceBoundingBox.Max;

                // Create the section box
                var sectionBox = new BoundingBoxXYZ
                {
                    Transform = transform,
                    Min = min,
                    Max = max
                };

                // Create the section view
                var newSection = ViewSection.CreateSection(hostDoc, hostViewFamilyType.Id, sectionBox);

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

        private Autodesk.Revit.DB.View CloneElevationView(Document hostDoc, Autodesk.Revit.DB.View sourceView, string namePrefix)
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

                // Get the view direction and origin
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

                // Create a bounding box for the elevation in the host document
                var transform = Transform.Identity;
                transform.Origin = sourceOrigin;
                transform.BasisX = sourceRightDirection;
                transform.BasisY = sourceUpDirection;
                transform.BasisZ = sourceDirection;

                var min = sourceBoundingBox.Min;
                var max = sourceBoundingBox.Max;

                // Create the elevation box
                var elevationBox = new BoundingBoxXYZ
                {
                    Transform = transform,
                    Min = min,
                    Max = max
                };

                // Create the elevation view using the section creation method
                // (Elevations are technically sections in Revit API)
                var newElevation = ViewSection.CreateSection(hostDoc, hostViewFamilyType.Id, elevationBox);

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
    }
}
