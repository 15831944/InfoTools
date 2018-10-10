using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using static NavisWorksInfoTools.Constants;

namespace NavisWorksInfoTools
{
    //TODO: Запретить на этапе ввода свойств ввод 2 свойств с одинаковыми именами!!!


    /// <summary>
    /// http://adndevblog.typepad.com/aec/2013/03/add-custom-properties-to-all-desired-model-items.html
    /// http://adndevblog.typepad.com/aec/2012/08/addmodifyremove-custom-attribute-using-com-api.html
    /// </summary>
    [Plugin("SetProps",
        DEVELOPER_ID,
        ToolTip = "Заполнить атрибуты",
        DisplayName = "Заполнить атрибуты")]
    public class SetProps : AddInPlugin
    {
        /// <summary>
        /// Свойства, которые не должны изменяться с помощью этой команды
        /// Это могут быть только свойства S1NF0
        /// Эти свойства не отображаются в окне
        /// </summary>
        public static readonly HashSet<string> propsNotModifiable = new HashSet<string>()
        {
            ID_PROP_DISPLAY_NAME,
            //PROPER_NAME_PROP_DISPLAY_NAME,
            //PARENT_PROP_DISPLAY_NAME
        };


        public override int Execute(params string[] parameters)
        {
            try
            {
                //get state object of COM API
                ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;


                Document doc = Application.ActiveDocument;

                ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;

                if (currSelectionColl.Count > 0)
                {
                    //Получить список свойств из первого выбранного объекта из пользовательской панели если она есть
                    ModelItem sample = currSelectionColl.First;


                    Debug.Print("\n[Item Display Name]: " + sample.DisplayName + "\n");
                    foreach (PropertyCategory oPC in sample.PropertyCategories)
                    {
                        Debug.Print("\n[Display Name]: " + oPC.DisplayName + "  [Internal Name]: " + oPC.Name + "\n");
                        Debug.Print("\tProperties\n");
                        foreach (DataProperty oDP in oPC.Properties)
                        {
                            Debug.Print("\t[Display Name]: " + oDP.DisplayName + "\t[Internal Name]:" + oDP.Name + "\t[Value]: " + oDP.Value.ToString());
                            //if (oDP.Value.IsNamedConstant)
                            //{
                            //    NamedConstant namedConstant = oDP.Value.ToNamedConstant();
                            //    int v = namedConstant.Value;
                            //    string bn = namedConstant.BaseName;
                            //    string dn = namedConstant.DisplayName;

                            //    NamedConstant clone = new NamedConstant(v, bn, dn);
                            //    bool eq1 = namedConstant.Equals(clone);
                            //    VariantData vd1 = new VariantData(namedConstant);
                            //    VariantData vd2 = new VariantData(clone);
                            //    bool eq2 = vd1.Equals(vd2);
                            //}
                        }
                    }

                    SetPropsWindow setPropsWindow = ConfigureAndOppenSetPropsWindow(doc, currSelectionColl);
                    bool? result = setPropsWindow.ShowDialog();
                    if (result != null && result.Value)
                    {
                        List<DisplayDataTab> displayDataTabs = setPropsWindow.DataTabs;
                        List<DisplayURL> displayURLs = setPropsWindow.URLs;
                        bool overwriteUserAttr = setPropsWindow.OverwriteUserAttr;
                        bool overwriteLinks = setPropsWindow.OverwriteLinks;
                        bool preserveExistingProperties = setPropsWindow.PreserveExistingProperties;
                        SetPropsMethod(oState, currSelectionColl, displayDataTabs, displayURLs,
                            overwriteUserAttr, overwriteLinks, preserveExistingProperties);

                    }
                }


            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при заполнении атрибутов в Navis");
            }



            return 0;
        }

        public static void SetPropsMethod(ComApi.InwOpState3 oState,
            ModelItemCollection modelItemColl, List<DisplayDataTab> displayDataTabs,
            List<DisplayURL> displayURLs, bool overwriteUserAttr, bool overwriteLinks,
            bool preserveExistingProperties)
        {
            //Удалить пустые строки из наборов
            displayDataTabs.RemoveAll(ddt => String.IsNullOrEmpty(ddt.DisplayName));
            foreach (DisplayDataTab ddt in displayDataTabs)
            {
                ddt.DisplayProperties.RemoveAll(dp => String.IsNullOrEmpty(dp.DisplayName));
            }
            displayURLs.RemoveAll(dUrl =>
            String.IsNullOrEmpty(dUrl.DisplayName) || String.IsNullOrEmpty(dUrl.URL));

            //Если пользователь зачем-то ввел значение нередактируемого свойства, то убрать его
            DisplayDataTab idDataTab
                = displayDataTabs.Find(ddt => ddt.DisplayName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME));

            if (idDataTab != null)
            {
                idDataTab.DisplayProperties.RemoveAll(p
                    => propsNotModifiable.Contains(p.DisplayName));
            }

            //Конвертировать значения всех свойств
            foreach (DisplayDataTab ddt in displayDataTabs)
            {
                foreach (DisplayProperty dp in ddt.DisplayProperties)
                {
                    dp.ConvertValue();
                }
            }


            #region Старое
            /*
                //Заполнить список свойств, которые нужно будет добавить на панель Id
                ComApi.InwOaPropertyVec idDataTabPropertyVec = (ComApi.InwOaPropertyVec)oState
                                .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec,
                                null, null);

                //Заполнить список свойств для панели Id
                //(в ней хранятся уникальные Id для каждого элемента, которые не должны меняться этой командой)
                if (idDataTab != null)
                {
                    foreach (DisplayProperty dp in idDataTab.DisplayProperties)
                    {
                        idDataTabPropertyVec.Properties()
                            .Add(Utils.CreateNewUserProp(oState, dp.DisplayName, dp.Value));
                    }
                }
                */

            //Заполнены ли списки свойств?
            //Если список пустой, то панели должны быть удалена
            //bool userPropsDefined = displayDataTabs.Count > 0;


            //Создание набора для присоединения к объектам модели
            //List<DisplayDataTab> propsToSet = new List<DisplayDataTab>(); 
            #endregion

            //Словарь свойств, которые добавляются
            Dictionary<string, DisplayProperty> propsDefined = new Dictionary<string, DisplayProperty>();

            //Создать базовые наборы свойств, которые будут привязываться к объектам
            foreach (DisplayDataTab ddt in displayDataTabs)
            {
                //if (!ddt.DisplayName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME))
                //{
                //ddt.InwOaPropertyVec = (ComApi.InwOaPropertyVec)oState
                //        .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec,
                //        null, null);
                //propsToSet.Add(ddt);
                foreach (DisplayProperty dp in ddt.DisplayProperties)
                {
                    //ComApi.InwOaProperty newP
                    //        = Utils.CreateNewUserProp(oState, dp.DisplayName, dp.Value);
                    //// add the new property to the new property category
                    //ddt.InwOaPropertyVec.Properties().Add(newP);

                    string key = ddt.DisplayName + dp.DisplayName;
                    if (!propsDefined.ContainsKey(key))
                        propsDefined.Add(key, dp);
                }
                //}
            }

            //словарь ссылок, которые добавляются
            Dictionary<string, string> linksDefined = new Dictionary<string, string>();

            //Создание набора ссылок для привязки к объектам
            //ComApi.InwOpState10 state = ComApiBridge.ComApiBridge.State;
            //ComApi.InwURLOverride urlOverride
            //    = (ComApi.InwURLOverride)state
            //    .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwURLOverride, null, null);
            //ComApi.InwURLColl oURLColl = urlOverride.URLs();
            foreach (DisplayURL dUrl in displayURLs)
            {
                //ComApi.InwURL2 oUrl = (ComApi.InwURL2)state
                //    .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwURL, null, null);
                //oUrl.name = dUrl.DisplayName;
                //oUrl.URL = dUrl.URL;
                //oUrl.SetCategory("Hyperlink", "LcOaURLCategoryHyperlink");//Тип - всегда гиперссылка
                //oURLColl.Add(oUrl);

                string key = dUrl.DisplayName;
                if (!linksDefined.ContainsKey(key))
                    linksDefined.Add(key, dUrl.URL);
            }


            foreach (ModelItem item in modelItemColl.DescendantsAndSelf)
            {
                //convert the .NET object to COM object
                ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);


                //Переделать панель атрибутов в соответствии с заполненными строками в окне
                if (overwriteUserAttr)//Только если стояла галка в окне!!!
                {
                    //наборы свойств
                    //ключ - имя панели, значение - набор свойств для привязки к объекту
                    Dictionary<string, ComApi.InwOaPropertyVec> propVectorsCurr
                        = new Dictionary<string, ComApi.InwOaPropertyVec>();
                    // Сначала скопировать базовые наборы свойств для каждой из заданных панелей в словарь
                    //foreach (DisplayDataTab ddt in displayDataTabs)
                    //{
                    //    propVectorsCurr.Add(ddt.DisplayName, ddt.InwOaPropertyVec.Copy());
                    //}

                    //Изучаются текущие свойства объекта модели
                    //Сначала в наборы свойств нужно добавить если присутствуют:
                    //- свойства, которые не редактируются данной командой
                    //- если нажата галка "Не удалять свойства", то любые свойства, которых не было в окне SetProps
                    //- свойства которые были заданы в окне SetProps, но они уже присутствуют в модели 
                    //  (их значение задается как введено в окне,
                    //  если эти свойства добавлены на этом этапе, то они не должны добавляться на следующем)
                    HashSet<string> alreadyAdded = new HashSet<string>();//набор ключей свойств, которые добавлены на этом этапе

                    ComApi.InwGUIPropertyNode2 propn
                        = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);
                    foreach (ComApi.InwGUIAttribute2 attr in propn.GUIAttributes())
                    {
                        if (attr.UserDefined)
                        {
                            foreach (ComApi.InwOaProperty prop in attr.Properties())
                            {
                                string key = attr.ClassUserName + prop.UserName;

                                if (
                                    (attr.ClassUserName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME) && propsNotModifiable.Contains(prop.UserName))//- свойства, которые не редактируются данной командой
                                    ||
                                    (preserveExistingProperties && !propsDefined.ContainsKey(key))//- если нажата галка Не удалять свойства, то любые свойства, которых не было в окне
                                    ||
                                    propsDefined.ContainsKey(key)//- свойства которые были заданы в окне SetProps, но они уже присутствуют в модели
                                    )
                                {
                                    ComApi.InwOaPropertyVec vec = null;
                                    propVectorsCurr.TryGetValue(attr.ClassUserName, out vec);
                                    if (vec == null)
                                    {
                                        vec = (ComApi.InwOaPropertyVec)oState
                                            .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec,
                                            null, null);
                                        propVectorsCurr.Add(attr.ClassUserName, vec);
                                    }
                                    if (!propsDefined.ContainsKey(key))
                                        vec.Properties().Add(Utils.CopyProp(oState, prop));
                                    else
                                    {
                                        //Учесть введенное значение
                                        DisplayProperty dp = propsDefined[key];
                                        vec.Properties().Add(Utils.CreateNewUserProp(oState, dp.DisplayName, dp.Value));
                                        alreadyAdded.Add(key);
                                    }
                                }
                            }

                        }
                    }

                    //Затем добавить вновь создаваемые свойства, которых ранее не было в модели
                    //(с учетом тех, которые были добавлены на предыдущем этапе)
                    foreach (DisplayDataTab ddt in displayDataTabs)
                    {
                        ComApi.InwOaPropertyVec vec = null;
                        propVectorsCurr.TryGetValue(ddt.DisplayName, out vec);
                        if (vec == null)
                        {
                            vec = (ComApi.InwOaPropertyVec)oState
                                .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec,
                                null, null);
                            propVectorsCurr.Add(ddt.DisplayName, vec);
                        }

                        foreach (DisplayProperty dp in ddt.DisplayProperties)
                        {
                            string key = ddt.DisplayName + dp.DisplayName;
                            if (!alreadyAdded.Contains(key))
                            {
                                ComApi.InwOaProperty newP
                                    = Utils.CreateNewUserProp(oState, dp.DisplayName, dp.Value);
                                // add the new property to the new property category
                                vec.Properties().Add(newP);
                            }
                        }
                    }


                    //Удалить старые панели
                    try
                    { propn.RemoveUserDefined(0); }
                    catch (System.Runtime.InteropServices.COMException) { }
                    //Создать новые
                    foreach (KeyValuePair<string, ComApi.InwOaPropertyVec> kvp in propVectorsCurr)
                    {
                        propn.SetUserDefined(0, kvp.Key, "S1NF0",
                                kvp.Value);
                    }

                    #region Старое
                    /*
                                // get properties collection of the path
                                ComApi.InwGUIPropertyNode2 propn
                                    = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);



                                ComApi.InwOaPropertyVec idDataTabPropertyVecCurr = idDataTabPropertyVec.Copy();

                                //Добавить нередактируемые свойства если они есть в исходном
                                foreach (string dn in propsNotModifiable)
                                {
                                    DataProperty prop = item.PropertyCategories
                                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME, dn);

                                    if (prop != null)
                                    {
                                        ComApi.InwOaProperty copyProp = Utils.CopyProp(oState, prop);
                                        idDataTabPropertyVecCurr.Properties().Add(copyProp);
                                    }
                                }


                                //Удалить старые панели
                                try
                                { propn.RemoveUserDefined(0); }
                                catch (System.Runtime.InteropServices.COMException) { }
                                //Создать новые
                                if (userPropsDefined)
                                {
                                    foreach (DisplayDataTab ddt in propsToSet)
                                    {
                                        //Создание одной панели
                                        propn.SetUserDefined(0, ddt.DisplayName, "S1NF0",
                                            ddt.InwOaPropertyVec);
                                    }

                                    //Создание панели Id
                                    if (idDataTabPropertyVecCurr.Properties().Count > 0)
                                    {
                                        propn.SetUserDefined(0, S1NF0_DATA_TAB_DISPLAY_NAME, "S1NF0",
                                            idDataTabPropertyVecCurr);
                                    }


                                }
                                */
                    #endregion
                }




                //Переделать все ссылки в соответствии с заполненными строками в окне
                if (overwriteLinks)//Только если стояла галка в окне!!!
                {
                    ComApi.InwURLOverride urlOverrideCurr = (ComApi.InwURLOverride)oState
                        .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwURLOverride, null, null);//urlOverride.Copy();

                    //Изучаются текущие ссылки
                    //Сначала в набор ссылок нужно добавить если присутствуют:
                    //- если нажата галка "Не удалять свойства", то любые ссылки, которых не было в окне SetProps
                    //- ссылки которые были заданы в окне SetProps, но они уже присутствуют в модели 
                    //  (их значение задается как введено в окне,
                    //  если эти ссылки добавлены на этом этапе, то они не должны добавляться на следующем)
                    HashSet<string> alreadyAdded = new HashSet<string>();//набор ключей ссылок, которые добавлены на этом этапе

                    PropertyCategory linksCat = item.PropertyCategories.FindCategoryByName("LcOaExURLAttribute");
                    if (linksCat != null)
                    {
                        int linksCount = linksCat.Properties.Count / 3;

                        for (int i = 0; i < linksCount; i++)
                        {
                            string suffix = i == 0 ? "" : i.ToString();
                            DataProperty nameProp = item.PropertyCategories
                                .FindPropertyByName("LcOaExURLAttribute", "LcOaURLAttributeName" + suffix);
                            DataProperty urlProp = item.PropertyCategories
                                .FindPropertyByName("LcOaExURLAttribute", "LcOaURLAttributeURL" + suffix);
                            if (nameProp != null && urlProp != null)
                            {
                                string key = nameProp.Value.ToDisplayString();
                                if ((preserveExistingProperties && !linksDefined.ContainsKey(key))//- если нажата галка "Не удалять свойства", то любые ссылки, которых не было в окне SetProps
                                    ||
                                    (linksDefined.ContainsKey(key)))
                                {
                                    ComApi.InwURL2 oUrl = (ComApi.InwURL2)oState
                                        .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwURL, null, null);
                                    oUrl.name = nameProp.Value.ToDisplayString();
                                    if (!linksDefined.ContainsKey(key))
                                        oUrl.URL = urlProp.Value.ToDisplayString();//Сохранить существующее значение
                                    else
                                    {
                                        oUrl.URL = linksDefined[key];//присвоить заданное в окне значение ссылки
                                        alreadyAdded.Add(key);
                                    }
                                    oUrl.SetCategory("Hyperlink", "LcOaURLCategoryHyperlink");//Тип - всегда гиперссылка

                                    urlOverrideCurr.URLs().Add(oUrl);
                                }
                            }
                        }
                    }


                    foreach (DisplayURL dUrl in displayURLs)
                    {
                        string key = dUrl.DisplayName;
                        if (!alreadyAdded.Contains(key))
                        {
                            ComApi.InwURL2 oUrl = (ComApi.InwURL2)oState
                            .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwURL, null, null);
                            oUrl.name = dUrl.DisplayName;
                            oUrl.URL = dUrl.URL;
                            oUrl.SetCategory("Hyperlink", "LcOaURLCategoryHyperlink");//Тип - всегда гиперссылка
                            urlOverrideCurr.URLs().Add(oUrl);
                        }

                        
                    }


                    ComApi.InwOpSelection comSelectionOut =
                            ComApiBridge.ComApiBridge.ToInwOpSelection(new ModelItemCollection() { item });
                    oState.SetOverrideURL(comSelectionOut, urlOverrideCurr);
                }

            }
        }

        public static SetPropsWindow ConfigureAndOppenSetPropsWindow
            (Document doc, IEnumerable<ModelItem> currSelectionColl)
        {
            List<DisplayDataTab> dataTabs = new List<DisplayDataTab>();
            List<DisplayURL> urls = new List<DisplayURL>();

            //Поиск среди выбранных объектов и их потомков первого объекта,
            //1 - который содержит пользовательские свойства
            //2 - который содержит ссылки
            Search userDataSearch = new Search();
            userDataSearch.Selection.CopyFrom(currSelectionColl);
            userDataSearch.SearchConditions.Add(SearchCondition
                .HasCategoryByName("LcOaPropOverrideCat"));
            ModelItem itemWithUserData = userDataSearch.FindFirst(doc, false);
            if (itemWithUserData != null)
            {
                //Перебор всех пользовательских панелей. Взять данные о каждой
                foreach (PropertyCategory c in itemWithUserData.PropertyCategories)
                {
                    if (c.Name.Equals("LcOaPropOverrideCat"))
                    {
                        DisplayDataTab dataTab = new DisplayDataTab() { DisplayName = c.DisplayName };
                        foreach (DataProperty p in c.Properties)
                        {
                            if ((!c.DisplayName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME)
                                || !propsNotModifiable.Contains(p.DisplayName)))
                            {
                                dataTab.DisplayProperties
                                    .Add(new DisplayProperty()
                                    {
                                        DisplayName = p.DisplayName,
                                        DisplayValue = Utils.GetUserPropValue(p.Value).ToString()
                                    });
                            }

                        }

                        dataTabs.Add(dataTab);
                    }
                }
            }
            Search linksSearch = new Search();
            linksSearch.Selection.CopyFrom(currSelectionColl);
            linksSearch.SearchConditions.Add(SearchCondition
                .HasCategoryByName("LcOaExURLAttribute"));
            ModelItem itemWithLinks = linksSearch.FindFirst(doc, false);
            if (itemWithLinks != null)
            {
                //Взять все ссылки
                PropertyCategory linksCat
                       = itemWithLinks.PropertyCategories.FindCategoryByName("LcOaExURLAttribute");

                int linksCount = linksCat.Properties.Count / 3;

                for (int i = 0; i < linksCount; i++)
                {
                    string suffix = i == 0 ? "" : i.ToString();
                    DataProperty nameProp = itemWithLinks.PropertyCategories
                        .FindPropertyByName("LcOaExURLAttribute", "LcOaURLAttributeName" + suffix);
                    DataProperty urlProp = itemWithLinks.PropertyCategories
                        .FindPropertyByName("LcOaExURLAttribute", "LcOaURLAttributeURL" + suffix);
                    if (nameProp != null && urlProp != null)
                    {
                        DisplayURL displayURL = new DisplayURL()
                        {
                            DisplayName = nameProp.Value.ToDisplayString(),
                            URL = urlProp.Value.ToDisplayString()
                        };
                        urls.Add(displayURL);
                    }
                }
            }

            return new SetPropsWindow(dataTabs, urls);


        }


    }


}
