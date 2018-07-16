using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.XMLClasses
{
    public class PositionData
    {
        [XmlArray("ObjectPositions"), XmlArrayItem("ObjectPosition")]
        public List<ObjectPosition> ObjectPositions { get; set; } = new List<ObjectPosition>();

        [XmlArray("SpillwayPositions"), XmlArrayItem("SpillwayPosition")]
        public List<SpillwayPosition> SpillwayPositions { get; set; } = new List<SpillwayPosition>();
    }
}
