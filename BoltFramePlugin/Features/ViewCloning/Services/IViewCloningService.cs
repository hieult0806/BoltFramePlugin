using System.Collections.Generic;
using Autodesk.Revit.DB;
using BoltFramePlugin.Features.ViewCloning.Models;

namespace BoltFramePlugin.Features.ViewCloning.Services
{
    /// <summary>
    /// Service for detecting and cloning views from linked Revit files
    /// </summary>
    public interface IViewCloningService
    {
        /// <summary>
        /// Get all linked Revit files in the current document
        /// </summary>
        /// <param name="doc">Host document</param>
        /// <returns>List of linked file information</returns>
        List<LinkedFileInfo> GetLinkedFiles(Document doc);

        /// <summary>
        /// Get all views from a linked document
        /// </summary>
        /// <param name="linkedDoc">Linked document</param>
        /// <param name="parentLink">Parent link info</param>
        /// <returns>List of view information</returns>
        List<LinkedViewInfo> GetViewsFromLinkedFile(Document linkedDoc, LinkedFileInfo parentLink);

        /// <summary>
        /// Clone a view from a linked file to the host document
        /// </summary>
        /// <param name="hostDoc">Host document</param>
        /// <param name="viewInfo">View to clone</param>
        /// <param name="namePrefix">Optional prefix for the cloned view name</param>
        /// <returns>Element ID of the cloned view, or null if failed</returns>
        ElementId? CloneView(Document hostDoc, LinkedViewInfo viewInfo, string namePrefix = "");

        /// <summary>
        /// Clone multiple views from linked files
        /// </summary>
        /// <param name="hostDoc">Host document</param>
        /// <param name="viewsToClone">List of views to clone</param>
        /// <param name="namePrefix">Optional prefix for cloned view names</param>
        /// <returns>Number of successfully cloned views</returns>
        int CloneViews(Document hostDoc, List<LinkedViewInfo> viewsToClone, string namePrefix = "");
    }
}
