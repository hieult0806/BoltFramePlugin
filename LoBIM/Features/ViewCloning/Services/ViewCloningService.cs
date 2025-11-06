using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Factories;
using LoBIM.Services;

namespace LoBIM.Features.ViewCloning.Services
{
    /// <summary>
    /// Service for detecting and cloning views from linked Revit files
    /// Uses Factory pattern to delegate view cloning to specialized strategies
    /// </summary>
    public class ViewCloningService : IViewCloningService
    {
        private readonly ILoggingService _logger;
        private readonly ViewCloningStrategyFactory _strategyFactory;

        public ViewCloningService(ILoggingService logger)
        {
            _logger = logger;
            _strategyFactory = new ViewCloningStrategyFactory(logger);
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

                // CRITICAL: For ALL callout views, ensure parent view exists first
                // Callouts can be any view type (ViewSection, ViewPlan, etc.)
                if (sourceView.IsCallout)
                {
                    _logger.LogInformation($"=== CALLOUT PARENT DEPENDENCY CHECK ===");
                    _logger.LogInformation($"View '{sourceView.Name}' is a CALLOUT (Type: {sourceView.GetType().Name})");

                    ElementId calloutParentId = GetCalloutParentId(sourceView);

                    if (calloutParentId != null && calloutParentId != ElementId.InvalidElementId)
                    {
                        // Get parent view from linked document
                        var parentView = linkedDoc.GetElement(calloutParentId) as Autodesk.Revit.DB.View;
                        if (parentView != null)
                        {
                            _logger.LogInformation($"Parent view found: {parentView.Name} (ID: {calloutParentId.Value}, Type: {parentView.GetType().Name})");

                            // Check if parent already exists in host document
                            string expectedParentName = namePrefix + parentView.Name;
                            bool parentExistsInHost = new FilteredElementCollector(hostDoc)
                                .OfClass(typeof(Autodesk.Revit.DB.View))
                                .Cast<Autodesk.Revit.DB.View>()
                                .Any(v => v.Name == expectedParentName || v.Name == parentView.Name);

                            if (!parentExistsInHost)
                            {
                                _logger.LogInformation($"Parent view does not exist in host document - cloning parent first");

                                // Create LinkedViewInfo for parent and clone it recursively
                                var parentViewInfo = new LinkedViewInfo
                                {
                                    View = parentView,
                                    ViewName = parentView.Name,
                                    ViewType = parentView.ViewType.ToString(),
                                    ParentLink = viewInfo.ParentLink
                                };

                                var parentClonedId = CloneView(hostDoc, parentViewInfo, namePrefix, positioningMode);
                                if (parentClonedId != null)
                                {
                                    _logger.LogInformation($"Parent view cloned successfully with ID: {parentClonedId.Value}");
                                }
                                else
                                {
                                    _logger.LogWarning($"Failed to clone parent view - callout positioning may be incorrect");
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"Parent view already exists in host document");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Could not find parent view with ID: {calloutParentId.Value}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Could not get parent ID for callout view: {sourceView.Name}");
                    }
                }

                // Use factory pattern to get the appropriate cloning strategy
                // IMPORTANT: Pass the view itself (not just ViewType) so factory can check if it's a callout
                var strategy = _strategyFactory.GetStrategy(sourceView);

                if (strategy == null)
                {
                    _logger.LogWarning($"No cloning strategy available for view: {sourceView.Name} (Type: {sourceView.ViewType})");
                    return null;
                }

                // Delegate cloning to the strategy
                Autodesk.Revit.DB.View newView = strategy.CloneView(
                    hostDoc,
                    sourceView,
                    linkedDoc,
                    linkInstance,
                    namePrefix,
                    positioningMode);

                if (newView != null)
                {
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

        public List<ElementId> CloneViews(Document hostDoc, List<LinkedViewInfo> viewsToClone, string namePrefix = "", ViewPositioningMode positioningMode = ViewPositioningMode.InternalOriginToInternalOrigin)
        {
            var clonedViewIds = new List<ElementId>();

            using (var transaction = new Transaction(hostDoc, "Clone Views from Linked Files"))
            {
                transaction.Start();

                foreach (var viewInfo in viewsToClone)
                {
                    var result = CloneView(hostDoc, viewInfo, namePrefix, positioningMode);
                    if (result != null && result != ElementId.InvalidElementId)
                    {
                        clonedViewIds.Add(result);
                    }
                }

                transaction.Commit();
            }

            _logger.LogInformation($"Cloned {clonedViewIds.Count} out of {viewsToClone.Count} views");
            return clonedViewIds;
        }

        /// <summary>
        /// Gets the parent ID for a callout view (works for all view types)
        /// </summary>
        private ElementId GetCalloutParentId(Autodesk.Revit.DB.View calloutView)
        {
            try
            {
                // ViewSection (includes Section and Elevation views)
                if (calloutView is ViewSection viewSection)
                {
                    return viewSection.GetCalloutParentId();
                }
                // ViewPlan (includes FloorPlan, CeilingPlan, EngineeringPlan, etc.)
                else if (calloutView is ViewPlan viewPlan)
                {
                    return viewPlan.GetCalloutParentId();
                }
                else
                {
                    _logger.LogWarning($"GetCalloutParentId not implemented for view type: {calloutView.GetType().Name}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error getting callout parent ID: {ex.Message}");
                return null;
            }
        }

        // All view cloning logic has been moved to strategy classes in the Strategies folder:
        // - SectionViewCloningStrategy handles ViewSection (sections, elevations, and their callouts)
        // - PlanViewCloningStrategy handles ViewPlan (all plan types and their callouts)
        // This factory pattern makes it easy to add new view types and keeps the service clean
    }
}
