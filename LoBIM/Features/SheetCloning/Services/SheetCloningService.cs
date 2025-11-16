using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Features.SheetCloning.Helpers;
using LoBIM.Features.SheetCloning.Models;
using LoBIM.Features.ViewCloning.Models;
using LoBIM.Features.ViewCloning.Services;
using LoBIM.Services;
using LoBIM.Services.Parameters;
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
        private readonly IProjectParameterService _parameterService;
        private readonly SheetSourceTrackingHelper _sourceTrackingHelper;

        public SheetCloningService(ILoggingService logger, IViewCloningService viewCloningService, IProjectParameterService parameterService)
        {
            _logger = logger;
            _viewCloningService = viewCloningService;
            _parameterService = parameterService;
            _sourceTrackingHelper = new SheetSourceTrackingHelper(logger, parameterService);
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
        public int CloneSheets(Document hostDoc, List<LinkedSheetInfo> sheets, Dictionary<string, Document> linkedDocuments, HashSet<string>? createdSheetNumbers = null, ViewPositioningMode viewPositioningMode = ViewPositioningMode.InternalOriginToInternalOrigin)
        {
            int successCount = 0;
            // Track sheet numbers created in this session to avoid conflicts
            // Use provided set or create new one
            if (createdSheetNumbers == null)
            {
                createdSheetNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            // Track cloned views to avoid duplicating views when cloning multiple sheets
            var clonedViewsCache = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);

            _logger?.LogInformation($"=== SHEET CLONING SESSION START ===");
            _logger?.LogInformation($"View positioning mode: {viewPositioningMode}");
            _logger?.LogInformation($"Sheets to clone: {sheets.Count}");

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
                        var clonedSheet = CloneSingleSheet(hostDoc, sourceSheet, linkedDoc, createdSheetNumbers, clonedViewsCache, viewPositioningMode);
                        if (clonedSheet != null)
                        {
                            // Note: The sheet number is already added to createdSheetNumbers inside CloneSingleSheet
                            // to reserve it before the sheet is actually created
                            _logger?.LogInformation($"Successfully cloned sheet: {sheetInfo.SheetNumber} -> {clonedSheet.SheetNumber}");
                            successCount++;
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

            _logger?.LogInformation($"=== SHEET CLONING SESSION END ===");
            _logger?.LogInformation($"Successfully cloned {successCount} out of {sheets.Count} sheets");

            return successCount;
        }

        /// <summary>
        /// Clones a single sheet from linked document to host document
        /// </summary>
        private ViewSheet CloneSingleSheet(Document hostDoc, ViewSheet sourceSheet, Document linkedDoc, HashSet<string> createdSheetNumbers, Dictionary<string, ElementId> clonedViewsCache, ViewPositioningMode viewPositioningMode)
        {
            // STEP 1: Calculate the unique sheet number BEFORE creating the sheet
            _logger?.LogInformation($"=== Calculating unique sheet number for '{sourceSheet.SheetNumber}' ===");
            var uniqueSheetNumber = GenerateUniqueSheetNumber(hostDoc, sourceSheet.SheetNumber, null, createdSheetNumbers);
            _logger?.LogInformation($"Calculated unique sheet number: '{uniqueSheetNumber}'");

            // CRITICAL: Reserve this number immediately to prevent race conditions
            // This ensures that if we're cloning multiple sheets, each one gets a unique number
            if (createdSheetNumbers != null && !createdSheetNumbers.Contains(uniqueSheetNumber))
            {
                createdSheetNumbers.Add(uniqueSheetNumber);
                _logger?.LogInformation($"Reserved sheet number '{uniqueSheetNumber}' in session cache");
            }

            // Get or create matching titleblock
            var titleblockId = GetOrCreateTitleblock(hostDoc, sourceSheet, linkedDoc);
            if (titleblockId == null || titleblockId == ElementId.InvalidElementId)
            {
                _logger?.LogWarning($"Could not find or create titleblock for sheet {sourceSheet.SheetNumber}");
                // Remove the reserved number since we're not creating the sheet
                if (createdSheetNumbers != null)
                {
                    createdSheetNumbers.Remove(uniqueSheetNumber);
                }
                return null;
            }

            // STEP 2: Create the sheet - Revit will auto-assign a temporary number
            ViewSheet newSheet = ViewSheet.Create(hostDoc, titleblockId);
            if (newSheet == null)
            {
                _logger?.LogError($"Failed to create sheet");
                // Remove the reserved number since sheet creation failed
                if (createdSheetNumbers != null)
                {
                    createdSheetNumbers.Remove(uniqueSheetNumber);
                }
                return null;
            }

            var autoGeneratedNumber = newSheet.SheetNumber;
            _logger?.LogInformation($"Sheet created with Revit's auto-generated number: '{autoGeneratedNumber}' (ID: {newSheet.Id})");

            // Check if Revit auto-assigned a number that conflicts with our target
            if (autoGeneratedNumber.Equals(uniqueSheetNumber, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogError($"CONFLICT! Revit auto-assigned the same number '{autoGeneratedNumber}' that we wanted to use!");
            }

            // STEP 3: Immediately rename it to a guaranteed unique temporary number
            var tempNumber = $"TEMP_{Guid.NewGuid().ToString().Substring(0, 8)}";
            try
            {
                newSheet.SheetNumber = tempNumber;
                _logger?.LogInformation($"Set temporary number: '{tempNumber}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to set temporary number '{tempNumber}': {ex.Message}");
                try { hostDoc.Delete(newSheet.Id); } catch { }
                // Remove the reserved number since we're deleting the sheet
                createdSheetNumbers?.Remove(uniqueSheetNumber);
                return null;
            }

            // STEP 4: Now set the final desired sheet number
            try
            {
                newSheet.SheetNumber = uniqueSheetNumber;
                _logger?.LogInformation($"Successfully set final sheet number to: '{uniqueSheetNumber}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to set sheet number to '{uniqueSheetNumber}': {ex.Message}");

                // Check if the error is about the number already being in use
                if (ex.Message.Contains("Sheet Number is already in use"))
                {
                    _logger?.LogError($"Sheet number '{uniqueSheetNumber}' is already in use!");
                    _logger?.LogError($"This suggests a race condition or the number was used between calculation and assignment.");

                    // Try to generate a new unique number as a recovery attempt
                    _logger?.LogInformation($"Attempting to generate a new unique sheet number...");
                    try
                    {
                        // Re-calculate with the current sheet excluded
                        var recoveryNumber = GenerateUniqueSheetNumber(hostDoc, sourceSheet.SheetNumber, newSheet.Id, createdSheetNumbers);
                        _logger?.LogInformation($"Generated recovery number: '{recoveryNumber}'");

                        newSheet.SheetNumber = recoveryNumber;
                        _logger?.LogInformation($"Successfully set recovery sheet number to: '{recoveryNumber}'");

                        // Update the reserved number
                        createdSheetNumbers?.Remove(uniqueSheetNumber);
                        createdSheetNumbers?.Add(recoveryNumber);

                        // Update the uniqueSheetNumber for subsequent code
                        uniqueSheetNumber = recoveryNumber;
                    }
                    catch (Exception recoveryEx)
                    {
                        _logger?.LogError($"Recovery attempt failed: {recoveryEx.Message}");
                        try { hostDoc.Delete(newSheet.Id); } catch { }
                        createdSheetNumbers?.Remove(uniqueSheetNumber);
                        return null;
                    }
                }
                else
                {
                    // Different error, just clean up and exit
                    try { hostDoc.Delete(newSheet.Id); } catch { }
                    createdSheetNumbers?.Remove(uniqueSheetNumber);
                    return null;
                }
            }

            // Set the sheet name (number was already set above immediately after creation)
            _logger?.LogInformation($"Setting sheet name...");
            try
            {
                newSheet.Name = sourceSheet.Name;
                _logger?.LogInformation($"Successfully set sheet name to: {newSheet.Name}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error setting sheet name: {ex.Message}", ex);
                // Try to delete the failed sheet to avoid leaving orphaned sheets
                try
                {
                    hostDoc.Delete(newSheet.Id);
                    _logger?.LogInformation($"Deleted failed sheet {newSheet.Id}");
                }
                catch (Exception delEx)
                {
                    _logger?.LogWarning($"Could not delete failed sheet: {delEx.Message}");
                }
                throw; // Re-throw to prevent creating invalid sheets
            }

            // Copy parameters
            _logger?.LogInformation($"Copying sheet parameters...");
            CopySheetParameters(sourceSheet, newSheet);

            // Store source tracking information
            _logger?.LogInformation($"Storing source tracking information...");
            string linkedFileName = System.IO.Path.GetFileNameWithoutExtension(linkedDoc.Title);
            _sourceTrackingHelper.StoreSourceSheetInfo(sourceSheet, newSheet, linkedFileName);

            // Verify the sheet number is still correct before cloning viewports
            _logger?.LogInformation($"Verifying sheet number before cloning viewports: {newSheet.SheetNumber}");
            if (newSheet.SheetNumber != uniqueSheetNumber)
            {
                _logger?.LogWarning($"Sheet number changed from '{uniqueSheetNumber}' to '{newSheet.SheetNumber}'! Resetting...");
                newSheet.SheetNumber = uniqueSheetNumber;
            }

            // Clone viewports (views placed on sheet)
            _logger?.LogInformation($"Cloning viewports with positioning mode: {viewPositioningMode}...");
            try
            {
                CloneViewports(hostDoc, sourceSheet, newSheet, linkedDoc, titleblockId, clonedViewsCache, viewPositioningMode);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error during viewport cloning: {ex.Message}", ex);
                // Check if the error is about sheet numbers
                if (ex.Message.Contains("Sheet Number is already in use"))
                {
                    _logger?.LogError($"Sheet number conflict detected during viewport cloning! Current sheet number: {newSheet.SheetNumber}");
                }
                throw;
            }

            // Verify the sheet number is still correct after cloning viewports
            _logger?.LogInformation($"Verifying sheet number after cloning viewports: {newSheet.SheetNumber}");
            if (newSheet.SheetNumber != uniqueSheetNumber)
            {
                _logger?.LogError($"Sheet number changed from '{uniqueSheetNumber}' to '{newSheet.SheetNumber}' during viewport cloning!");
                // Try to set it back
                try
                {
                    newSheet.SheetNumber = uniqueSheetNumber;
                    _logger?.LogInformation($"Successfully reset sheet number to '{uniqueSheetNumber}'");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to reset sheet number: {ex.Message}");
                    throw;
                }
            }

            _logger?.LogInformation($"=== SHEET CLONING COMPLETED SUCCESSFULLY FOR {newSheet.SheetNumber} ===");
            return newSheet;
        }

        /// <summary>
        /// Finds a sheet that's blocking Revit's auto-numbering and temporarily renames it.
        /// Must be called in a separate transaction BEFORE the main cloning transaction.
        /// </summary>
        public ViewSheet FindAndClearAutoNumberBlocker(Document hostDoc)
        {
            try
            {
                // Revit typically uses patterns like "Unnamed", "Sheet - 0001", etc.
                // Find sheets with these patterns and rename one temporarily
                var sheets = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .ToList();

                // Look for sheets that match Revit's auto-naming patterns
                var autoNamedSheets = sheets.Where(s =>
                    s.SheetNumber.StartsWith("Unnamed", StringComparison.OrdinalIgnoreCase) ||
                    s.SheetNumber.StartsWith("Sheet -", StringComparison.OrdinalIgnoreCase) ||
                    s.SheetNumber.StartsWith("Sheet-", StringComparison.OrdinalIgnoreCase) ||
                    s.SheetNumber.StartsWith("A000", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (autoNamedSheets.Any())
                {
                    var blocker = autoNamedSheets.First();
                    var originalNumber = blocker.SheetNumber;

                    // Rename it to something unique
                    var tempNumber = $"TEMP_{Guid.NewGuid().ToString().Substring(0, 8)}";
                    blocker.SheetNumber = tempNumber;

                    _logger?.LogInformation($"Temporarily renamed blocking sheet from '{originalNumber}' to '{tempNumber}'");
                    return blocker;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error finding auto-number blocker: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets or creates a matching titleblock type in the host document
        /// If the titleblock doesn't exist in the host, it will be transferred from the source document
        /// </summary>
        private ElementId GetOrCreateTitleblock(Document hostDoc, ViewSheet sourceSheet, Document linkedDoc)
        {
            _logger?.LogInformation($"=== GETTING OR CREATING TITLEBLOCK ===");

            // STEP 1: Get the titleblock instance from the source sheet
            var sourceTitleblockInstances = new FilteredElementCollector(linkedDoc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .Where(e => e.OwnerViewId == sourceSheet.Id)
                .ToList();

            if (sourceTitleblockInstances.Count == 0)
            {
                _logger?.LogWarning($"Source sheet {sourceSheet.SheetNumber} has no titleblock");
                // Fallback to first available titleblock in host
                return GetFirstAvailableTitleblock(hostDoc);
            }

            var sourceTitleblockInstance = sourceTitleblockInstances.First();
            var sourceTitleblockTypeId = sourceTitleblockInstance.GetTypeId();
            var sourceTitleblockType = linkedDoc.GetElement(sourceTitleblockTypeId) as FamilySymbol;

            if (sourceTitleblockType == null)
            {
                _logger?.LogWarning($"Could not get titleblock type from source sheet");
                return GetFirstAvailableTitleblock(hostDoc);
            }

            var sourceFamily = sourceTitleblockType.Family;
            var sourceFamilyName = sourceFamily?.Name ?? "Unknown";
            var sourceTypeName = sourceTitleblockType.Name;

            _logger?.LogInformation($"Source titleblock: Family='{sourceFamilyName}', Type='{sourceTypeName}'");

            // STEP 2: Try to find matching titleblock in host document by family name and type name
            var hostTitleblockTypes = new FilteredElementCollector(hostDoc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();

            _logger?.LogInformation($"Found {hostTitleblockTypes.Count} titleblock types in host document");

            // Log all titleblock families in host for debugging
            var uniqueFamilies = hostTitleblockTypes
                .Select(tb => tb.Family?.Name)
                .Distinct()
                .ToList();
            _logger?.LogInformation($"Titleblock families in host: {string.Join(", ", uniqueFamilies)}");

            // Try exact match by family name and type name
            var matchingTitleblock = hostTitleblockTypes.FirstOrDefault(tb =>
                tb.Family?.Name == sourceFamilyName &&
                tb.Name == sourceTypeName);

            if (matchingTitleblock != null)
            {
                _logger?.LogInformation($"Found exact matching titleblock in host: '{sourceFamilyName}' - '{sourceTypeName}'");
                return matchingTitleblock.Id;
            }

            // Try to find by family name only (including Revit-renamed versions with suffixes like ".1", ".2", etc.)
            // First try exact family name
            matchingTitleblock = hostTitleblockTypes.FirstOrDefault(tb =>
                tb.Family?.Name == sourceFamilyName);

            if (matchingTitleblock != null)
            {
                _logger?.LogInformation($"Found titleblock with exact family name '{sourceFamilyName}'. Using type '{matchingTitleblock.Name}'");
                return matchingTitleblock.Id;
            }

            // Try to find by family name with Revit's auto-rename pattern (e.g., "FamilyName.1", "FamilyName.2")
            // This handles cases where Revit renamed the family due to conflicts
            matchingTitleblock = hostTitleblockTypes.FirstOrDefault(tb =>
            {
                var familyName = tb.Family?.Name;
                if (string.IsNullOrEmpty(familyName))
                    return false;

                // Check if it starts with the source family name followed by a dot and number
                if (familyName.StartsWith(sourceFamilyName + "."))
                {
                    // Verify it's actually a Revit rename pattern (ends with .number)
                    var suffix = familyName.Substring(sourceFamilyName.Length);
                    return System.Text.RegularExpressions.Regex.IsMatch(suffix, @"^\.\d+$");
                }

                return false;
            });

            if (matchingTitleblock != null)
            {
                _logger?.LogInformation($"Found titleblock with Revit-renamed family '{matchingTitleblock.Family?.Name}' (original: '{sourceFamilyName}'). Using type '{matchingTitleblock.Name}'");
                return matchingTitleblock.Id;
            }

            // STEP 3: No matching titleblock found - need to transfer from source document
            _logger?.LogInformation($"No matching titleblock found in host. Checking if we need to transfer from source document...");

            if (sourceFamily != null)
            {
                // Before attempting transfer, do a final comprehensive check for any similar families
                // This includes checking all families, not just titleblocks, in case it was imported as a different category
                var allHostFamilies = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .ToList();

                _logger?.LogInformation($"Checking {allHostFamilies.Count} families in host document for potential matches...");

                // Check for exact match or Revit-renamed versions
                var existingFamily = allHostFamilies.FirstOrDefault(f =>
                {
                    var name = f.Name;
                    if (name == sourceFamilyName)
                        return true;

                    // Check for Revit rename pattern (FamilyName.1, FamilyName.2, etc.)
                    if (name.StartsWith(sourceFamilyName + "."))
                    {
                        var suffix = name[sourceFamilyName.Length..];
                        return System.Text.RegularExpressions.Regex.IsMatch(suffix, @"^\.\d+$");
                    }

                    return false;
                });

                if (existingFamily != null)
                {
                    _logger?.LogInformation($"Found existing family '{existingFamily.Name}' in host document that matches source '{sourceFamilyName}'");

                    // Get the family symbols from this existing family
                    var existingSymbolIds = existingFamily.GetFamilySymbolIds();
                    if (existingSymbolIds.Count > 0)
                    {
                        // Try to find matching type name
                        foreach (var symbolId in existingSymbolIds)
                        {
                            var symbol = hostDoc.GetElement(symbolId) as FamilySymbol;
                            if (symbol != null && symbol.Name == sourceTypeName)
                            {
                                if (!symbol.IsActive)
                                {
                                    symbol.Activate();
                                }
                                _logger?.LogInformation($"Using existing type '{symbol.Name}' from family '{existingFamily.Name}'");
                                return symbol.Id;
                            }
                        }

                        // No matching type found, use first available
                        var firstSymbol = hostDoc.GetElement(existingSymbolIds.First()) as FamilySymbol;
                        if (firstSymbol != null)
                        {
                            if (!firstSymbol.IsActive)
                            {
                                firstSymbol.Activate();
                            }
                            _logger?.LogInformation($"Using first available type '{firstSymbol.Name}' from existing family '{existingFamily.Name}'");
                            return firstSymbol.Id;
                        }
                    }
                }

                // No existing family found, proceed with transfer
                _logger?.LogInformation($"No existing family found. Attempting to transfer from source document...");

                try
                {
                    var transferredTitleblockId = TransferTitleblockFamily(hostDoc, linkedDoc, sourceFamily, sourceTitleblockType);

                    if (transferredTitleblockId != null && transferredTitleblockId != ElementId.InvalidElementId)
                    {
                        _logger?.LogInformation($"Successfully transferred titleblock '{sourceFamilyName}' - '{sourceTypeName}' to host document");
                        return transferredTitleblockId;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to transfer titleblock family: {ex.Message}", ex);

                    // After failed transfer, check again if the family was created despite the error
                    // This can happen when Revit renames the family but throws an exception
                    _logger?.LogInformation($"Transfer failed, but checking if family was created anyway...");

                    var retryCheck = new FilteredElementCollector(hostDoc)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .WhereElementIsElementType()
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(tb =>
                        {
                            var familyName = tb.Family?.Name;
                            if (string.IsNullOrEmpty(familyName))
                                return false;

                            // Check exact match or renamed version
                            if (familyName == sourceFamilyName)
                                return true;

                            if (familyName.StartsWith(sourceFamilyName + "."))
                            {
                                var suffix = familyName[sourceFamilyName.Length..];
                                return System.Text.RegularExpressions.Regex.IsMatch(suffix, @"^\.\d+$");
                            }

                            return false;
                        });

                    if (retryCheck != null)
                    {
                        _logger?.LogInformation($"Found titleblock after failed transfer: '{retryCheck.Family?.Name}' - '{retryCheck.Name}'. Using it.");
                        if (!retryCheck.IsActive)
                        {
                            retryCheck.Activate();
                        }
                        return retryCheck.Id;
                    }
                }
            }
            else
            {
                _logger?.LogWarning($"Source family is null, cannot transfer titleblock");
            }

            // STEP 4: Fallback - use first available titleblock in host
            _logger?.LogWarning($"Could not transfer titleblock, falling back to first available titleblock in host");
            return GetFirstAvailableTitleblock(hostDoc);
        }

        /// <summary>
        /// Transfers a titleblock family from source document to host document
        /// </summary>
        private ElementId TransferTitleblockFamily(Document hostDoc, Document linkedDoc, Family sourceFamily, FamilySymbol sourceTitleblockType)
        {
            try
            {
                // Method 1: Try to copy the family symbol directly using ElementTransformUtils.CopyElements
                // This is the preferred method as it preserves all family parameters and types
                _logger?.LogInformation($"Attempting to copy titleblock family using CopyElements...");

                var elementsToCopy = new List<ElementId> { sourceFamily.Id };
                var copyOptions = new CopyPasteOptions();
                copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeNamesHandler());

                var copiedElementIds = ElementTransformUtils.CopyElements(
                    linkedDoc,
                    elementsToCopy,
                    hostDoc,
                    Transform.Identity,
                    copyOptions);

                if (copiedElementIds != null && copiedElementIds.Count > 0)
                {
                    // The copied element should be the Family
                    var copiedFamilyId = copiedElementIds.First();
                    var copiedFamily = hostDoc.GetElement(copiedFamilyId) as Family;

                    if (copiedFamily != null)
                    {
                        _logger?.LogInformation($"Successfully copied family '{copiedFamily.Name}'");

                        // Find the matching type in the newly copied family
                        var copiedFamilySymbolIds = copiedFamily.GetFamilySymbolIds();
                        foreach (var symbolId in copiedFamilySymbolIds)
                        {
                            var symbol = hostDoc.GetElement(symbolId) as FamilySymbol;
                            if (symbol != null && symbol.Name == sourceTitleblockType.Name)
                            {
                                // Activate the symbol if not already activated
                                if (!symbol.IsActive)
                                {
                                    symbol.Activate();
                                }
                                _logger?.LogInformation($"Found and activated matching type '{symbol.Name}' in copied family");
                                return symbol.Id;
                            }
                        }

                        // If exact type not found, use the first type in the family
                        if (copiedFamilySymbolIds.Count > 0)
                        {
                            var firstSymbol = hostDoc.GetElement(copiedFamilySymbolIds.First()) as FamilySymbol;
                            if (firstSymbol != null)
                            {
                                if (!firstSymbol.IsActive)
                                {
                                    firstSymbol.Activate();
                                }
                                _logger?.LogInformation($"Using first available type '{firstSymbol.Name}' from copied family");
                                return firstSymbol.Id;
                            }
                        }
                    }
                }

                _logger?.LogWarning($"CopyElements did not return expected results");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error copying titleblock family with CopyElements: {ex.Message}", ex);
            }

            // Method 2: Try to export and reload the family
            // This method requires the family to be saved as a file first
            try
            {
                _logger?.LogInformation($"Attempting to transfer titleblock via family document...");

                // Try to open the family document and save it, then load it into the host
                var familyDoc = linkedDoc.EditFamily(sourceFamily);
                if (familyDoc != null)
                {
                    // Create a temporary file path
                    var tempPath = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"{sourceFamily.Name}_{Guid.NewGuid()}.rfa");

                    _logger?.LogInformation($"Saving family to temporary location: {tempPath}");

                    // Save the family document
                    var saveOptions = new SaveAsOptions();
                    saveOptions.OverwriteExistingFile = true;
                    familyDoc.SaveAs(tempPath, saveOptions);
                    familyDoc.Close(false);

                    // Load the family into the host document
                    _logger?.LogInformation($"Loading family from temporary file into host document...");
                    Family loadedFamily;
                    bool loaded = hostDoc.LoadFamily(tempPath, new TitleblockFamilyLoadOptions(_logger), out loadedFamily);

                    // Clean up the temporary file
                    try
                    {
                        if (System.IO.File.Exists(tempPath))
                        {
                            System.IO.File.Delete(tempPath);
                            _logger?.LogInformation($"Deleted temporary file: {tempPath}");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger?.LogWarning($"Could not delete temporary file: {cleanupEx.Message}");
                    }

                    if (loaded && loadedFamily != null)
                    {
                        _logger?.LogInformation($"Successfully loaded family '{loadedFamily.Name}' into host document");

                        // Find the matching type
                        var symbolIds = loadedFamily.GetFamilySymbolIds();
                        foreach (var symbolId in symbolIds)
                        {
                            var symbol = hostDoc.GetElement(symbolId) as FamilySymbol;
                            if (symbol != null && symbol.Name == sourceTitleblockType.Name)
                            {
                                if (!symbol.IsActive)
                                {
                                    symbol.Activate();
                                }
                                return symbol.Id;
                            }
                        }

                        // Use first available type
                        if (symbolIds.Count > 0)
                        {
                            var firstSymbol = hostDoc.GetElement(symbolIds.First()) as FamilySymbol;
                            if (firstSymbol != null)
                            {
                                if (!firstSymbol.IsActive)
                                {
                                    firstSymbol.Activate();
                                }
                                return firstSymbol.Id;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error transferring titleblock via family document: {ex.Message}", ex);
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Gets the first available titleblock in the host document as a fallback
        /// </summary>
        private ElementId GetFirstAvailableTitleblock(Document hostDoc)
        {
            var titleblockTypes = new FilteredElementCollector(hostDoc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();

            if (titleblockTypes.Count > 0)
            {
                var firstTitleblock = titleblockTypes.First();
                _logger?.LogInformation($"Using first available titleblock: '{firstTitleblock.Family?.Name}' - '{firstTitleblock.Name}'");
                return firstTitleblock.Id;
            }

            _logger?.LogWarning($"No titleblocks found in host document");
            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Handler for duplicate type names when copying elements
        /// </summary>
        private class DuplicateTypeNamesHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                // Use the destination type if it already exists
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }

        /// <summary>
        /// Load options for titleblock families
        /// </summary>
        private class TitleblockFamilyLoadOptions : IFamilyLoadOptions
        {
            private readonly ILoggingService? _logger;

            public TitleblockFamilyLoadOptions(ILoggingService? logger = null)
            {
                _logger = logger;
            }

            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                // If family already exists, use the existing one instead of overwriting
                // This prevents Revit from renaming the family with .1, .2, etc. suffixes
                _logger?.LogInformation($"Family already exists in host document. Using existing family (familyInUse={familyInUse})");
                overwriteParameterValues = false;
                return false; // Don't load/overwrite - use existing family
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                // Use the existing shared family
                _logger?.LogInformation($"Shared family '{sharedFamily?.Name}' found. Using existing family (familyInUse={familyInUse})");
                source = FamilySource.Project;
                overwriteParameterValues = false;
                return false; // Don't overwrite - use existing
            }
        }

        /// <summary>
        /// Generates a unique sheet number in the host document
        /// </summary>
        /// <param name="excludeSheetId">Optional sheet ID to exclude from uniqueness check (e.g., the newly created sheet)</param>
        /// <param name="createdSheetNumbers">Sheet numbers created in the current cloning session</param>
        private string GenerateUniqueSheetNumber(Document hostDoc, string baseNumber, ElementId excludeSheetId = null, HashSet<string> createdSheetNumbers = null)
        {
            // Get all existing sheet numbers (including those in Sheet Collections)
            // Exclude the newly created sheet if provided to avoid self-conflict
            var allSheets = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .ToList();

            if (excludeSheetId != null)
            {
                var excludedSheet = allSheets.FirstOrDefault(s => s.Id == excludeSheetId);
                if (excludedSheet != null)
                {
                    _logger?.LogInformation($"Excluding sheet ID {excludeSheetId} (number: '{excludedSheet.SheetNumber}') from uniqueness check");
                }
            }

            var existingSheets = allSheets.Where(s => excludeSheetId == null || s.Id != excludeSheetId).ToList();

            var existingNumbers = existingSheets
                .Select(s => s.SheetNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase); // Case-insensitive comparison

            // Log sheet collection information
            foreach (var sheet in allSheets.Take(5))
            {
                var isInCollection = sheet.LookupParameter("Sheet Collection")?.AsString();
                _logger?.LogInformation($"  Sample sheet {sheet.SheetNumber}: InCollection={isInCollection ?? "Not in collection"}");
            }

            // Also add sheet numbers created in this session that might not be committed yet
            if (createdSheetNumbers != null)
            {
                foreach (var createdNumber in createdSheetNumbers)
                {
                    existingNumbers.Add(createdNumber);
                }
                _logger?.LogInformation($"Also checking against {createdSheetNumbers.Count} sheet(s) created in this session");
            }

            _logger?.LogInformation($"Checking uniqueness for sheet number: '{baseNumber}'");
            _logger?.LogInformation($"Found {existingSheets.Count} existing sheets in host document (excluding the new sheet)");

            // Log existing sheet numbers for debugging
            if (existingNumbers.Count > 0 && existingNumbers.Count <= 20)
            {
                _logger?.LogInformation($"Existing sheet numbers: {string.Join(", ", existingNumbers.Take(20))}");
            }

            // Start with suffix 0 and keep incrementing until we find a unique number
            // We always use a suffix to differentiate cloned sheets from originals
            string newNumber;
            int suffix = 0;

            do
            {
                if (suffix == 0)
                {
                    // First try the base number without suffix
                    newNumber = baseNumber;
                    _logger?.LogInformation($"Trying base number: '{newNumber}'");
                }
                else
                {
                    // Add suffix for subsequent attempts
                    newNumber = $"{baseNumber}-{suffix:D2}";
                    _logger?.LogInformation($"Trying with suffix {suffix}: '{newNumber}'");
                }

                // Check if this number exists
                if (existingNumbers.Contains(newNumber))
                {
                    _logger?.LogInformation($"  → '{newNumber}' already exists, incrementing...");
                }
                else
                {
                    _logger?.LogInformation($"  → '{newNumber}' is available!");
                }

                suffix++;

                // Safety check to prevent infinite loop
                if (suffix > 999)
                {
                    _logger?.LogError($"Could not generate unique sheet number for {baseNumber} - too many duplicates");
                    throw new InvalidOperationException($"Could not generate unique sheet number for {baseNumber}");
                }
            }
            while (existingNumbers.Contains(newNumber));

            if (newNumber != baseNumber)
            {
                _logger?.LogInformation($"Sheet number '{baseNumber}' already exists, using '{newNumber}' instead");
            }
            else
            {
                _logger?.LogInformation($"Sheet number '{baseNumber}' is unique");
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
        private void CloneViewports(Document hostDoc, ViewSheet sourceSheet, ViewSheet targetSheet, Document linkedDoc, ElementId targetTitleblockId, Dictionary<string, ElementId> clonedViewsCache, ViewPositioningMode viewPositioningMode)
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

                        // Clone the view using the ViewCloningService with the specified positioning mode
                        _logger?.LogInformation($"Cloning view '{sourceView.Name}' with positioning mode: {viewPositioningMode}");
                        var clonedViewIdResult = _viewCloningService.CloneView(
                            hostDoc,
                            linkedViewInfo,
                            "",
                            viewPositioningMode,
                            clonedViewsCache);

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
