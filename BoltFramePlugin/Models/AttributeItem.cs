using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.Models
{
    public class AttributeItem
    {
        public string UniqueId { get; set; }
        public string Parameter { get; set; }
        public object Value { get; set; }

        public AttributeItem(string parameter, object value)
        {
            Parameter = parameter;
            Value = value;
        }
    }
    public static class StudStartEnumValues
    {
        public static IEnumerable<StudStart> Values => Enum.GetValues(typeof(StudStart)).Cast<StudStart>();
    }

    public enum StudStart
    {
        Left,
        Center,
        Right,
    }
}
