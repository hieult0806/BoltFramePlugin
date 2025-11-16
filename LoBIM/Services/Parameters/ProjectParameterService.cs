using Autodesk.Revit.DB;
using LoBIM.Services.Parameters.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LoBIM.Services.Parameters
{
    /// <summary>
    /// Global service for managing project parameters across all features
    /// Provides a unified interface for creating, updating, and reading project parameters
    /// </summary>
    public class ProjectParameterService : IProjectParameterService
    {
        private readonly ILoggingService _logger;
        private const string LOBIM_SHARED_PARAM_GROUP = "LoBIM";

        public ProjectParameterService(ILoggingService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Parameter Creation

        public ParameterCreationResult EnsureParameterExists(Document doc, ProjectParameterDefinition parameterDef)
        {
            var result = new ParameterCreationResult
            {
                ParameterName = parameterDef.Name,
                Success = false
            };

            try
            {
                // Check if parameter already exists
                var testElement = GetTestElementForCategories(doc, parameterDef.Categories);
                if (testElement != null)
                {
                    var existingParam = testElement.LookupParameter(parameterDef.Name);
                    if (existingParam != null)
                    {
                        result.Success = true;
                        result.Status = ParameterStatus.AlreadyExists;
                        result.AlreadyExisted = true;
                        return result;
                    }
                }

                _logger.LogInformation($"Creating project parameter: {parameterDef.Name}");

                // Resolve ForgeTypeIds if not already set
                if (parameterDef.ForgeParameterType == null)
                {
                    parameterDef.ForgeParameterType = GetForgeTypeIdFromString(parameterDef.ParameterType);
                }

                if (parameterDef.ForgeGroupType == null)
                {
                    parameterDef.ForgeGroupType = GetGroupTypeIdFromString(parameterDef.GroupName);
                }

                using (Transaction trans = new Transaction(doc, $"Create Parameter: {parameterDef.Name}"))
                {
                    trans.Start();

                    // Get categories
                    CategorySet categories = doc.Application.Create.NewCategorySet();
                    foreach (var categoryName in parameterDef.Categories)
                    {
                        var category = GetCategoryByName(doc, categoryName);
                        if (category != null)
                        {
                            categories.Insert(category);
                        }
                        else
                        {
                            _logger.LogWarning($"Category not found: {categoryName}");
                        }
                    }

                    if (categories.IsEmpty)
                    {
                        result.ErrorMessage = "No valid categories found";
                        result.Status = ParameterStatus.Failed;
                        return result;
                    }

                    Definition def = null;

                    if (parameterDef.IsProjectParameter)
                    {
                        // Create PROJECT PARAMETER (internal to document, not locked by templates)
                        _logger.LogInformation($"Creating as Project Parameter (not locked by templates)");

                        // Try to find existing parameter definition by name
                        var bindingMap = doc.ParameterBindings;
                        var iterator = bindingMap.ForwardIterator();
                        while (iterator.MoveNext())
                        {
                            var existingDef = iterator.Key as InternalDefinition;
                            if (existingDef != null && existingDef.Name == parameterDef.Name)
                            {
                                def = existingDef;
                                break;
                            }
                        }

                        // If not found, create new internal definition
                        if (def == null)
                        {
                            // For project parameters, we create an InternalDefinition
                            // This is done by creating a temporary parameter and getting its definition
                            // Unfortunately, Revit API doesn't provide a direct way to create InternalDefinition
                            // So we use GlobalParameter as a workaround or use the binding directly

                            // Alternative: Use shared parameter file temporarily, then the parameter becomes "project-based"
                            // when the shared parameter file is no longer referenced
                            def = CreateInternalParameterDefinition(doc, parameterDef);
                        }
                    }
                    else
                    {
                        // Create SHARED PARAMETER (from shared parameter file, can be locked by templates)
                        _logger.LogInformation($"Creating as Shared Parameter (from shared parameter file)");

                        DefinitionFile defFile = doc.Application.OpenSharedParameterFile();
                        if (defFile == null)
                        {
                            result.ErrorMessage = "Shared parameter file not found";
                            result.Status = ParameterStatus.Failed;
                            return result;
                        }

                        DefinitionGroup defGroup = defFile.Groups.get_Item(parameterDef.SharedParameterGroup);
                        if (defGroup == null)
                        {
                            defGroup = defFile.Groups.Create(parameterDef.SharedParameterGroup);
                        }

                        // Get or create definition
                        def = defGroup.Definitions.get_Item(parameterDef.Name);
                        if (def == null)
                        {
                            ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(
                                parameterDef.Name,
                                parameterDef.ForgeParameterType);

                            if (!string.IsNullOrEmpty(parameterDef.Description))
                            {
                                options.Description = parameterDef.Description;
                            }

                            def = defGroup.Definitions.Create(options);
                        }
                    }

                    if (def == null)
                    {
                        result.ErrorMessage = "Failed to create parameter definition";
                        result.Status = ParameterStatus.Failed;
                        return result;
                    }

                    // Check if binding exists
                    Autodesk.Revit.DB.Binding existingBinding = doc.ParameterBindings.get_Item(def);

                    if (existingBinding != null)
                    {
                        // Update existing binding
                        CategorySet existingCategories = GetCategoriesFromBinding(existingBinding);

                        // Add new categories
                        bool categoriesAdded = false;
                        foreach (Category cat in categories)
                        {
                            if (!existingCategories.Contains(cat))
                            {
                                existingCategories.Insert(cat);
                                categoriesAdded = true;
                            }
                        }

                        if (categoriesAdded)
                        {
                            // Re-insert binding with updated categories
                            Autodesk.Revit.DB.Binding updatedBinding = parameterDef.IsInstance
                                ? doc.Application.Create.NewInstanceBinding(existingCategories)
                                : (Autodesk.Revit.DB.Binding)doc.Application.Create.NewTypeBinding(existingCategories);

                            doc.ParameterBindings.ReInsert(def, updatedBinding, parameterDef.ForgeGroupType);
                            result.Status = ParameterStatus.Updated;
                            _logger.LogInformation($"Updated parameter binding: {parameterDef.Name}");
                        }
                        else
                        {
                            result.Status = ParameterStatus.AlreadyExists;
                        }
                    }
                    else
                    {
                        // Create new binding
                        Autodesk.Revit.DB.Binding binding = parameterDef.IsInstance
                            ? doc.Application.Create.NewInstanceBinding(categories)
                            : (Autodesk.Revit.DB.Binding)doc.Application.Create.NewTypeBinding(categories);

                        doc.ParameterBindings.Insert(def, binding, parameterDef.ForgeGroupType);
                        result.Status = ParameterStatus.Created;
                        _logger.LogInformation($"Created parameter: {parameterDef.Name}");
                    }

                    trans.Commit();
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.Status = ParameterStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError($"Failed to create parameter {parameterDef.Name}: {ex.Message}", ex);
            }

            return result;
        }

        public IEnumerable<ParameterCreationResult> EnsureParametersExist(Document doc, IEnumerable<ProjectParameterDefinition> parameterDefs)
        {
            var results = new List<ParameterCreationResult>();

            foreach (var paramDef in parameterDefs)
            {
                var result = EnsureParameterExists(doc, paramDef);
                results.Add(result);
            }

            return results;
        }

        #endregion

        #region JSON Loading

        public IEnumerable<ProjectParameterDefinition> LoadParametersFromJson(string jsonFilePath)
        {
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    _logger.LogError($"JSON file not found: {jsonFilePath}");
                    return Enumerable.Empty<ProjectParameterDefinition>();
                }

                var jsonContent = File.ReadAllText(jsonFilePath);
                var config = JsonSerializer.Deserialize<ProjectParameterConfig>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config?.ProjectParameters == null)
                {
                    _logger.LogWarning($"No parameters found in JSON file: {jsonFilePath}");
                    return Enumerable.Empty<ProjectParameterDefinition>();
                }

                // Convert to internal model and resolve ForgeTypeIds
                var parameters = new List<ProjectParameterDefinition>();
                foreach (var jsonParam in config.ProjectParameters)
                {
                    var param = new ProjectParameterDefinition
                    {
                        Name = jsonParam.Name,
                        ParameterType = jsonParam.ParameterType,
                        SharedParameterGroup = string.IsNullOrEmpty(jsonParam.SharedParameterGroup)
                            ? LOBIM_SHARED_PARAM_GROUP
                            : jsonParam.SharedParameterGroup,
                        GroupName = jsonParam.GroupName,
                        Description = jsonParam.Description,
                        IsInstance = jsonParam.IsInstance,
                        Categories = jsonParam.Categories ?? new List<string>(),
                        ForgeParameterType = GetForgeTypeIdFromString(jsonParam.ParameterType),
                        ForgeGroupType = GetGroupTypeIdFromString(jsonParam.GroupName)
                    };

                    parameters.Add(param);
                }

                return parameters;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load parameters from JSON: {ex.Message}", ex);
                return Enumerable.Empty<ProjectParameterDefinition>();
            }
        }

        #endregion

        #region Parameter Reading/Writing

        public bool ParameterExists(Element element, string parameterName)
        {
            return element?.LookupParameter(parameterName) != null;
        }

        public string GetParameterValue(Element element, string parameterName)
        {
            try
            {
                var param = element?.LookupParameter(parameterName);
                if (param == null || !param.HasValue)
                    return null;

                return param.AsString() ?? param.AsValueString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get parameter value for {parameterName}: {ex.Message}");
                return null;
            }
        }

        public bool SetParameterValue(Element element, string parameterName, string value)
        {
            try
            {
                var param = element?.LookupParameter(parameterName);
                if (param == null || param.IsReadOnly)
                {
                    _logger.LogWarning($"Parameter {parameterName} not found or is read-only");
                    return false;
                }

                param.Set(value);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set parameter {parameterName}: {ex.Message}", ex);
                return false;
            }
        }

        public bool SetParameterValue(Element element, string parameterName, int value)
        {
            try
            {
                var param = element?.LookupParameter(parameterName);
                if (param == null || param.IsReadOnly)
                {
                    _logger.LogWarning($"Parameter {parameterName} not found or is read-only");
                    return false;
                }

                param.Set(value);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set parameter {parameterName}: {ex.Message}", ex);
                return false;
            }
        }

        public bool SetParameterValue(Element element, string parameterName, double value)
        {
            try
            {
                var param = element?.LookupParameter(parameterName);
                if (param == null || param.IsReadOnly)
                {
                    _logger.LogWarning($"Parameter {parameterName} not found or is read-only");
                    return false;
                }

                param.Set(value);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set parameter {parameterName}: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        #region Source Tracking - Predefined Parameters

        public IEnumerable<ProjectParameterDefinition> GetSheetSourceTrackingParameters()
        {
            return new List<ProjectParameterDefinition>
            {
                new ProjectParameterDefinition
                {
                    Name = "LoBIM_SourceFile",
                    ParameterType = "Text",
                    SharedParameterGroup = LOBIM_SHARED_PARAM_GROUP,
                    GroupName = "Other",
                    Description = "Source linked file name",
                    IsInstance = true,
                    IsProjectParameter = true, // Project parameter - NOT locked by templates
                    Categories = new List<string> { "Sheets" },
                    ForgeParameterType = SpecTypeId.String.Text,
                    ForgeGroupType = GroupTypeId.IdentityData
                },
                new ProjectParameterDefinition
                {
                    Name = "LoBIM_SourceSheet",
                    ParameterType = "Text",
                    SharedParameterGroup = LOBIM_SHARED_PARAM_GROUP,
                    GroupName = "Other",
                    Description = "Source sheet number",
                    IsInstance = true,
                    IsProjectParameter = true, // Project parameter - NOT locked by templates
                    Categories = new List<string> { "Sheets" },
                    ForgeParameterType = SpecTypeId.String.Text,
                    ForgeGroupType = GroupTypeId.IdentityData
                },
                new ProjectParameterDefinition
                {
                    Name = "LoBIM_SourceSheetId",
                    ParameterType = "Text",
                    SharedParameterGroup = LOBIM_SHARED_PARAM_GROUP,
                    GroupName = "Other",
                    Description = "Source sheet element ID",
                    IsInstance = true,
                    IsProjectParameter = true, // Project parameter - NOT locked by templates
                    Categories = new List<string> { "Sheets" },
                    ForgeParameterType = SpecTypeId.String.Text,
                    ForgeGroupType = GroupTypeId.IdentityData
                }
            };
        }

        public IEnumerable<ProjectParameterDefinition> GetViewSourceTrackingParameters()
        {
            return new List<ProjectParameterDefinition>
            {
                new ProjectParameterDefinition
                {
                    Name = "LoBIM_SourceFile",
                    ParameterType = "Text",
                    SharedParameterGroup = LOBIM_SHARED_PARAM_GROUP,
                    GroupName = "Other",
                    Description = "Source linked file name",
                    IsInstance = true,
                    IsProjectParameter = true, // Project parameter - NOT locked by templates
                    Categories = new List<string> { "Views" },
                    ForgeParameterType = SpecTypeId.String.Text,
                    ForgeGroupType = GroupTypeId.IdentityData
                },
                new ProjectParameterDefinition
                {
                    Name = "LoBIM_SourceView",
                    ParameterType = "Text",
                    SharedParameterGroup = LOBIM_SHARED_PARAM_GROUP,
                    GroupName = "Other",
                    Description = "Source view name",
                    IsInstance = true,
                    IsProjectParameter = true, // Project parameter - NOT locked by templates
                    Categories = new List<string> { "Views" },
                    ForgeParameterType = SpecTypeId.String.Text,
                    ForgeGroupType = GroupTypeId.IdentityData
                },
                new ProjectParameterDefinition
                {
                    Name = "LoBIM_SourceViewId",
                    ParameterType = "Text",
                    SharedParameterGroup = LOBIM_SHARED_PARAM_GROUP,
                    GroupName = "Other",
                    Description = "Source view element ID",
                    IsInstance = true,
                    IsProjectParameter = true, // Project parameter - NOT locked by templates
                    Categories = new List<string> { "Views" },
                    ForgeParameterType = SpecTypeId.String.Text,
                    ForgeGroupType = GroupTypeId.IdentityData
                }
            };
        }

        #endregion

        #region Source Tracking - Storage and Retrieval

        public void StoreSheetSourceTracking(ViewSheet clonedSheet, string sourceFileName, string sourceSheetNumber, ElementId sourceSheetId)
        {
            try
            {
                _logger.LogInformation($"Storing sheet source tracking: {sourceFileName} > {sourceSheetNumber}");

                SetParameterValue(clonedSheet, "LoBIM_SourceFile", sourceFileName);
                SetParameterValue(clonedSheet, "LoBIM_SourceSheet", sourceSheetNumber);
                SetParameterValue(clonedSheet, "LoBIM_SourceSheetId", sourceSheetId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to store sheet source tracking: {ex.Message}", ex);
            }
        }

        public void StoreViewSourceTracking(Autodesk.Revit.DB.View clonedView, string sourceFileName, string sourceViewName, ElementId sourceViewId)
        {
            try
            {
                _logger.LogInformation($"Storing view source tracking: {sourceFileName} > {sourceViewName}");

                SetParameterValue(clonedView, "LoBIM_SourceFile", sourceFileName);
                SetParameterValue(clonedView, "LoBIM_SourceView", sourceViewName);
                SetParameterValue(clonedView, "LoBIM_SourceViewId", sourceViewId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to store view source tracking: {ex.Message}", ex);
            }
        }

        public SourceTrackingInfo GetSheetSourceTracking(ViewSheet sheet)
        {
            try
            {
                var info = new SourceTrackingInfo
                {
                    SourceFileName = GetParameterValue(sheet, "LoBIM_SourceFile"),
                    SourceSheetNumber = GetParameterValue(sheet, "LoBIM_SourceSheet"),
                    SourceSheetId = GetParameterValue(sheet, "LoBIM_SourceSheetId")
                };

                return info.HasSourceTracking ? info : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get sheet source tracking: {ex.Message}");
                return null;
            }
        }

        public SourceTrackingInfo GetViewSourceTracking(Autodesk.Revit.DB.View view)
        {
            try
            {
                var info = new SourceTrackingInfo
                {
                    SourceFileName = GetParameterValue(view, "LoBIM_SourceFile"),
                    SourceViewName = GetParameterValue(view, "LoBIM_SourceView"),
                    SourceViewId = GetParameterValue(view, "LoBIM_SourceViewId")
                };

                return info.HasSourceTracking ? info : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get view source tracking: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper Methods

        private ForgeTypeId GetForgeTypeIdFromString(string parameterType)
        {
            return parameterType?.ToUpperInvariant() switch
            {
                "TEXT" => SpecTypeId.String.Text,
                "INTEGER" => SpecTypeId.Int.Integer,
                "NUMBER" => SpecTypeId.Number,
                "LENGTH" => SpecTypeId.Length,
                "AREA" => SpecTypeId.Area,
                "VOLUME" => SpecTypeId.Volume,
                "ANGLE" => SpecTypeId.Angle,
                "YESNO" => SpecTypeId.Boolean.YesNo,
                "URL" => SpecTypeId.String.Url,
                "MATERIAL" => SpecTypeId.Reference.Material,
                _ => SpecTypeId.String.Text // Default to text
            };
        }

        private ForgeTypeId GetGroupTypeIdFromString(string groupName)
        {
            return groupName?.ToUpperInvariant() switch
            {
                "IDENTITY DATA" => GroupTypeId.IdentityData,
                "TEXT" => GroupTypeId.Text,
                "CONSTRAINTS" => GroupTypeId.Constraints,
                "DIMENSIONS" => GroupTypeId.Data, // Note: GroupTypeId.Dimensions doesn't exist in Revit 2025
                "CONSTRUCTION" => GroupTypeId.Construction,
                "MATERIALS AND FINISHES" => GroupTypeId.Materials, // Note: GroupTypeId.MaterialsFinishes doesn't exist
                "MATERIALS" => GroupTypeId.Materials,
                "GRAPHICS" => GroupTypeId.Graphics,
                "PHASING" => GroupTypeId.Phasing,
                "MECHANICAL" => GroupTypeId.Mechanical,
                "ELECTRICAL" => GroupTypeId.Electrical,
                "PLUMBING" => GroupTypeId.Plumbing,
                "STRUCTURAL" => GroupTypeId.Structural,
                "DATA" => GroupTypeId.Data,
                "OTHER" => GroupTypeId.IdentityData, // Note: GroupTypeId.Other doesn't exist, default to IdentityData
                _ => GroupTypeId.IdentityData // Default
            };
        }

        private Category GetCategoryByName(Document doc, string categoryName)
        {
            try
            {
                // Try to get by name directly
                var category = doc.Settings.Categories.get_Item(categoryName);
                if (category != null)
                    return category;

                // Try common variations
                var categoryNameUpper = categoryName.ToUpperInvariant();

                // Map common names to built-in categories
                var builtInCategory = categoryNameUpper switch
                {
                    "WALLS" => BuiltInCategory.OST_Walls,
                    "DOORS" => BuiltInCategory.OST_Doors,
                    "WINDOWS" => BuiltInCategory.OST_Windows,
                    "FLOORS" => BuiltInCategory.OST_Floors,
                    "ROOFS" => BuiltInCategory.OST_Roofs,
                    "CEILINGS" => BuiltInCategory.OST_Ceilings,
                    "ROOMS" => BuiltInCategory.OST_Rooms,
                    "SHEETS" => BuiltInCategory.OST_Sheets,
                    "VIEWS" => BuiltInCategory.OST_Views,
                    "CURTAIN WALLS" => BuiltInCategory.OST_CurtainWallPanels,
                    _ => BuiltInCategory.INVALID
                };

                if (builtInCategory != BuiltInCategory.INVALID)
                {
                    return doc.Settings.Categories.get_Item(builtInCategory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not find category: {categoryName} - {ex.Message}");
            }

            return null;
        }

        private Element GetTestElementForCategories(Document doc, List<string> categoryNames)
        {
            foreach (var categoryName in categoryNames)
            {
                var category = GetCategoryByName(doc, categoryName);
                if (category == null)
                    continue;

                try
                {
                    var element = new FilteredElementCollector(doc)
                        .OfCategoryId(category.Id)
                        .FirstElement();

                    if (element != null)
                        return element;
                }
                catch
                {
                    // Continue to next category
                }
            }

            return null;
        }

        private CategorySet GetCategoriesFromBinding(Autodesk.Revit.DB.Binding binding)
        {
            if (binding is InstanceBinding instBinding)
                return instBinding.Categories;

            if (binding is TypeBinding typeBinding)
                return typeBinding.Categories;

            return null;
        }

        /// <summary>
        /// Creates an internal parameter definition (project parameter).
        /// Revit API doesn't provide direct way to create InternalDefinition,
        /// so we use a workaround with shared parameters that we immediately unbind from the file.
        /// </summary>
        private Definition CreateInternalParameterDefinition(Document doc, ProjectParameterDefinition parameterDef)
        {
            try
            {
                _logger.LogInformation($"Creating internal parameter definition for: {parameterDef.Name}");

                // Workaround: Use shared parameter temporarily to create the definition
                // Then the parameter becomes "internal" to the document when not linked to shared param file

                var app = doc.Application;
                string originalSharedParamFile = app.SharedParametersFilename;
                string tempSharedParamFile = null;

                try
                {
                    // Create a temporary shared parameter file
                    tempSharedParamFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"TempSharedParams_{Guid.NewGuid()}.txt");

                    // Write minimal shared parameter file content
                    System.IO.File.WriteAllText(tempSharedParamFile,
                        "*META\tVERSION\tMINVERSION\n" +
                        "META\t2\t1\n" +
                        "*GROUP\tID\tNAME\n" +
                        "GROUP\t1\tTempGroup\n" +
                        "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\n");

                    // Set temporary shared parameter file
                    app.SharedParametersFilename = tempSharedParamFile;

                    // Open and create the definition
                    var defFile = app.OpenSharedParameterFile();
                    if (defFile == null)
                    {
                        _logger.LogError("Failed to open temporary shared parameter file");
                        return null;
                    }

                    var defGroup = defFile.Groups.get_Item("TempGroup");
                    if (defGroup == null)
                    {
                        defGroup = defFile.Groups.Create("TempGroup");
                    }

                    // Create the external definition
                    var options = new ExternalDefinitionCreationOptions(
                        parameterDef.Name,
                        parameterDef.ForgeParameterType)
                    {
                        GUID = Guid.NewGuid()
                    };

                    if (!string.IsNullOrEmpty(parameterDef.Description))
                    {
                        options.Description = parameterDef.Description;
                    }

                    var externalDef = defGroup.Definitions.Create(options) as ExternalDefinition;

                    _logger.LogInformation($"Created external definition for: {parameterDef.Name}");

                    return externalDef;
                }
                finally
                {
                    // Restore original shared parameter file
                    if (!string.IsNullOrEmpty(originalSharedParamFile))
                    {
                        app.SharedParametersFilename = originalSharedParamFile;
                    }

                    // Clean up temporary file
                    if (!string.IsNullOrEmpty(tempSharedParamFile) && System.IO.File.Exists(tempSharedParamFile))
                    {
                        try
                        {
                            System.IO.File.Delete(tempSharedParamFile);
                        }
                        catch
                        {
                            // Ignore deletion errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create internal parameter definition: {ex.Message}", ex);
                return null;
            }
        }

        #endregion

        #region Supporting Model Classes

        private class ProjectParameterConfig
        {
            public List<JsonProjectParameter> ProjectParameters { get; set; }
        }

        private class JsonProjectParameter
        {
            public string Name { get; set; }
            public string ParameterType { get; set; }
            public string SharedParameterGroup { get; set; }
            public string GroupName { get; set; }
            public string Description { get; set; }
            public bool IsInstance { get; set; }
            public List<string> Categories { get; set; }
        }

        #endregion
    }
}
