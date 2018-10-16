using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Civil3DInfoTools.Controls.SelectBlockUserControl3
{
    /// <summary>
    /// Логика взаимодействия для View.xaml
    /// </summary>
    public partial class View : UserControl
    {
        /// <summary>
        /// Через XAML нельзя использовать конструктор с параметрами
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="mainWindow"></param>
        /// <param name="blocks"></param>
        /// <param name="defaulBlockIfExists"></param>
        public View(Document doc, Window mainWindow,
            ObservableCollection<BlockTableRecord> blocks = null, string defaulBlockIfExists = null)
        {
            InitializeComponent();

            DataContext = new ViewModel(doc, mainWindow, blocks, defaulBlockIfExists);
        }

        /// <summary>
        /// Конструктор без параметров.
        /// 
        /// </summary>
        public View()
        {
            InitializeComponent();

        }
    }
}
