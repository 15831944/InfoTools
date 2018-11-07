using Autodesk.AutoCAD.DatabaseServices;
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
    //объекты классов Autodesk.Civil.DatabaseServices.Styles.PartFamily
    //и Autodesk.Civil.DatabaseServices.Styles.PartSize связаны иерархически
    //каждая PartFamily содержит несколько PartSize
    /// <summary>
    /// PartFamily и все содержащиеся в нем PartSize
    /// </summary>
    public class SelectPartFamilyModel : INotifyPropertyChanged
    {
        private PartFamily partFamily = null;
        private ObservableCollection<PartSize> partSizes = new ObservableCollection<PartSize>();


        public PartFamily PartFamily
        {
            get { return partFamily; }
            set
            {
                partFamily = value;
                OnPropertyChanged("PartFamily");
                OnPropertyChanged("PartSizes");
                OnPropertyChanged("Name");
            }
        }

        public ObservableCollection<PartSize> PartSizes
        {
            get { return partSizes; }
        }

        public string Name { get { return partFamily?.Name; } }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }


        public SelectPartFamilyModel(PartFamily partFamily, ObservableCollection<PartSize> partSizes)
        {
            this.partFamily = partFamily;
            this.partSizes = partSizes;
        }
    }
}
