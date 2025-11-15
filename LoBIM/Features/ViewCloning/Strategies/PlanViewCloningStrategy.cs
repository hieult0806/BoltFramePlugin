using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Services;
using System;
using System.Linq;

namespace LoBIM.Features.ViewCloning.Strategies
{
    /// <summary>
    /// Strategy for cloning ViewPlan (includes FloorPlan, CeilingPlan, EngineeringPlan, AreaPlan)
    /// Handles both regular plans and plan callouts
    /// </summary>
    public class PlanViewCloningStrategy : BaseViewCloningStrategy
    {
        public PlanViewCloningStrategy(ILoggingService logger) : base(logger)
        {
        }

        public override bool CanHandle(ViewType viewType)
        {
            // This strategy handles ViewPlan concrete type, which covers:
            // - ViewType.FloorPlan
            // - ViewType.CeilingPlan
            // - ViewType.EngineeringPlan
            // - ViewType.AreaPlan
            return viewType == ViewType.FloorPlan ||
                   viewType == ViewType.CeilingPlan ||
                   viewType == ViewType.EngineeringPlan ||
                   viewType == ViewType.AreaPlan;
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
                if (!(sourceView is ViewPlan sourcePlan))
                {
                    _logger.LogWarning($"Source view is not a ViewPlan: {sourceView.Name}");
                    return null;
                }

                _logger.LogInformation($"=== CLONING VIEW PLAN ===");
                _logger.LogInformation($"View Name: {sourceView.Name}");
                _logger.LogInformation($"ViewType: {sourceView.ViewType}");
                _logger.LogInformation($"Is Callout: {sourceView.IsCallout}");

                // Check if this is a callout
                if (sourceView.IsCallout)
                {
                    _logger.LogInformation($"=== PLAN CALLOUT DETECTED ===");
                    return ClonePlanCallout(hostDoc, sourcePlan, linkedDoc, linkInstance, namePrefix, positioningMode);
                }
                else
                {
                    _logger.LogInformation($"=== REGULAR PLAN (NON-CALLOUT) ===");
                    return CloneRegularPlan(hostDoc, sourcePlan, linkedDoc, linkInstance, sourceView.Name, namePrefix, positioningMode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning plan view {sourceView.Name}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Clones a regular (non-callout) plan view
        /// </summary>
        private ViewPlan CloneRegularPlan(
            Document hostDoc,
            ViewPlan sourcePlan,
            Document linkedDoc,
            RevitLinkInstance linkInstance,
            string sourceViewName,
            string namePrefix,
            ViewPositioningMode positioningMode)
        {
            _logger.LogInformation($"Cloning regular plan view: {sourceViewName}");
            _logger.LogInformation($"Positioning mode: {positioningMode}");

            // For plan views, we need to find a matching level in the host by NAME
            // (We cannot use the level ID from the linked document)
            var sourceLevelFromLinkedDoc = sourcePlan.GenLevel;
            var sourceLevelName = sourceLevelFromLinkedDoc.Name;

            _logger.LogInformation($"Source plan uses level: {sourceLevelName}");
            _logger.LogInformation($"Searching for matching level in host document by name...");

            // Get or create matching level in host document
            var hostLevel = GetOrCreateLevel(hostDoc, linkedDoc, sourceLevelFromLinkedDoc);

            if (hostLevel == null)
            {
                _logger.LogError($"Could not find or create matching level '{sourceLevelName}' in host document for plan view: {sourceViewName}");
                return null;
            }

            _logger.LogInformation($"Using level in host: {hostLevel.Name} (ID: {hostLevel.Id}, Elevation: {hostLevel.Elevation})");

            // Create a new plan view in the host document
            ViewPlan newPlan = ViewPlan.Create(hostDoc, sourcePlan.GetTypeId(), hostLevel.Id);

            if (newPlan == null)
            {
                _logger.LogWarning($"Failed to create plan view in host document");
                return null;
            }

            // Copy properties
            CopyScale(sourcePlan, newPlan);
            ApplyViewName(newPlan, sourceViewName, namePrefix);

            // Get the link transform based on positioning mode
            Transform linkTransform = GetLinkTransform(linkInstance, positioningMode);

            // Copy crop region if source has one (plan views support custom shapes)
            // Pass the transform so crop regions are positioned correctly
            CopyCropRegion(sourcePlan, newPlan, linkTransform, supportsCustomShapes: true);

            // Store source view information for tracking
            string linkedFileName = System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title);
            StoreSourceViewInfo(sourcePlan, newPlan, linkedFileName);

            _logger.LogInformation($"Successfully cloned regular plan view: {newPlan.Name}");

            return newPlan;
        }

        /// <summary>
        /// Clones a plan callout view (parent-relative positioning)
        /// </summary>
        private ViewPlan ClonePlanCallout(
            Document hostDoc,
            ViewPlan calloutPlan,
            Document linkedDoc,
            RevitLinkInstance linkInstance,
            string namePrefix,
            ViewPositioningMode positioningMode)
        {
            _logger.LogInformation($"Cloning plan callout view: {calloutPlan.Name}");

            // Get parent view
            ElementId parentId = null;
            try
            {
                parentId = calloutPlan.GetCalloutParentId();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get callout parent ID: {ex.Message}");
                return null;
            }

            if (parentId == null || parentId == ElementId.InvalidElementId)
            {
                _logger.LogWarning($"No valid parent ID found for plan callout");
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

            // Get callout crop box
            var calloutBoundingBox = calloutPlan.CropBox;
            if (calloutBoundingBox == null)
            {
                _logger.LogWarning($"Could not get crop box from plan callout");
                return null;
            }

            // Get parent crop box
            var parentCropBox = parentView.CropBox;
            if (parentCropBox == null)
            {
                _logger.LogWarning($"Parent view has no cropbox - cannot transform callout");
                return null;
            }

            // Get callout and parent transforms
            var parentTransform = parentCropBox.Transform;
            var srcTransform = calloutBoundingBox.Transform;

            _logger.LogInformation($"=== CALLOUT COORDINATE TRANSFORMATION ===");
            _logger.LogInformation($"Analyzing callout POSITION relative to parent view");

            // CRITICAL INSIGHT: For plan callouts, the cropbox origin is ALREADY in world coordinates
            // Similar to section callouts, we should NOT transform it through the parent
            // The callout's cropbox defines its absolute position in the model

            _logger.LogInformation($"=== ANALYSIS: CALLOUT VS PARENT ===");
            _logger.LogInformation($"Original callout origin: {calloutPlan.Origin}");
            _logger.LogInformation($"Parent view origin: {parentView.Origin}");
            _logger.LogInformation($"Callout cropbox origin: {srcTransform.Origin}");
            _logger.LogInformation($"Parent cropbox origin: {parentTransform.Origin}");

            // Use the callout's cropbox origin directly (already in world coordinates)
            XYZ calloutWorldOrigin = srcTransform.Origin;

            _logger.LogInformation($"Using callout cropbox origin directly: {calloutWorldOrigin}");

            // Apply positioning mode
            Transform linkTransform = GetLinkTransform(linkInstance, positioningMode);
            _logger.LogInformation($"=== APPLYING POSITIONING MODE: {positioningMode} ===");

            Transform finalTransform;

            switch (positioningMode)
            {
                case ViewPositioningMode.ProjectBasePointToProjectBasePoint:
                case ViewPositioningMode.BySharedCoordinates:
                    _logger.LogInformation($"Using shared coordinates - no link transform applied");
                    finalTransform = Transform.Identity;
                    finalTransform.Origin = calloutWorldOrigin;
                    finalTransform.BasisX = srcTransform.BasisX;
                    finalTransform.BasisY = srcTransform.BasisY;
                    finalTransform.BasisZ = srcTransform.BasisZ;
                    break;

                case ViewPositioningMode.InternalOriginToInternalOrigin:
                default:
                    _logger.LogInformation($"Applying link transform to plan callout");
                    XYZ transformedCalloutOrigin = linkTransform.OfPoint(calloutWorldOrigin);
                    _logger.LogInformation($"Transformed callout origin (in host): {transformedCalloutOrigin}");

                    finalTransform = Transform.Identity;
                    finalTransform.Origin = transformedCalloutOrigin;
                    finalTransform.BasisX = srcTransform.BasisX;
                    finalTransform.BasisY = srcTransform.BasisY;
                    finalTransform.BasisZ = srcTransform.BasisZ;
                    break;
            }

            // For plan callouts, we still need a matching level in the host by NAME
            var sourceLevelFromLinkedDoc = calloutPlan.GenLevel;
            var sourceLevelName = sourceLevelFromLinkedDoc.Name;

            _logger.LogInformation($"Source callout uses level: {sourceLevelName}");
            _logger.LogInformation($"Searching for matching level in host document by name...");

            // Get or create matching level in host document
            var hostLevel = GetOrCreateLevel(hostDoc, linkedDoc, sourceLevelFromLinkedDoc);

            if (hostLevel == null)
            {
                _logger.LogError($"Could not find or create matching level '{sourceLevelName}' in host document for plan callout");
                return null;
            }

            _logger.LogInformation($"Using level in host: {hostLevel.Name} (ID: {hostLevel.Id}, Elevation: {hostLevel.Elevation})");

            // Create a new plan view in the host document
            ViewPlan newPlan = ViewPlan.Create(hostDoc, calloutPlan.GetTypeId(), hostLevel.Id);

            if (newPlan == null)
            {
                _logger.LogWarning($"Failed to create plan callout in host document");
                return null;
            }

            // For plan callouts, we need to set the crop box to match the callout's boundaries
            try
            {
                // Enable crop view
                newPlan.CropBoxActive = true;
                newPlan.CropBoxVisible = true;

                // Get the current crop region
                var cropManager = newPlan.GetCropRegionShapeManager();

                // Calculate the callout crop region boundaries based on the final transform
                XYZ min = calloutBoundingBox.Min;
                XYZ max = calloutBoundingBox.Max;

                // Transform crop boundaries
                XYZ transformedMin = finalTransform.OfPoint(min);
                XYZ transformedMax = finalTransform.OfPoint(max);

                _logger.LogInformation($"Plan callout crop region - Min: {transformedMin}, Max: {transformedMax}");

                // Create crop region curve loop (rectangle)
                var curveLoop = new CurveLoop();
                double z = transformedMin.Z; // Plan views are at level elevation

                XYZ p1 = new XYZ(transformedMin.X, transformedMin.Y, z);
                XYZ p2 = new XYZ(transformedMax.X, transformedMin.Y, z);
                XYZ p3 = new XYZ(transformedMax.X, transformedMax.Y, z);
                XYZ p4 = new XYZ(transformedMin.X, transformedMax.Y, z);

                curveLoop.Append(Line.CreateBound(p1, p2));
                curveLoop.Append(Line.CreateBound(p2, p3));
                curveLoop.Append(Line.CreateBound(p3, p4));
                curveLoop.Append(Line.CreateBound(p4, p1));

                cropManager.SetCropShape(curveLoop);

                _logger.LogInformation($"Successfully set crop region for plan callout");

                // CRITICAL FIX: Update crop box to tight bounds after setting custom crop shape
                // This ensures the viewport size matches the actual crop shape extent
                // We use the curveLoop we just created instead of getting it back from the manager
                try
                {
                    // Calculate tight bounding box around the crop shape we just created
                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;

                    foreach (Curve curve in curveLoop)
                    {
                        var pt0 = curve.GetEndPoint(0);
                        var pt1 = curve.GetEndPoint(1);

                        minX = Math.Min(minX, Math.Min(pt0.X, pt1.X));
                        minY = Math.Min(minY, Math.Min(pt0.Y, pt1.Y));
                        maxX = Math.Max(maxX, Math.Max(pt0.X, pt1.X));
                        maxY = Math.Max(maxY, Math.Max(pt0.Y, pt1.Y));
                    }

                    // Update crop box to tight bounds
                    var currentCropBox = newPlan.CropBox;
                    var tightCropBox = new BoundingBoxXYZ
                    {
                        Min = new XYZ(minX, minY, currentCropBox.Min.Z),
                        Max = new XYZ(maxX, maxY, currentCropBox.Max.Z),
                        Transform = currentCropBox.Transform
                    };

                    newPlan.CropBox = tightCropBox;
                    _logger.LogInformation($"Updated callout crop box to tight bounds: ({minX:F2}, {minY:F2}) to ({maxX:F2}, {maxY:F2})");
                }
                catch (Exception tightBoxEx)
                {
                    _logger.LogWarning($"Could not update crop box to tight bounds: {tightBoxEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not set crop region for plan callout: {ex.Message}");
            }

            // Copy annotation crop settings
            CopyAnnotationCrop(calloutPlan, newPlan);

            // Copy properties
            CopyScale(calloutPlan, newPlan);
            ApplyViewName(newPlan, calloutPlan.Name, namePrefix);

            // Store source view information for tracking
            string linkedFileName = System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title);
            StoreSourceViewInfo(calloutPlan, newPlan, linkedFileName);

            _logger.LogInformation($"=== CREATED PLAN CALLOUT PROPERTIES ===");
            _logger.LogInformation($"Created Origin: {newPlan.Origin}");
            _logger.LogInformation($"Created Direction: {newPlan.ViewDirection}");
            _logger.LogInformation($"Successfully cloned plan callout: {newPlan.Name}");

            return newPlan;
        }

        /// <summary>
        /// Gets or creates a matching level in the host document
        /// If the level doesn't exist in the host, it will be created with matching name and elevation
        /// </summary>
        private Level GetOrCreateLevel(Document hostDoc, Document linkedDoc, Level sourceLevel)
        {
            var sourceLevelName = sourceLevel.Name;
            var sourceLevelElevation = sourceLevel.Elevation;

            _logger.LogInformation($"=== GETTING OR CREATING LEVEL ===");
            _logger.LogInformation($"Source level: '{sourceLevelName}', Elevation: {sourceLevelElevation}");

            // STEP 1: Try to find matching level in host document by name
            var hostLevels = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            _logger.LogInformation($"Found {hostLevels.Count} levels in host document");
            _logger.LogInformation($"Available levels in host: {string.Join(", ", hostLevels.Select(l => $"{l.Name} ({l.Elevation:F2})"))}");

            // Try exact name match first
            var matchingLevel = hostLevels.FirstOrDefault(l =>
                l.Name.Equals(sourceLevelName, StringComparison.OrdinalIgnoreCase));

            if (matchingLevel != null)
            {
                _logger.LogInformation($"Found existing level with exact name match: '{matchingLevel.Name}' (Elevation: {matchingLevel.Elevation})");

                // Check if elevation matches
                const double ELEVATION_TOLERANCE = 0.001; // ~1mm tolerance
                if (Math.Abs(matchingLevel.Elevation - sourceLevelElevation) > ELEVATION_TOLERANCE)
                {
                    _logger.LogWarning($"Level '{matchingLevel.Name}' exists but elevation differs: Host={matchingLevel.Elevation}, Source={sourceLevelElevation}");
                    _logger.LogWarning($"Using existing level anyway - elevation difference may affect view positioning");
                }

                return matchingLevel;
            }

            // STEP 2: Try to find by elevation (in case level was renamed)
            matchingLevel = hostLevels.FirstOrDefault(l =>
            {
                const double ELEVATION_TOLERANCE = 0.001; // ~1mm tolerance
                return Math.Abs(l.Elevation - sourceLevelElevation) <= ELEVATION_TOLERANCE;
            });

            if (matchingLevel != null)
            {
                _logger.LogInformation($"Found existing level with matching elevation: '{matchingLevel.Name}' at {matchingLevel.Elevation} (source was '{sourceLevelName}')");
                _logger.LogInformation($"Using existing level '{matchingLevel.Name}' even though name differs from source");
                return matchingLevel;
            }

            // STEP 3: Level doesn't exist - create it
            _logger.LogInformation($"No matching level found in host. Creating new level '{sourceLevelName}' at elevation {sourceLevelElevation}...");

            try
            {
                // Check if the name conflicts (shouldn't happen since we already checked, but be safe)
                var nameToUse = sourceLevelName;
                int suffix = 1;
                while (hostLevels.Any(l => l.Name.Equals(nameToUse, StringComparison.OrdinalIgnoreCase)))
                {
                    nameToUse = $"{sourceLevelName}_{suffix}";
                    suffix++;
                    _logger.LogInformation($"Name conflict detected, trying: '{nameToUse}'");
                }

                // Create the new level
                Level newLevel = Level.Create(hostDoc, sourceLevelElevation);

                if (newLevel == null)
                {
                    _logger.LogError($"Failed to create level at elevation {sourceLevelElevation}");
                    return null;
                }

                // Set the name
                try
                {
                    newLevel.Name = nameToUse;
                    _logger.LogInformation($"Successfully created level '{newLevel.Name}' at elevation {newLevel.Elevation}");
                }
                catch (Exception nameEx)
                {
                    _logger.LogWarning($"Created level but failed to set name to '{nameToUse}': {nameEx.Message}");
                    _logger.LogInformation($"Using Revit's auto-generated name: '{newLevel.Name}'");
                }

                // Copy additional properties from source level
                try
                {
                    CopyLevelProperties(sourceLevel, newLevel);
                }
                catch (Exception propEx)
                {
                    _logger.LogWarning($"Could not copy all level properties: {propEx.Message}");
                }

                _logger.LogInformation($"Successfully created and configured level '{newLevel.Name}' (ID: {newLevel.Id})");
                return newLevel;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating level '{sourceLevelName}': {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Copies properties from source level to target level
        /// </summary>
        private void CopyLevelProperties(Level sourceLevel, Level targetLevel)
        {
            try
            {
                _logger.LogInformation($"Copying level properties from '{sourceLevel.Name}' to '{targetLevel.Name}'...");

                // Copy parameters that are not read-only
                foreach (Parameter sourceParam in sourceLevel.Parameters)
                {
                    if (sourceParam.IsReadOnly)
                        continue;

                    var targetParam = targetLevel.LookupParameter(sourceParam.Definition.Name);
                    if (targetParam == null || targetParam.IsReadOnly)
                        continue;

                    try
                    {
                        switch (sourceParam.StorageType)
                        {
                            case StorageType.String:
                                var stringValue = sourceParam.AsString();
                                if (!string.IsNullOrEmpty(stringValue))
                                    targetParam.Set(stringValue);
                                break;
                            case StorageType.Integer:
                                targetParam.Set(sourceParam.AsInteger());
                                break;
                            case StorageType.Double:
                                targetParam.Set(sourceParam.AsDouble());
                                break;
                            case StorageType.ElementId:
                                // Skip ElementId parameters as they may not be valid in host doc
                                break;
                        }
                    }
                    catch (Exception paramEx)
                    {
                        _logger.LogDebug($"Could not copy parameter '{sourceParam.Definition.Name}': {paramEx.Message}");
                    }
                }

                _logger.LogInformation($"Level properties copied successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error copying level properties: {ex.Message}");
            }
        }
    }
}
