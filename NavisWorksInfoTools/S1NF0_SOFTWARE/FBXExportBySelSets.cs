using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NavisWorksInfoTools.Constants;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using WinForms = System.Windows.Forms;
using Win = System.Windows;
using System.IO;

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    [Plugin("FBXExportBySelSets",
            DEVELOPER_ID,
            ToolTip = "Выполнить выгрузку в FBX по сохраненным наборам выбора",
            DisplayName = S1NF0_APP_NAME + ". 2. FBX по наборам выбора")]
    public class FBXExportBySelSets : AddInPlugin
    {

        public override int Execute(params string[] parameters)
        {
            Win.MessageBoxResult result
                = Win.MessageBox.Show("Перед экспортом FBX, нужно настроить параметры экспорта в "
                + "FBX на экспорт ЛИБО В ФОРМАТЕ ASCII, ЛИБО В ДВОИЧНОМ ФОРМАТЕ ВЕРСИИ НЕ НОВЕЕ 2018. "
                + "Рекомендуется так же отключить экспорт источников света и камер. "
                + "\n\nНачать выгрузку FBX?", "Выгрузка FBX", Win.MessageBoxButton.YesNo);
            if (result == Win.MessageBoxResult.Yes)
            {
                try
                {
                    Document doc = Application.ActiveDocument;
                    DocumentSelectionSets selectionSets = doc.SelectionSets;
                    FolderItem rootFolderItem = selectionSets.RootItem;
                    List<FolderItem> folders = new List<FolderItem>();
                    foreach (SavedItem item in rootFolderItem.Children)
                    {
                        if (item is FolderItem)
                        {
                            folders.Add(item as FolderItem);
                        }
                    }

                    if (folders.Count == 0)
                    {
                        WinForms.MessageBox.Show("Для работы этой команды необходимо наличие папок с сохраненными наборами выбора",
                            "Отменено", WinForms.MessageBoxButtons.OK);
                        return 0;
                    }

                    //Вывести окно для выбора корневой папки для формирования структуры
                    SelectRootFolderWindow selectRootFolderWindow = new SelectRootFolderWindow(folders, false);
                    bool? resultRootSelect = selectRootFolderWindow.ShowDialog();
                    if (resultRootSelect != null && resultRootSelect.Value)
                    {
                        string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string docPath = doc.FileName;
                        if (!String.IsNullOrEmpty(docPath))
                        {
                            initialPath = Path.GetDirectoryName(docPath);
                        }


                        WinForms.FolderBrowserDialog fbd = new WinForms.FolderBrowserDialog();
                        fbd.Description = "Выберите расположение файлов FBX";
                        fbd.ShowNewFolderButton = true;
                        fbd.SelectedPath = initialPath;
                        WinForms.DialogResult fbdResult = fbd.ShowDialog();
                        if (fbdResult == WinForms.DialogResult.OK)
                        {
                            FolderItem rootFolder = selectRootFolderWindow.RootFolder;
                            FBXExport.ManualUse = false;
                            FBXExport.FBXSavePath = fbd.SelectedPath;
                            ExportBySelSets(rootFolder, doc);

                            Win.MessageBox.Show("Готово", "Готово", Win.MessageBoxButton.OK,
                                Win.MessageBoxImage.Information);
                        }

                            
                    }
                }
                catch (Exception ex)
                {
                    CommonException(ex, "Ошибка при экспорте объектов по отдельным наборам выбора");
                }
                finally
                {
                    FBXExport.ManualUse = true;
                    FBXExport.FBXSavePath = null;
                    FBXExport.FBXFileName = null;
                }
            }

            

            return 0;
        }


        private void ExportBySelSets(SavedItem item, Document doc)
        {
            if (item is FolderItem)
            {
                //рекурсивный вызов для каждого вложенного
                FolderItem folder = item as FolderItem;
                foreach (SavedItem nestedItem in folder.Children)
                {
                    ExportBySelSets(nestedItem, doc);
                }
            }
            else if (item is SelectionSet)
            {
                SelectionSet selectionSet = item as SelectionSet;
                ModelItemCollection itemsToExport = selectionSet.GetSelectedItems();
                if (itemsToExport.DescendantsAndSelf.Count()>0)
                {
                    ModelItemCollection itemsToHide = new ModelItemCollection(itemsToExport);
                    itemsToHide.Invert(doc);
                    doc.Models.SetHidden(itemsToHide, true);
                    doc.Models.SetHidden(itemsToExport, false);

                    FBXExport.FBXFileName = selectionSet.DisplayName + ".fbx";
                    S1NF0_RibbonPanel.ExecuteAddInPlugin("FBXExport." + DEVELOPER_ID);

                    doc.Models.SetHidden(itemsToHide, false);
                }
                
            }
        }

    }
}
