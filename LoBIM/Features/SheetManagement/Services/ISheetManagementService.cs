using Autodesk.Revit.DB;
using LoBIM.Features.SheetManagement.Models;
using System.Collections.Generic;

namespace LoBIM.Features.SheetManagement.Services
{
    /// <summary>
    /// Service for managing ViewSheet operations including querying, filtering, and renumbering
    /// </summary>
    public interface ISheetManagementService
    {
        /// <summary>
        /// Gets all sheets in the document with their properties
        /// </summary>
        /// <param name="document">The Revit document</param>
        /// <returns>List of sheet items</returns>
        List<SheetItemModel> GetAllSheets(Document document);

        /// <summary>
        /// Gets all available parameter names from sheets in the document
        /// </summary>
        /// <param name="document">The Revit document</param>
        /// <returns>List of parameter names that can be used for filtering</returns>
        List<string> GetAvailableSheetParameters(Document document);

        /// <summary>
        /// Renumbers sheets according to the provided model
        /// </summary>
        /// <param name="document">The Revit document</param>
        /// <param name="model">Renumbering configuration and sheet order</param>
        /// <returns>True if successful, false otherwise</returns>
        bool RenumberSheets(Document document, SheetRenumberingModel model);

        /// <summary>
        /// Gets a ViewSheet by element ID
        /// </summary>
        /// <param name="document">The Revit document</param>
        /// <param name="elementId">The element ID</param>
        /// <returns>The ViewSheet or null if not found</returns>
        ViewSheet GetSheetById(Document document, int elementId);

        /// <summary>
        /// Validates if a sheet number is unique in the document
        /// </summary>
        /// <param name="document">The Revit document</param>
        /// <param name="sheetNumber">The sheet number to validate</param>
        /// <param name="excludeElementId">Element ID to exclude from validation (for editing existing sheet)</param>
        /// <returns>True if unique, false if duplicate</returns>
        bool IsSheetNumberUnique(Document document, string sheetNumber, int excludeElementId = -1);

        /// <summary>
        /// Generates preview of new sheet numbers based on renumbering configuration
        /// </summary>
        /// <param name="model">Renumbering configuration</param>
        /// <returns>Dictionary of element ID to new sheet number</returns>
        Dictionary<int, string> PreviewRenumbering(SheetRenumberingModel model);
    }
}
