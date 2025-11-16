using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Factories;
using LoBIM.Features.ViewCloning.Strategies;
using LoBIM.Services;
using LoBIM.Services.Parameters;

namespace LoBIM.Features.ViewCloning.Services
{
    /// <summary>
    /// Service for detecting and cloning views from linked Revit files
    /// Uses Factory pattern to delegate view cloning to specialized strategies
    /// </summary>
    public class ViewCloningService : IViewCloningService
    {
        private readonly ILoggingService _logger;
        private readonly IProjectParameterService _parameterService;
        private readonly ViewCloningStrategyFactory _strategyFactory;

        public ViewCloningService(ILoggingService logger, IProjectParameterService parameterService)
        {
            _logger = logger;
            _parameterService = parameterService;
            _strategyFactory = new ViewCloningStrategyFactory(logger, parameterService);
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

                        // Check which views already exist in the host document with source tracking
                        PopulateSourceTrackingInfo(doc, linkInfo);

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
                // Get all views from the linked document
                var allViews = new FilteredElementCollector(linkedDoc)
                    .OfClass(typeof(Autodesk.Revit.DB.View))
                    .Cast<Autodesk.Revit.DB.View>()
                    .ToList();

                _logger.LogInformation($"=== ANALYZING VIEWS IN {linkedDoc.Title} ===");
                _logger.LogInformation($"Total views found: {allViews.Count}");

                // Group by ViewType to see what we have
                var viewTypeGroups = allViews.GroupBy(v => v.ViewType).OrderByDescending(g => g.Count());
                foreach (var group in viewTypeGroups)
                {
                    var templates = group.Count(v => v.IsTemplate);
                    var nonTemplates = group.Count(v => !v.IsTemplate);
                    _logger.LogInformation($"  {group.Key}: {group.Count()} total ({nonTemplates} views, {templates} templates)");
                }

                // Filter to only views that should be cloned
                // Exclude: templates, sheets, internal views, legends, schedules
                var viewCollector = allViews
                    .Where(v => !v.IsTemplate &&
                                v.ViewType != ViewType.DrawingSheet &&
                                v.ViewType != ViewType.ProjectBrowser &&
                                v.ViewType != ViewType.SystemBrowser &&
                                v.ViewType != ViewType.Internal &&
                                v.ViewType != ViewType.Legend &&
                                v.ViewType != ViewType.Schedule &&
                                v.ViewType != ViewType.Report &&
                                v.ViewType != ViewType.Undefined &&
                                v.CanBePrinted) // Only printable views
                    .ToList();

                _logger.LogInformation($"After filtering: {viewCollector.Count} cloneable views");

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

        public ElementId? CloneView(Document hostDoc, LinkedViewInfo viewInfo, string namePrefix = "", ViewPositioningMode positioningMode = ViewPositioningMode.InternalOriginToInternalOrigin, Dictionary<string, ElementId>? clonedViewsCache = null)
        {
            try
            {
                var sourceView = viewInfo.View;
                var linkedDoc = viewInfo.ParentLink.LinkedDocument;
                var linkInstance = viewInfo.ParentLink.LinkInstance;

                _logger.LogInformation($"Cloning view: {sourceView.Name} from {linkedDoc.Title} using positioning mode: {positioningMode}");

                // Create cache key using linked file name + source view ID to uniquely identify this view
                string cacheKey = $"{System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title)}_{sourceView.Id.Value}";

                // Check if this view was already cloned in this session
                if (clonedViewsCache != null && clonedViewsCache.ContainsKey(cacheKey))
                {
                    var existingViewId = clonedViewsCache[cacheKey];
                    _logger.LogInformation($"View '{sourceView.Name}' was already cloned in this session (ID: {existingViewId.Value}) - reusing existing clone");
                    return existingViewId;
                }

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

                            // Check cache first for parent view
                            string parentCacheKey = $"{System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title)}_{parentView.Id.Value}";
                            bool parentExistsInCache = clonedViewsCache != null && clonedViewsCache.ContainsKey(parentCacheKey);

                            if (parentExistsInCache)
                            {
                                _logger.LogInformation($"Parent view already cloned in this session (cached)");
                            }
                            else
                            {
                                // Check if parent already exists in host document
                                string expectedParentName = namePrefix + parentView.Name;
                                bool parentExistsInHost = new FilteredElementCollector(hostDoc)
                                    .OfClass(typeof(Autodesk.Revit.DB.View))
                                    .Cast<Autodesk.Revit.DB.View>()
                                    .Any(v => v.Name == expectedParentName || v.Name == parentView.Name);

                                if (!parentExistsInHost)
                                {
                                    _logger.LogInformation($"Parent view does not exist in host document - cloning parent first");

                                    // Create LinkedViewInfo for parent and clone it recursively (passing cache)
                                    var parentViewInfo = new LinkedViewInfo
                                    {
                                        View = parentView,
                                        ViewName = parentView.Name,
                                        ViewType = parentView.ViewType.ToString(),
                                        ParentLink = viewInfo.ParentLink
                                    };

                                    var parentClonedId = CloneView(hostDoc, parentViewInfo, namePrefix, positioningMode, clonedViewsCache);
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

                    // Add to cache to prevent duplicate cloning
                    if (clonedViewsCache != null)
                    {
                        clonedViewsCache[cacheKey] = newView.Id;
                        _logger.LogInformation($"Added view '{sourceView.Name}' to cloning cache");
                    }

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

            // Ensure source tracking parameters exist BEFORE starting the transaction
            // This must be done outside of any transaction since parameter creation requires its own transaction
            try
            {
                // Create a temporary strategy instance to access the public parameter creation method
                var tempStrategy = new PlanViewCloningStrategy(_logger, _parameterService);
                tempStrategy.EnsureSourceTrackingParameters(hostDoc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not ensure source tracking parameters before cloning: {ex.Message}");
            }

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
        /// Populates source tracking information for views by checking the host document
        /// for views that were cloned from the linked file
        /// </summary>
        private void PopulateSourceTrackingInfo(Document hostDoc, LinkedFileInfo linkInfo)
        {
            try
            {
                // Get all views in the host document
                var hostViews = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(Autodesk.Revit.DB.View))
                    .Cast<Autodesk.Revit.DB.View>()
                    .Where(v => !v.IsTemplate)
                    .ToList();

                string linkedFileName = System.IO.Path.GetFileNameWithoutExtension(linkInfo.FileName);

                // Check each view in the linked file to see if it exists in the host
                foreach (var linkedViewInfo in linkInfo.Views)
                {
                    foreach (var hostView in hostViews)
                    {
                        try
                        {
                            // Check source tracking parameters
                            var sourceFileParam = hostView.LookupParameter("LoBIM_SourceFile");
                            var sourceViewParam = hostView.LookupParameter("LoBIM_SourceView");
                            var sourceIdParam = hostView.LookupParameter("LoBIM_SourceViewId");

                            if (sourceFileParam != null && sourceViewParam != null && sourceIdParam != null)
                            {
                                string sourceFile = sourceFileParam.AsString();
                                string sourceViewName = sourceViewParam.AsString();
                                string sourceViewId = sourceIdParam.AsString();

                                // Check if this host view was cloned from the current linked view
                                if (!string.IsNullOrEmpty(sourceFile) &&
                                    !string.IsNullOrEmpty(sourceViewName) &&
                                    sourceFile.Contains(linkedFileName) &&
                                    sourceViewName == linkedViewInfo.ViewName)
                                {
                                    // This view in the host was cloned from this linked view
                                    linkedViewInfo.SourceFileName = sourceFile;
                                    linkedViewInfo.SourceViewName = sourceViewName;
                                    linkedViewInfo.SourceViewId = sourceViewId;
                                    linkedViewInfo.IsCloned = true;
                                    linkedViewInfo.ClonedViewId = hostView.Id;

                                    _logger.LogInformation($"Found existing cloned view: {hostView.Name} from {sourceFile} > {sourceViewName}");
                                    break; // Found the match, no need to check other host views for this linked view
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Error checking source tracking for view {hostView.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error populating source tracking info: {ex.Message}", ex);
            }
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
