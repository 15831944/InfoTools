using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.XMLClasses
{
    public class Slope
    {
        [XmlAttribute("S")]
        public double S { get; set; }

        [XmlAttribute("Len")]
        public double Len { get; set; }
    }
}
