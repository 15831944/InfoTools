using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Civil3DInfoTools.Controls
{
    /// <summary>
    /// Логика взаимодействия для SelectLayerUserControl.xaml
    /// </summary>
    public partial class SelectLayerUserControl : UserControl
    {
        public ObjectId LayerId
        {
            get
            {
                return ((DisplayLayer)layerComboBox.SelectedItem).LayerId;
            }
        }

        private Window owner;
        public Window Owner { get { return owner; } set { owner = value; EnableControl(); } }//обязательно задавать

        private Document doc;
        public Document Doc { get { return doc; } set { doc = value; EnableControl(); } }//обязательно задавать

        public List<DisplayLayer> DisplayLayers { get; set; }

        public string DefaultLayerIfExists { get; set; }

        /// <summary>
        /// поле проверяется только при загрузке контрола
        /// </summary>
        private bool controlIsValid = false;

        private Dictionary<ObjectId, int> layerInexesLookup = new Dictionary<ObjectId, int>();

        public SelectLayerUserControl()
        {

            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (controlIsValid)
            {
                //В comboBox отобразить названия всех слоев с отображением цвета
                //http://www.codescratcher.com/wpf/wpf-combobox-with-image/
                if (DisplayLayers == null)
                {
                    DisplayLayers = GetDisplayLayers(Doc);
                }

                DisplayLayers.Sort();
                layerComboBox.ItemsSource = DisplayLayers;
                int startSelIndex = -1;
                int i = 0;
                foreach (DisplayLayer dl in DisplayLayers)
                {
                    layerInexesLookup.Add(dl.LayerId, i);
                    if (!String.IsNullOrEmpty(DefaultLayerIfExists) && dl.Name.Equals(DefaultLayerIfExists))
                        startSelIndex = i;

                    i++;
                }

                if (startSelIndex != -1)
                    layerComboBox.SelectedIndex = startSelIndex;
            }
            else
            {
                //Необходимые свойства должны быть присвоены до загрузки окна
                //иначе элемент управления нельзя использовать
                this.IsEnabled = false;
            }



        }

        /// <summary>
        /// Включить контрол если необходимые свойства заданы
        /// </summary>
        private void EnableControl()
        {
            controlIsValid = Owner != null && Doc != null;
        }

        public class DisplayLayer : IComparable<DisplayLayer>
        {
            public string Name { get; set; }

            public SolidColorBrush Color { get; set; }

            public ObjectId LayerId { get; set; }

            public DisplayLayer(string name, ObjectId layerId, byte r, byte g, byte b)
            {
                Name = name;
                LayerId = layerId;
                Color = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            }

            public int CompareTo(DisplayLayer other)
            {
                return this.Name.CompareTo(other.Name);
            }
        }

        /// <summary>
        /// Считать все слои из документа в набор DisplayLayer
        /// </summary>
        /// <param name="doc"></param>
        public static List<DisplayLayer> GetDisplayLayers(Document doc)
        {
            List<DisplayLayer> displayLayers = new List<DisplayLayer>();
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layerId in layerTable)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                    string name = ltr.Name;

                    displayLayers.Add(new DisplayLayer(name, layerId,
                        ltr.Color.ColorValue.R, ltr.Color.ColorValue.G, ltr.Color.ColorValue.B));
                }

                tr.Commit();
            }
            return displayLayers;
        }

        /// <summary>
        /// Скрыть окно. Перейти в режим указания 1 объекта для задания слоя
        /// После выбора объекта, получить id слоя и по нему задать выбранный элемент в combo box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (controlIsValid)
            {
                Owner.Hide();

                Editor ed = doc.Editor;
                Database db = doc.Database;

                PromptEntityOptions peo = new PromptEntityOptions("\nУкажите объект для выбора слоя:");
                PromptEntityResult per1 = ed.GetEntity(peo);
                if (per1.Status == PromptStatus.OK)
                {
                    ObjectId selId = per1.ObjectId;

                    ObjectId layerId = ObjectId.Null;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Entity ent = (Entity)tr.GetObject(selId, OpenMode.ForRead);
                        layerId = ent.LayerId;

                        tr.Commit();
                    }

                    int i = -1;
                    layerInexesLookup.TryGetValue(layerId, out i);
                    if (i != -1)
                    {
                        layerComboBox.SelectedIndex = i;
                    }
                }
                //Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(Owner);
                Owner.Show();

            }

        }
    }
}
