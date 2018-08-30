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

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    [Plugin("3GetStructure",
        "S-Info",
        ToolTip = "Указать файлы структуры и классификатора для " + S1NF0_APP_NAME,
        DisplayName = S1NF0_APP_NAME + ". 3. Файлы структуры")]
    public class GetStructure : AddInPlugin
    {
        public static DataStorage DataStorage { get; set; } = null;

        public override int Execute(params string[] parameters)
        {
            try
            {
                Document doc = Application.ActiveDocument;


                if (DataStorage==null)
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
                                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Structure));
                                StringReader stringReader = new StringReader(serializedData);
                                structure = (Structure)xmlSerializer.Deserialize(stringReader);

                            }

                            Classifier classifier = null;
                            using (StreamReader sr = new StreamReader(clFilename))
                            {
                                string serializedData = sr.ReadToEnd();
                                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Classifier));
                                StringReader stringReader = new StringReader(serializedData);
                                classifier = (Classifier)xmlSerializer.Deserialize(stringReader);
                            }

                            DataStorage = new DataStorage(doc, stFilename, clFilename, structure, classifier);

                        }
                    }
                }

                if(DataStorage != null)
                {
                    //StructureWindow structureWindow = new StructureWindow(DataStorage);
                    StructureWindow structureWindow = DataStorage.StructureWindow;
                    //Изучается набор выбора на наличие объектов геометрии
                    //Объекты геометрии передаются в свойство StructureWindow.SelectedGeometryModelItems
                    ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;
                    List<ModelItem> geometryItems = new List<ModelItem>();
                    foreach (ModelItem item in currSelectionColl.DescendantsAndSelf)
                    {
                        if (item.HasGeometry)
                        {
                            geometryItems.Add(item);
                        }
                    }

                    structureWindow.SelectedGeometryModelItems = geometryItems;
                    //Открывается окно StructureWindow
                    structureWindow.BeforeShow();
                    structureWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                CommonException(ex, "Задании файла структуры " + S1NF0_APP_NAME);
            }

            return 0;
        }
    }
}
