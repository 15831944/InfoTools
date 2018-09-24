using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Internal.ApiImplementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Win = System.Windows;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System.Diagnostics;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    //[Plugin("FBXExport",
    //    DEVELOPER_ID,
    //    ToolTip = "Экспорт в FBX",
    //    DisplayName = "Экспорт в FBX")]
    class FBXExport0 : AddInPlugin
    {
        //int n = 0;//TEST
        public override int Execute(params string[] parameters)
        {
            //n = 0;//TEST

            Win.MessageBoxResult result = Win.MessageBox.Show("Начать выгрузку FBX по слоям?", "Выгрузка FBX", Win.MessageBoxButton.YesNo);

            if(result == Win.MessageBoxResult.Yes)
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
                        if (!String.IsNullOrEmpty(docPath))
                        {
                            fbxPath = Path.GetDirectoryName(docPath);
                        }

                        DocumentModels docModels = doc.Models;
                        ModelItemEnumerableCollection rootItems = docModels.RootItems;
                        ExportByLayers(rootItems, fbxPath, docModels, FBXplugin);

                        Win.MessageBox.Show("Готово", "Готово", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    CommonException(ex, "Ошибка при экспорте в FBX по слоям");
                }
            }

            return 0;
        }

        /// <summary>
        /// Нужно пройти по дереву до уровня слоев и затем выполнять выгрузку
        /// Слои раскладывать по папкам в соответствии с названиями файлов и вложенностью файлов
        /// </summary>
        /// <param name="items"></param>
        private void ExportByLayers(ModelItemEnumerableCollection items, string fbxPath, DocumentModels docModels, NativeExportPluginAdaptor FBXplugin)
        {

            //if (n < 20)//TEST
            //{
            int n = 0;
            foreach (ModelItem item in items)
            {
                //Скрывать все кроме текущего элемента
                docModels.SetHidden(items, true);
                docModels.SetHidden(new List<ModelItem> { item }, false);
                //item.is

                //Нужно проверить, что у объекта нет свойства путь к источнику
                DataProperty sourceProp = item.PropertyCategories.FindPropertyByName("LcOaNode", "LcOaPartitionSourceFilename");


                if (sourceProp==null)
                {
                    //Выполнять выгрузку
                    if (!Directory.Exists(fbxPath))
                    {
                        Directory.CreateDirectory(fbxPath);
                    }

                    string name = item.DisplayName;
                    if (String.IsNullOrEmpty(name))//Если у объекта нет имени, то подставить цифру
                    {
                        name = n.ToString();
                        n++;
                    }

                    name = name + ".fbx";
                    name = Common.Utils.GetSafeFilename(name);

                    string fileName = Path.Combine(fbxPath, name);
                    if (!File.Exists(fileName))
                    {
                        FBXplugin.Execute(fileName);
                    }

                    //n++;//TEST
                }
                else
                {
                    //Дополнить путь выгрузки новой папкой
                    string name = item.DisplayName;
                    name = Common.Utils.GetSafeFilename(name);
                    name = Path.GetFileNameWithoutExtension(name);
                    string fbxPathToChildren = Path.Combine(fbxPath, name);
                    //Рекурсивный вызов
                    ExportByLayers(item.Children, fbxPathToChildren, docModels, FBXplugin);
                }
            }
            //}


        }


        

    }
}
