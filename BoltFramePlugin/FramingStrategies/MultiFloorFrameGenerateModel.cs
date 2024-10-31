using Autodesk.Revit.DB;

namespace BoltFramePlugin.FramingStrategies
{
    /// <summary>
    /// Model containing parameters for generating floor frames on multiple floors.
    /// Each floor can have its own level.
    /// </summary>
    public class MultiFloorFrameGenerateModel : IFrameGenerateModel
    {
        /// <summary>
        /// Collection of target floor elements to frame.
        /// </summary>
        public IEnumerable<Element> TargetElements { get; set; }

        /// <summary>
        /// The level associated with each floor element.
        /// Key: Floor ElementId
        /// Value: Corresponding Level
        /// </summary>
        public Dictionary<ElementId, Level> FloorLevels { get; set; } = new Dictionary<ElementId, Level>();

        /// <summary>
        /// Collection of grid configurations (horizontal and vertical).
        /// </summary>
        public List<GridConfig> GridConfigs { get; set; } = new List<GridConfig>();
    }

    public class FrameModel
    {
        /// <summary>
        /// Associated ELement
        /// </summary>
        public Element TargetElement { get; set; }

        /// <summary>
        /// Associated Level
        /// </summary>
        public Level Level { get; set; }

        /// <summary>
        /// Collection of grid configurations (horizontal and vertical).
        /// </summary>
        public List<GridConfig> GridConfigs { get; set; } = new List<GridConfig>();
    }
}
