using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Win = System.Windows;


namespace NavisWorksInfoTools
{
    [Plugin("ChangeAllLinks",
        "S-Info",
        ToolTip = "Поменять все ссылки",
        DisplayName = "Поменять все ссылки")]
    public class ChangeAllLinks : AddInPlugin
    {
        /// <summary>
        /// Замена и добавление ссылок только через COM - http://adndevblog.typepad.com/aec/2012/05/create-hyperlinks-for-model-objects-using-net-api.html
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public override int Execute(params string[] parameters)
        {
            try
            {
                ChangeLinksProps changeLinksPropsWindow = new ChangeLinksProps();
                bool? result = changeLinksPropsWindow.ShowDialog();
                if (result != null && result.Value)
                {
                    Document oDoc = Application.ActiveDocument;

                    ModelItemEnumerableCollection allItems = oDoc.Models.RootItemDescendantsAndSelf;

                    ComApi.InwOpState10 state;
                    state = ComApiBridge.ComApiBridge.State;

                    foreach (ModelItem item in allItems)
                    {
                        DataProperty urlProp = item.PropertyCategories.FindPropertyByName("LcOaExURLAttribute", "LcOaURLAttributeURL");

                        if (urlProp != null)
                        {
                            ComApi.InwOaPath p_path = ComApiBridge.ComApiBridge.ToInwOaPath(item);
                            try
                            {
                                ComApi.InwURLOverride urlOverride = state.GetOverrideURL(p_path);
                                ComApi.InwURLColl oURLColl = urlOverride.URLs();
                                bool changed = false;
                                foreach (ComApi.InwURL2 url in oURLColl)
                                {
                                    //Проверять исходный URL
                                    string initialUrl = url.URL;
                                    if (!String.IsNullOrEmpty(initialUrl))
                                    {
                                        //Получение директории по-разному для локальных путей и для интернета
                                        string initialDir = null;
                                        if (initialUrl.Contains("/"))
                                        {
                                            Uri uri = new Uri(initialUrl);
                                            Uri initialDirUri = new Uri(uri, ".");
                                            initialDir = initialDirUri.ToString().TrimEnd('/');
                                        }
                                        else
                                        {
                                            initialDir = Path.GetDirectoryName(initialUrl);
                                        }

                                        string fileName = Path.GetFileName(initialUrl);
                                        if (changeLinksPropsWindow.ChangeAllUrls
                                            || changeLinksPropsWindow.OldUrl.Equals(initialDir))
                                        {
                                            //Разделитель может быть либо прямым либо обратным слешем
                                            string divider = "/";
                                            string newUrl = changeLinksPropsWindow.NewUrl;
                                            if (newUrl.Contains("\\"))
                                            {
                                                divider = "\\";
                                            }

                                            url.URL = newUrl + divider + fileName;
                                            changed = true;
                                        }
                                    }

                                }
                                if (changed)
                                {
                                    ComApi.InwOpSelection comSelectionOut =
                                        ComApiBridge.ComApiBridge.ToInwOpSelection(new ModelItemCollection() { item });
                                    state.SetOverrideURL(comSelectionOut, urlOverride);
                                }
                            }
                            catch (System.Runtime.InteropServices.COMException)
                            { }

                        }
                    }

                    state.URLsEnabled = true;

                    Win.MessageBox.Show("Готово", "Готово", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при замене ссылок в Navis");
            }

            return 0;
        }
    }
}
