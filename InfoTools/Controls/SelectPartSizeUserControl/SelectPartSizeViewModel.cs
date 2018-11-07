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
                PartFamilies = GetPartFams(doc, partsList, ObjectId.Null, ObjectId.Null);
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

        public SelectPartFamilyModel SelectedPartFamily
        {
            get
            {
                return selectedPartFamilyItem as SelectPartFamilyModel;
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

        private ObservableCollection<SelectPartFamilyModel> partFamilies
            = new ObservableCollection<SelectPartFamilyModel>();
        /// <summary>
        /// binding
        /// этот список меняется если изменяется PartsList
        /// </summary>
        public ObservableCollection<SelectPartFamilyModel> PartFamilies
        {
            get { return partFamilies; }
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



        public SelectPartSizeViewModel(Document doc, PartsList partsList, PartType partType,
            ObjectId defPartFamIfExists, ObjectId defPartSizeIfExists)
        {
            this.doc = doc;
            this.partsList = partsList;
            this.partType = partType;

            if (PartFamilies.Count == 0 && partsList != null)
            {
                pfStartSelection = null;
                psStartSelection = null;
                PartFamilies = GetPartFams(doc, partsList, defPartFamIfExists, defPartSizeIfExists);

                if (pfStartSelection != null)
                {
                    SelectedPartFamilyItem = pfStartSelection;
                    if (psStartSelection != null)
                    {
                        SelectedPartSizeItem = psStartSelection;
                    }
                }

                
            }



        }

        //переменные присваиваются только методом GetPartFams
        private SelectPartFamilyModel pfStartSelection = null;
        private PartSize psStartSelection = null;

        private ObservableCollection<SelectPartFamilyModel> GetPartFams(Document doc, PartsList partsList,
            ObjectId defPartFamIfExists, ObjectId defPartSizeIfExists)
        {
            ObservableCollection<SelectPartFamilyModel> partfams = new ObservableCollection<SelectPartFamilyModel>();
            Database db = doc.Database;

            pfStartSelection = null;
            psStartSelection = null;


            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                for (int i = 0; i < partsList.PartFamilyCount; i++)
                {
                    ObjectId plFamId = partsList[i];
                    PartFamily pf = (PartFamily)tr.GetObject(plFamId, OpenMode.ForRead);

                    


                    if (partType.HasFlag(pf.PartType))
                    {
                        ObservableCollection<PartSize> partSizes = new ObservableCollection<PartSize>();
                        PartSize startSelSizeCandidate = null;
                        for (int j = 0; j < pf.PartSizeCount; j++)
                        {
                            ObjectId psId = pf[j];
                            PartSize ps = (PartSize)psId.GetObject(OpenMode.ForRead);
                            if (defPartSizeIfExists.Equals(psId))
                            {
                                startSelSizeCandidate = ps;
                            }
                            partSizes.Add(ps);
                        }

                        SelectPartFamilyModel spfm = new SelectPartFamilyModel(pf, partSizes);
                        if (defPartFamIfExists.Equals(plFamId))
                        {
                            pfStartSelection = spfm;
                            if (startSelSizeCandidate!=null)
                            {
                                psStartSelection = startSelSizeCandidate;
                            }
                        }
                        partfams.Add(spfm);
                    }

                }


                tr.Commit();
            }

            return partfams;
        }


    }
}
