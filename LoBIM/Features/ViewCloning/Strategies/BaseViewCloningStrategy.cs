using Autodesk.Revit.DB;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Services;
using System;
using System.Linq;

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
                _logger.LogInformation($"=== GET LINK TRANSFORM ===");
                _logger.LogInformation($"Positioning mode: {positioningMode}");

                // First, let's see what the link's actual transform is
                var linkTransform = linkInstance.GetTransform();
                _logger.LogInformation($"Link instance transform:");
                _logger.LogInformation($"  Origin: ({linkTransform.Origin.X:F4}, {linkTransform.Origin.Y:F4}, {linkTransform.Origin.Z:F4})");
                _logger.LogInformation($"  BasisX: ({linkTransform.BasisX.X:F4}, {linkTransform.BasisX.Y:F4}, {linkTransform.BasisX.Z:F4})");
                _logger.LogInformation($"  BasisY: ({linkTransform.BasisY.X:F4}, {linkTransform.BasisY.Y:F4}, {linkTransform.BasisY.Z:F4})");
                _logger.LogInformation($"  BasisZ: ({linkTransform.BasisZ.X:F4}, {linkTransform.BasisZ.Y:F4}, {linkTransform.BasisZ.Z:F4})");

                // Check if it's identity
                bool isIdentity = linkTransform.IsIdentity;
                _logger.LogInformation($"  Is Identity: {isIdentity}");

                // CRITICAL FIX: We ALWAYS need to apply the link transform
                // The link transform tells us where the linked content is positioned in the host
                // regardless of which coordinate system was used to place it

                _logger.LogInformation($"Mode: {positioningMode}");
                _logger.LogInformation($"✓ ALWAYS applying link transform (this is correct!)");
                _logger.LogInformation($"");
                _logger.LogInformation($"EXPLANATION:");
                _logger.LogInformation($"  - View crop regions are in the LINKED document's coordinate system");
                _logger.LogInformation($"  - linkInstance.GetTransform() tells us where the linked doc is in the host");
                _logger.LogInformation($"  - We MUST apply this transform to position views correctly");
                _logger.LogInformation($"");

                if (isIdentity)
                {
                    _logger.LogInformation($"ℹ️ Link transform is Identity (no offset/rotation)");
                    _logger.LogInformation($"ℹ️ Linked content is at same position as host content");
                }
                else
                {
                    _logger.LogInformation($"ℹ️ Link has offset: ({linkTransform.Origin.X:F4}, {linkTransform.Origin.Y:F4}, {linkTransform.Origin.Z:F4})");
                    _logger.LogInformation($"ℹ️ Views will be shifted by this amount to match linked content position");
                }

                // ALWAYS return the link transform
                // This is correct for ALL positioning modes because the view coordinates
                // are always relative to the linked document, not the host document
                return linkTransform;
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

        /// <summary>
        /// Copies crop region from source view to target view
        /// Handles both custom crop shapes (for section/plan views) and rectangular crop boxes
        /// </summary>
        /// <param name="sourceView">Source view to copy from</param>
        /// <param name="targetView">Target view to copy to</param>
        /// <param name="transform">Transform to apply to crop region coordinates (for positioning modes)</param>
        /// <param name="supportsCustomShapes">Whether the view type supports custom crop shapes (false for 3D views)</param>
        protected void CopyCropRegion(Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View targetView, Transform transform = null, bool supportsCustomShapes = true)
        {
            try
            {
                _logger.LogInformation($"=== COPYING CROP REGION ===");

                // Check if source has crop box enabled
                if (!sourceView.CropBoxActive)
                {
                    _logger.LogInformation($"Source view does not have crop box active - skipping crop region copy");
                    return;
                }

                _logger.LogInformation($"Source has crop box active - copying crop region");

                // Use identity transform if none provided
                if (transform == null)
                {
                    transform = Transform.Identity;
                    _logger.LogInformation($"No transform provided - using Identity transform");
                }
                else
                {
                    _logger.LogInformation($"Applying transform to crop region - Origin: {transform.Origin}");
                }

                // Enable crop box on target
                targetView.CropBoxActive = true;
                targetView.CropBoxVisible = sourceView.CropBoxVisible;

                // Copy annotation crop settings
                CopyAnnotationCrop(sourceView, targetView);

                if (supportsCustomShapes)
                {
                    // Try to copy custom crop shape first
                    if (TryCopyCustomCropShape(sourceView, targetView, transform))
                    {
                        _logger.LogInformation($"Successfully copied custom crop region with transform");
                        return;
                    }
                }
                else
                {
                    _logger.LogInformation($"View type only supports rectangular crop boxes");
                }

                // Fall back to rectangular crop box
                CopyRectangularCropBox(sourceView, targetView, transform);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy crop region: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to copy custom crop shape from source to target view
        /// </summary>
        /// <returns>True if custom shape was copied, false otherwise</returns>
        private bool TryCopyCustomCropShape(Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View targetView, Transform transform)
        {
            try
            {
                var sourceCropManager = sourceView.GetCropRegionShapeManager();
                var sourceCropShape = sourceCropManager.GetCropShape();

                if (sourceCropShape != null && sourceCropShape.Count > 0)
                {
                    _logger.LogInformation($"Source crop region has {sourceCropShape.Count} curve loops - copying custom shape");

                    // Transform the crop shape curves to the target coordinate system
                    var sourceCropCurves = sourceCropShape[0];
                    var transformedCurveLoop = new CurveLoop();

                    foreach (Curve curve in sourceCropCurves)
                    {
                        // Transform the curve endpoints
                        var transformedCurve = curve.CreateTransformed(transform);
                        transformedCurveLoop.Append(transformedCurve);
                    }

                    var targetCropManager = targetView.GetCropRegionShapeManager();
                    targetCropManager.SetCropShape(transformedCurveLoop);

                    // CRITICAL FIX: Update the crop box bounding box to match the custom shape extent
                    // Without this, the crop box stays huge and viewports will be incorrectly sized
                    // Use the TRANSFORMED curves for calculating bounds
                    try
                    {
                        // Calculate tight bounding box around the transformed custom crop shape
                        double minX = double.MaxValue, minY = double.MaxValue;
                        double maxX = double.MinValue, maxY = double.MinValue;

                        foreach (Curve curve in transformedCurveLoop)
                        {
                            var pt0 = curve.GetEndPoint(0);
                            var pt1 = curve.GetEndPoint(1);

                            minX = Math.Min(minX, Math.Min(pt0.X, pt1.X));
                            minY = Math.Min(minY, Math.Min(pt0.Y, pt1.Y));
                            maxX = Math.Max(maxX, Math.Max(pt0.X, pt1.X));
                            maxY = Math.Max(maxY, Math.Max(pt0.Y, pt1.Y));
                        }

                        // Update the target view's crop box to this tight bounding box
                        var currentCropBox = targetView.CropBox;
                        var tightCropBox = new BoundingBoxXYZ
                        {
                            Min = new XYZ(minX, minY, currentCropBox.Min.Z),
                            Max = new XYZ(maxX, maxY, currentCropBox.Max.Z),
                            Transform = currentCropBox.Transform
                        };

                        targetView.CropBox = tightCropBox;
                        _logger.LogInformation($"Updated crop box to tight bounds (transformed): ({minX:F2}, {minY:F2}) to ({maxX:F2}, {maxY:F2})");
                    }
                    catch (Exception cropBoxEx)
                    {
                        _logger.LogWarning($"Could not update crop box bounds after copying custom shape: {cropBoxEx.Message}");
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy custom crop shape: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Copies rectangular crop box from source to target view, applying the specified transform
        /// </summary>
        private void CopyRectangularCropBox(Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View targetView, Transform transform)
        {
            try
            {
                var sourceCropBox = sourceView.CropBox;
                if (sourceCropBox != null)
                {
                    _logger.LogInformation($"Source has default rectangular crop region");
                    _logger.LogInformation($"Source crop box: Min={sourceCropBox.Min}, Max={sourceCropBox.Max}");

                    // Transform the crop box min/max points to the target coordinate system
                    XYZ transformedMin = transform.OfPoint(sourceCropBox.Min);
                    XYZ transformedMax = transform.OfPoint(sourceCropBox.Max);

                    _logger.LogInformation($"Transformed crop box: Min={transformedMin}, Max={transformedMax}");

                    // Create a new crop box with transformed coordinates
                    var transformedCropBox = new BoundingBoxXYZ
                    {
                        Min = transformedMin,
                        Max = transformedMax,
                        Transform = sourceCropBox.Transform
                    };

                    targetView.CropBox = transformedCropBox;
                    _logger.LogInformation($"Applied transformed crop box to target view");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy rectangular crop box: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies annotation crop settings from source to target view
        /// </summary>
        protected void CopyAnnotationCrop(Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View targetView)
        {
            try
            {
                _logger.LogInformation($"=== COPYING ANNOTATION CROP ===");

                // Copy annotation crop active parameter
                var sourceAnnotCropParam = sourceView.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
                var targetAnnotCropParam = targetView.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);

                _logger.LogInformation($"Source annotation crop parameter: {(sourceAnnotCropParam != null ? "Found" : "NULL")}");
                _logger.LogInformation($"Target annotation crop parameter: {(targetAnnotCropParam != null ? "Found" : "NULL")}");

                if (sourceAnnotCropParam != null && targetAnnotCropParam != null)
                {
                    var isAnnotCropActive = sourceAnnotCropParam.AsInteger();
                    _logger.LogInformation($"Source annotation crop value: {isAnnotCropActive}");

                    if (!targetAnnotCropParam.IsReadOnly)
                    {
                        targetAnnotCropParam.Set(isAnnotCropActive);
                        _logger.LogInformation($"Successfully set annotation crop active to: {(isAnnotCropActive == 1 ? "True" : "False")}");
                    }
                    else
                    {
                        _logger.LogWarning($"Target annotation crop parameter is read-only");
                    }
                }
                else
                {
                    _logger.LogWarning($"Annotation crop parameter not available on source or target view");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not copy annotation crop settings: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
            }
        }

        /// <summary>
        /// Stores source view information in the cloned view's custom project parameters
        /// This allows tracking which view was cloned from which source
        /// NOTE: This must be called within an active transaction
        /// </summary>
        protected void StoreSourceViewInfo(Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View clonedView, string linkedFileName)
        {
            try
            {
                _logger.LogInformation($"=== STORING SOURCE VIEW METADATA ===");

                Document doc = clonedView.Document;

                // Check if parameters exist, if not log a warning
                // Parameters must be created outside of transactions (e.g., on document open)
                var sourceFileParam = clonedView.LookupParameter("LoBIM_SourceFile");
                var sourceViewParam = clonedView.LookupParameter("LoBIM_SourceView");
                var sourceIdParam = clonedView.LookupParameter("LoBIM_SourceViewId");

                if (sourceFileParam == null || sourceViewParam == null || sourceIdParam == null)
                {
                    _logger.LogWarning($"Source tracking parameters not found. Creating them now (requires separate transaction)...");
                    // Try to create parameters - this will fail if we're in a transaction
                    EnsureSourceTrackingParameters(doc);

                    // Re-lookup parameters after creation attempt
                    sourceFileParam = clonedView.LookupParameter("LoBIM_SourceFile");
                    sourceViewParam = clonedView.LookupParameter("LoBIM_SourceView");
                    sourceIdParam = clonedView.LookupParameter("LoBIM_SourceViewId");
                }

                // Store source file name
                if (sourceFileParam != null && !sourceFileParam.IsReadOnly)
                {
                    sourceFileParam.Set(linkedFileName);
                    _logger.LogInformation($"Stored source file: {linkedFileName}");
                }
                else
                {
                    _logger.LogWarning($"LoBIM_SourceFile parameter not found or read-only");
                }

                // Store source view name
                if (sourceViewParam != null && !sourceViewParam.IsReadOnly)
                {
                    sourceViewParam.Set(sourceView.Name);
                    _logger.LogInformation($"Stored source view name: {sourceView.Name}");
                }
                else
                {
                    _logger.LogWarning($"LoBIM_SourceView parameter not found or read-only");
                }

                // Store source view ID
                if (sourceIdParam != null && !sourceIdParam.IsReadOnly)
                {
                    sourceIdParam.Set(sourceView.Id.ToString());
                    _logger.LogInformation($"Stored source view ID: {sourceView.Id}");
                }
                else
                {
                    _logger.LogWarning($"LoBIM_SourceViewId parameter not found or read-only");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not store source view info: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
            }
        }

        /// <summary>
        /// Ensures that the custom project parameters for source tracking exist
        /// Creates them if they don't exist
        /// This method must be called OUTSIDE of any active transaction
        /// </summary>
        public void EnsureSourceTrackingParameters(Document doc)
        {
            try
            {
                // Check if parameters already exist by looking at one view
                var testView = new FilteredElementCollector(doc)
                    .OfClass(typeof(Autodesk.Revit.DB.View))
                    .Cast<Autodesk.Revit.DB.View>()
                    .FirstOrDefault(v => !v.IsTemplate);

                if (testView == null)
                {
                    _logger.LogWarning($"No views found to check for parameters");
                    return;
                }

                bool hasSourceFile = testView.LookupParameter("LoBIM_SourceFile") != null;
                bool hasSourceView = testView.LookupParameter("LoBIM_SourceView") != null;
                bool hasSourceId = testView.LookupParameter("LoBIM_SourceViewId") != null;

                // If all parameters exist, no need to create them
                if (hasSourceFile && hasSourceView && hasSourceId)
                {
                    return;
                }

                _logger.LogInformation($"Creating source tracking project parameters...");

                using (Transaction trans = new Transaction(doc, "Create LoBIM Source Tracking Parameters"))
                {
                    trans.Start();

                    CategorySet categories = doc.Application.Create.NewCategorySet();
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Views));

                    DefinitionFile defFile = doc.Application.OpenSharedParameterFile();
                    DefinitionGroup defGroup = null;

                    // Try to get or create definition group
                    if (defFile != null)
                    {
                        defGroup = defFile.Groups.get_Item("LoBIM") ?? defFile.Groups.Create("LoBIM");
                    }

                    // Create parameters
                    if (!hasSourceFile)
                    {
                        CreateProjectParameterFromSharedParam(doc, defGroup, "LoBIM_SourceFile",
                            SpecTypeId.String.Text, categories, GroupTypeId.IdentityData);
                    }

                    if (!hasSourceView)
                    {
                        CreateProjectParameterFromSharedParam(doc, defGroup, "LoBIM_SourceView",
                            SpecTypeId.String.Text, categories, GroupTypeId.IdentityData);
                    }

                    if (!hasSourceId)
                    {
                        CreateProjectParameterFromSharedParam(doc, defGroup, "LoBIM_SourceViewId",
                            SpecTypeId.String.Text, categories, GroupTypeId.IdentityData);
                    }

                    trans.Commit();
                    _logger.LogInformation($"Successfully created source tracking parameters");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not ensure source tracking parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a project parameter from a shared parameter definition
        /// </summary>
        private void CreateProjectParameterFromSharedParam(Document doc, DefinitionGroup defGroup,
            string paramName, ForgeTypeId paramType, CategorySet categories, ForgeTypeId group)
        {
            try
            {
                Definition def = defGroup?.Definitions.get_Item(paramName);

                if (def == null && defGroup != null)
                {
                    ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(paramName, paramType);
                    def = defGroup.Definitions.Create(options);
                }

                if (def != null)
                {
                    InstanceBinding binding = doc.Application.Create.NewInstanceBinding(categories);
                    doc.ParameterBindings.Insert(def, binding, group);
                    _logger.LogInformation($"Created parameter: {paramName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create parameter {paramName}: {ex.Message}");
            }
        }
    }
}
