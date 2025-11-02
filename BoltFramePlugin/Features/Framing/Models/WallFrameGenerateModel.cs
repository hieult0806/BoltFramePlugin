using Autodesk.Revit.DB;
using BoltFramePlugin.Features.Framing.Strategies;

namespace BoltFramePlugin.Features.Framing.Models
{
    /// <summary>
    /// Model containing parameters for generating wall frames with plates, columns, and studs.
    /// </summary>
    public class WallFrameGenerateModel : IFrameGenerateModel
    {
        /// <summary>
        /// Collection of target wall elements to frame.
        /// </summary>
        public IEnumerable<Element> TargetElements { get; set; }

        /// <summary>
        /// The level associated with each wall element.
        /// Key: Wall ElementId
        /// Value: Corresponding Level
        /// </summary>
        public Dictionary<ElementId, Level> WallLevels { get; set; } = new Dictionary<ElementId, Level>();

        /// <summary>
        /// Configuration for top plate (horizontal beam at top of wall).
        /// Null if top plate should not be generated.
        /// </summary>
        public GridConfig TopPlateConfig { get; set; }

        /// <summary>
        /// Configuration for bottom plate (horizontal beam at bottom of wall).
        /// Null if bottom plate should not be generated.
        /// </summary>
        public GridConfig BottomPlateConfig { get; set; }

        /// <summary>
        /// Configuration for columns (vertical structural members at wall ends).
        /// Null if columns should not be generated.
        /// </summary>
        public GridConfig ColumnConfig { get; set; }

        /// <summary>
        /// Configuration for studs (vertical members distributed along wall).
        /// Includes spacing for stud placement.
        /// Null if studs should not be generated.
        /// </summary>
        public GridConfig StudConfig { get; set; }

        /// <summary>
        /// Whether to create a group containing all generated framing members.
        /// </summary>
        public bool CreateGroup { get; set; } = true;

        /// <summary>
        /// Name for the created group (if CreateGroup is true).
        /// </summary>
        public string GroupName { get; set; } = "Wall Framing";
    }
}
