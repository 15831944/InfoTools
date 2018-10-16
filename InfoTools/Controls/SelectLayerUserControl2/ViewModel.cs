using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Common;

namespace Civil3DInfoTools.Controls.SelectLayerUserControl2
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event EventHandler SelectionChanged;

        private Document doc;
        private Window mainWindow;

        private Dictionary<ObjectId, int> layerIndexesLookup = new Dictionary<ObjectId, int>();

        //TODO: То что у меня завязаны и SelectedIndex и SelectedItem - это явно избыточно!!!
        private int selectedIndex = -1;
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                selectedIndex = value;
                OnPropertyChanged("SelectedIndex");

                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new EventArgs());
                }
            }
        }

        private object selectedItem = null;
        public object SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value;
                OnPropertyChanged("SelectedItem");

                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new EventArgs());
                }
            }
        }

        public LayerTableRecord SelectedLayer {
            get
            {
                Model selectedModel = selectedItem as Model;
                if (selectedModel!=null)
                {
                    return selectedModel.LayerTableRecord;
                }
                return null;
            }
        }

        private readonly RelayCommand selectLayerCommand1 = null;
        public RelayCommand SelectLayerCommand1
        { get { return selectLayerCommand1; } }

        public ObservableCollection<Model> Layers { get; set; } = new ObservableCollection<Model>();

        public ViewModel(Document doc, Window mainWindow,
            ObservableCollection<Model> layers = null, string defaulLayerIfExists = null)
        {
            //создание объекта команды для выбора объекта
            selectLayerCommand1 = new RelayCommand(new Action<object>(SelectLayer));

            this.doc = doc;
            this.mainWindow = mainWindow;
            if (layers != null)
            {
                Layers = layers;
            }
            if (Layers.Count == 0)
            {
                Layers = GetLayers(doc);
            }
            //не лучший путь для сортировки. подробнее - https://stackoverflow.com/a/19113072
            Layers = new ObservableCollection<Model>(Layers.OrderBy(btr => btr.Name));

            int startSelIndex = -1;
            int i = 0;
            foreach (Model l in Layers)
            {
                layerIndexesLookup.Add(l.LayerTableRecord.Id, i);
                if (!String.IsNullOrEmpty(defaulLayerIfExists) && l.Name.Equals(defaulLayerIfExists))
                    startSelIndex = i;

                i++;
            }

            if (startSelIndex != -1)
                SelectedIndex = startSelIndex;
        }

        /// <summary>
        /// Получить все слои чертежа кроме слоев внешних ссылок
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static ObservableCollection<Model> GetLayers(Document doc)
        {
            ObservableCollection<Model> layers = new ObservableCollection<Model>();
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layerId in layerTable)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                    string name = ltr.Name;
                    if (!name.Contains("|"))//не брать слои внешних ссылок
                        layers.Add(new Model(ltr));
                }

                tr.Commit();
            }
            return layers;
        }



        /// <summary>
        /// Выбор объекта для указания слоя на чертеже AutoCAD
        /// </summary>
        /// <param name="obj"></param>
        private void SelectLayer(object obj)
        {
            if (doc != null && mainWindow != null)
            {
                mainWindow.Hide();

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
                    layerIndexesLookup.TryGetValue(layerId, out i);
                    if (i != -1)
                    {
                        SelectedIndex = i;
                    }
                }
                mainWindow.Show();
            }
        }


        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
