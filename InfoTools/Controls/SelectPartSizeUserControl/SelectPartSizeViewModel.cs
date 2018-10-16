using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.Controls.SelectPartSizeUserControl
{

    public class SelectPartSizeViewModel : INotifyPropertyChanged
    {
        public event EventHandler SelectionChanged;

        private Document doc = null;
        private PartsList partsList = null;
        private PartType partType = PartType.StructGeneral;

        public PartsList PartsList
        {
            get { return partsList; }
            set
            {
                partsList = value;
                PartFamilies = GetPartFams(doc, partsList);
                OnPropertyChanged("PartsList");
                OnPropertyChanged("PartSizes");
            }
        }



        private object selectedPartFamilyItem = null;
        /// <summary>
        /// binding
        /// </summary>
        public object SelectedPartFamilyItem
        {
            get { return selectedPartFamilyItem; }
            set
            {
                selectedPartFamilyItem = value;
                OnPropertyChanged("SelectedPartFamilyItem");
                OnPropertyChanged("PartSizes");//эта строка нужна?
                OnPropertyChanged("PartFamDefined");
            }
        }

        public SelectPartSizeModel SelectedPartFamily
        {
            get
            {
                return selectedPartFamilyItem as SelectPartSizeModel;
            }
        }

        public bool PartFamDefined { get { return SelectedPartFamily != null; } }

        private object selectedPartSizeItem = null;
        /// <summary>
        /// binding
        /// </summary>
        public object SelectedPartSizeItem
        {
            get { return selectedPartSizeItem; }
            set
            {
                selectedPartSizeItem = value;
                OnPropertyChanged("SelectedPartSizeItem");

                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new EventArgs());
                }
            }
        }

        public PartSize SelectedPartSize
        {
            get { return selectedPartSizeItem as PartSize; }
        }

        private ObservableCollection<SelectPartSizeModel> partFamilies
            = new ObservableCollection<SelectPartSizeModel>();
        /// <summary>
        /// binding
        /// этот список меняется если изменяется PartsList
        /// </summary>
        public ObservableCollection<SelectPartSizeModel> PartFamilies
        { get { return partFamilies; }
            set
            {
                partFamilies = value;
                OnPropertyChanged("PartFamilies");
            }
        }
            

        /// <summary>
        /// binding
        /// </summary>
        public ObservableCollection<PartSize> PartSizes { get { return SelectedPartFamily.PartSizes; } }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }



        public SelectPartSizeViewModel(Document doc, PartsList partsList, PartType partType/*,
            ObservableCollection<SelectPartSizeModel> partFams = null*/)
        {
            this.doc = doc;
            this.partsList = partsList;
            this.partType = partType;

            //if (partFams!=null)
            //{
            //    PartFamilies = partFams;
            //}
            if (PartFamilies.Count == 0 && partsList!=null)
            {
                PartFamilies = GetPartFams(doc, partsList);
            }

            //нужна сортировка по алфавиту?

        }


        public ObservableCollection<SelectPartSizeModel> GetPartFams(Document doc, PartsList partsList)
        {
            ObservableCollection<SelectPartSizeModel> partfams = new ObservableCollection<SelectPartSizeModel>();
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                for (int i = 0; i < partsList.PartFamilyCount; i++)
                {
                    ObjectId plFamId = partsList[i];
                    PartFamily pf = (PartFamily)tr.GetObject(plFamId, OpenMode.ForRead);

                    if (partType.HasFlag(pf.PartType))
                    {
                        partfams.Add(new SelectPartSizeModel(pf));
                    }
                    
                }


                tr.Commit();
            }

            return partfams;
        }


    }
}
