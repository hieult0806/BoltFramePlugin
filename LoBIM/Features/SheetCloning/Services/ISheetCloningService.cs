using System.Collections.Generic;
using Autodesk.Revit.DB;
using LoBIM.Features.SheetCloning.Models;

namespace LoBIM.Features.SheetCloning.Services
{
    /// <summary>
    /// Service for cloning sheets from linked Revit files
    /// </summary>
    public interface ISheetCloningService
    {
        /// <summary>
        /// Gets all sheets from a linked document
        /// </summary>
        /// <param name="hostDoc">The host document to check for existing cloned sheets</param>
        /// <param name="linkedDoc">The linked document to get sheets from</param>
        /// <param name="fileName">The file name of the linked document</param>
        List<LinkedSheetInfo> GetSheetsFromLinkedFile(Document hostDoc, Document linkedDoc, string fileName);

        /// <summary>
        /// Clones selected sheets from linked files to the host document
        /// </summary>
        /// <param name="hostDoc">The host document</param>
        /// <param name="sheets">List of sheets to clone</param>
        /// <param name="linkedDocuments">Dictionary mapping source file names to linked documents</param>
        /// <returns>Number of sheets successfully cloned</returns>
        int CloneSheets(Document hostDoc, List<LinkedSheetInfo> sheets, Dictionary<string, Document> linkedDocuments);
    }
}
