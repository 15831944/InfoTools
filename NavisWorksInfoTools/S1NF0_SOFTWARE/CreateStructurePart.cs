using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using static NavisWorksInfoTools.Constants;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using WinForms = System.Windows.Forms;
using System.IO;
using NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl;
using NavisWorksInfoTools.S1NF0_SOFTWARE.XML.St;
using System.Xml.Serialization;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    //[Plugin("CreateStructurePart",
    //    DEVELOPER_ID,
    //    ToolTip = "Выгрузка куска структуры",
    //    DisplayName = "Выгрузка куска структуры")]
    public class CreateStructurePart //: AddInPlugin
    {

        public static int Execute(/*params string[] parameters*/)
        {
            try
            {
                Document doc = Application.ActiveDocument;
                ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;

                if (currSelectionColl.Count > 0)
                {
                    //Найти все объекты геометрии с id в текущем наборе выбора
                    Search searchForAllIDs = new Search();
                    searchForAllIDs.Selection.CopyFrom(currSelectionColl);
                    searchForAllIDs.Locations = SearchLocations.DescendantsAndSelf;
                    StructureDataStorage.ConfigureSearchForAllGeometryItemsWithIds(searchForAllIDs);
                    ModelItemCollection selectedGeometry = searchForAllIDs.FindAll(doc, false);
                    if (selectedGeometry.Count > 0)
                    {
                        //Указать имя файла структуры.
                        string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string initialFileName = "Кусок проекта";
                        string docFileName = doc.FileName;
                        if (!String.IsNullOrEmpty(docFileName))
                        {
                            initialPath = Path.GetDirectoryName(docFileName);
                            initialFileName = Path.GetFileNameWithoutExtension(docFileName) + "_PART";
                        }

                        WinForms.SaveFileDialog saveFileDialog = new WinForms.SaveFileDialog();
                        saveFileDialog.InitialDirectory = initialPath;
                        saveFileDialog.Filter = "st.xml files (*.st.xml)|*.st.xml";
                        saveFileDialog.FilterIndex = 1;
                        saveFileDialog.RestoreDirectory = true;
                        if (!String.IsNullOrWhiteSpace(initialFileName))
                            saveFileDialog.FileName = initialFileName;
                        saveFileDialog.Title = "Укажите файл для создания куска структуры";

                        if (saveFileDialog.ShowDialog() == WinForms.DialogResult.OK)
                        {
                            string stFilename = saveFileDialog.FileName;
                            string name = Path.GetFileName(stFilename);
                            string clFilename = Path.Combine(Path.GetDirectoryName(stFilename),
                                name.Substring(0, name.Length - 6) + "cl.xml");

                            //пустой классификатор
                            Classifier classifier = new Classifier()
                            {
                                Name = "PartialClassifier",
                                IsPrimary = true,
                                DetailLevels = new List<string>() { "Folder", "Geometry" }
                            };
                            if (File.Exists(clFilename))
                            {
                                //TODO: Если там уже существует структура и классификатор,
                                //то сохранить классы из этого классификатора
                                //(лучше вывести еще MessageBox Yes/No использовать или нет)
                                Class tryReadClassifier = null;
                                try
                                {
                                    using (StreamReader sr = new StreamReader(clFilename))
                                    {
                                        string serializedData = Common.Utils.RemoveInvalidXmlSubstrs(sr.ReadToEnd());

                                        XmlSerializer xmlDeSerializer = new XmlSerializer(typeof(NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl.Class));
                                        StringReader stringReader = new StringReader(serializedData);
                                        tryReadClassifier = (Class)xmlDeSerializer.Deserialize(stringReader);
                                    }
                                }
                                catch { }

                                if (tryReadClassifier != null)
                                {
                                    WinForms.DialogResult result = WinForms.MessageBox
                                        .Show("Использовать классы из файла <" + clFilename + ">?",
                                        "Сохранение предыдущих классов", WinForms.MessageBoxButtons.YesNo);
                                    if (result == WinForms.DialogResult.Yes)
                                    {
                                        classifier.NestedClasses = tryReadClassifier.NestedClasses;
                                    }
                                }

                            }

                            //пустая структура
                            Structure structure = new Structure()
                            {
                                Name = "PartialStructure",
                                Classifier = classifier.Name,
                                IsPrimary = true,
                            };



                            //Создать StructureDataStorage
                            //TODO: Нужно добавить выбор категорий!
                            StructureDataStorage dataStorage = new StructureDataStorage(doc, stFilename, clFilename, structure, classifier, true);

                            //Напихать все объекты в StructureDataStorage сплошным списком
                            foreach (ModelItem item in selectedGeometry)
                            {
                                dataStorage.CreateNewModelObject(null, item);
                            }

                            //Создать пустой объект и пустой класс и в них передать соответственно объекты и классы из StructureDataStorage
                            string codeDummy = "_";
                            NavisWorksInfoTools.S1NF0_SOFTWARE.XML.Cl.Class partialClassifier = new Class()
                            { Name = "partialClassifier", NameInPlural = "partialClassifiers", DetailLevel = "Folder", Code = codeDummy };
                            partialClassifier.NestedClasses = dataStorage.Classifier.NestedClasses;
                            NavisWorksInfoTools.S1NF0_SOFTWARE.XML.St.Object partialStructure = new XML.St.Object()
                            {
                                Name = "partialStructure",
                                ClassCode = codeDummy
                            };
                            partialStructure.NestedObjects = dataStorage.Structure.NestedObjects;

                            //Сериализовать
                            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Class));
                            using (StreamWriter sw = new StreamWriter(clFilename))
                            {
                                xmlSerializer.Serialize(sw, partialClassifier);
                            }

                            xmlSerializer = new XmlSerializer(typeof(XML.St.Object));
                            using (StreamWriter sw = new StreamWriter(stFilename))
                            {
                                xmlSerializer.Serialize(sw, partialStructure);
                            }

                        }

                    }

                }


            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при создании куска структуры в Navis");
            }

            return 0;
        }
    }
}
