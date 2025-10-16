using Autodesk.Revit.DB;

namespace BoltFramePlugin.Models.LimitingDistance
{
    public class WallInfo
    {
        public Wall Wall { get; set; }
        public ElementId ElementId { get; set; }
        public string Name { get; set; }
        public double Length { get; set; }
        public string LengthFormatted => $"{Length:F2} ft";

        // Area properties
        public double GrossArea { get; set; }
        public double OpeningsArea { get; set; }
        public double NetArea => GrossArea - OpeningsArea;

        public string GrossAreaFormatted => $"{GrossArea:F2} ft²";
        public string OpeningsAreaFormatted => $"{OpeningsArea:F2} ft²";
        public string NetAreaFormatted => $"{NetArea:F2} ft²";
    }
}
