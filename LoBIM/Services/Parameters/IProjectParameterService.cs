using Autodesk.Revit.DB;
using LoBIM.Services.Parameters.Models;
using System.Collections.Generic;

namespace LoBIM.Services.Parameters
{
    /// <summary>
    /// Global service for managing project parameters across all features
    /// Provides a unified interface for creating, updating, and reading project parameters
    /// </summary>
    public interface IProjectParameterService
    {
        /// <summary>
        /// Ensures that a project parameter exists in the document
        /// Creates the parameter if it doesn't exist, or updates its binding if needed
        /// This method must be called OUTSIDE of any active transaction
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="parameterDef">Parameter definition</param>
        /// <returns>Result indicating success or failure</returns>
        ParameterCreationResult EnsureParameterExists(Document doc, ProjectParameterDefinition parameterDef);

        /// <summary>
        /// Ensures multiple project parameters exist in the document
        /// Creates parameters if they don't exist, or updates their bindings if needed
        /// This method must be called OUTSIDE of any active transaction
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="parameterDefs">Collection of parameter definitions</param>
        /// <returns>Results for each parameter</returns>
        IEnumerable<ParameterCreationResult> EnsureParametersExist(Document doc, IEnumerable<ProjectParameterDefinition> parameterDefs);

        /// <summary>
        /// Loads parameter definitions from a JSON configuration file
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON configuration file</param>
        /// <returns>Collection of parameter definitions</returns>
        IEnumerable<ProjectParameterDefinition> LoadParametersFromJson(string jsonFilePath);

        /// <summary>
        /// Checks if a parameter exists on an element
        /// </summary>
        /// <param name="element">The element to check</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <returns>True if parameter exists, false otherwise</returns>
        bool ParameterExists(Element element, string parameterName);

        /// <summary>
        /// Gets a parameter value from an element as a string
        /// Returns null if parameter doesn't exist or has no value
        /// </summary>
        /// <param name="element">The element to read from</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <returns>Parameter value as string, or null</returns>
        string GetParameterValue(Element element, string parameterName);

        /// <summary>
        /// Sets a parameter value on an element
        /// Must be called within an active transaction
        /// </summary>
        /// <param name="element">The element to update</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="value">Value to set</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetParameterValue(Element element, string parameterName, string value);

        /// <summary>
        /// Sets a parameter value on an element (integer)
        /// Must be called within an active transaction
        /// </summary>
        /// <param name="element">The element to update</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="value">Value to set</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetParameterValue(Element element, string parameterName, int value);

        /// <summary>
        /// Sets a parameter value on an element (double)
        /// Must be called within an active transaction
        /// </summary>
        /// <param name="element">The element to update</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="value">Value to set</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetParameterValue(Element element, string parameterName, double value);

        /// <summary>
        /// Gets the LoBIM source tracking parameters for sheets
        /// Returns pre-configured parameter definitions for:
        /// - LoBIM_SourceFile
        /// - LoBIM_SourceSheet
        /// - LoBIM_SourceSheetId
        /// </summary>
        IEnumerable<ProjectParameterDefinition> GetSheetSourceTrackingParameters();

        /// <summary>
        /// Gets the LoBIM source tracking parameters for views
        /// Returns pre-configured parameter definitions for:
        /// - LoBIM_SourceFile
        /// - LoBIM_SourceView
        /// - LoBIM_SourceViewId
        /// </summary>
        IEnumerable<ProjectParameterDefinition> GetViewSourceTrackingParameters();

        /// <summary>
        /// Stores source tracking information for a cloned sheet
        /// Must be called within an active transaction
        /// </summary>
        /// <param name="clonedSheet">The cloned sheet</param>
        /// <param name="sourceFileName">Name of the source linked file</param>
        /// <param name="sourceSheetNumber">Sheet number from source</param>
        /// <param name="sourceSheetId">Sheet ID from source</param>
        void StoreSheetSourceTracking(ViewSheet clonedSheet, string sourceFileName, string sourceSheetNumber, ElementId sourceSheetId);

        /// <summary>
        /// Stores source tracking information for a cloned view
        /// Must be called within an active transaction
        /// </summary>
        /// <param name="clonedView">The cloned view</param>
        /// <param name="sourceFileName">Name of the source linked file</param>
        /// <param name="sourceViewName">View name from source</param>
        /// <param name="sourceViewId">View ID from source</param>
        void StoreViewSourceTracking(Autodesk.Revit.DB.View clonedView, string sourceFileName, string sourceViewName, ElementId sourceViewId);

        /// <summary>
        /// Reads source tracking information from a sheet
        /// </summary>
        /// <param name="sheet">The sheet to read from</param>
        /// <returns>Source tracking information, or null if not available</returns>
        SourceTrackingInfo GetSheetSourceTracking(ViewSheet sheet);

        /// <summary>
        /// Reads source tracking information from a view
        /// </summary>
        /// <param name="view">The view to read from</param>
        /// <returns>Source tracking information, or null if not available</returns>
        SourceTrackingInfo GetViewSourceTracking(Autodesk.Revit.DB.View view);
    }
}
