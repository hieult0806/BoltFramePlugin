namespace LoBIM.Features.ViewCloning.Models
{
    /// <summary>
    /// Defines how cloned views should be positioned relative to the linked file
    /// </summary>
    public enum ViewPositioningMode
    {
        /// <summary>
        /// Use the link's transform based on link placement mode (Internal Origin to Internal Origin)
        /// This is the standard mode for most link configurations
        /// </summary>
        InternalOriginToInternalOrigin,

        /// <summary>
        /// Use shared coordinates - links placed by Project Base Point to Project Base Point
        /// Views are positioned using their absolute coordinates from the linked file
        /// </summary>
        ProjectBasePointToProjectBasePoint,

        /// <summary>
        /// Use shared coordinates with no transformation
        /// Assumes linked file shares the same coordinate system as host
        /// </summary>
        BySharedCoordinates
    }
}
