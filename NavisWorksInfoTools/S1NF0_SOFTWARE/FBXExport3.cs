﻿using Autodesk.Navisworks.Api.Plugins;
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
using Autodesk.Navisworks.Internal.ApiImplementation;
using System.IO;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using static NavisWorksInfoTools.Constants;
using Common.Controls.BusyIndicator;

namespace NavisWorksInfoTools
{
    [Plugin("FBXExport",
        DEVELOPER_ID,
        ToolTip = "Экспорт в FBX с идентификацией объектов",
        DisplayName = S1NF0_APP_NAME + ". 2. Экспорт в FBX")]
    public class FBXExport : AddInPlugin
    {
        //путь для сохранения FBX
        public static string FBXSavePath { get; set; }

        //имя для файла FBX
        public static string FBXFileName { get; set; }

        //выводить окна
        public static bool ManualUse { get; set; } = true;

        private static HashSet<string> notReliableClassDisplayNames =
            new HashSet<string>()
        {
            "Insert",
        };


        public override int Execute(params string[] parameters)
        {
            Win.MessageBoxResult result = Win.MessageBoxResult.Yes;
            if (ManualUse)
            {
                result = Win.MessageBox.Show("Перед экспортом FBX, нужно скрыть те элементы модели, которые не нужно экспортировать. "
                + "А так же нужно настроить параметры экспорта в FBX на экспорт ЛИБО В ФОРМАТЕ ASCII, ЛИБО В ДВОИЧНОМ ФОРМАТЕ ВЕРСИИ НЕ НОВЕЕ 2018. "
                + "Рекомендуется так же отключить экспорт источников света и камер. "
                + "\n\nНачать выгрузку FBX?", "Выгрузка FBX", Win.MessageBoxButton.YesNo);
            }



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
                        string fbxFullFileName = null;
                        if (ManualUse)
                        {
                            WinForms.SaveFileDialog sFD = new WinForms.SaveFileDialog();
                            sFD.InitialDirectory = fbxPath;
                            sFD.Filter = "fbx files (*.fbx)|*.fbx";
                            sFD.FilterIndex = 1;
                            sFD.RestoreDirectory = true;
                            if (!String.IsNullOrWhiteSpace(defFBXFileName))
                                sFD.FileName = defFBXFileName;
                            sFD.Title = "Укажите файл для записи fbx";
                            if (sFD.ShowDialog() == WinForms.DialogResult.OK)
                                fbxFullFileName = sFD.FileName;
                        }
                        else
                        {
                            fbxFullFileName = FBXSavePath;
                            FileAttributes attr = File.GetAttributes(fbxFullFileName);
                            if (attr.HasFlag(FileAttributes.Directory))
                            {
                                //добавить имя файла
                                fbxFullFileName = Path.Combine(fbxFullFileName, FBXFileName);
                            }
                        }


                        if (!String.IsNullOrEmpty(fbxFullFileName))
                        {
                            string notEditedDirectory = Path.Combine(Path.GetDirectoryName(fbxFullFileName), "NotEdited");
                            if (!Directory.Exists(notEditedDirectory))
                            {
                                Directory.CreateDirectory(notEditedDirectory);
                            }
                            string notEditedFileName = Path.Combine(notEditedDirectory,
                                Path.GetFileName(fbxFullFileName));

                            if (ManualUse)
                            {
                                BusyIndicatorHelper.ShowBusyIndicator();
                                BusyIndicatorHelper.SetMessage("Стандартный экспорт FBX");
                            }
                                

                            if (FBXplugin.Execute(notEditedFileName) == 0)//Выполнить экспорт в FBX
                            {
                                if (ManualUse)
                                {
                                    BusyIndicatorHelper.SetMessage("Редактирование FBX");
                                }

                                bool isASCII = IsASCIIFBXFile(notEditedFileName);
                                if (isASCII || GetBinaryVersionNum(notEditedFileName) <= 7500)
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
                                    if (IsASCIIFBXFile(notEditedFileName))
                                    {
                                        fbxEditor = new FBX.ASCIIModelNamesEditor(notEditedFileName, replacements);
                                    }
                                    else /*if (GetBinaryVersionNum(sFD.FileName) <= 7500)*/
                                    {
                                        fbxEditor = new FBX.BinaryModelNamesEditor(notEditedFileName, replacements);
                                    }
                                    fbxEditor.FbxFileNameEdited = fbxFullFileName;
                                    fbxEditor.EditModelNames();

                                    

                                    if (ManualUse)
                                    {
                                        BusyIndicatorHelper.CloseBusyIndicator();
                                        Win.MessageBox.Show("Файл FBX с отредактированными именами моделей - " + fbxEditor.FbxFileNameEdited,
                                        "Готово", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                                    }
                                        
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


                            if (ManualUse)
                            {
                                //на всякий случай
                                BusyIndicatorHelper.CloseBusyIndicator();
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

                string baseName, exportName, replacementName;
                bool baseNameTrustable;
                object id;
                CreateReplacementName(oState, item, out baseName, out exportName, out baseNameTrustable, out replacementName, out id, true);

                replacements.Enqueue(new FBX.NameReplacement(baseName, baseNameTrustable, replacementName));

                NameReplacementQueue(item.Children, replacements, oState);
            }
        }

        /// <summary>
        /// Создает имя с id для экспорта в fbx
        /// 
        /// </summary>
        /// <param name="oState"></param>
        /// <param name="item"></param>
        /// <param name="baseName"></param>
        /// <param name="baseNameTrustable"></param>
        /// <param name="replacementName"></param>
        /// <param name="id"></param>
        /// <param name="createIdIfNotExists">ЕСЛИ У ОБЪЕКТА НЕТ ID, ТО ОН БУДЕТ СОЗДАН</param>
        public static void CreateReplacementName(ComApi.InwOpState3 oState, ModelItem item,
            out string baseName, out string exportName, out bool baseNameTrustable, out string replacementName, out object id,
            bool createIdIfNotExists)
        {
            DataProperty idProp = item.PropertyCategories
                                .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                                ID_PROP_DISPLAY_NAME);
            DataProperty matIdProp = item.PropertyCategories
                .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                MATERIAL_ID_PROP_DISPLAY_NAME);

            exportName = null;
            DataProperty exportNameProp = item.PropertyCategories
                .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                PROPER_NAME_PROP_DISPLAY_NAME);
            if (exportNameProp != null)
            {
                exportName = Utils.GetDisplayValue(exportNameProp.Value);
            }

            if (idProp == null || matIdProp == null)
            {
                if (createIdIfNotExists)
                {
                    //Запускать простановку id перед выгрузкой в fbx, чтобы не решать проблему, когда частично не проставлены id
                    Utils.SetS1NF0PropsToItem(oState, item,
                    new Dictionary<string, object>()
                    {
                    { ID_PROP_DISPLAY_NAME, Utils.S1NF0PropSpecialValue.RandomGUID},
                    { MATERIAL_ID_PROP_DISPLAY_NAME, "_"}
                    }, new Dictionary<string, bool>());//ничего не переписывать

                    idProp = item.PropertyCategories
                        .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                        ID_PROP_DISPLAY_NAME);
                    matIdProp = item.PropertyCategories
                        .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME,
                       MATERIAL_ID_PROP_DISPLAY_NAME);
                }
            }


            //Один элемент в FBX должен быть переименован

            //item.DisplayName возвращает путую строку в 2 случаях:
            //- Если свойства имя нет
            //- Если имя - пустая строка
            //string baseName = item.DisplayName;
            //Проверить есть ли у элемента свойство имя
            //Если это свойство есть и оно не пустое, то имя узла в FBX будет таким же
            //Если этого свойства нет, то в большинстве случаев имя узла в FBX совпадет с item.ClassDisplayName
            baseName = null;
            DataProperty nameProp = item.PropertyCategories.FindPropertyByName("LcOaNode", "LcOaSceneBaseUserName");
            if (nameProp != null)
            {
                baseName = nameProp.Value.ToDisplayString();
            }

            baseNameTrustable = true;
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

            if (idProp != null && matIdProp != null)
            {
                id = Utils.GetDisplayValue(idProp.Value);
                replacementName = (!String.IsNullOrWhiteSpace(exportName) ? exportName : baseName)
                    + "|" + id + "|" + Utils.GetDisplayValue(matIdProp.Value);
            }
            else
            {
                id = null;
                replacementName = null;
            }
            

            return;
        }

        /// <summary>
        /// FBX файл в формате ASCII?
        /// </summary>
        /// <param name="fbxFilename"></param>
        /// <returns></returns>
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
        /// Прочитать версию из бинарного файла FBX
        /// </summary>
        /// <param name="fbxFilename"></param>
        /// <returns></returns>
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
