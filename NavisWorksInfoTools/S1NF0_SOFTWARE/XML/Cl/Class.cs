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
        /// Так как в мякише названия свойств становятся названиями полей таблиц базы данных
        /// к ним предъявляются особые требования
        /// </summary>
        public void PropsCorrection()
        {
            Properties = PropsCorrection(Properties);
        }


        public static List<Property> PropsCorrection(List<Property> clProps)
        {
            List<Property> editedProps = new List<Property>(clProps);
            //УДАЛИТЬ ВСЕ НЕДОПУСТИМЫЕ СИМВОЛЫ В НАЗВАНИЯХ СВОЙСТВ
            //ДЛИНА НАЗВАНИЯ СВОЙСТВА НЕ БОЛЕЕ 127 СИМВОЛОВ
            foreach (Property p in editedProps)
            {
                p.Name = Utils.GetSafeS1NF0AppPropName(p.Name);
            }

            //СВОЙСТВА МОГУТ ИМЕТЬ ОДИНАКОВЫЕ ИМЕНА. В МЯКИШЕ ВСЕ ИМЕНА ДОЛЖНЫ БЫТЬ РАЗНЫМИ. УДАЛИТЬ ДУБЛИКАТЫ
            editedProps = (new SortedSet<Property>(editedProps)).ToList();

            return editedProps;
        }
    }
}
