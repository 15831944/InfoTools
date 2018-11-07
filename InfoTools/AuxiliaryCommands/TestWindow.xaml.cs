
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3DInfoTools.Controls.SelectPartSizeUserControl;
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
using Autodesk.Civil.DatabaseServices;

namespace Civil3DInfoTools.AuxiliaryCommands
{
    /// <summary>
    /// Логика взаимодействия для TestWindow.xaml
    /// </summary>
    public partial class TestWindow : Window
    {
        private List<PartsList> partsLists = new List<PartsList>();
        int i = 0;

        SelectPartSizeViewModel pldc = null;

        public Document Doc { get; set; }
        public TestWindow(Document doc, CivilDocument cdok)
        {
            this.Doc = doc;

            InitializeComponent();

            //Loaded += (a, b) =>
            //{
            //    Civil3DInfoTools.Controls.SelectBlockUserControl3.View control
            //    = new Controls.SelectBlockUserControl3.View( doc, this);
            //    mainGrid.Children.Add(control);
            //};

            //Для теста взять первый PartsList
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


            Loaded += (a, b) =>
            {
                mvvmTest1.DataContext
                = new Civil3DInfoTools.Controls.SelectBlockUserControl3.ViewModel(doc, this, 
                defaulBlockIfExists : "M5_075");
                mvvmTest2.DataContext
                = new Controls.SelectLayerUserControl2.ViewModel(doc, this, defaulLayerIfExists: "0");


                pldc = new SelectPartSizeViewModel(doc, partsLists.First(), PartType.StructGeneral | /*PartType.StructEquipment |*/
                PartType.StructJunction | PartType.StructNull /*| PartType.StructInletOutlet*/, ObjectId.Null, ObjectId.Null);
                mvvmTest3.DataContext = pldc;
            };

            Closing += (a, b) =>
            {
                var x = mvvmTest1.DataContext as Civil3DInfoTools.Controls.SelectBlockUserControl3.ViewModel;
                ObjectId? blockId = x.SelectedBlock?.Id;
                var y = mvvmTest2.DataContext as Civil3DInfoTools.Controls.SelectLayerUserControl2.ViewModel;
                ObjectId? layerId = y.SelectedLayer?.Id;

                SelectPartSizeViewModel z = mvvmTest3.DataContext as SelectPartSizeViewModel;
                ObjectId? pfId = z.SelectedPartFamily?.PartFamily?.Id;
                ObjectId? psId = z.SelectedPartSize?.Id;
            };
        }

        private void btn_Click(object sender, RoutedEventArgs e)
        {
            i = (i + 1) % partsLists.Count;
            pldc.PartsList = partsLists[i];
        }
    }
}
