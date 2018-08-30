using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Serialization;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE.XML.St
{
    public class Object : INotifyPropertyChanged
    {
        [XmlIgnore]
        private string name;
        [XmlAttribute]
        public string Name { get { return name; } set { name = value.Trim(); } }

        [XmlAttribute]
        public string UnigineId { get; set; } = "";

        [XmlAttribute]
        public string ClassCode { get; set; }


        [XmlAttribute]
        public string SceneObjectId { get; set; }


        [XmlArray("Properties"), XmlArrayItem("Property")]
        public List<Property> Properties { get; set; } = new List<Property>();


        [XmlElement("Object")]
        public List<Object> NestedObjects { get; set; } = new List<Object>();

        /// <summary>
        /// Ссылка на объект Navis для соответствующих объектов
        /// </summary>
        [XmlIgnore]
        public ModelItem NavisItem { get; set; } = null;

        //[XmlIgnore]
        //public Object Parent { get; set; }



        /// <summary>
        /// IMMUTABLE
        /// Список вложенных для отображения в дереве. 
        /// </summary>
        [XmlIgnore]
        public List<Object> NestedDisplayObjects { get; set; }

        /// <summary>
        /// Объекты геометрии текущего документа Navis
        /// </summary>
        [XmlIgnore]
        public List<Object> NestedGeometryObjectsCurrDoc { get; set; }

        /// <summary>
        /// IMMUTABLE
        /// Объекты геометрии другого документа Navis.
        /// </summary>
        [XmlIgnore]
        public List<Object> NestedGeometryObjectsOtherDoc { get; set; }

        /// <summary>
        /// IMMUTABLE
        /// Список вложенных, очищенный от объектов геометрии текущего документа Navis.
        /// </summary>
        [XmlIgnore]
        public List<Object> ResetNestedObjects { get; set; }


        [XmlIgnore]
        public Brush Color
        {
            get
            {
                if (NestedGeometryObjectsCurrDoc.Count != 0)
                {
                    return new SolidColorBrush(Colors.Red);
                }
                else if (NestedGeometryObjectsOtherDoc.Count != 0)
                {
                    return new SolidColorBrush(Colors.Blue);
                }
                else
                {
                    return new SolidColorBrush(Colors.Black);
                }
            }
        }

        public string DisplayName
        {
            get
            {
                string str = Name;
                if (NestedGeometryObjectsOtherDoc.Count != 0)
                {
                    str += " : " + NestedGeometryObjectsOtherDoc.Count;
                }
                if (NestedGeometryObjectsCurrDoc.Count != 0)
                {
                    str += " : " + NestedGeometryObjectsCurrDoc.Count;
                }

                return str;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(Color)));
            }
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
