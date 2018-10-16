using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Civil3DInfoTools.Controls.SelectLayerUserControl2
{
    /// <summary>
    /// Для того, чтобы сделать Binding для отображения цвета слоя
    /// нужно использовать специальный класс модели со свойством типа SolidColorBrush
    /// Поддержка интефейса INotifyPropertyChanged
    /// </summary>
    public class Model : INotifyPropertyChanged
    {
        private LayerTableRecord ltr = null;
        public string Name
        {
            get
            {
                return ltr?.Name;
            }
        }
        public SolidColorBrush Color
        {
            get
            {
                if (ltr!=null)
                {
                    byte r = ltr.Color.ColorValue.R;
                    byte g = ltr.Color.ColorValue.G;
                    byte b = ltr.Color.ColorValue.B;
                    return new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
                }
                else
                {
                    return null;
                }
            }
        }

        public LayerTableRecord LayerTableRecord
        {
            get { return ltr; }
            set
            {
                ltr = value;
                OnPropertyChanged("LayerTableRecord");
                OnPropertyChanged("Color");
                OnPropertyChanged("Name");
            }
        }

        public Model(LayerTableRecord ltr)
        {
            this.ltr = ltr;
        }


        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
