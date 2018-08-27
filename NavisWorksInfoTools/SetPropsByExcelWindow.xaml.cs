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

        public bool IgnoreNonVisible
        {
            get
            {
                return ignoreNonVisibleCheckBox.IsChecked.Value;
            }
            set
            {
                ignoreNonVisibleCheckBox.IsChecked = value;
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
            //Заполнить варианты выбора navisDataTabComboBox
            propertyCategories = sampleItem.PropertyCategories.ToList();
            navisDataTabComboBox.ItemsSource = propertyCategories;

            //Заполнить значения по умолчанию
            try
            {
                //Для пути к Excel
                if (!String.IsNullOrEmpty(defaultPath))
                {
                    ExcelPath = defaultPath;
                }
                else if (!String.IsNullOrEmpty(initialPath))
                {
                    ExcelPath = initialPath;
                }
                //Для номера строки
                if (defaultRowNum > 0)
                {
                    excelRowNumericUpDown.NumValue = defaultRowNum;
                }
                //Для категории Navis
                if (!String.IsNullOrEmpty(defaultCategoryName) && propertyCategories != null && propertyCategories.Count > 0)
                {
                    PropertyCategory propertyCategory = propertyCategories.Find(c => c.DisplayName.Equals(defaultCategoryName));
                    navisDataTabComboBox.SelectedItem = propertyCategory;
                }
                //Для свойства Navis
                if (!String.IsNullOrEmpty(defaultPropertyName) && dataProperties != null && dataProperties.Count > 0)
                {
                    DataProperty dataProperty = dataProperties.Find(p => p.DisplayName.Equals(defaultPropertyName));
                    navisPropertyComboBox.SelectedItem = dataProperty;
                }
                //Для названия вкладки
                if (!String.IsNullOrEmpty(defaultTabName))
                {
                    TabName = defaultTabName;
                }
            }
            catch { }

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
                Excel.Workbooks workbooks = null;
                Excel.Sheets sheets = null;
                try
                {
                    workbooks = OXL.Workbooks;
                    OWB = workbooks.Open(ExcelPath, 0, true);

                    if (OWB != null)
                    {
                        sheets = OWB.Worksheets;
                        foreach (Excel._Worksheet sheet in sheets)
                        {
                            excelWorkSheets.Add(sheet);
                        }

                    }
                }
                catch { }
                finally
                {
                    if (workbooks != null)
                    {
                        //System.Runtime.InteropServices.Marshal.ReleaseComObject(workbooks);
                    }
                        
                    if (sheets != null)
                    {
                        //System.Runtime.InteropServices.Marshal.ReleaseComObject(sheets);
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


            //Заполнить значения по умолчанию
            try
            {
                //Для выбранного листа
                if (!String.IsNullOrEmpty(defaultSheetName) && excelWorkSheets != null && excelWorkSheets.Count > 0)
                {
                    Excel._Worksheet sheet = excelWorkSheets.Find(s => s.Name.Equals(defaultSheetName));
                    excelSheetComboBox.SelectedItem = sheet;
                }
            }
            catch { }

        }


        public void CloseUsingExcel()
        {
            //TODO: Процесс все равно остается в диспетчере задач
            //Возможно нужно освобождать вообще все объекты, которые использовались - https://stackoverflow.com/a/28080347. ЭТО НЕ ПОМОГАЕТ
            //При этом процесс исчезает при нормальном закрытии Navis

            if (excelWorkSheets != null)
            {
                foreach (Excel._Worksheet ws in excelWorkSheets)
                {
                    //System.Runtime.InteropServices.Marshal.ReleaseComObject(ws);
                }
            }
            


            //Закрытие предыдущей используемой книги Excel
            if (OWB != null)
            {
                try { OWB.Close(false); } catch { }
                //System.Runtime.InteropServices.Marshal.ReleaseComObject(OWB);
                OWB = null;
            }
            //Закрытие Excel
            if (OXL != null)
            {
                try { OXL.Quit(); } catch { }
                //System.Runtime.InteropServices.Marshal.ReleaseComObject(OXL);
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

            //Заполнить значения по умолчанию
            try
            {
                //Для столбца книги
                if (!String.IsNullOrEmpty(defaultColumnName) && TabelHeader != null && TabelHeader.Count > 0)
                {
                    Common.ExcelInterop.CellValue cellValue = TabelHeader.Values.First(c => c.DisplayString.Equals(defaultColumnName));
                    excelColComboBox.SelectedItem = cellValue;
                }
            }
            catch { }
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

        private void tabNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableOKButton();
        }

        /// <summary>
        /// Включить кнопку ОК если все заполнено
        /// </summary>
        private void EnableOKButton()
        {
            if (okButton != null)
            {
                if (excelSheetComboBox.SelectedItem != null
                && excelColComboBox.SelectedItem != null
                && navisDataTabComboBox.SelectedItem != null
                && navisPropertyComboBox.SelectedItem != null
                && TabelHeader != null && TabelHeader.Count > 0
                && !String.IsNullOrWhiteSpace(tabNameTextBox.Text) && !tabNameTextBox.Text.Equals(SetIds.IdDataTabDisplayName))
                {
                    okButton.IsEnabled = true;
                }
                else
                {
                    okButton.IsEnabled = false;
                }
            }
            
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //Задание значений по умолчанию
            defaultPath = ExcelPath;
            defaultRowNum = excelRowNumericUpDown.NumValue;
            defaultCategoryName = SelectedPropertyCategory.DisplayName;
            defaultPropertyName = SelectedDataProperty.DisplayName;
            defaultTabName = TabName;

            defaultSheetName = SelectedWorkSheet.Name;
            defaultColumnName = SelectedColumn.DisplayString;

            this.DialogResult = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //CloseUsingExcel();
        }

        
    }
}
