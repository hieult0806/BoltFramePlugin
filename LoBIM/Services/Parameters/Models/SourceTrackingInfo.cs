namespace LoBIM.Services.Parameters.Models
{
    /// <summary>
    /// Contains source tracking information for cloned sheets/views
    /// </summary>
    public class SourceTrackingInfo
    {
        /// <summary>
        /// Name of the source linked file
        /// </summary>
        public string SourceFileName { get; set; }

        /// <summary>
        /// Sheet number from source (for sheets only)
        /// </summary>
        public string SourceSheetNumber { get; set; }

        /// <summary>
        /// Sheet ID from source (for sheets only)
        /// </summary>
        public string SourceSheetId { get; set; }

        /// <summary>
        /// View name from source (for views only)
        /// </summary>
        public string SourceViewName { get; set; }

        /// <summary>
        /// View ID from source (for views only)
        /// </summary>
        public string SourceViewId { get; set; }

        /// <summary>
        /// Whether source tracking information is available
        /// </summary>
        public bool HasSourceTracking => !string.IsNullOrEmpty(SourceFileName);

        /// <summary>
        /// Display text for source tracking (for sheets)
        /// </summary>
        public string SourceTrackingText
        {
            get
            {
                if (!HasSourceTracking)
                    return string.Empty;

                if (!string.IsNullOrEmpty(SourceSheetNumber))
                    return $"From: {SourceFileName} > {SourceSheetNumber}";

                if (!string.IsNullOrEmpty(SourceViewName))
                    return $"From: {SourceFileName} > {SourceViewName}";

                return $"From: {SourceFileName}";
            }
        }
    }
}
