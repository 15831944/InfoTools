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
using static NavisWorksInfoTools.Constants;


namespace NavisWorksInfoTools
{
    [Plugin("ChangeAllLinks",
        DEVELOPER_ID,
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
                                bool changed = false;//становится true если была поменяна хотябы 1 ссылка
                                foreach (ComApi.InwURL2 url in oURLColl)
                                {
                                    //Проверять исходный URL
                                    string initialUrl = url.URL;

                                    if (!String.IsNullOrEmpty(initialUrl))
                                    {
                                        string newUrl = null;
                                        if (changeLinksPropsWindow.ChangeAllUrls)
                                        {
                                            //Нужно заменить целиком весь путь до файла
                                            char slash = '\\';
                                            if (initialUrl.Contains("/"))
                                            {
                                                slash = '/';
                                            }
                                            List<string> temp = initialUrl.Split(slash).ToList();
                                            temp.RemoveAt(temp.Count - 1);
                                            string fileName = initialUrl.Split(slash).Last();
                                            newUrl = changeLinksPropsWindow.NewUrlFragment + slash + fileName;
                                        }
                                        else if (initialUrl.Contains(changeLinksPropsWindow.OldUrlFragment))
                                        {
                                            //Если путь содержит подстроку, введенную в окне, то нужно заменить эту подстроку
                                            newUrl = initialUrl.Replace(changeLinksPropsWindow.OldUrlFragment, changeLinksPropsWindow.NewUrlFragment);
                                        }

                                        if (newUrl != null)
                                        {
                                            url.URL = newUrl;
                                            changed = true;
                                        }





                                        /*
                                        //Получение директории по-разному для локальных путей и для интернета
                                        string initialDir = null;
                                        char slash = '\\';
                                        if (initialUrl.Contains("/"))
                                        {
                                            slash = '/';
                                            Uri uri = new Uri(initialUrl);
                                            Uri initialDirUri = new Uri(uri, ".");
                                            initialDir = initialDirUri.ToString().TrimEnd('/');
                                        }
                                        else
                                        {
                                            //Записанный путь может содержать недопустимые символы из-за которых вываливается ошибка в методе GetDirectoryName
                                            //initialDir = Path.GetDirectoryName(initialUrl);//выдает ошибку
                                            List<string> temp = initialUrl.Split('\\').ToList();
                                            temp.RemoveAt(temp.Count - 1);
                                            initialDir = String.Join("\\", temp.ToArray());
                                        }

                                        string oldUrlToCompare = changeLinksPropsWindow.OldUrl;


                                        //string fileName = Path.GetFileName(initialUrl);//выдает ошибку
                                        string fileName = initialUrl.Split(slash).Last();

                                        if (changeLinksPropsWindow.ChangeAllUrls
                                            || oldUrlToCompare
                                            //.StartsWith(initialDir)
                                            .Equals(initialDir)
                                            )
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
                                        */
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
