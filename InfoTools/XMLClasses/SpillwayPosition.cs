using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Civil3DInfoTools.XMLClasses
{
    public class SpillwayPosition
    {
        [XmlAttribute("X")]
        public double X { get; set; }

        [XmlAttribute("Y")]
        public double Y { get; set; }

        [XmlAttribute("Z")]
        public double Z { get; set; }

        [XmlAttribute("Rotation")]
        public double Rotation { get; set; }

        [XmlAttribute("ToTheRight")]
        public bool ToTheRight { get; set; }

        [XmlArray("Slopes"), XmlArrayItem("Slope")]
        public List<Slope> Slopes { get; set; } = new List<Slope>();
    }
}
