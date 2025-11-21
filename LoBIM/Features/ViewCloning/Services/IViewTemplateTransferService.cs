using Autodesk.Revit.DB;

namespace LoBIM.Features.ViewCloning.Services
{
    /// <summary>
    /// Service for transferring View Templates from linked documents to the host document
    /// </summary>
    public interface IViewTemplateTransferService
    {
        /// <summary>
        /// Ensures that the View Template used by the source view exists in the host document.
        /// If the template doesn't exist, it will be transferred from the linked document.
        /// </summary>
        /// <param name="hostDoc">The host document where the template should exist</param>
        /// <param name="sourceView">The source view from the linked document that uses the template</param>
        /// <param name="linkedDoc">The linked document containing the source view</param>
        /// <returns>The ElementId of the View Template in the host document, or ElementId.InvalidElementId if no template or transfer failed</returns>
        ElementId EnsureViewTemplateExists(Document hostDoc, Autodesk.Revit.DB.View sourceView, Document linkedDoc);

        /// <summary>
        /// Checks if a View Template with the given name exists in the document
        /// </summary>
        /// <param name="doc">The document to search</param>
        /// <param name="templateName">The name of the template to find</param>
        /// <returns>The ElementId of the template if found, otherwise ElementId.InvalidElementId</returns>
        ElementId FindViewTemplateByName(Document doc, string templateName);

        /// <summary>
        /// Transfers a View Template from the linked document to the host document
        /// </summary>
        /// <param name="hostDoc">The host document where the template will be copied</param>
        /// <param name="templateId">The ElementId of the template in the linked document</param>
        /// <param name="linkedDoc">The linked document containing the template</param>
        /// <returns>The ElementId of the transferred template in the host document, or ElementId.InvalidElementId if transfer failed</returns>
        ElementId TransferViewTemplate(Document hostDoc, ElementId templateId, Document linkedDoc);

        /// <summary>
        /// Applies a View Template to a target view if the source view has one
        /// </summary>
        /// <param name="hostDoc">The host document</param>
        /// <param name="sourceView">The source view from the linked document</param>
        /// <param name="targetView">The target view in the host document</param>
        /// <param name="linkedDoc">The linked document containing the source view</param>
        /// <returns>True if template was applied successfully, false otherwise</returns>
        bool ApplyViewTemplateIfExists(Document hostDoc, Autodesk.Revit.DB.View sourceView, Autodesk.Revit.DB.View targetView, Document linkedDoc);
    }
}
