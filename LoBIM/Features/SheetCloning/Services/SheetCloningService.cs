using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Features.SheetCloning.Helpers;
using LoBIM.Features.SheetCloning.Models;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
using LoBIM.Services;
using RevitView = Autodesk.Revit.DB.View;

namespace LoBIM.Features.SheetCloning.Services
{
    /// <summary>
    /// Service for cloning sheets from linked Revit files
    /// </summary>
    public class SheetCloningService : ISheetCloningService
    {
        private readonly ILoggingService _logger;
        private readonly IViewCloningService _viewCloningService;
        private readonly SheetSourceTrackingHelper _sourceTrackingHelper;

        public SheetCloningService(ILoggingService logger, IViewCloningService viewCloningService)
        {
            _logger = logger;
            _viewCloningService = viewCloningService;
            _sourceTrackingHelper = new SheetSourceTrackingHelper(logger);
        }

        /// <summary>
        /// Gets all sheets from a linked document
        /// </summary>
        public List<LinkedSheetInfo> GetSheetsFromLinkedFile(Document hostDoc, Document linkedDoc, string fileName)
        {
            var sheets = new List<LinkedSheetInfo>();

            try
            {
                var sheetCollector = new FilteredElementCollector(linkedDoc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate); // Exclude sheet templates

                foreach (var sheet in sheetCollector)
                {
                    var viewportCount = sheet.GetAllViewports()?.Count ?? 0;

                    sheets.Add(new LinkedSheetInfo
                    {
                        Sheet = sheet,
                        SheetId = sheet.Id,
                        SheetNumber = sheet.SheetNumber,
                        SheetName = sheet.Name,
                        SourceFileName = fileName,
                        ViewportCount = viewportCount,
                        IsSelected = false
                    });
                }

                // Check which sheets already exist in the host document with source tracking
                PopulateSheetSourceTrackingInfo(hostDoc, sheets, fileName);

                _logger?.LogInformation($"Found {sheets.Count} sheets in {fileName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error getting sheets from {fileName}: {ex.Message}", ex);
            }

            return sheets.OrderBy(s => s.SheetNumber).ToList();
        }

        /// <summary>
        /// Populates source tracking information for sheets by checking the host document
        /// for sheets that were cloned from the linked file
        /// </summary>
        private void PopulateSheetSourceTrackingInfo(Document hostDoc, List<LinkedSheetInfo> linkedSheets, string linkedFileName)
        {
            try
            {
                _logger?.LogInformation($"=== CHECKING FOR EXISTING CLONED SHEETS ===");
                _logger?.LogInformation($"Linked file: {linkedFileName}");

                // Get all sheets in the host document
                var hostSheets = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .ToList();

                _logger?.LogInformation($"Found {hostSheets.Count} sheets in host document");

                // Check first sheet to see if parameters exist
                if (hostSheets.Count > 0)
                {
                    var testSheet = hostSheets[0];
                    var testParam = testSheet.LookupParameter("LoBIM_SourceFile");
                    _logger?.LogInformation($"LoBIM_SourceFile parameter exists on sheets: {testParam != null}");
                }

                // Check each sheet in the linked file to see if it exists in the host
                foreach (var linkedSheetInfo in linkedSheets)
                {
                    foreach (var hostSheet in hostSheets)
                    {
                        try
                        {
                            // Check source tracking parameters
                            var sourceFileParam = hostSheet.LookupParameter("LoBIM_SourceFile");
                            var sourceSheetParam = hostSheet.LookupParameter("LoBIM_SourceSheet");
                            var sourceIdParam = hostSheet.LookupParameter("LoBIM_SourceSheetId");

                            if (sourceFileParam != null && sourceSheetParam != null && sourceIdParam != null)
                            {
                                string sourceFile = sourceFileParam.AsString();
                                string sourceSheetNumber = sourceSheetParam.AsString();
                                string sourceSheetId = sourceIdParam.AsString();

                                // Check if this host sheet was cloned from the current linked sheet
                                if (!string.IsNullOrEmpty(sourceFile) &&
                                    !string.IsNullOrEmpty(sourceSheetNumber) &&
                                    sourceFile.Contains(linkedFileName) &&
                                    sourceSheetNumber == linkedSheetInfo.SheetNumber)
                                {
                                    // This sheet in the host was cloned from this linked sheet
                                    linkedSheetInfo.SourceTrackingFileName = sourceFile;
                                    linkedSheetInfo.SourceTrackingSheetNumber = sourceSheetNumber;
                                    linkedSheetInfo.SourceTrackingSheetId = sourceSheetId;
                                    linkedSheetInfo.IsCloned = true;
                                    linkedSheetInfo.ClonedSheetId = hostSheet.Id;

                                    _logger?.LogInformation($"✓ Found existing cloned sheet: {hostSheet.SheetNumber} from {sourceFile} > {sourceSheetNumber}");
                                    break; // Found the match, no need to check other host sheets for this linked sheet
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning($"Error checking source tracking for sheet {hostSheet.SheetNumber}: {ex.Message}");
                        }
                    }
                }

                _logger?.LogInformation($"=== END CHECKING FOR EXISTING CLONED SHEETS ===");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error populating sheet source tracking info: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clones selected sheets from linked files to the host document
        /// </summary>
        public int CloneSheets(Document hostDoc, List<LinkedSheetInfo> sheets, Dictionary<string, Document> linkedDocuments)
        {
            int successCount = 0;

            try
            {
                foreach (var sheetInfo in sheets)
                {
                    if (!linkedDocuments.TryGetValue(sheetInfo.SourceFileName, out var linkedDoc))
                    {
                        _logger?.LogWarning($"Could not find linked document for {sheetInfo.SourceFileName}");
                        continue;
                    }

                    var sourceSheet = linkedDoc.GetElement(sheetInfo.SheetId) as ViewSheet;
                    if (sourceSheet == null)
                    {
                        _logger?.LogWarning($"Could not find source sheet {sheetInfo.SheetNumber}");
                        continue;
                    }

                    try
                    {
                        var clonedSheet = CloneSingleSheet(hostDoc, sourceSheet, linkedDoc);
                        if (clonedSheet != null)
                        {
                            successCount++;
                            _logger?.LogInformation($"Successfully cloned sheet: {sheetInfo.SheetNumber} - {sheetInfo.SheetName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Failed to clone sheet {sheetInfo.SheetNumber}: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error during sheet cloning: {ex.Message}", ex);
            }

            return successCount;
        }

        /// <summary>
        /// Clones a single sheet from linked document to host document
        /// </summary>
        private ViewSheet CloneSingleSheet(Document hostDoc, ViewSheet sourceSheet, Document linkedDoc)
        {
            // Get or create matching titleblock
            var titleblockId = GetOrCreateTitleblock(hostDoc, sourceSheet, linkedDoc);
            if (titleblockId == null || titleblockId == ElementId.InvalidElementId)
            {
                _logger?.LogWarning($"Could not find or create titleblock for sheet {sourceSheet.SheetNumber}");
                return null;
            }

            // Create new sheet in host document
            var newSheet = ViewSheet.Create(hostDoc, titleblockId);
            if (newSheet == null)
            {
                _logger?.LogError($"Failed to create sheet {sourceSheet.SheetNumber}");
                return null;
            }

            // Copy sheet number and name
            try
            {
                newSheet.SheetNumber = GenerateUniqueSheetNumber(hostDoc, sourceSheet.SheetNumber);
                newSheet.Name = sourceSheet.Name;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error setting sheet properties: {ex.Message}");
            }

            // Copy parameters
            CopySheetParameters(sourceSheet, newSheet);

            // Store source tracking information
            string linkedFileName = System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title);
            _sourceTrackingHelper.StoreSourceSheetInfo(sourceSheet, newSheet, linkedFileName);

            // Clone viewports (views placed on sheet)
            CloneViewports(hostDoc, sourceSheet, newSheet, linkedDoc, titleblockId);

            return newSheet;
        }

        /// <summary>
        /// Gets or creates a matching titleblock type in the host document
        /// </summary>
        private ElementId GetOrCreateTitleblock(Document hostDoc, ViewSheet sourceSheet, Document linkedDoc)
        {
            // Get the titleblock from source sheet
            var sourceViewport = sourceSheet.GetAllViewports().FirstOrDefault();
            if (sourceViewport != null)
            {
                var viewportElement = linkedDoc.GetElement(sourceViewport);
                // Could match titleblock type here if needed
            }

            // Try to find matching titleblock in host by family and type name
            var titleblockCollector = new FilteredElementCollector(hostDoc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>();

            // For now, just use the first available titleblock
            // TODO: Match by family name and type name
            var hostTitleblock = titleblockCollector.FirstOrDefault();

            if (hostTitleblock != null)
                return hostTitleblock.Id;

            _logger?.LogWarning($"No titleblock found in host document");
            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Generates a unique sheet number in the host document
        /// </summary>
        private string GenerateUniqueSheetNumber(Document hostDoc, string baseNumber)
        {
            var existingSheets = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Select(s => s.SheetNumber)
                .ToHashSet();

            string newNumber = baseNumber;
            int suffix = 1;

            while (existingSheets.Contains(newNumber))
            {
                newNumber = $"{baseNumber}-{suffix:D2}";
                suffix++;
            }

            return newNumber;
        }

        /// <summary>
        /// Copies parameters from source sheet to cloned sheet
        /// </summary>
        private void CopySheetParameters(ViewSheet sourceSheet, ViewSheet targetSheet)
        {
            try
            {
                foreach (Parameter sourceParam in sourceSheet.Parameters)
                {
                    if (sourceParam.IsReadOnly)
                        continue;

                    var targetParam = targetSheet.LookupParameter(sourceParam.Definition.Name);
                    if (targetParam == null || targetParam.IsReadOnly)
                        continue;

                    try
                    {
                        switch (sourceParam.StorageType)
                        {
                            case StorageType.String:
                                targetParam.Set(sourceParam.AsString() ?? "");
                                break;
                            case StorageType.Integer:
                                targetParam.Set(sourceParam.AsInteger());
                                break;
                            case StorageType.Double:
                                targetParam.Set(sourceParam.AsDouble());
                                break;
                            case StorageType.ElementId:
                                targetParam.Set(sourceParam.AsElementId());
                                break;
                        }
                    }
                    catch
                    {
                        // Skip parameters that can't be set
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error copying sheet parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Clones all viewports (views placed on sheet) from source sheet to target sheet
        /// </summary>
        private void CloneViewports(Document hostDoc, ViewSheet sourceSheet, ViewSheet targetSheet, Document linkedDoc, ElementId targetTitleblockId)
        {
            try
            {
                // Log titleblock information
                var sourceTitleblockIds = new FilteredElementCollector(linkedDoc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .Where(e => e.OwnerViewId == sourceSheet.Id)
                    .Select(e => e.Id)
                    .ToList();

                var targetTitleblock = hostDoc.GetElement(targetTitleblockId);
                var sourceTitleblockName = sourceTitleblockIds.Count > 0 ? linkedDoc.GetElement(sourceTitleblockIds[0])?.Name : "Unknown";
                var targetTitleblockName = targetTitleblock?.Name ?? "Unknown";

                _logger?.LogInformation($"Source titleblock: {sourceTitleblockName}, Target titleblock: {targetTitleblockName}");

                var sourceViewports = sourceSheet.GetAllViewports();
                _logger?.LogInformation($"Found {sourceViewports.Count} viewport(s) on sheet {sourceSheet.SheetNumber}");

                foreach (var viewportId in sourceViewports)
                {
                    try
                    {
                        var sourceViewport = linkedDoc.GetElement(viewportId) as Viewport;
                        if (sourceViewport == null)
                            continue;

                        var sourceViewId = sourceViewport.ViewId;
                        var sourceView = linkedDoc.GetElement(sourceViewId) as RevitView;
                        if (sourceView == null || sourceView.IsTemplate)
                            continue;

                        _logger?.LogInformation($"Cloning view: {sourceView.Name} (Type: {sourceView.ViewType})");

                        // Get the RevitLinkInstance for this linked document
                        RevitLinkInstance linkInstance = null;
                        try
                        {
                            linkInstance = new FilteredElementCollector(hostDoc)
                                .OfClass(typeof(RevitLinkInstance))
                                .Cast<RevitLinkInstance>()
                                .FirstOrDefault(link => link.GetLinkDocument()?.Title == linkedDoc.Title);

                            if (linkInstance == null)
                            {
                                _logger?.LogWarning($"Could not find RevitLinkInstance for linked document: {linkedDoc.Title}");
                            }
                        }
                        catch (Exception linkEx)
                        {
                            _logger?.LogWarning($"Error finding link instance: {linkEx.Message}");
                        }

                        // Create LinkedViewInfo for the view cloning service
                        // We need to create a LinkedFileInfo for the ParentLink property
                        var parentLink = new LinkedFileInfo
                        {
                            LinkedDocument = linkedDoc,
                            FileName = System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title),
                            LinkInstance = linkInstance  // Set the link instance
                        };

                        var linkedViewInfo = new LinkedViewInfo
                        {
                            View = sourceView,
                            ViewId = sourceViewId,
                            ViewName = sourceView.Name,
                            ViewType = sourceView.ViewType.ToString(),
                            IsSelected = true,
                            ParentLink = parentLink
                        };

                        // Clone the view using the ViewCloningService - it returns ElementId? but we handle as object
                        var clonedViewIdResult = _viewCloningService.CloneView(
                            hostDoc,
                            linkedViewInfo,
                            "",
                            ViewPositioningMode.InternalOriginToInternalOrigin);

                        // The CloneView returns ElementId? - we check if it returned something
                        if (clonedViewIdResult != null)
                        {
                            // Convert to ElementId - the Value property gives us the actual ElementId
                            ElementId clonedViewId = new ElementId((long)clonedViewIdResult.GetType().GetProperty("Value").GetValue(clonedViewIdResult));

                            // Place the cloned view on the new sheet at the same position
                            var clonedView = hostDoc.GetElement(clonedViewId) as RevitView;
                            if (clonedView != null && !clonedView.IsTemplate)
                            {
                                try
                                {
                                    // Get original viewport position and properties
                                    var sourceBoxCenter = sourceViewport.GetBoxCenter();
                                    var sourceBoxOutline = sourceViewport.GetBoxOutline();
                                    var sourceMin = sourceBoxOutline.MinimumPoint;
                                    var sourceMax = sourceBoxOutline.MaximumPoint;

                                    _logger?.LogInformation($"Source viewport position - Center: ({sourceBoxCenter.X:F4}, {sourceBoxCenter.Y:F4}), " +
                                        $"Min: ({sourceMin.X:F4}, {sourceMin.Y:F4}), Max: ({sourceMax.X:F4}, {sourceMax.Y:F4})");

                                    // The crop region is now properly handled by ViewCloningService (BaseViewCloningStrategy)
                                    // which updates the crop box bounding box after copying custom crop shapes

                                    // Create viewport on target sheet
                                    var newViewport = Viewport.Create(hostDoc, targetSheet.Id, clonedViewId, sourceBoxCenter);

                                    if (newViewport != null)
                                    {
                                        // Log initial target viewport position
                                        var initialTargetBoxCenter = newViewport.GetBoxCenter();
                                        var initialTargetBoxOutline = newViewport.GetBoxOutline();
                                        var initialTargetMin = initialTargetBoxOutline.MinimumPoint;
                                        var initialTargetMax = initialTargetBoxOutline.MaximumPoint;

                                        _logger?.LogInformation($"Initial target viewport - Center: ({initialTargetBoxCenter.X:F4}, {initialTargetBoxCenter.Y:F4}), " +
                                            $"Min: ({initialTargetMin.X:F4}, {initialTargetMin.Y:F4}), Max: ({initialTargetMax.X:F4}, {initialTargetMax.Y:F4})");

                                        // Copy viewport properties (this will update the view to match)
                                        CopyViewportProperties(sourceViewport, newViewport);

                                        // Verify final position
                                        var finalBoxCenter = newViewport.GetBoxCenter();
                                        var finalBoxOutline = newViewport.GetBoxOutline();
                                        var finalMin = finalBoxOutline.MinimumPoint;
                                        var finalMax = finalBoxOutline.MaximumPoint;

                                        _logger?.LogInformation($"Final target viewport - Center: ({finalBoxCenter.X:F4}, {finalBoxCenter.Y:F4}), " +
                                            $"Min: ({finalMin.X:F4}, {finalMin.Y:F4}), Max: ({finalMax.X:F4}, {finalMax.Y:F4})");

                                        _logger?.LogInformation($"Successfully placed view '{sourceView.Name}' on sheet");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning($"Failed to place view '{sourceView.Name}' on sheet: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            _logger?.LogWarning($"Failed to clone view '{sourceView.Name}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error cloning viewport: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error cloning viewports: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Copies viewport properties from source to target
        /// </summary>
        private void CopyViewportProperties(Viewport sourceViewport, Viewport targetViewport)
        {
            try
            {
                // Copy viewport type if different
                if (sourceViewport.GetTypeId() != targetViewport.GetTypeId())
                {
                    try
                    {
                        targetViewport.ChangeTypeId(sourceViewport.GetTypeId());
                    }
                    catch
                    {
                        // Type might not exist in target document
                    }
                }

                // Copy label position offset if label is shown
                try
                {
                    if (sourceViewport.LabelLineLength > 0)
                    {
                        targetViewport.LabelLineLength = sourceViewport.LabelLineLength;
                        targetViewport.LabelOffset = sourceViewport.LabelOffset;
                    }
                }
                catch
                {
                    // Label properties might not be accessible
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error copying viewport properties: {ex.Message}");
            }
        }
    }
}
