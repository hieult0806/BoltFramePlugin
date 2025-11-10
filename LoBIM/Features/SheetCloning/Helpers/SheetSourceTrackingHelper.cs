using System;
using System.Linq;
using Autodesk.Revit.DB;
using LoBIM.Services;

namespace LoBIM.Features.SheetCloning.Helpers
{
    /// <summary>
    /// Helper class for managing source tracking parameters on sheets
    /// </summary>
    public class SheetSourceTrackingHelper
    {
        private readonly ILoggingService _logger;

        public SheetSourceTrackingHelper(ILoggingService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Ensures that the custom project parameters for sheet source tracking exist
        /// Creates them if they don't exist
        /// This method must be called OUTSIDE of any active transaction
        /// </summary>
        public void EnsureSourceTrackingParameters(Document doc)
        {
            try
            {
                // Check if parameters already exist by looking at one sheet
                var testSheet = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .FirstOrDefault();

                if (testSheet == null)
                {
                    _logger.LogWarning($"No sheets found to check for parameters");
                    return;
                }

                bool hasSourceFile = testSheet.LookupParameter("LoBIM_SourceFile") != null;
                bool hasSourceSheet = testSheet.LookupParameter("LoBIM_SourceSheet") != null;
                bool hasSourceId = testSheet.LookupParameter("LoBIM_SourceSheetId") != null;

                // If all parameters exist, no need to create them
                if (hasSourceFile && hasSourceSheet && hasSourceId)
                {
                    return;
                }

                _logger.LogInformation($"Creating sheet source tracking project parameters...");

                using (Transaction trans = new Transaction(doc, "Create LoBIM Sheet Source Tracking Parameters"))
                {
                    trans.Start();

                    CategorySet categories = doc.Application.Create.NewCategorySet();
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Sheets));

                    DefinitionFile defFile = doc.Application.OpenSharedParameterFile();
                    DefinitionGroup defGroup = null;

                    // Try to get or create definition group
                    if (defFile != null)
                    {
                        defGroup = defFile.Groups.get_Item("LoBIM") ?? defFile.Groups.Create("LoBIM");
                    }

                    // Create parameters (or update bindings if they exist for other categories)
                    CreateOrUpdateProjectParameter(doc, defGroup, "LoBIM_SourceFile",
                        SpecTypeId.String.Text, categories, GroupTypeId.IdentityData, !hasSourceFile);

                    CreateOrUpdateProjectParameter(doc, defGroup, "LoBIM_SourceSheet",
                        SpecTypeId.String.Text, categories, GroupTypeId.IdentityData, !hasSourceSheet);

                    CreateOrUpdateProjectParameter(doc, defGroup, "LoBIM_SourceSheetId",
                        SpecTypeId.String.Text, categories, GroupTypeId.IdentityData, !hasSourceId);

                    trans.Commit();
                    _logger.LogInformation($"Successfully created sheet source tracking parameters");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not ensure sheet source tracking parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Stores source sheet information in the cloned sheet's custom project parameters
        /// This must be called within an active transaction
        /// </summary>
        public void StoreSourceSheetInfo(ViewSheet sourceSheet, ViewSheet clonedSheet, string linkedFileName)
        {
            try
            {
                _logger.LogInformation($"=== STORING SOURCE SHEET METADATA ===");

                Document doc = clonedSheet.Document;

                // Check if parameters exist
                var sourceFileParam = clonedSheet.LookupParameter("LoBIM_SourceFile");
                var sourceSheetParam = clonedSheet.LookupParameter("LoBIM_SourceSheet");
                var sourceIdParam = clonedSheet.LookupParameter("LoBIM_SourceSheetId");

                if (sourceFileParam == null || sourceSheetParam == null || sourceIdParam == null)
                {
                    _logger.LogWarning($"Source tracking parameters not found. They should have been created before cloning.");
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

                // Store source sheet number
                if (sourceSheetParam != null && !sourceSheetParam.IsReadOnly)
                {
                    sourceSheetParam.Set(sourceSheet.SheetNumber);
                    _logger.LogInformation($"Stored source sheet: {sourceSheet.SheetNumber}");
                }
                else
                {
                    _logger.LogWarning($"LoBIM_SourceSheet parameter not found or read-only");
                }

                // Store source sheet ID
                if (sourceIdParam != null && !sourceIdParam.IsReadOnly)
                {
                    sourceIdParam.Set(sourceSheet.Id.ToString());
                    _logger.LogInformation($"Stored source sheet ID: {sourceSheet.Id}");
                }
                else
                {
                    _logger.LogWarning($"LoBIM_SourceSheetId parameter not found or read-only");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not store source sheet info: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
            }
        }

        /// <summary>
        /// Creates a project parameter or updates its binding to include the new categories
        /// </summary>
        private void CreateOrUpdateProjectParameter(Document doc, DefinitionGroup defGroup,
            string paramName, ForgeTypeId paramType, CategorySet newCategories, ForgeTypeId group, bool shouldCreate)
        {
            try
            {
                Definition def = defGroup?.Definitions.get_Item(paramName);

                // Create definition if it doesn't exist
                if (def == null && defGroup != null && shouldCreate)
                {
                    ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(paramName, paramType);
                    def = defGroup.Definitions.Create(options);
                }

                if (def != null)
                {
                    // Check if parameter binding already exists
                    Autodesk.Revit.DB.Binding existingBinding = doc.ParameterBindings.get_Item(def);

                    if (existingBinding != null)
                    {
                        // Parameter exists - add new categories to existing binding
                        CategorySet existingCategories = null;

                        if (existingBinding is InstanceBinding instBinding)
                        {
                            existingCategories = instBinding.Categories;
                        }
                        else if (existingBinding is TypeBinding typeBinding)
                        {
                            existingCategories = typeBinding.Categories;
                        }

                        // Add new categories to existing ones
                        if (existingCategories != null)
                        {
                            foreach (Category cat in newCategories)
                            {
                                if (!existingCategories.Contains(cat))
                                {
                                    existingCategories.Insert(cat);
                                }
                            }

                            // Re-insert binding with updated categories
                            InstanceBinding updatedBinding = doc.Application.Create.NewInstanceBinding(existingCategories);
                            doc.ParameterBindings.ReInsert(def, updatedBinding, group);
                            _logger.LogInformation($"Updated parameter binding: {paramName}");
                        }
                    }
                    else if (shouldCreate)
                    {
                        // Parameter doesn't exist - create new binding
                        InstanceBinding binding = doc.Application.Create.NewInstanceBinding(newCategories);
                        doc.ParameterBindings.Insert(def, binding, group);
                        _logger.LogInformation($"Created parameter: {paramName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create/update parameter {paramName}: {ex.Message}");
            }
        }
    }
}
