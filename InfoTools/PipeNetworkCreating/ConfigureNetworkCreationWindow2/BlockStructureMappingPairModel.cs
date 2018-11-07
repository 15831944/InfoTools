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
        public event EventHandler BlockSelectionChanged;

        public event EventHandler PartSizeSelectionChanged;

        private Controls.SelectBlockUserControl3.ViewModel blockVM = null;
        public Controls.SelectBlockUserControl3.ViewModel BlockVM
        {
            get
            { return blockVM; }
            set
            {
                blockVM = value;
                blockVM.SelectionChanged += FireBlockSelectionChanged;
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
                structureVM.SelectionChanged += FirePartSizeSelectionChanged;
                OnPropertyChanged("StructureVM");
            }
        }

        public BlockStructureMappingPairModel(Document doc, Window mainWindow,
            ObservableCollection<BlockTableRecord> blocks, PartsList partsList,
            ObjectId defPartFam, ObjectId defPartSize, string defBlock = null)
        {

            BlockVM = new Controls.SelectBlockUserControl3.ViewModel(doc, mainWindow, blocks, defBlock);
            StructureVM = new SelectPartSizeViewModel(doc, partsList, PartType.StructJunction, defPartFam, defPartSize);

            //blockVM.SelectionChanged += FireBlockSelectionChanged;//передать информацию о том, что выбор изменен в ViewModel
            //structureVM.SelectionChanged += FirePartSizeSelectionChanged;
        }










        private void FireBlockSelectionChanged(object sender, EventArgs args)
        {
            if (BlockSelectionChanged!=null)
            {
                BlockSelectionChanged(this, new EventArgs());
            }
        }

        private void FirePartSizeSelectionChanged(object sender, EventArgs args)
        {
            if (PartSizeSelectionChanged != null)
            {
                PartSizeSelectionChanged(this, new EventArgs());
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
