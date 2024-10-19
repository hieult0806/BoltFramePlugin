using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.FramingStrategies
{
    /// <summary>
    /// Enumeration for grid orientation.
    /// </summary>
    public enum GridOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Configuration settings for grid lines (beams or joists).
    /// </summary>
    public class GridConfig
    {
        /// <summary>
        /// The orientation of the grid (horizontal or vertical).
        /// </summary>
        public GridOrientation Orientation { get; set; }

        /// <summary>
        /// The family symbol for the grid element (beam or joist).
        /// </summary>
        public FamilySymbol FamilySymbol { get; set; }

        /// <summary>
        /// Spacing between grid elements.
        /// </summary>
        public double Spacing { get; set; }

        /// <summary>
        /// Z-axis offset for grid element placement.
        /// </summary>
        public double ZOffset { get; set; }

        /// <summary>
        /// Structural type of the grid element.
        /// </summary>
        public StructuralType StructuralType { get; set; }

        /// <summary>
        /// Optional: Additional properties specific to certain grid orientations can be added here.
        /// </summary>
    }
}

