using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Internal.ApiImplementation;
using Win = System.Windows;
using WinForms = System.Windows.Forms;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System.IO;
using System.Text.RegularExpressions;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using static NavisWorksInfoTools.Constants;

namespace NavisWorksInfoTools
{
    //[Plugin("FBXExport",
    //    DEVELOPER_ID,
    //    ToolTip = "Экспорт в FBX",
    //    DisplayName = "Экспорт в FBX")]
    public class FBXExport2 : AddInPlugin
    {
        private static Regex regex = new Regex("^\\s*Model:.*\"Model::.*$");

        private static string fBXCurrObjLine;

        public override int Execute(params string[] parameters)
        {
            Win.MessageBoxResult result = Win.MessageBox.Show("Перед экспортом FBX, нужно скрыть те элементы модели, которые не нужно экспортировать. "
                + "А так же нужно настроить параметры экспорта в FBX на экспорт в формате ASCII"
                + "\n\nНачать выгрузку FBX?", "Выгрузка FBX", Win.MessageBoxButton.YesNo);

            if (result == Win.MessageBoxResult.Yes)
            {
                try
                {
                    //ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;

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
                            //Выполнить экспорт в FBX
                            if (0 == FBXplugin.Execute(sFD.FileName))
                            {
                                if (IsASCIIFBXFile(sFD.FileName))
                                {
                                    DocumentModels docModels = doc.Models;
                                    ModelItemEnumerableCollection rootItems = docModels.RootItems;

                                    string editedFBXFileName = Common.Utils
                                        .GetNonExistentFileName(Path.GetDirectoryName(sFD.FileName),
                                        Path.GetFileNameWithoutExtension(sFD.FileName) + "_Edited", "fbx");

                                    fBXCurrObjLine = null;
                                    using (StreamWriter sw = new StreamWriter(editedFBXFileName))
                                    {
                                        using (StreamReader sr = new StreamReader(sFD.FileName))
                                        {

                                            OverwriteFBX(rootItems, sw, sr, false);

                                            //Дописать FBX до конца
                                            sw.Write(sr.ReadToEnd());
                                        }
                                    }

                                    Win.MessageBox.Show("Готово", "Готово", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                                }
                                else
                                {
                                    //FBX не ASCII
                                    throw new Exception("FBX не в формате ASCII");
                                }
                            }
                            else
                            {
                                //Ошибки при экспорте
                                throw new Exception("Возникли ошибки при экспорте FBX");
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    CommonException(ex, "Ошибка при экспорте в FBX");
                }
            }

            return 0;
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


        /// <summary>
        /// Работа метода основана на том, что порядок объектов в текстовом FBX такой же как в дереве Navis
        /// </summary>
        /// <param name="items"></param>
        private void OverwriteFBX(IEnumerable<ModelItem> items, StreamWriter sw, StreamReader sr, bool searchMatchesThisLevel)
        {
            foreach (ModelItem item in items)
            {
                if (item.IsHidden)
                {
                    continue;//Пропустить скрытый элемент
                }

                if (searchMatchesThisLevel)
                {
                    DataProperty idProp = item.PropertyCategories
                        .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME, ID_PROP_DISPLAY_NAME);
                    DataProperty matIdProp = item.PropertyCategories
                        .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME, MATERIAL_ID_PROP_DISPLAY_NAME);
                    if (idProp != null && matIdProp != null)
                    {
                        //Поиск соответствующей строки в FBX
                        string searchingName = item.DisplayName;

                        if (String.IsNullOrEmpty(searchingName))
                        {
                            //В большинстве случаев соответствие можно искать по item.ClassDisplayName
                            //но для блоков AutoCAD это не работает. Как быть с ними не понятно, поэтому просто пропукать их
                            string alternativeName = item.ClassDisplayName;
                            if (!alternativeName.Equals("Insert") && !alternativeName.Equals("Block"))//В ЭТОМ СЛУЧАЕ ЕСЛИ У ОБЪЕКТА ЕСТЬ ВЛОЖЕННЫЕ, ТО МОЖНО СОПОСТАВИТЬ ПО НИМ
                                searchingName = item.ClassDisplayName;
                        }
                        if (!String.IsNullOrEmpty(searchingName))//ЕСЛИ В ДЕРЕВЕ МОДЕЛИ СТАЛКИВАЕМСЯ С ОБЪЕКТОМ, КОТОРЫЙ НЕВОЗМОЖНО СОПОСТАВИТЬ С FBX, ТО В FBX НУЖНО ПРОПУСТИТЬ ОДИН ОБЪЕКТ (с непустым именем)
                        {
                            //Сопоставлять по searchingName
                            do
                            {
                                if (fBXCurrObjLine != null)
                                {
                                    //Запись строки с именем объекта если соответствие не найдено в предыдущем цикле
                                    sw.WriteLine(fBXCurrObjLine);
                                }
                                //Переход к следующей строке, содержащей имя объекта пока не найдено совпадение
                                fBXCurrObjLine = NextObjFBXLine(sw, sr);
                                if (fBXCurrObjLine == null)
                                {
                                    throw new Exception("Не найдено соответствия в файле FBX для имени элемента " + searchingName + " Выполнение прервано.");
                                }

                            }
                            while (!fBXCurrObjLine.Contains("\"Model::" + searchingName + "\""));

                            string editedName = searchingName + "|" + Utils.GetDisplayValue(idProp.Value) + "|" + Utils.GetDisplayValue(matIdProp.Value);
                            fBXCurrObjLine = fBXCurrObjLine.Replace("\"Model::" + searchingName + "\"", "\"Model::" + editedName + "\"");

                            sw.WriteLine(fBXCurrObjLine);
                            fBXCurrObjLine = null;
                        }
                        
                    }
                }



                OverwriteFBX(item.Children, sw, sr, true);
            }
        }


        private static string NextObjFBXLine(StreamWriter sw, StreamReader sr)
        {
            //Найти внутри узла Objects следующий подузел Model
            //До тех пор пока не найден, нужно записывать каждую строку в новый файл без изменений
            string FBXline = null;
            while ((FBXline = sr.ReadLine()) != null)
            {
                if (regex.IsMatch(FBXline))
                {
                    //Найдена строка с именем объекта
                    return FBXline;
                }
                else
                {
                    sw.WriteLine(FBXline);
                }
            }
            return null;
        }

    }
}
