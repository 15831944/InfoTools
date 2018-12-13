using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE.PropCategoriesControl
{
    public class PropCategoriesViewModel : INotifyPropertyChanged
    {

        public ObservableCollection<Category> Categories { get; set; }
            = new ObservableCollection<Category>()
            {
                new Category(){ InternalName = "LcOaPropOverrideCat", Accept = true},
                new Category(){ InternalName = "LcRevitData_Parameter", Accept = false},
                new Category(){ InternalName = "LcRevitData_Type", Accept = false},
                new Category(){ InternalName = "LcRevitData_Element", Accept = false},
                new Category(){ InternalName = "LcRevitMaterialProperties", Accept = false},
                new Category(){ InternalName = "AecDbPropertySet", Accept = false},
            };


        //
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }


    public class Category
    {
        public string InternalName { get; set; }
        public bool Accept { get; set; }
    }

}
