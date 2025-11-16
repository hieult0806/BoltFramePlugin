namespace LoBIM.Services.Parameters.Models
{
    /// <summary>
    /// Result of a parameter creation or update operation
    /// </summary>
    public class ParameterCreationResult
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Status of the parameter (e.g., "Created", "Already Exists", "Failed")
        /// </summary>
        public ParameterStatus Status { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Whether this parameter already existed before the operation
        /// </summary>
        public bool AlreadyExisted { get; set; }
    }

    /// <summary>
    /// Status of a project parameter
    /// </summary>
    public enum ParameterStatus
    {
        /// <summary>
        /// Parameter was created successfully
        /// </summary>
        Created,

        /// <summary>
        /// Parameter already exists
        /// </summary>
        AlreadyExists,

        /// <summary>
        /// Parameter binding was updated
        /// </summary>
        Updated,

        /// <summary>
        /// Parameter creation or update failed
        /// </summary>
        Failed,

        /// <summary>
        /// Parameter definition not found in shared parameter file
        /// </summary>
        DefinitionNotFound
    }
}
