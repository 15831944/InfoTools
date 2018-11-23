using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NavisWorksInfoTools.Constants;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using Win = System.Windows;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System.IO;
using WinForms = System.Windows.Forms;
using System.Xml.Serialization;
using NavisWorksInfoTools.S1NF0_SOFTWARE.XML.St;
using NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl;
using Common.XMLClasses;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    [Plugin("AddObjectsToStructure",
        DEVELOPER_ID,
        ToolTip = "Указать файлы структуры и классификатора для " + S1NF0_APP_NAME +". Добавить объекты геометрии в структуру",
        DisplayName = S1NF0_APP_NAME + ". 3. Добавление объектов модели в структуру")]
    public class AddObjectsToStructure : AddInPlugin
    {
        public static StructureDataStorage DataStorage { get; set; } = null;

        private bool startEditXML = false;

        public override int Execute(params string[] parameters)
        {
            try
            {
                Document doc = Application.ActiveDocument;

                if (DataStorage == null)
                {
                    string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string docFileName = doc.FileName;
                    if (!String.IsNullOrEmpty(docFileName))
                    {
                        initialPath = Path.GetDirectoryName(docFileName);
                    }


                    //выбрать два файла st.xml и cl.xml
                    WinForms.OpenFileDialog ofd1 = new WinForms.OpenFileDialog();
                    ofd1.InitialDirectory = initialPath;
                    ofd1.Filter = ".st.xml files (*.st.xml)|*.st.xml";
                    ofd1.FilterIndex = 1;
                    ofd1.RestoreDirectory = true;
                    ofd1.Title = "Выберите файл структуры модели " + S1NF0_APP_NAME;

                    if (ofd1.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        string stFilename = ofd1.FileName;
                        initialPath = Path.GetDirectoryName(stFilename);

                        WinForms.OpenFileDialog ofd2 = new WinForms.OpenFileDialog();
                        ofd2.InitialDirectory = initialPath;
                        ofd2.Filter = ".cl.xml files (*.cl.xml)|*.cl.xml";
                        ofd2.FilterIndex = 1;
                        ofd2.RestoreDirectory = true;
                        ofd2.Title = "Выберите файл классификатора модели " + S1NF0_APP_NAME;

                        if (ofd2.ShowDialog() == WinForms.DialogResult.OK)
                        {
                            string clFilename = ofd2.FileName;

                            //Десериализовать
                            Structure structure = null;
                            using (StreamReader sr = new StreamReader(stFilename))
                            {
                                string serializedData = sr.ReadToEnd();
                                //serializedData = serializedData.Replace((char)(0x1F), '_');//это не работает
                                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Structure));
                                StringReader stringReader = new StringReader(serializedData);
                                structure = (Structure)xmlSerializer.Deserialize(stringReader);

                            }

                            Classifier classifier = null;
                            using (StreamReader sr = new StreamReader(clFilename))
                            {
                                string serializedData = sr.ReadToEnd();
                                //serializedData = serializedData.Replace((char)(0x1F), ' ');
                                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Classifier));
                                StringReader stringReader = new StringReader(serializedData);
                                classifier = (Classifier)xmlSerializer.Deserialize(stringReader);
                            }

                            DataStorage = new StructureDataStorage(doc, stFilename, clFilename, structure, classifier);

                            startEditXML = true;
                        }
                    }
                }

                if (DataStorage != null)
                {
                    //StructureWindow structureWindow = new StructureWindow(DataStorage);
                    StructureWindow structureWindow = DataStorage.StructureWindow;
                    //Изучается набор выбора на наличие объектов геометрии
                    //Объекты геометрии передаются в свойство StructureWindow.SelectedGeometryModelItems
                    IEnumerable<ModelItem> geometryItems = null;
                    if (startEditXML)
                    {
                        //Для того, чтобы не увеличивать время ожидания при начале редактирования XML
                        //Принудительно настроить выбор объектов на те объекты, которые были не скрыты в начале редактирования
                        //doc.CurrentSelection.CopyFrom(DataStorage.AllItemsLookup.Values);//выглядит не очень красиво
                        doc.CurrentSelection.Clear();//Просто сбросить текущий выбор
                        geometryItems = DataStorage.AllItemsLookup.Values;
                        startEditXML = false;
                    }
                    else
                    {
                        ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;
                        int n = 50000;
                        //Проверит, что выбрано не более n конечных геометрических элементов
                        Search searchForAllIDs = new Search();
                        searchForAllIDs.Selection.CopyFrom(currSelectionColl);
                        searchForAllIDs.Locations = SearchLocations.DescendantsAndSelf;

                        StructureDataStorage.ConfigureSearchForAllNotHiddenGeometryItemsWithIds(searchForAllIDs);
                        ModelItemCollection selectedGeometry = searchForAllIDs.FindAll(doc, false);
                        int nSel = selectedGeometry.Count;
                        if (nSel > n)
                        {
                            doc.CurrentSelection.CopyFrom(selectedGeometry);
                            WinForms.DialogResult dialogResult = WinForms.MessageBox.Show("Вы выбрали очень большое количество конечных геометрических элементов - " + nSel
                                + ". Это приведет к большой задержке в работе программы. Продолжить?", "Предупреждение",
                                WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning);
                            if (dialogResult== WinForms.DialogResult.No)
                            {
                                return 0;
                            }
                        }

                        #region Найти все конечные геометрические элементы с id с помощью Search API. Нельзя настроить на поиск только не скрытых элементов
                        /*
                        Search searchForAllIDs = new Search();
                        searchForAllIDs.Selection.CopyFrom(currSelectionColl);
                        searchForAllIDs.Locations = SearchLocations.DescendantsAndSelf;

                        DataStorage.ConfigureSearchForAllNotHiddenGeometryItemsWithIds(searchForAllIDs);
                        ModelItemCollection selectedGeometry = searchForAllIDs.FindAll(doc, false);


                        if (!DataStorage.AllNotHiddenGeometryModelItems.IsSelected(selectedGeometry))
                        {
                            WinForms.MessageBox.Show("Выбраны объекты, которые были скрыты при начале редактирования XML");
                            return 0;
                        }


                        List<ModelItem> geometryItems = new List<ModelItem>();
                        selectedGeometry.CopyTo(geometryItems);//ЗДЕСЬ БУДЕТ ЗАДЕРЖКА
                        //foreach (ModelItem item in selectedGeometry)
                        //{
                        //    geometryItems.Add(item);
                        //}
                        */
                        #endregion


                        //Начать поиск элементов с id
                        currSelectionColl = new ModelItemCollection(currSelectionColl);
                        currSelectionColl.MakeDisjoint();//Убрать все вложенные объекты
                        geometryItems = new List<ModelItem>();
                        RecurseSearchForAllNotHiddenGeometryItemsWithIdsContainedInBaseLookup
                            (currSelectionColl, (List<ModelItem>)geometryItems, DataStorage.AllItemsLookup);
                    }


                    

                    structureWindow.SelectedGeometryModelItems = geometryItems;
                    //Открывается окно StructureWindow
                    structureWindow.BeforeShow();
                    structureWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при задании файла структуры " + S1NF0_APP_NAME);
            }

            return 0;
        }


        private static void RecurseSearchForAllNotHiddenGeometryItemsWithIdsContainedInBaseLookup
            (IEnumerable<ModelItem> items, List<ModelItem> list, Dictionary<string, ModelItem> itemsLookup)
        {
            foreach (ModelItem item in items)
            {
                if (!item.IsHidden)
                {
                    if (item.HasGeometry)
                    {
                        DataProperty idProp = item.PropertyCategories
                                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                                    ID_PROP_DISPLAY_NAME);
                        if (idProp != null)
                        {
                            string key = Utils.GetDisplayValue(idProp.Value);
                            if (itemsLookup.ContainsKey(key))
                            {
                                list.Add(item);
                            }

                        }
                    }

                    RecurseSearchForAllNotHiddenGeometryItemsWithIdsContainedInBaseLookup(item.Children, list, itemsLookup);
                }

            }
        }


    }
}
