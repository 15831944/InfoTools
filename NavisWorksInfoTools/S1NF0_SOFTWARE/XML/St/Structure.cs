using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE.XML.St
{
    public class Structure
    {
        [XmlIgnore]
        private string name;
        [XmlAttribute]
        public string Name { get { return name; } set { name = value.Trim(); } }

        [XmlAttribute]
        public string Classifier { get; set; }

        [XmlAttribute]
        public bool IsPrimary { get; set; }

        //[XmlElement("Object")]
        //public Object RootObject { get; set; }

        [XmlElement("Object")]
        public List<Object> NestedObjects { get; set; } = new List<Object>();
    }
}
