using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LoBIM.Services.Parameters.Models
{
    /// <summary>
    /// Defines a project parameter to be created in Revit
    /// </summary>
    public class ProjectParameterDefinition
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Parameter type (e.g., "Text", "YesNo", "Integer", "Length")
        /// </summary>
        public string ParameterType { get; set; }

        /// <summary>
        /// Group name in the shared parameter file (e.g., "LoBIM", "Other")
        /// </summary>
        public string SharedParameterGroup { get; set; }

        /// <summary>
        /// Group where the parameter appears in Revit (e.g., "Identity Data", "Other")
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Description of the parameter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether this is an instance parameter (true) or type parameter (false)
        /// </summary>
        public bool IsInstance { get; set; }

        /// <summary>
        /// List of category names this parameter applies to (e.g., "Walls", "Doors", "Sheets")
        /// </summary>
        public List<string> Categories { get; set; }

        /// <summary>
        /// The Revit ForgeTypeId for the parameter type
        /// This is computed from ParameterType string
        /// </summary>
        public ForgeTypeId ForgeParameterType { get; set; }

        /// <summary>
        /// The Revit GroupTypeId for where the parameter appears in properties
        /// This is computed from GroupName string
        /// </summary>
        public ForgeTypeId ForgeGroupType { get; set; }

        /// <summary>
        /// If true, creates as Project Parameter (internal to document).
        /// If false, creates as Shared Parameter (from shared parameter file).
        /// Project parameters are NOT locked by templates and stay within the document.
        /// </summary>
        public bool IsProjectParameter { get; set; }

        public ProjectParameterDefinition()
        {
            Categories = new List<string>();
            IsInstance = true; // Default to instance parameter
            SharedParameterGroup = "LoBIM"; // Default shared parameter group
            GroupName = "Identity Data"; // Default Revit group
            IsProjectParameter = true; // Default to project parameter (not locked by templates)
        }
    }
}
