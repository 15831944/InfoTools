using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Civil3DInfoTools.ObjectInsertion.XMLClasses
{
    public class ObjectPosition
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("X")]
        public double X { get; set; }

        [XmlAttribute("Y")]
        public double Y { get; set; }

        [XmlAttribute("Z")]
        public double Z { get; set; }

        [XmlAttribute("Z_Rotation")]
        public double Z_Rotation { get; set; }
    }
}
