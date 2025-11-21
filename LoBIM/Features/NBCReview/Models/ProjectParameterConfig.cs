using System.Collections.Generic;

namespace LoBIM.Features.NBCReview.Models
{
    /// <summary>
    /// Root configuration class for project parameters JSON
    /// </summary>
    public class ProjectParameterConfiguration
    {
        public List<ProjectParameterDefinition> ProjectParameters { get; set; } = new List<ProjectParameterDefinition>();
    }

    /// <summary>
    /// Defines a single project parameter
    /// </summary>
    public class ProjectParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string ParameterType { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsInstance { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
    }
}
