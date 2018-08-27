using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Win = System.Windows;
using WinForms = System.Windows.Forms;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Internal.ApiImplementation;
using System.IO;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using static NavisWorksInfoTools.Constants;

namespace NavisWorksInfoTools
{
    [Plugin("FBXExport",
        "S-Info",
        ToolTip = "Экспорт в FBX",
        DisplayName = "Экспорт в FBX")]
    public class FBXExport3 : AddInPlugin
    {
        //Проверить будет ли работать если нет единого корневого узла!!!!+
        //Проверить будет ли работать если полностью или частично не проставлены id+ (частично не проверил)
        //Если есть несопоставляемые имена, обнаруженные в FBXExport2+
        //TODO: То же для ASCII

        private HashSet<string> notReliableClassDisplayNames =
            new HashSet<string>()
        {
            "Insert",
        };


        public override int Execute(params string[] parameters)
        {
            Win.MessageBoxResult result = Win.MessageBox.Show("Перед экспортом FBX, нужно скрыть те элементы модели, которые не нужно экспортировать. "
                + "А так же нужно настроить параметры экспорта в FBX на экспорт ЛИБО В ФОРМАТЕ ASCII, ЛИБО В ДВОИЧНОМ ФОРМАТЕ ВЕРСИИ НЕ НОВЕЕ 2018. "
                + "Рекомендуется так же отключить экспорт источников света и камер. "
                + "\n\nНачать выгрузку FBX?", "Выгрузка FBX", Win.MessageBoxButton.YesNo);

            if (result == Win.MessageBoxResult.Yes)
            {
                try
                {
                    PluginRecord FBXPluginrecord = Application.Plugins.
                            FindPlugin("NativeExportPluginAdaptor_LcFbxExporterPlugin_Export.Navisworks");
                    if (FBXPluginrecord != null)
                    {
                        if (!FBXPluginrecord.IsLoaded)
                        {
                            FBXPluginrecord.LoadPlugin();
                        }

                        NativeExportPluginAdaptor FBXplugin = FBXPluginrecord.LoadedPlugin as NativeExportPluginAdaptor;

                        Document doc = Application.ActiveDocument;

                        string fbxPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string docPath = doc.FileName;
                        string defFBXFileName = "";
                        if (!String.IsNullOrEmpty(docPath))
                        {
                            fbxPath = Path.GetDirectoryName(docPath);
                            defFBXFileName = Path.GetFileNameWithoutExtension(docPath) + ".fbx";
                        }

                        //Указание пользователем имени файла для fbx
                        WinForms.SaveFileDialog sFD = new WinForms.SaveFileDialog();
                        sFD.InitialDirectory = fbxPath;
                        sFD.Filter = "fbx files (*.fbx)|*.fbx";
                        sFD.FilterIndex = 1;
                        sFD.RestoreDirectory = true;
                        if (!String.IsNullOrWhiteSpace(defFBXFileName))
                            sFD.FileName = defFBXFileName;
                        sFD.Title = "Укажите файл для записи fbx";

                        if (sFD.ShowDialog() == WinForms.DialogResult.OK)
                        {
                            if (FBXplugin.Execute(sFD.FileName) == 0)//Выполнить экспорт в FBX
                            {
                                bool isASCII = IsASCIIFBXFile(sFD.FileName);
                                if (isASCII || GetBinaryVersionNum(sFD.FileName) <= 7500)
                                {
                                    //Прочитать модель, составить очередь имен для подстановки в FBX
                                    Queue<FBX.NameReplacement> replacements = new Queue<FBX.NameReplacement>();
                                    DocumentModels docModels = doc.Models;
                                    ModelItemEnumerableCollection rootItems = docModels.RootItems;
                                    //if (rootItems.Count() > 1)
                                    //{
                                    //    //Если в Navis несколько корневых узлов (как в nwf), 
                                    //    //то один узел в самом начале FBX должен быть пропущен
                                    //    //Там появится узел Environment
                                    //    replacements.Enqueue(new FBX.NameReplacement());
                                    //}
                                    ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;
                                    NameReplacementQueue(rootItems, replacements, oState);
                                    if (rootItems.Count() == 1)
                                    {
                                        //Обозначить, что первый узел имеет ненадежное имя.
                                        //В FBX оно всегда - Environment, а в Navis - имя открытого файла
                                        //replacements.Peek().OldNameTrustable = false;

                                        //Если корневой узел один, то убрать его из списка. Его не будет в FBX
                                        replacements.Dequeue();
                                    }

                                    //Первый узел в списке замены должен обязательно иметь верное имя
                                    //(в начале списка могут быть с пустым значением, которые отключены в Navis)
                                    while (!replacements.Peek().OldNameTrustable)
                                    {
                                        replacements.Dequeue();
                                    }


                                    //Отредактировать FBX
                                    FBX.ModelNamesEditor fbxEditor = null;
                                    if (IsASCIIFBXFile(sFD.FileName))
                                    {
                                        fbxEditor = new FBX.ASCIIModelNamesEditor(sFD.FileName, replacements);
                                    }
                                    else /*if (GetBinaryVersionNum(sFD.FileName) <= 7500)*/
                                    {
                                        fbxEditor = new FBX.BinaryModelNamesEditor(sFD.FileName, replacements);
                                    }
                                    fbxEditor.EditModelNames();

                                    Win.MessageBox.Show("Файл FBX с отредактированными именами моделей - " + fbxEditor.FbxFileNameEdited,
                                        "Готово", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                                }
                                else
                                {
                                    throw new Exception("Неподдерживаемый формат FBX");
                                }
                            }
                            else
                            {
                                throw new Exception("При экспорте FBX из NavisWorks произошли ошибки");
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    CommonException(ex, "Ошибка при экспорте в FBX из Navis");
                }
            }

            return 0;
        }



        private void NameReplacementQueue(IEnumerable<ModelItem> items,
            Queue<FBX.NameReplacement> replacements, ComApi.InwOpState3 oState)
        {
            foreach (ModelItem item in items)
            {
                if (item.IsHidden)
                {
                    //Элемент в FBX должен быть пропущен (это будет элемент с пустым именем)
                    replacements.Enqueue(new FBX.NameReplacement());
                    continue;//Не заходить во вложенные узлы
                }

                DataProperty idProp = item.PropertyCategories
                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                    ID_PROP_DISPLAY_NAME);
                DataProperty matIdProp = item.PropertyCategories
                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                    MATERIAL_ID_PROP_DISPLAY_NAME);
                if (idProp == null || matIdProp == null)
                {
                    //Запускать простановку id перед выгрузкой в fbx, чтобы не решать проблему, когда частично не проставлены id
                    Utils.SetS1NF0PropsToItem(oState, item,
                    new Dictionary<string, object>()
                    {
                    { ID_PROP_DISPLAY_NAME, Utils.S1NF0PropSpecialValue.RandomGUID},
                    { MATERIAL_ID_PROP_DISPLAY_NAME, "_"}
                    });

                    idProp = item.PropertyCategories
                        .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                        ID_PROP_DISPLAY_NAME);
                    matIdProp = item.PropertyCategories
                        .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                       MATERIAL_ID_PROP_DISPLAY_NAME);
                }


                //Один элемент в FBX должен быть переименован

                //item.DisplayName возвращает путую строку в 2 случаях:
                //- Если свойства имя нет
                //- Если имя - пустая строка
                //string baseName = item.DisplayName;
                //Проверить есть ли у элемента свойство имя
                //Если это свойство есть и оно не пустое, то имя узла в FBX будет таким же
                //Если этого свойства нет, то в большинстве случаев имя узла в FBX совпадет с item.ClassDisplayName
                string baseName = null;
                DataProperty nameProp
                    = item.PropertyCategories.FindPropertyByName("LcOaNode", "LcOaSceneBaseUserName");
                if (nameProp != null)
                {
                    baseName = nameProp.Value.ToDisplayString();
                }

                bool baseNameTrustable = true;

                if (baseName == null /*String.IsNullOrEmpty(baseName)*/)
                {
                    baseName = item.ClassDisplayName;
                    //Если item.DisplayName не возвращает значения, то нет гарантии, что 
                    //item.ClassDisplayName совпадет с именем в FBX
                    if (notReliableClassDisplayNames.Contains(baseName))
                    {
                        baseNameTrustable = false;
                    }
                }
                else if (baseName.Equals(""))
                {
                    //Если объект имеет имя и оно пустое,
                    //то в FBX оно будет заменено (была замечена замена на LcOaExGroup)
                    //но точно не известно на что будет замена в каких случаях
                    baseNameTrustable = false;
                }
                string replacementName = baseName + "|" + Utils.GetDisplayValue(idProp.Value) + "|" + Utils.GetDisplayValue(matIdProp.Value);

                replacements.Enqueue(new FBX.NameReplacement(baseName, baseNameTrustable, replacementName));

                NameReplacementQueue(item.Children, replacements, oState);
            }
        }


        private bool IsASCIIFBXFile(string fbxFilename)
        {
            bool yes = false;
            if (File.Exists(fbxFilename))
            {
                //Считать первую строку. Она должна начинаться с ";"
                using (StreamReader sr = new StreamReader(fbxFilename))
                {
                    string firstLine = sr.ReadLine();
                    if (firstLine.StartsWith(";"))
                    {
                        yes = true;
                    }
                }
            }
            return yes;
        }


        private uint GetBinaryVersionNum(string fbxFilename)
        {
            uint versionNum = uint.MaxValue;
            using (BinaryReader br = new BinaryReader(File.Open(fbxFilename, FileMode.Open)))
            {
                br.BaseStream.Position += 23;//пропуск байтов
                versionNum = br.ReadUInt32();
            }

            return versionNum;
        }
    }
}
