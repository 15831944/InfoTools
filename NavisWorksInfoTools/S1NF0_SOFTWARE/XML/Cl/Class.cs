using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl
{
    public class Class
    {
        [XmlIgnore]
        private string name;
        [XmlAttribute]
        public string Name { get { return name; } set { name = value.Trim(); } }

        [XmlAttribute]
        public string NameInPlural { get; set; }

        [XmlAttribute]
        public string DetailLevel { get; set; }

        [XmlAttribute]
        public string Code { get; set; }


        [XmlArray("Properties"), XmlArrayItem("Property")]
        public List<Property> Properties { get; set; } = new List<Property>();


        [XmlElement("Class")]
        public List<Class> NestedClasses { get; set; } = new List<Class>();

        public override int GetHashCode()
        {
            return Code.GetHashCode();
        }

        /// <summary>
        /// СВОЙСТВА МОГУТ ИМЕТЬ ОДИНАКОВЫЕ ИМЕНА. В МЯКИШЕ ВСЕ ИМЕНА ДОЛЖНЫ БЫТЬ РАЗНЫМИ
        /// </summary>
        public void DeleteDuplicateProps()
        {
            Properties = (new SortedSet<Property>(Properties)).ToList();
        }
    }
}
