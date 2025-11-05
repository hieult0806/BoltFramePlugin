using Autodesk.Revit.DB;
using LoBIM.Features.SheetManagement.Models;
using LoBIM.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoBIM.Features.SheetManagement.Services
{
    /// <summary>
    /// Implementation of sheet management service
    /// </summary>
    public class SheetManagementService : ISheetManagementService
    {
        private readonly ILoggingService _logger;

        public SheetManagementService(ILoggingService logger)
        {
            _logger = logger;
        }

        public List<SheetItemModel> GetAllSheets(Document document)
        {
            try
            {
                var sheets = new FilteredElementCollector(document)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate && !s.IsPlaceholder)
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                var sheetModels = new List<SheetItemModel>();
                int order = 0;

                foreach (var sheet in sheets)
                {
                    var model = new SheetItemModel
                    {
                        ElementId = (int)sheet.Id.Value,
                        SheetNumber = sheet.SheetNumber,
                        SheetName = sheet.Name,
                        OriginalSheetNumber = sheet.SheetNumber,
                        DisplayOrder = order,
                        OriginalDisplayOrder = order
                    };
                    order++;

                    // Extract all parameters
                    foreach (Parameter param in sheet.Parameters)
                    {
                        if (param.HasValue && param.StorageType != StorageType.None)
                        {
                            try
                            {
                                string value = GetParameterValueAsString(param);
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    model.Parameters[param.Definition.Name] = value;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to read parameter {param.Definition.Name}: {ex.Message}");
                            }
                        }
                    }

                    sheetModels.Add(model);
                }

                _logger.LogInformation($"Retrieved {sheetModels.Count} sheets from document");
                return sheetModels;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting all sheets: {ex.Message}", ex);
                return new List<SheetItemModel>();
            }
        }

        public List<string> GetAvailableSheetParameters(Document document)
        {
            try
            {
                var sheet = new FilteredElementCollector(document)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .FirstOrDefault(s => !s.IsTemplate);

                if (sheet == null)
                {
                    _logger.LogWarning("No sheets found in document");
                    return new List<string>();
                }

                var parameterNames = new HashSet<string>();

                foreach (Parameter param in sheet.Parameters)
                {
                    if (param.Definition != null)
                    {
                        parameterNames.Add(param.Definition.Name);
                    }
                }

                var sortedList = parameterNames.OrderBy(p => p).ToList();
                _logger.LogInformation($"Found {sortedList.Count} sheet parameters");
                return sortedList;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting sheet parameters: {ex.Message}", ex);
                return new List<string>();
            }
        }

        public bool RenumberSheets(Document document, SheetRenumberingModel model)
        {
            try
            {
                if (model.Sheets == null || model.Sheets.Count == 0)
                {
                    _logger.LogWarning("No sheets provided for renumbering");
                    return false;
                }

                using (Transaction trans = new Transaction(document, "Renumber Sheets"))
                {
                    trans.Start();

                    try
                    {
                        // First pass: Rename all sheets to temporary names to avoid conflicts
                        var tempNames = new Dictionary<int, string>();
                        foreach (var item in model.Sheets)
                        {
                            var sheet = GetSheetById(document, item.ElementId);
                            if (sheet != null)
                            {
                                string tempNumber = $"TEMP_{Guid.NewGuid().ToString().Substring(0, 8)}";
                                tempNames[item.ElementId] = sheet.SheetNumber;
                                sheet.SheetNumber = tempNumber;
                            }
                        }

                        // Second pass: Apply actual new numbers
                        foreach (var item in model.Sheets)
                        {
                            var sheet = GetSheetById(document, item.ElementId);
                            if (sheet != null)
                            {
                                sheet.SheetNumber = item.NewSheetNumber;
                                _logger.LogInformation($"Renumbered sheet from {tempNames[item.ElementId]} to {item.NewSheetNumber}");
                            }
                        }

                        trans.Commit();
                        _logger.LogInformation($"Successfully renumbered {model.Sheets.Count} sheets");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        _logger.LogError($"Error during renumbering transaction: {ex.Message}", ex);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renumbering sheets: {ex.Message}", ex);
                return false;
            }
        }

        public ViewSheet GetSheetById(Document document, int elementId)
        {
            try
            {
                Element element = document.GetElement(new ElementId(elementId));
                return element as ViewSheet;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting sheet by ID {elementId}: {ex.Message}", ex);
                return null;
            }
        }

        public bool IsSheetNumberUnique(Document document, string sheetNumber, int excludeElementId = -1)
        {
            try
            {
                var sheets = new FilteredElementCollector(document)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => s.SheetNumber == sheetNumber && (int)s.Id.Value != excludeElementId);

                return !sheets.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking sheet number uniqueness: {ex.Message}", ex);
                return false;
            }
        }

        public Dictionary<int, string> PreviewRenumbering(SheetRenumberingModel model)
        {
            var preview = new Dictionary<int, string>();

            if (model.Sheets == null || model.Sheets.Count == 0)
                return preview;

            for (int i = 0; i < model.Sheets.Count; i++)
            {
                int newNumber = model.StartNumber + (i * model.Increment);
                string paddedNumber = newNumber.ToString().PadLeft(model.PaddingDigits, '0');
                string fullSheetNumber = $"{model.Prefix}{paddedNumber}";

                preview[model.Sheets[i].ElementId] = fullSheetNumber;
            }

            return preview;
        }

        private string GetParameterValueAsString(Parameter param)
        {
            if (!param.HasValue)
                return string.Empty;

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString() ?? string.Empty;

                case StorageType.Integer:
                    return param.AsInteger().ToString();

                case StorageType.Double:
                    return param.AsDouble().ToString("F2");

                case StorageType.ElementId:
                    ElementId elemId = param.AsElementId();
                    if (elemId != null && elemId.Value > 0)
                    {
                        try
                        {
                            Element elem = param.Element.Document.GetElement(elemId);
                            return elem?.Name ?? elemId.Value.ToString();
                        }
                        catch
                        {
                            return elemId.Value.ToString();
                        }
                    }
                    return string.Empty;

                default:
                    return string.Empty;
            }
        }
    }
}
