using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.IO;
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
using Excel = Microsoft.Office.Interop.Excel;

namespace NavisWorksInfoTools
{
    /// <summary>
    /// При использовании данного окна необходимо предусамтривать закрытие Excel в блоке finaly
    /// </summary>
    public partial class SetPropsByExcelWindow : Window
    {
        private static string defaultPath = null;

        private static string defaultSheetName = null;

        private static int defaultRowNum = -1;

        private static string defaultColumnName = null;

        private static string defaultCategoryName = null;

        private static string defaultPropertyName = null;

        private static string defaultTabName = null;

        private string initialPath = null;

        private ModelItem sampleItem = null;

        public Excel.Application OXL { get; set; } = null;

        public Excel._Workbook OWB { get; set; } = null;

        private List<Excel._Worksheet> excelWorkSheets = null;

        private List<PropertyCategory> propertyCategories = null;

        private List<DataProperty> dataProperties = null;


        public string ExcelPath
        {
            get
            {
                return fileNameInput.FileName;
            }
            set
            {
                fileNameInput.FileName = value;
            }
        }

        public Excel._Worksheet SelectedWorkSheet
        {
            get
            {
                return (Excel._Worksheet)excelSheetComboBox.SelectedItem;
            }
        }

        public SortedDictionary<int, Common.ExcelInterop.CellValue> TabelHeader { get; set; } = null;

        public Common.ExcelInterop.CellValue SelectedColumn
        {
            get
            {
                return (Common.ExcelInterop.CellValue)excelColComboBox.SelectedItem;
            }
        }

        public PropertyCategory SelectedPropertyCategory
        {
            get
            {
                return (PropertyCategory)navisDataTabComboBox.SelectedItem;
            }
        }

        public DataProperty SelectedDataProperty
        {
            get
            {
                return (DataProperty)navisPropertyComboBox.SelectedItem;
            }
        }

        public string TabName
        {
            get
            {
                return tabNameTextBox.Text;
            }
            set
            {
                tabNameTextBox.Text = value;
            }
        }


        public SetPropsByExcelWindow(ModelItem sampleItem, string initialPath = null)
        {
            if (sampleItem == null)
            {
                throw new ArgumentException(nameof(sampleItem));
            }

            InitializeComponent();
            this.sampleItem = sampleItem;
            this.initialPath = initialPath;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(defaultPath))
            {
                ExcelPath = defaultPath;
            }
            else if (!String.IsNullOrEmpty(initialPath))
            {
                ExcelPath = initialPath;
            }
            //Заполнить варианты выбора navisDataTabComboBox
            propertyCategories = sampleItem.PropertyCategories.ToList();
            navisDataTabComboBox.ItemsSource = propertyCategories;
        }

        /// <summary>
        /// Определить есть ли такой файл Excel и попытаться открыть его
        /// Если все успешно, то разблокировать элементы настройки выбора данных из Excel
        /// Заполнить варианты выбора для excelLayoutComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileNameChanged(object sender, EventArgs e)
        {
            CloseUsingExcel();

            excelWorkSheets = new List<Excel._Worksheet>();
            if (!String.IsNullOrEmpty(ExcelPath) && File.Exists(ExcelPath))
            {
                //Start Excel and get Application object.
                OXL = new Excel.Application();
                try
                { OWB = OXL.Workbooks.Open(ExcelPath, 0, true); }
                catch { }

                if (OWB != null)
                {
                    Excel.Sheets sheets = OWB.Worksheets;
                    foreach (Excel._Worksheet sheet in sheets)
                    {
                        excelWorkSheets.Add(sheet);
                    }

                }
            }

            excelSheetComboBox.ItemsSource = excelWorkSheets;
            if (excelWorkSheets.Count > 0)
            {
                excelSheetComboBox.IsEnabled = true;
                excelRowNumericUpDown.IsEnabled = true;
                excelColComboBox.IsEnabled = true;
            }
            else
            {
                excelSheetComboBox.IsEnabled = false;
                excelRowNumericUpDown.IsEnabled = false;
                excelColComboBox.IsEnabled = false;
            }

        }


        public void CloseUsingExcel()
        {
            //Закрытие предыдущей используемой книги Excel
            if (OWB != null)
            {
                try { OWB.Close(false); } catch { }
                OWB = null;
            }
            //Закрытие Excel
            if (OXL != null)
            {
                try { OXL.Quit(); } catch { }
                OXL = null;
            }
        }

        private void excelSheetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EnableOKButton();
            SetExcelColItemSource();
        }


        private void ExcelRowNumChanged(object sender, EventArgs e)
        {
            SetExcelColItemSource();
        }

        /// <summary>
        /// Заполнить варианты выбора для excelColComboBox
        /// </summary>
        private void SetExcelColItemSource()
        {
            if (excelColComboBox == null)
                return;

            TabelHeader = new SortedDictionary<int, Common.ExcelInterop.CellValue>();
            if (excelSheetComboBox.SelectedItem != null)
            {
                Excel._Worksheet sheet = (Excel._Worksheet)excelSheetComboBox.SelectedItem;
                TabelHeader = Common.ExcelInterop.Utils.GetRowValues(sheet, excelRowNumericUpDown.NumValue);

            }
            excelColComboBox.ItemsSource = TabelHeader.Values;
        }



        private void excelColComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EnableOKButton();
        }



        /// <summary>
        /// Заполнить варианты выбора navisPropertyComboBox для выбранной категории
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void navisDataTabComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EnableOKButton();
            PropertyCategory pc = navisDataTabComboBox.SelectedItem as PropertyCategory;
            if (pc != null)
            {
                dataProperties = pc.Properties.ToList();
            }
            else
            {
                dataProperties = new List<DataProperty>();
            }

            navisPropertyComboBox.ItemsSource = dataProperties;
        }

        private void navisPropertyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EnableOKButton();
        }

        /// <summary>
        /// Включить кнопку ОК если все заполнено
        /// </summary>
        private void EnableOKButton()
        {
            if (excelSheetComboBox.SelectedItem != null
                && excelColComboBox.SelectedItem != null
                && navisDataTabComboBox.SelectedItem != null
                && navisPropertyComboBox.SelectedItem != null
                && TabelHeader!= null && TabelHeader.Count>0)
            {
                okButton.IsEnabled = true;
            }
            else
            {
                okButton.IsEnabled = false;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            defaultPath = ExcelPath;
            this.DialogResult = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //CloseUsingExcel();
        }
    }
}
