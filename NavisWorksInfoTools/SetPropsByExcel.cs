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

namespace NavisWorksInfoTools
{
    [Plugin("SetPropsByExcel",
        "S-Info",
        ToolTip = "Заполнить атрибуты по таблице Excel",
        DisplayName = "Заполнить атрибуты по таблице Excel")]
    class SetPropsByExcel : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {

            SetPropsByExcelWindow setPropsByExcelWindow = null;
            try
            {
                Document doc = Application.ActiveDocument;

                

                ModelItemCollection selection = doc.CurrentSelection.SelectedItems;

                if (selection.Count != 1)
                {
                    Win.MessageBox.Show("Перед вызовом этой команды нужно выбрать 1 из элементов модели, к которым должны быть привязаны данные из Excel",
                        "Отмена");
                    return 0;
                }

                ModelItem sampleItem = selection.First;

                string initialPath = Path.GetDirectoryName(doc.FileName);
                setPropsByExcelWindow = new SetPropsByExcelWindow(sampleItem, initialPath);
                bool? result = null;
                result = setPropsByExcelWindow.ShowDialog();



                if (result != null && result.Value)
                {
                    PropertyCategory propertyCategory = setPropsByExcelWindow.SelectedPropertyCategory;
                    string catDispName = propertyCategory.DisplayName;
                    DataProperty dataProperty = setPropsByExcelWindow.SelectedDataProperty;
                    string propDispName = dataProperty.DisplayName;

                    Excel._Worksheet worksheet = setPropsByExcelWindow.SelectedWorkSheet;
                    Common.ExcelInterop.CellValue excelColumn = setPropsByExcelWindow.SelectedColumn;
                    Excel.Range keyColumn = worksheet.Columns[excelColumn.ColumnNum];

                    SortedDictionary<int, Common.ExcelInterop.CellValue> tableHeader = setPropsByExcelWindow.TabelHeader;
                    Dictionary<string, int> columnHeaderLookup = new Dictionary<string, int>();
                    foreach (KeyValuePair<int, Common.ExcelInterop.CellValue> kvp in tableHeader)
                    {
                        columnHeaderLookup.Add(kvp.Value.DisplayString, kvp.Key);
                    }

                    //dynamic x = keyColumn.Value2;

                    //Поиск всех объектов, у которых есть указанное свойство
                    //http://adndevblog.typepad.com/aec/2012/05/navisworks-net-api-find-item.html
                    Search search = new Search();
                    search.Selection.SelectAll();
                    search.SearchConditions
                        .Add(SearchCondition.HasPropertyByDisplayName(catDispName, propDispName));
                    ModelItemCollection items = search.FindAll(doc, false);

                    //get state object of COM API
                    ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;
                    foreach (ModelItem item in items)
                    {
                        DataProperty property = item.PropertyCategories.FindPropertyByDisplayName(catDispName, propDispName);
                        string searchStringValue = property.Value.ToDisplayString();

                        //Найти в выбранном столбце Excel ячейку с таким же значением (если его перевести в строку)
                        Excel.Range row = keyColumn.Find(searchStringValue,
                            LookIn: Excel.XlFindLookIn.xlValues, LookAt: Excel.XlLookAt.xlWhole, SearchOrder: Excel.XlSearchOrder.xlByColumns,
                            MatchByte: false);
                        if (row!=null)
                        {
                            int rowNum = row.Row;
                            //Получить данные из этой строки таблицы
                            SortedDictionary<int, Common.ExcelInterop.CellValue> rowValues
                                = Common.ExcelInterop.Utils.GetRowValues(worksheet, rowNum);

                            foreach (ModelItem dItem in item.DescendantsAndSelf)
                            {
                                //Привязать пользовательские атрибуты как в строке таблицы. Если такие атрибуты уже были созданы, то переписать их значение
                                //convert the .NET collection to COM object
                                ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(dItem);

                                ComApi.InwOaPropertyVec propsToSet
                                    = oState.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec);//Набор свойств для задания для этого элемента модели
                                                                                                             //Получить текущие свойства элемента
                                ComApi.InwGUIPropertyNode2 propertyNode = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);

                                //Перенести без изменения те свойства, которые не содержатся в таблице Excel
                                
                                foreach (ComApi.InwGUIAttribute2 attr in propertyNode.GUIAttributes())
                                {
                                    //string x = attr.ClassName;

                                    if (attr.ClassName.Equals("LcOaPropOverrideCat"))
                                    {
                                        foreach (ComApi.InwOaProperty prop in attr.Properties())
                                        {
                                            //string y = prop.name;
                                            if (!columnHeaderLookup.ContainsKey(prop.UserName))//Если это свойство не содержится в Excel, 
                                            {
                                                //то добавить его без изменений в InwOaPropertyVec
                                                //Но при этом нужно создать новый объект
                                                ComApi.InwOaProperty newProp = oState.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaProperty);
                                                newProp.name = prop.name;
                                                newProp.UserName = prop.UserName;
                                                newProp.value = prop.value;
                                                propsToSet.Properties().Add(newProp);
                                            }
                                        }
                                    }
                                }

                                foreach (KeyValuePair<int, Common.ExcelInterop.CellValue> kvp in tableHeader)
                                {
                                    int colIndex = kvp.Key;
                                    string propName = kvp.Value.DisplayString;
                                    Common.ExcelInterop.CellValue cellValue = null;
                                    rowValues.TryGetValue(colIndex, out cellValue);
                                    string propValue = null;
                                    if (cellValue != null)
                                    {
                                        propValue = cellValue.DisplayString;
                                    }

                                    // create new property
                                    ComApi.InwOaProperty newP = (ComApi.InwOaProperty)oState
                                        .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaProperty, null, null);

                                    // set the name, username and value of the new property
                                    newP.name = Guid.NewGuid().ToString();
                                    newP.UserName = propName;
                                    if (String.IsNullOrEmpty(propValue))
                                        propValue = "_";
                                    newP.value = propValue;

                                    // add the new property to the new property category
                                    propsToSet.Properties().Add(newP);
                                }

                                try
                                { propertyNode.RemoveUserDefined(0); }
                                catch (System.Runtime.InteropServices.COMException) { }
                                string tabName = setPropsByExcelWindow.TabName;
                                propertyNode.SetUserDefined(0, tabName, "S1NF0", propsToSet);
                            }
                        }
                        
                    }
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
    }
}
