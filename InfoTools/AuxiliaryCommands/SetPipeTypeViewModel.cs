using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3DInfoTools.Controls.SelectPartSizeUserControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.AuxiliaryCommands
{
    class SetPipeTypeViewModel : INotifyPropertyChanged
    {
        private SelectPartSizeViewModel pipeVM;
        public SelectPartSizeViewModel PipeVM
        {
            get { return pipeVM; }
            set
            {
                pipeVM = value;
                OnPropertyChanged(nameof(PipeVM));
            }
        }

        public bool OkBtnIsEnabled
        {
            get => PipeVM.SelectedPartSize != null;
        }

        public SetPipeTypeViewModel(Document doc, PartsList partsList)
        {
            pipeVM = new SelectPartSizeViewModel(doc, partsList, PartType.Pipe,
                ObjectId.Null, ObjectId.Null);

            pipeVM.SelectionChanged += (a, b) => { OnPropertyChanged(nameof(OkBtnIsEnabled)); };
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
