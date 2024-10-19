using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        /// <summary>
        /// Additional spacing parameter if needed.
        /// </summary>
        public double Spacing { get; set; }

        /// <summary>
        /// Offset in the Z-axis for beam placement.
        /// </summary>
        public double Z_Offset { get; set; }

        // Implement or remove other properties as per interface requirements

        // Implement both TargetElement and TargetElements for interface compliance
        public Element TargetElement
        {
            get => null;
            set => throw new NotImplementedException("Use TargetElements for multiple floors.");
        }
    }
}
