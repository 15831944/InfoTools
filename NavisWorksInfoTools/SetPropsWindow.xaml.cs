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
        //Значение по умолчанию для чекбоксов
        private static bool defaultStateOverwriteUserAttr = false;
        private static bool defaultStateOverwriteLinks = false;

        public bool OverwriteUserAttr
        {
            get
            {
                bool? checkBoxState = overwriteUserAttrCheckBox.IsChecked;
                return checkBoxState != null ? checkBoxState.Value : false;
            }
            set
            {
                overwriteUserAttrCheckBox.IsChecked = value;
            }
        }

        public bool OverwriteLinks
        {
            get
            {
                bool? checkBoxState = overwriteLinksCheckBox.IsChecked;
                return checkBoxState != null ? checkBoxState.Value : false;
            }
            set
            {
                overwriteLinksCheckBox.IsChecked = value;
            }
        }


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
        public List<DisplayProperty> Props { get; private set; }

        public List<DisplayURL> URLs { get; private set; }


        public SetPropsWindow(List<DisplayProperty> props, List<DisplayURL> urls)
        {
            Props = props;
            URLs = urls;
            InitializeComponent();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            overwriteUserAttrCheckBox.IsChecked = defaultStateOverwriteUserAttr;
            overwriteLinksCheckBox.IsChecked = defaultStateOverwriteLinks;
            //вызвать обработчики событий для переключения чекбоксов
            overwriteUserAttrCheckBox_CheckedChanged(null, null);
            overwriteLinksCheckBox_CheckedChanged(null, null);
            propsDataGrid.ItemsSource = Props;
            linksDataGrid.ItemsSource = URLs;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            defaultStateOverwriteUserAttr = overwriteUserAttrCheckBox.IsChecked.Value;
            defaultStateOverwriteLinks = overwriteLinksCheckBox.IsChecked.Value;
            this.DialogResult = true;
            this.Close();
        }

        

        private void overwriteUserAttrCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            propsDataGrid.IsEnabled = overwriteUserAttrCheckBox.IsChecked.Value;
            tabNameTextBox.IsEnabled = overwriteUserAttrCheckBox.IsChecked.Value;
        }


        private void overwriteLinksCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            linksDataGrid.IsEnabled = overwriteLinksCheckBox.IsChecked.Value;
        }


    }
}
