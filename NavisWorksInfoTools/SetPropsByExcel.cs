using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Win = System.Windows;
using Excel = Microsoft.Office.Interop.Excel;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using System.IO;
using static NavisWorksInfoTools.Constants;

namespace NavisWorksInfoTools
{
    [Plugin("SetPropsByExcel",
        DEVELOPER_ID,
        ToolTip = "Заполнить атрибуты по таблице Excel",
        DisplayName = "Заполнить атрибуты по таблице Excel")]
    class SetPropsByExcel : AddInPlugin
    {
        //Обнаружена очень большая проблема - https://forums.autodesk.com/t5/navisworks-api/disable-screen-updating/m-p/8211317/highlight/false#M4165
        //Открытые панели могут тормозить работу программы


        private int matchCount = 0;
        public override int Execute(params string[] parameters)
        {
            SetPropsByExcelWindow setPropsByExcelWindow = null;
            try
            {
                Document doc = Application.ActiveDocument;

                ModelItemCollection selection = doc.CurrentSelection.SelectedItems;

                if (selection.Count != 1)
                {
                    Win.MessageBox.Show("Перед вызовом этой команды нужно выбрать 1 из элементов модели, "
                        +"к которым должны быть привязаны данные из Excel",
                        "Подсказка");
                    return 0;
                }

                ModelItem sampleItem = selection.First;


                string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string docFileName = doc.FileName;
                if (!String.IsNullOrEmpty(docFileName))
                {
                    initialPath = Path.GetDirectoryName(docFileName);
                }
                setPropsByExcelWindow = new SetPropsByExcelWindow(sampleItem, initialPath);
                bool? result = null;
                result = setPropsByExcelWindow.ShowDialog();



                if (result != null && result.Value)
                {
                    Common.Timer timer = new Common.Timer();
                    timer.Start();

                    PropertyCategory propertyCategory = setPropsByExcelWindow.SelectedPropertyCategory;
                    /*NamedConstant*/
                    string keyCatName = propertyCategory.DisplayName;//.CombinedName;//.DisplayName;
                    DataProperty dataProperty = setPropsByExcelWindow.SelectedDataProperty;
                    /*NamedConstant*/ string keyPropName = dataProperty.DisplayName;//.CombinedName;//

                    Excel._Worksheet worksheet = setPropsByExcelWindow.SelectedWorkSheet;
                    Common.ExcelInterop.CellValue excelColumn = setPropsByExcelWindow.SelectedColumn;


                    //Перенести все значения из столбца в словарь - не ускоряет работу
                    //Dictionary<string, int> keyValues
                    //    = Common.ExcelInterop.Utils.GetColumnValues(worksheet, excelColumn.ColumnNum);

                    Excel.Range columns = worksheet.Columns;
                    Excel.Range keyColumn = columns[excelColumn.ColumnNum];
                    string tabName = setPropsByExcelWindow.TabName;
                    bool ignoreNonVisible = setPropsByExcelWindow.IgnoreNonVisible;

                    SortedDictionary<int, Common.ExcelInterop.CellValue> tableHeader = setPropsByExcelWindow.TabelHeader;
                    Dictionary<string, int> columnHeaderLookup = new Dictionary<string, int>();
                    foreach (KeyValuePair<int, Common.ExcelInterop.CellValue> kvp in tableHeader)
                    {
                        columnHeaderLookup.Add(kvp.Value.DisplayString, kvp.Key);
                    }

                    //get state object of COM API
                    ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;

                    

                    //Поиск всех объектов, у которых есть указанное свойство
                    //http://adndevblog.typepad.com/aec/2012/05/navisworks-net-api-find-item.html

                    Search search = new Search();
                    search.Selection.SelectAll();
                    search.PruneBelowMatch = true;//В наборе не будет вложенных элементов
                    search.SearchConditions
                        .Add(SearchCondition.HasPropertyByDisplayName/*.HasPropertyByCombinedName*/(keyCatName, keyPropName));
                    ModelItemCollection items = search.FindAll(doc, false);




                    matchCount = 0;
                    SearchForExcelTableMatches(doc, items, ignoreNonVisible,
                        keyColumn,
                        //keyValues,
                        keyCatName, keyPropName,
                        oState, worksheet, tableHeader, tabName
                        );


                    //System.Runtime.InteropServices.Marshal.ReleaseComObject(columns);
                    //System.Runtime.InteropServices.Marshal.ReleaseComObject(keyColumn);


                    Win.MessageBox.Show(timer.TimeOutput("Общее время")
                        + "\nНайдено совпадений - "+ matchCount,
                        "Готово", Win.MessageBoxButton.OK,
                        Win.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при заполнении атрибутов в Navis из таблицы Excel");
            }
            finally
            {
                //Закрыть Excel
                if (setPropsByExcelWindow != null)
                    setPropsByExcelWindow.CloseUsingExcel();
            }

            return 0;
        }


        private void SearchForExcelTableMatches
            (Document doc, IEnumerable<ModelItem> items, bool ignoreNonVisible,
            Excel.Range keyColumn,
            //Dictionary<string, int> keyValues,
            /*NamedConstant*/ string keyCatName, /*NamedConstant*/ string keyPropName,
            ComApi.InwOpState3 oState, Excel._Worksheet worksheet,
            SortedDictionary<int, Common.ExcelInterop.CellValue> tableHeader, string tabName
            )
        {
            foreach (ModelItem item in items)
            {
                
                if (ignoreNonVisible && item.IsHidden)
                {
                    continue;//Пропустить скрытый элемент
                }

                DataProperty property
                    = item.PropertyCategories.FindPropertyByDisplayName/*.FindPropertyByCombinedName*/(keyCatName, keyPropName);
                //object searchValue = Utils.GetUserPropValue(property.Value);
                string searchValue = Utils.GetDisplayValue(property.Value);
                //Найти в выбранном столбце Excel ячейку с таким же значением


                Excel.Range row = keyColumn.Find(searchValue,
                    LookIn: Excel.XlFindLookIn.xlValues, LookAt: Excel.XlLookAt.xlWhole,
                    SearchOrder: Excel.XlSearchOrder.xlByColumns,
                    MatchByte: false);

                //int rowNum = 0;
                //keyValues.TryGetValue(searchValue, out rowNum);

                if (/*rowNum != 0*/row != null)
                {
                    matchCount++;

                    int rowNum = row.Row;
                    //Получить данные из этой строки таблицы
                    SortedDictionary<int, Common.ExcelInterop.CellValue> rowValues
                        = Common.ExcelInterop.Utils.GetRowValues(worksheet, rowNum);
                    //Привязать пользовательские атрибуты как в строке таблицы. Если такие атрибуты уже были созданы, то переписать их значение
                    //Набор свойств для задания для этого элемента модели
                    ComApi.InwOaPropertyVec propsToSet
                        = oState.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec);
                    //Заполнить данными из строки Excel
                    foreach (KeyValuePair<int, Common.ExcelInterop.CellValue> kvp in tableHeader)
                    {
                        int colIndex = kvp.Key;
                        string propName = kvp.Value.DisplayString;
                        Common.ExcelInterop.CellValue cellValue = null;
                        rowValues.TryGetValue(colIndex, out cellValue);
                        object propValue = null;
                        if (cellValue != null)
                        {
                            propValue = Utils.ConvertValueByString(cellValue.DisplayString);//.Value2;
                        }

                        // create new property
                        ComApi.InwOaProperty newP = Utils.CreateNewUserProp(oState, propName, propValue);

                        // add the new property to the new property category
                        propsToSet.Properties().Add(newP);
                    }


                    foreach (ModelItem dItem in item.DescendantsAndSelf)
                    {
                        //convert the .NET collection to COM object
                        ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(dItem);
                        //Получить текущие свойства элемента
                        ComApi.InwGUIPropertyNode2 propertyNode
                            = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);
                        //Проверить есть ли у элемента панель данных пользователя с точно таким же названием
                        //Получить ее индекс
                        int indexToSet = 0;
                        int i = 1;
                        foreach (ComApi.InwGUIAttribute2 attr in propertyNode.GUIAttributes())
                        {
                            if (attr.UserDefined)
                            {
                                if (attr.ClassUserName.Equals(tabName))
                                {
                                    indexToSet = i;
                                    break;
                                }
                                else
                                {
                                    i++;
                                }
                            }
                        }

                        //Перезаписать панель данными из Excel
                        propertyNode.SetUserDefined(
                            indexToSet,
                            tabName, "S1NF0", propsToSet);
                    }
                    //System.Runtime.InteropServices.Marshal.ReleaseComObject(row);

                }
                else
                {
                    //Если неправильно указать ключевое свойство и столбец, возникает зависание из-за многократного поиска Search во вложенных элементах

                    //Search search = new Search();
                    //search.Selection.CopyFrom(item.Descendants);
                    //search.PruneBelowMatch = true;//объекты без вложенных
                    //search.SearchConditions
                    //    .Add(SearchCondition.HasPropertyByCombinedName(keyCatCombName, keyPropCombName));
                    //ModelItemCollection dItems = search.FindAll(doc, false);

                    //IEnumerable<ModelItem> dItems
                    //    = item.Descendants.Where(SearchCondition.HasPropertyByCombinedName(keyCatCombName, keyPropCombName));//Не обрезает вложенные объекты


                    //TODO: Это чревато огромными задержками на большой модели если неправильно задано ключевое поле!!!
                    //Вместо Search API Линейный поиск по дереву до нахождения соответствия
                    List<ModelItem> dItems = new List<ModelItem>();
                    SearchHasPropertyByCombinedName(item.Children, keyCatName, keyPropName, dItems);


                    SearchForExcelTableMatches(doc, dItems, ignoreNonVisible,
                        keyColumn,
                        //keyValues,
                        keyCatName, keyPropName,
                        oState, worksheet, tableHeader, tabName
                        );
                }
            }
        }



        private void SearchHasPropertyByCombinedName(IEnumerable<ModelItem> searchColl,
            /*NamedConstant*/ string keyCatName, /*NamedConstant*/ string keyPropName, List<ModelItem> resultColl)
        {
            foreach(ModelItem item in searchColl)
            {
                DataProperty property
                    = item.PropertyCategories.FindPropertyByDisplayName/*.FindPropertyByCombinedName*/(keyCatName, keyPropName);
                if (property!=null)
                {
                    resultColl.Add(item);
                }
                else
                {
                    SearchHasPropertyByCombinedName(item.Children, keyCatName, keyPropName, resultColl);
                }
            }
        }


    }
}

