namespace LoBIM.Features.ViewCloning.Models
{
    /// <summary>
    /// Defines how cloned views should be positioned relative to the linked file
    /// </summary>
    public enum ViewPositioningMode
    {
        /// <summary>
        /// Use the link's transform (Internal Origin to Internal Origin)
        /// </summary>
        InternalOriginToInternalOrigin,

        /// <summary>
        /// Center to Center positioning
        /// </summary>
        CenterToCenter,

        /// <summary>
        /// Use shared coordinates (no transform)
        /// </summary>
        BySharedCoordinates,

        /// <summary>
        /// Project Base Point to Project Base Point
        /// </summary>
        ProjectBasePointToProjectBasePoint,

        /// <summary>
        /// Origin to last placed
        /// </summary>
        OriginToLastPlaced,

        /// <summary>
        /// No coordinate transformation - use exact coordinates from linked file
        /// </summary>
        NoTransform,

        /// <summary>
        /// TEST: Use Identity transform (no offset, no rotation)
        /// </summary>
        TestIdentity,

        /// <summary>
        /// TEST: Apply only link origin offset (no rotation)
        /// </summary>
        TestLinkOriginOnly,

        /// <summary>
        /// TEST: Inverse of link transform
        /// </summary>
        TestInverseLinkTransform,

        /// <summary>
        /// TEST: Use source cropbox transform exactly as-is (most likely correct for shared coordinates)
        /// </summary>
        TestSourceTransformOnly,

        /// <summary>
        /// TEST: Create section using absolute world coordinates from linked view
        /// </summary>
        TestAbsoluteWorldCoordinates,

        /// <summary>
        /// TEST: Ignore cropbox, use section Origin/Direction and reconstruct box from scratch
        /// </summary>
        TestReconstructFromOrigin
    }
}
