﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE.XML.St
{
    public class Property : IComparable<Property>
    {
        [XmlIgnore]
        private string name;
        [XmlAttribute]
        public string Name { get {return name; } set { name = value.Trim(); } }

        [XmlIgnore]
        private string _value;
        /// <summary>
        /// TODO: Navis может хранить четыре типа string, int, double и bool
        /// </summary>
        [XmlAttribute]
        public string Value { get { return _value; } set { _value = value.Trim(); } }

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
