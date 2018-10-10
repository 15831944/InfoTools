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
using System.Windows.Shapes;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3DInfoTools.Controls;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    /// <summary>
    /// Логика взаимодействия для ConfigureNetworkCreationWindow.xaml
    /// </summary>
    public partial class ConfigureNetworkCreationWindow : Window
    {
        private const string DEFAULT_GRID_LAYER = "02_Сетка";
        private const string DEFAULT_STRUCTURES_LAYER = "44_Крышки колодцев";
        private const string DEFAULT_STRUCTURE_LABELS_LAYER = "45_Номера колодцев";
        private const string DEFAULT_STRUCTURE_BLOCK = "M5_075";

        private Document doc;
        private CivilDocument cdok;
        public ConfigureNetworkCreationWindow(Document doc, CivilDocument cdok)
        {
            this.doc = doc;
            this.cdok = cdok;
            InitializeComponent();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Создание элементов управления для задания слоев
            List<SelectLayerUserControl.DisplayLayer> displayLayers = SelectLayerUserControl.GetDisplayLayers(doc);

            SelectLayerUserControl gridLayerInput = new SelectLayerUserControl()
            { Owner = this, Doc = doc, DisplayLayers = displayLayers, DefaultLayerIfExists = DEFAULT_GRID_LAYER };
            Grid.SetRow(gridLayerInput, 0);
            Grid.SetColumn(gridLayerInput, 1);
            gridLayerInput.Margin = new Thickness(0, 0, 0, 0);
            mainGrid.Children.Add(gridLayerInput);

            SelectLayerUserControl structuresLayerInput = new SelectLayerUserControl()
            { Owner = this, Doc = doc, DisplayLayers = displayLayers, DefaultLayerIfExists = DEFAULT_STRUCTURES_LAYER };
            Grid.SetRow(structuresLayerInput, 1);
            Grid.SetColumn(structuresLayerInput, 1);
            structuresLayerInput.Margin = new Thickness(0, 0, 0, 0);
            mainGrid.Children.Add(structuresLayerInput);

            SelectLayerUserControl structureLabelsLayerInput = new SelectLayerUserControl()
            { Owner = this, Doc = doc, DisplayLayers = displayLayers, DefaultLayerIfExists = DEFAULT_STRUCTURE_LABELS_LAYER };
            Grid.SetRow(structureLabelsLayerInput, 2);
            Grid.SetColumn(structureLabelsLayerInput, 1);
            structureLabelsLayerInput.Margin = new Thickness(0, 0, 0, 0);
            mainGrid.Children.Add(structureLabelsLayerInput);

            //Создать коллекцию для выбора списка элементов сети
            List<PartsList> partsLists = new List<PartsList>();
            Database db = doc.Database;
            PartsListCollection partListColl = cdok.Styles.PartsListSet;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId plId in partListColl)
                {
                    PartsList pl = (PartsList)tr.GetObject(plId, OpenMode.ForRead);
                    partsLists.Add(pl);
                }
                tr.Commit();
            }
            partListsComboBox.ItemsSource = partsLists;


            //DataGridTemplateColumn blockSelectionCol = new DataGridTemplateColumn();
            //blockSelectionCol.Header = "Блоки";
            //FrameworkElementFactory factory1 = new FrameworkElementFactory(typeof(SelectBlockUserControl));
            //factory1.SetValue(SelectBlockUserControl.OwnerProperty, this);
            //factory1.SetValue(SelectBlockUserControl.DocProperty, doc);
            //DataTemplate blockSelTemplate = new DataTemplate();
            //blockSelTemplate.VisualTree = factory1;
            //blockSelectionCol.CellTemplate = blockSelTemplate;
            //blockMappingDataGrid.Columns.Add(blockSelectionCol);


            
            blockMappingDataGrid.ItemsSource = new List<BindingObj> { new BindingObj(this, doc), new BindingObj(this, doc) };



            //Combo box в Datagrid https://www.c-sharpcorner.com/uploadfile/dpatra/combobox-in-datagrid-in-wpf/
            //для выбора структур в ячейках Datagrid должно быть TreeView?
            //List<BlockTableRecord> blocks = SelectBlockUserControl.GetBlocks(doc);
            //SelectBlockUserControl test = new SelectBlockUserControl()
            //{ Owner = this, Doc = doc, Blocks = blocks, DefaultBlockIfExists = DEFAULT_STRUCTURE_BLOCK };
            //Grid.SetRow(test, 5);
            //Grid.SetColumn(test, 1);
            //test.Margin = new Thickness(0, 0, 0, 0);
            //mainGrid.Children.Add(test);
        }

        public class BindingObj
        {
            public Window Owner { get; set; }
            public Document Doc { get; set; }

            public string Name { get; set; }

            public BindingObj(Window owner, Document doc)
            {
                Owner = owner;
                Doc = doc;
                Name = doc.Name;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
