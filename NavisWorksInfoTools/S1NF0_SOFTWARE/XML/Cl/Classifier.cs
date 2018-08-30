﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl
{
    public class Classifier
    {
        [XmlIgnore]
        private string name;
        [XmlAttribute]
        public string Name { get { return name; } set { name = value.Trim(); } }

        [XmlAttribute]
        public bool IsPrimary { get; set; }


        [XmlArray("DetailLevels"), XmlArrayItem("DetailLevel")]
        public List<string> DetailLevels { get; set; } = new List<string>();


        //[XmlElement("Class")]
        //public Class RootClass { get; set; }

        [XmlElement("Class")]
        public List<Class> NestedClasses { get; set; } = new List<Class>();
    }
}
