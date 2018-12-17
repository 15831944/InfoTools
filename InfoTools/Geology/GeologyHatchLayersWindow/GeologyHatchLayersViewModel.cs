using Autodesk.AutoCAD.ApplicationServices;
using Common;
using Common.Controls.FileNameInputControl;
using Common.Controls.NumericUpDownControl;
using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

namespace Civil3DInfoTools.Geology.GeologyHatchLayersWindow
{
    public class GeologyHatchLayersViewModel : INotifyPropertyChanged
    {
        private static string defaultExcelPath = null;
        private static string defaultLayoutName = null;
        private static int defaultColumnNumber = 1;
        private static int defaultRowNumber = 1;


        public bool LayerNamesListSpecified { get; private set; } = false;

        public List<string> LayerNames { get; private set; } = new List<string>();





        private FileNameInputViewModel excelPathVM = null;

        public FileNameInputViewModel ExcelPathVM
        {
            get { return excelPathVM; }
            set
            {
                excelPathVM = value;
                OnPropertyChanged("ExcelPathVM");
            }
        }

        public ObservableCollection<DataTable> Layouts { get; set; }
            = new ObservableCollection<DataTable>();

        public bool ExcelSelected { get; set; } = false;


        private object selectedLayout;
        public object SelectedLayout
        {
            get { return selectedLayout; }
            set
            {
                selectedLayout = value;
                OnPropertyChanged("SelectedLayout");
                SomethingDifferent(null, null);
            }
        }


        private NumericUpDownViewModel сolumnNumberVM = null;
        public NumericUpDownViewModel СolumnNumberVM
        {
            get { return сolumnNumberVM; }
            set
            {
                сolumnNumberVM = value;
                OnPropertyChanged("СolumnNumberVM");
            }
        }

        private NumericUpDownViewModel rowNumberVM = null;
        public NumericUpDownViewModel RowNumberVM
        {
            get { return rowNumberVM; }
            set
            {
                rowNumberVM = value;
                OnPropertyChanged("RowNumberVM");
            }
        }

        public ObservableCollection<LayerData> TableData { get; set; }
            = new ObservableCollection<LayerData>();


        public bool AcceptBtnIsEnabled
        {
            get { return LayerNames != null && LayerNames.Count > 0; }
        }


        private readonly RelayCommand acceptCommand = null;
        public RelayCommand AcceptCommand
        { get { return acceptCommand; } }

        public GeologyHatchLayersViewModel(Document doc)
        {
            acceptCommand = new RelayCommand(new Action<object>(Accept));

            string initialPath = defaultExcelPath == null ? Path.GetDirectoryName(doc.Name) : defaultExcelPath;
            excelPathVM = new FileNameInputViewModel("Excel Files|*.xls;*.xlsx;", "Укажите путь к файлу Excel");
            
            сolumnNumberVM = new NumericUpDownViewModel(defaultColumnNumber, 1, 1, formatting: "f0");
            
            rowNumberVM = new NumericUpDownViewModel(defaultRowNumber, 1, 1, formatting: "f0");
            

            excelPathVM.FileNameChanged += OnFileNameChanged;
            //excelPathVM.FileNameChanged += SomethingDifferent;
            сolumnNumberVM.ValueChanged += SomethingDifferent;
            rowNumberVM.ValueChanged += SomethingDifferent;


            excelPathVM.FileName = initialPath;

            if (defaultLayoutName != null)
            {
                DataTable item = Layouts.ToList().Find(dt => dt.TableName.Equals(defaultLayoutName));
                SelectedLayout = item;
            }


            
        }

        private void OnFileNameChanged(object sender, EventArgs e)
        {
            ExcelSelected = false;
            if (excelPathVM.FileNameIsValid)
            {
                string path = excelPathVM.FileName;

                if (File.Exists(path))
                {
                    FileStream fs = null;
                    try
                    {
                        fs = File.Open(path, FileMode.Open, FileAccess.Read);
                    }
                    catch (System.IO.IOException ex)
                    {
                        AccessException(ex);
                    }
                    if (fs != null)
                    {
                        IExcelDataReader reader = null;
                        switch (Path.GetExtension(path))
                        {
                            case ".xls":
                                reader = ExcelReaderFactory.CreateBinaryReader(fs);
                                break;
                            case ".xlsx":
                                reader = ExcelReaderFactory.CreateOpenXmlReader(fs);
                                break;
                        }
                        if (reader != null)
                        {
                            DataSet result = reader.AsDataSet();
                            reader.Close();

                            Layouts.Clear();
                            foreach (DataTable table in result.Tables)
                            {
                                Layouts.Add(table);
                            }
                            ExcelSelected = true;
                        }
                    }
                }

            }

            OnPropertyChanged("ExcelSelected");
        }


        private void SomethingDifferent(object sender, EventArgs e)
        {
            TableData.Clear();
            if (SelectedLayout != null)
            {
                //Заполнить Layers согласно заданным строке и столбцу
                int colNum = Convert.ToInt32(СolumnNumberVM.NumValue) - 1;
                int startRowNum = Convert.ToInt32(RowNumberVM.NumValue) - 1;

                DataTable table = (DataTable)SelectedLayout;


                DataColumnCollection columns = table.Columns;
                if (columns.Count> colNum)
                {
                    DataColumn column = table.Columns[colNum];

                    int currRowNum = 0;
                    foreach (DataRow row in table.Rows)
                    {
                        if (currRowNum >= startRowNum)
                        {
                            TableData.Add(new LayerData() { Name = row[colNum].ToString() });
                        }

                        currRowNum++;
                    }
                }
            }
            SetResultLayerNames();
            OnPropertyChanged("AcceptBtnIsEnabled");
        }

        private void SetResultLayerNames()
        {
            HashSet<string> uniqueValidLayerNames = new HashSet<string>(
                    TableData.Select(ld => Utils.GetSafeSymbolName(ld.Name))
                    .Where(s => !String.IsNullOrWhiteSpace(s)));
            LayerNames = uniqueValidLayerNames.ToList();
        }


        private void Accept(object obj)
        {
            LayerNamesListSpecified = true;

            //запомнить настройки!
            defaultExcelPath = excelPathVM.FileName;
            defaultLayoutName = (SelectedLayout as DataTable).TableName;
            defaultColumnNumber = Convert.ToInt32(СolumnNumberVM.NumValue);
            defaultRowNumber = Convert.ToInt32(RowNumberVM.NumValue);

            if (obj!=null && obj is Window)
            {
                (obj as Window).Close();
            }
        }

        //////////////////////////////////////////////////////////////////////////////
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }

    public class LayerData
    {
        public string Name { get; set; }
    }
}
