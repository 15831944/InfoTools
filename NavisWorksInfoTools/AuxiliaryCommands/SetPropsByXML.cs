using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NavisWorksInfoTools.Constants;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Autodesk.Navisworks.Api;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using Win = System.Windows;
using System.Xml.Serialization;
using WinForms = System.Windows.Forms;
using System.IO;

namespace NavisWorksInfoTools.AuxiliaryCommands
{
    [Plugin("SetPropsByXML",
        DEVELOPER_ID,
        ToolTip = "Заполнить атрибуты через XML",
        DisplayName = "Заполнить атрибуты через XML")]
    public class SetPropsByXML : AddInPlugin
    {
        private static List<ObjectsData> ObjectsDataStorage = null;

        private static readonly NamedConstant tabCN = new NamedConstant("LcOaPropOverrideCat", S1NF0_DATA_TAB_DISPLAY_NAME);
        private static readonly NamedConstant idCN = new NamedConstant(ID_PROP_DISPLAY_NAME, ID_PROP_DISPLAY_NAME);

        public override int Execute(params string[] parameters)
        {
            try
            {



                Document doc = Application.ActiveDocument;

                //ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;

                string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string initialFileName = Environment.UserName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                string docFileName = doc.FileName;
                if (!String.IsNullOrEmpty(docFileName))
                {
                    initialPath = Path.GetDirectoryName(docFileName);
                    initialFileName += "_" + Path.GetFileNameWithoutExtension(docFileName);
                }


                //if (currSelectionColl.Count > 0)
                //{
                //    //Если выбран какой-либо объект в дереве (или несколько), то открывается такое же окно как в команде SetProps 
                //    //(но при этом у каждого объекта обязательно должен быть настроенный id)
                //    //Объекты DisplayDataTab, DisplayProperty, DisplayURL должны быть serializable
                //    List<string> ids = new List<string>();
                //    List<ModelItem> itemsToSetProps = new List<ModelItem>();
                //    foreach (ModelItem item in currSelectionColl)
                //    {
                //        DataProperty idProp = item.PropertyCategories
                //                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                //                    ID_PROP_DISPLAY_NAME);
                //        if (idProp != null)
                //        {
                //            ids.Add(Utils.GetDisplayValue(idProp.Value));
                //            itemsToSetProps.Add(item);
                //        }
                //    }

                //    if (ids.Count == 0)
                //    {
                //        Win.MessageBox.Show("Ни у одного из выбранных элементов нет id");
                //        return 0;
                //    }

                //    //Инициализация окна SetProps
                //    SetPropsWindow setPropsWindow = SetProps.ConfigureAndOppenSetPropsWindow(doc, itemsToSetProps);
                //    setPropsWindow.Loaded += (a, b) =>
                //    {
                //        setPropsWindow.PreserveExistingProperties = true;
                //    };
                //    bool? result = setPropsWindow.ShowDialog();
                //    if (result != null && result.Value)
                //    {
                //        //После подтверждения объекты DisplayDataTab и DisplayURL сохраняются в объект ObjectData
                //        //Объект ObjectsData сразу же сериализуется. Открывается окно сохранения файла
                //        //Имя XML по умолчанию - дата и время до минут + имя пользователя + имя файла
                //        ObjectsData objectsData = new ObjectsData()
                //        {
                //            PreserveExistingProperties = setPropsWindow.PreserveExistingProperties,
                //            OverwriteLinks = setPropsWindow.OverwriteLinks,
                //            OverwriteUserAttr = setPropsWindow.OverwriteUserAttr,
                //            Ids = ids,
                //            DataTabs = setPropsWindow.OverwriteUserAttr ? setPropsWindow.DataTabs : null,
                //            URLs = setPropsWindow.OverwriteLinks ? setPropsWindow.URLs : null

                //        };




                //        WinForms.SaveFileDialog saveFileDialog = new WinForms.SaveFileDialog();
                //        saveFileDialog.InitialDirectory = initialPath;
                //        saveFileDialog.Filter = "xml files (*.xml)|*.xml";
                //        saveFileDialog.FilterIndex = 1;
                //        saveFileDialog.RestoreDirectory = true;
                //        if (!String.IsNullOrWhiteSpace(initialFileName))
                //            saveFileDialog.FileName = initialFileName;
                //        saveFileDialog.Title = "Укажите файл для записи свойств";

                //        if (saveFileDialog.ShowDialog() == WinForms.DialogResult.OK)
                //        {
                //            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ObjectsData));
                //            using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName))
                //            {
                //                xmlSerializer.Serialize(sw, objectsData);
                //            }
                //        }

                //        //выбранные объекты скрываются, чтобы пользователь не выбирал их по второму разу
                //        doc.Models.SetHidden(itemsToSetProps, true);

                //    }
                //}
                //else
                //{
                    //get state object of COM API
                    ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;

                    //предлагается выбрать несколько готовых XML для создания свойств в текущей модели
                    WinForms.OpenFileDialog ofd = new WinForms.OpenFileDialog();
                    ofd.InitialDirectory = initialPath;
                    ofd.Filter = "xml files (*.xml)|*.xml";
                    ofd.FilterIndex = 1;
                    ofd.RestoreDirectory = true;
                    ofd.Multiselect = true;
                    ofd.Title = "Выберите файлы с данными о свойствах ";

                    if (ofd.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        List<string> filenames = ofd.FileNames.ToList();
                        filenames.Sort();
                        foreach (string filename in filenames)
                        {
                            ObjectsData objectsData = null;
                            try
                            {
                                //Десериализуются объекты ObjectData.
                                using (StreamReader sr = new StreamReader(filename))
                                {
                                    string serializedData = sr.ReadToEnd();
                                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(ObjectsData));
                                    StringReader stringReader = new StringReader(serializedData);
                                    objectsData = (ObjectsData)xmlSerializer.Deserialize(stringReader);
                                }
                            }
                            catch { }

                            if (objectsData != null)
                            {
                                //Для каждого объекта ищется соответствующий объект по Id
                                ModelItemCollection itemsToSetProps = new ModelItemCollection();
                                foreach (string id in objectsData.Ids)
                                {
                                    //Search API
                                    VariantData idVd = VariantData.FromDisplayString(id);

                                    SearchCondition searchForIDCondition
                                        = new SearchCondition(tabCN, idCN, SearchConditionOptions.None,
                                        SearchConditionComparison.Equal, idVd);
                                    Search searchForCertainID = new Search();
                                    searchForCertainID.Selection.SelectAll();
                                    searchForCertainID.PruneBelowMatch = false;
                                    searchForCertainID.SearchConditions.Add(searchForIDCondition);

                                    ModelItem itemIdMatch = searchForCertainID.FindFirst(doc, false);
                                    if (itemIdMatch != null)
                                        itemsToSetProps.Add(itemIdMatch);
                                }

                                bool preserveExistingProperties = objectsData.PreserveExistingProperties;
                                bool overwriteUserAttr = objectsData.OverwriteUserAttr;
                                bool overwriteLinks = objectsData.OverwriteLinks;

                                List<DisplayDataTab> displayDataTabs = objectsData.DataTabs;
                                List<DisplayURL> displayURLs = objectsData.URLs;

                                //и для этого набора объектов запускается процедура создания свойств как в команде SetProps
                                SetProps.SetPropsMethod(oState, itemsToSetProps, displayDataTabs,
                                    displayURLs, overwriteUserAttr, overwriteLinks, preserveExistingProperties);
                            }
                        }


                    }


                //}


            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при заполнении атрибутов в Navis");
            }



            return 0;
        }


    }


    public class ObjectsData
    {
        [XmlAttribute]
        public bool PreserveExistingProperties { get; set; }

        [XmlAttribute]
        public bool OverwriteUserAttr { get; set; }

        [XmlAttribute]
        public bool OverwriteLinks { get; set; }

        [XmlArray("Ids"), XmlArrayItem("Id")]
        public List<string> Ids { get; set; } = new List<string>();

        [XmlArray("DataTabs"), XmlArrayItem("DisplayDataTab")]
        public List<DisplayDataTab> DataTabs { get; set; } = new List<DisplayDataTab>();

        [XmlArray("URLs"), XmlArrayItem("DisplayURL")]
        public List<DisplayURL> URLs { get; set; } = new List<DisplayURL>();
    }
}
