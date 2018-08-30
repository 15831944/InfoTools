using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl
{
    public class Property : IComparable<Property>
    {
        [XmlIgnore]
        private string name;
        [XmlAttribute]
        public string Name { get { return name; } set { name = value.Trim(); } }

        [XmlIgnore]
        private string tag;
        [XmlAttribute]
        public string Tag { get { return tag; } set { tag = value.Trim(); } }

        [XmlAttribute]
        public string Type { get; set; } = "string";

        [XmlAttribute]
        public string DefaultValue { get; set; } = "...";

        public int CompareTo(Property other)
        {
           return Name.CompareTo(other.Name);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
