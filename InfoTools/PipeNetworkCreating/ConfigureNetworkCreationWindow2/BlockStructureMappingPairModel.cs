using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3DInfoTools.Controls.SelectPartSizeUserControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
//using Civil3DInfoTools.Controls;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DInfoTools.PipeNetworkCreating.ConfigureNetworkCreationWindow2
{
    public class BlockStructureMappingPairModel : INotifyPropertyChanged
    {
        public event EventHandler SelectionChanged;

        private Controls.SelectBlockUserControl3.ViewModel blockVM = null;
        public Controls.SelectBlockUserControl3.ViewModel BlockVM
        {
            get
            { return blockVM; }
            set
            {
                blockVM = value;
                OnPropertyChanged("BlockVM");
            }
        }

        private SelectPartSizeViewModel structureVM = null;

        public SelectPartSizeViewModel StructureVM
        {
            get { return structureVM; }
            set
            {
                structureVM = value;
                OnPropertyChanged("StructureVM");
            }
        }

        public BlockStructureMappingPairModel(Document doc, Window mainWindow,
            ObservableCollection<BlockTableRecord> blocks, PartsList partsList)
        {

            blockVM = new Controls.SelectBlockUserControl3.ViewModel(doc, mainWindow, blocks);
            structureVM = new SelectPartSizeViewModel(doc, partsList,
                PartType.StructJunction | PartType.StructGeneral | PartType.UndefinedPartType);

            blockVM.SelectionChanged += FireSelectionChanged;//передать информацию о том, что выбор изменен в ViewModel
            structureVM.SelectionChanged += FireSelectionChanged;
        }

        private void FireSelectionChanged(object sender, EventArgs args)
        {
            if (SelectionChanged!=null)
            {
                SelectionChanged(this, new EventArgs());
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
