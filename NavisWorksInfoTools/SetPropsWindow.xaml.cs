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
    /// Логика взаимодействия для SetPropsWindow.xaml
    /// </summary>
    public partial class SetPropsWindow : Window
    {

        public string TabName {
            get
            {
                return tabNameTextBox.Text;
            }
            set
            {
                tabNameTextBox.Text = value;
            }
        }
        public List<DisplayProperty> Props { get; set; }


        public SetPropsWindow(List<DisplayProperty> props)
        {
            Props = props;
            
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            propsDataGrid.ItemsSource = Props;

        }
    }
}
