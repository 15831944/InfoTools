using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace NavisWorksInfoTools
{
    /// <summary>
    /// Логика взаимодействия для DataTabToDeleteWindow.xaml
    /// </summary>
    public partial class DataTabToDeleteWindow : Window
    {
        public string DataTabName
        {
            get
            {
                return dataTabNameTxt.Text;
            }
            set
            {
                dataTabNameTxt.Text = value;
            }
        }


        public DataTabToDeleteWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
