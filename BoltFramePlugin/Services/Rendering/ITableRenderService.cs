using Autodesk.Revit.DB;
using BoltFramePlugin.Models.Tables;

namespace BoltFramePlugin.Services.Rendering
{
    /// <summary>
    /// Interface for rendering table data to Revit views
    /// </summary>
    public interface ITableRenderService
    {
        /// <summary>
        /// Render imported table data to a drafting view
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="view">Drafting view to render to</param>
        /// <param name="data">Imported table data</param>
        /// <param name="options">Rendering options</param>
        void RenderToView(Document doc, Autodesk.Revit.DB.View view, ImportedTableData data, TableRenderOptions options);

        /// <summary>
        /// Create a new drafting view for the table
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="viewName">Name for the new view</param>
        /// <param name="viewScale">View scale (e.g., 96 for 1/8" = 1'-0")</param>
        /// <returns>Created drafting view</returns>
        ViewDrafting CreateDraftingView(Document doc, string viewName, int viewScale);
    }
}
