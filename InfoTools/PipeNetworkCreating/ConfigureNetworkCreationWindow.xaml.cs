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
    /// Это окно и контролы, которые в нем есть сделаны очень плохо
    /// 
    /// Необходимо учиться MVVM
    /// </summary>
    public partial class ConfigureNetworkCreationWindow : Window
    {
        private const string DEFAULT_GRID_LAYER = "02_Сетка";
        private const string DEFAULT_STRUCTURES_LAYER = "44_Крышки колодцев";
        private const string DEFAULT_STRUCTURE_LABELS_LAYER = "45_Номера колодцев";
        private const string DEFAULT_STRUCTURE_BLOCK = "M5_075";

        private Document doc;
        private CivilDocument cdok;


        private List<BlockStructureMapping> blockStructures = new List<BlockStructureMapping>();

        private BindingObj partFamiliesRoot = new BindingObj("Выбор типоразмера");
        private List<BindingObj> partFamiliesItemSource;

        //private List<BindingObj> partFamilies = new List<BindingObj>();


        public ConfigureNetworkCreationWindow(Document doc, CivilDocument cdok)
        {
            this.doc = doc;
            this.cdok = cdok;
            partFamiliesItemSource = new List<BindingObj>() { partFamiliesRoot };
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Создание элементов управления для задания слоев
            //List<SelectLayerUserControl.DisplayLayer> displayLayers = SelectLayerUserControl.GetDisplayLayers(doc);

            //SelectLayerUserControl gridLayerInput = new SelectLayerUserControl()
            //{ Owner = this, Doc = doc, DisplayLayers = displayLayers, DefaultLayerIfExists = DEFAULT_GRID_LAYER };
            //Grid.SetRow(gridLayerInput, 0);
            //Grid.SetColumn(gridLayerInput, 1);
            //gridLayerInput.Margin = new Thickness(0, 0, 0, 0);
            //mainGrid.Children.Add(gridLayerInput);

            //SelectLayerUserControl structuresLayerInput = new SelectLayerUserControl()
            //{ Owner = this, Doc = doc, DisplayLayers = displayLayers, DefaultLayerIfExists = DEFAULT_STRUCTURES_LAYER };
            //Grid.SetRow(structuresLayerInput, 1);
            //Grid.SetColumn(structuresLayerInput, 1);
            //structuresLayerInput.Margin = new Thickness(0, 0, 0, 0);
            //mainGrid.Children.Add(structuresLayerInput);

            //SelectLayerUserControl structureLabelsLayerInput = new SelectLayerUserControl()
            //{ Owner = this, Doc = doc, DisplayLayers = displayLayers, DefaultLayerIfExists = DEFAULT_STRUCTURE_LABELS_LAYER };
            //Grid.SetRow(structureLabelsLayerInput, 2);
            //Grid.SetColumn(structureLabelsLayerInput, 1);
            //structureLabelsLayerInput.Margin = new Thickness(0, 0, 0, 0);
            //mainGrid.Children.Add(structureLabelsLayerInput);

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


            blockStructures.Add(new BlockStructureMapping(this, doc));//test
            blockStructures.Add(new BlockStructureMapping(this, doc));//test
            blockMappingDataGrid.ItemsSource = blockStructures;

            testMenu.ItemsSource = partFamiliesItemSource;



        }



        public class BlockStructureMapping
        {
            public Window Owner { get; set; }

            public Document Doc { get; set; }

            public ObjectId BlockId { get; set; }

            public ObjectId PartSizeId { get; set; }

            public BlockStructureMapping(Window owner, Document doc)
            {
                Owner = owner;
                Doc = doc;
            }
        }


        private class BindingObj
        {
            public string Name { get; set; }

            public PartFamily PartFamily { get; set; }

            public PartSize PartSize { get; set; }

            public List<BindingObj> NestedObjs { get; set; } = new List<BindingObj>();

            public BindingObj(string name, PartFamily partFamily = null, PartSize partSize = null)
            {
                Name = name;
                PartFamily = partFamily;
                PartSize = partSize;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// При выборе PartList считать из документа все, что содержит данный PartList 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void partListsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PartsList selectedPartsList = partListsComboBox.SelectedItem as PartsList;

            if (selectedPartsList != null)
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < selectedPartsList.PartFamilyCount; i++)
                    {
                        ObjectId plFamId = selectedPartsList[i];
                        PartFamily pf = (PartFamily)tr.GetObject(plFamId, OpenMode.ForRead);
                        BindingObj pfBinding = new BindingObj(pf.Name, pf);
                        partFamiliesRoot.NestedObjs.Add(pfBinding);
                        for (int j = 0; j < pf.PartSizeCount; j++)
                        {
                            ObjectId psId = pf[j];
                            PartSize ps = (PartSize)tr.GetObject(psId, OpenMode.ForRead);
                            pfBinding.NestedObjs.Add(new BindingObj(ps.Name, partSize: ps));
                        }

                    }

                    tr.Commit();
                }

            }
            else
            {
                partFamiliesRoot.NestedObjs = new List<BindingObj>();
            }

        }



    }
}
