using Autodesk.Navisworks.Api;
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
using System.Xml.Serialization;

namespace NavisWorksInfoTools
{
    //TODO: Удаление строк таблиц 
    /// <summary>
    /// Логика взаимодействия для SetPropsWindow.xaml
    /// </summary>
    public partial class SetPropsWindow : Window
    {
        //Значение по умолчанию для чекбоксов
        private static bool defaultStateOverwriteUserAttr = false;
        private static bool defaultStateOverwriteLinks = false;
        private static bool defaultPreserveExistingProperties = false;

        public bool PreserveExistingProperties
        {
            get
            {
                bool? checkBoxState = dontDeleteAnyPropertyCheckBox.IsChecked;
                return checkBoxState != null ? checkBoxState.Value : false;
            }
            set
            {
                dontDeleteAnyPropertyCheckBox.IsChecked = value;
            }
        }


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




        public string DataTabDisplayName { get; private set; }

        public List<DisplayDataTab> DataTabs { get; private set; }

        public List<DisplayURL> URLs { get; private set; }


        public SetPropsWindow(List<DisplayDataTab> dataTabs, List<DisplayURL> urls)
        {
            DataTabs = dataTabs;
            URLs = urls;
            InitializeComponent();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            overwriteUserAttrCheckBox.IsChecked = defaultStateOverwriteUserAttr;
            overwriteLinksCheckBox.IsChecked = defaultStateOverwriteLinks;
            dontDeleteAnyPropertyCheckBox.IsChecked = defaultPreserveExistingProperties;
            //вызвать обработчики событий для переключения чекбоксов
            overwriteUserAttrCheckBox_CheckedChanged(null, null);
            overwriteLinksCheckBox_CheckedChanged(null, null);

            //Назначение источников данных
            tabsDataGrid.ItemsSource = DataTabs;
            linksDataGrid.ItemsSource = URLs;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            defaultStateOverwriteUserAttr = overwriteUserAttrCheckBox.IsChecked.Value;
            defaultStateOverwriteLinks = overwriteLinksCheckBox.IsChecked.Value;
            defaultPreserveExistingProperties = dontDeleteAnyPropertyCheckBox.IsChecked.Value;
            this.DialogResult = true;
            this.Close();
        }



        private void overwriteUserAttrCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (overwriteUserAttrCheckBox.IsChecked != null)
            {
                tabsDataGrid.IsEnabled = overwriteUserAttrCheckBox.IsChecked.Value;
                //propsDataGrid.IsEnabled = overwriteUserAttrCheckBox.IsChecked.Value;
            }

        }


        private void overwriteLinksCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (overwriteLinksCheckBox.IsChecked != null)
                linksDataGrid.IsEnabled = overwriteLinksCheckBox.IsChecked.Value;
        }

        /// <summary>
        /// Назначает источник данных для таблицы свойств
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisplayDataTab selectedDataTab = tabsDataGrid.SelectedItem as DisplayDataTab;
            if (selectedDataTab != null)
            {
                propsDataGrid.ItemsSource = selectedDataTab.DisplayProperties;
                propsDataGrid.IsEnabled = true;
            }
            else
            {
                propsDataGrid.ItemsSource = new List<DisplayProperty>();
                propsDataGrid.IsEnabled = false;
            }
        }

        /// <summary>
        /// ОТМЕНЕНО
        /// Если в таблице изменяется DisplayName то проверить его уникальность
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            DataGrid dataGrid = sender as DataGrid;
            if (dataGrid!=null)
            {
                DataGridBoundColumn boundColumn = e.Column as DataGridBoundColumn;
                Binding binding = boundColumn.Binding as Binding;
                if (binding!=null && binding.Path.Path.Equals("DisplayName"))
                {
                    //...
                }
            }

        }


    }

    /// <summary>
    /// Класс для хранения данных о вводе пользователем новых пользовательских панелей
    /// </summary>
    public class DisplayDataTab
    {
        [XmlAttribute]
        public string DisplayName { get; set; }

        [XmlArray("DisplayProperties"), XmlArrayItem("DisplayProperty")]

        public List<DisplayProperty> DisplayProperties = new List<DisplayProperty>();

        //[XmlIgnore]
        //public Autodesk.Navisworks.Api.Interop.ComApi.InwOaPropertyVec InwOaPropertyVec { get; set; }
        //    = null;

    }

    /// <summary>
    /// Класс для хранения данных о вводе пользователем новых пользовательских свойств
    /// </summary>
    public class DisplayProperty
    {
        [XmlAttribute]
        public string DisplayName { get; set; }
        [XmlAttribute]
        public string DisplayValue { get; set; }
        [XmlIgnore]
        public object Value { get; private set; } = null;

        public void ConvertValue()
        {
            Value = Utils.ConvertValueByString(DisplayValue);
        }

    }

    /// <summary>
    /// Класс для хранения данных о вводе пользователем гиперссылок
    /// </summary>
    public class DisplayURL
    {
        [XmlAttribute]
        public string DisplayName { get; set; }

        [XmlAttribute]
        public string URL { get; set; }
    }
}
