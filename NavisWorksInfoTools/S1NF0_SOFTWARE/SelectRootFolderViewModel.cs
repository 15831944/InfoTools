using Common.Controls.FileNameInputControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    public class SelectRootFolderViewModel : INotifyPropertyChanged
    {
        private FileNameInputViewModel classifierSamplePathVM = null;
        public FileNameInputViewModel ClassifierSamplePathVM
        {
            get { return classifierSamplePathVM; }
            set
            {
                classifierSamplePathVM = value;
                OnPropertyChanged(nameof(ClassifierSamplePathVM));
            }
        }

        public SelectRootFolderViewModel(string sampleInitialPath)
        {
            classifierSamplePathVM
                = new FileNameInputViewModel(".cl.xml files(*.cl.xml) | *.cl.xml",
                "Укажите файл образца классификатора", new System.Windows.Media.SolidColorBrush(Colors.Red), false)
                { FileName = sampleInitialPath };
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
