using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Windows;
using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.Geology
{
    public class SelectLayerViewModel : INotifyPropertyChanged
    {
        private PaletteSet ps;

        public ObservableCollection<LayerTableRecord> Layers { get; private set; }

        private object selectedLayer;
        public object SelectedLayer
        {
            get { return selectedLayer; }
            set
            {
                selectedLayer = value;
                OnPropertyChanged("SelectedLayer");
            }
        }

        public SelectLayerViewModel(List<LayerTableRecord> ltrList, PaletteSet ps)
        {
            this.ps = ps;
            Layers = new ObservableCollection<LayerTableRecord>(ltrList);
        }


        //////////////////////////////////////////////////////////////////////////////
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
