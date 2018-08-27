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
    /// <summary>
    /// http://adndevblog.typepad.com/aec/2013/03/add-custom-properties-to-all-desired-model-items.html
    /// http://adndevblog.typepad.com/aec/2012/08/addmodifyremove-custom-attribute-using-com-api.html
    /// </summary>
    [Plugin("SetProps",
        "S-Info",
        ToolTip = "Заполнить атрибуты",
        DisplayName = "Заполнить атрибуты")]
    public class SetProps : AddInPlugin
    {
        /// <summary>
        /// Свойства, которые не должны изменяться с помощью этой команды
        /// </summary>
        private static HashSet<string> propsNotModifiable = new HashSet<string>()
        {
            ID_PROP_DISPLAY_NAME,
            PROPER_NAME_PROP_DISPLAY_NAME,
            PARENT_PROP_DISPLAY_NAME
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
                        }
                    }




                    List<DisplayDataTab> dataTabs = new List<DisplayDataTab>();
                    List<DisplayURL> urls = new List<DisplayURL>();

                    //Поиск среди выбранных объектов и их потомков первого объекта,
                    //1 - который содержит пользовательские свойства
                    //2 - который содержит ссылки
                    Search userDataSearch = new Search();
                    userDataSearch.Selection.CopyFrom(currSelectionColl);
                    userDataSearch.SearchConditions.Add(SearchCondition
                        .HasCategoryByName("LcOaPropOverrideCat"));
                    ModelItem itemWithUserData = userDataSearch.FindFirst(false);
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
                    ModelItem itemWithLinks = linksSearch.FindFirst(false);
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



                    SetPropsWindow setPropsWindow = new SetPropsWindow(dataTabs, urls);

                    bool? result = setPropsWindow.ShowDialog();
                    if (result != null && result.Value)
                    {

                        //Удалить пустые строки из наборов
                        setPropsWindow.DataTabs.RemoveAll(ddt => String.IsNullOrEmpty(ddt.DisplayName));
                        foreach (DisplayDataTab ddt in setPropsWindow.DataTabs)
                        {
                            ddt.DisplayProperties.RemoveAll(dp => String.IsNullOrEmpty(dp.DisplayName));
                        }
                        setPropsWindow.URLs.RemoveAll(dUrl =>
                        String.IsNullOrEmpty(dUrl.DisplayName) || String.IsNullOrEmpty(dUrl.URL));

                        //Если пользователь зачем-то ввел значение нередактируемого свойства, то убрать его
                        DisplayDataTab idDataTab
                            = setPropsWindow.DataTabs.Find(ddt => ddt.DisplayName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME));
                        
                        if (idDataTab != null)
                        {
                            idDataTab.DisplayProperties.RemoveAll(p
                                => propsNotModifiable.Contains(p.DisplayName));
                        }

                        //Конвертировать значения всех свойств
                        foreach (DisplayDataTab ddt in setPropsWindow.DataTabs)
                        {
                            foreach (DisplayProperty dp in ddt.DisplayProperties)
                            {
                                dp.ConvertValue();
                            }
                        }

                        //Как обеспечить уникальность каждого имени свойства и имени панели еще на этапе ввода??
                        //Обнаружилось, что одинаковые имена свойств не вызывают никаких проблем.


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

                        //Заполнены ли списки свойств?
                        //Если список пустой, то панели должны быть удалена
                        bool userPropsDefined = setPropsWindow.DataTabs.Count > 0;
                        //bool urlsDefined = setPropsWindow.URLs.Count > 0;


                        //Создание набора для присоединения к объектам модели
                        List<DisplayDataTab> propsToSet = new List<DisplayDataTab>();

                        foreach (DisplayDataTab ddt in setPropsWindow.DataTabs)
                        {
                            if (!ddt.DisplayName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME))
                            {
                                ddt.InwOaPropertyVec = (ComApi.InwOaPropertyVec)oState
                                        .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec,
                                        null, null);
                                propsToSet.Add(ddt);
                                foreach (DisplayProperty dp in ddt.DisplayProperties)
                                {
                                    ComApi.InwOaProperty newP
                                            = Utils.CreateNewUserProp(oState, dp.DisplayName, dp.Value);
                                    // add the new property to the new property category
                                    ddt.InwOaPropertyVec.Properties().Add(newP);
                                }
                            }
                        }


                        //Создание набора ссылок для привязки к объектам
                        ComApi.InwOpState10 state = ComApiBridge.ComApiBridge.State;
                        ComApi.InwURLOverride urlOverride
                            = (ComApi.InwURLOverride)state
                            .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwURLOverride, null, null);
                        ComApi.InwURLColl oURLColl = urlOverride.URLs();
                        foreach (DisplayURL dUrl in setPropsWindow.URLs)
                        {
                            ComApi.InwURL2 oUrl = (ComApi.InwURL2)state
                                .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwURL, null, null);
                            oUrl.name = dUrl.DisplayName;
                            oUrl.URL = dUrl.URL;
                            oUrl.SetCategory("Hyperlink", "LcOaURLCategoryHyperlink");//Тип - всегда гиперссылка
                            oURLColl.Add(oUrl);
                        }

                        foreach (ModelItem item in currSelectionColl.DescendantsAndSelf)
                        {
                            //convert the .NET object to COM object
                            ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);


                            //Переделать панель атрибутов в соответствии с заполненными строками в окне
                            //При этом нужно сохранять нестроковые свойства если они были
                            if (setPropsWindow.OverwriteUserAttr)//Только если стояла галка в окне!!!
                            {
                                // get properties collection of the path
                                ComApi.InwGUIPropertyNode2 propn
                                    = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);



                                ComApi.InwOaPropertyVec idDataTabPropertyVecCurr = idDataTabPropertyVec.Copy();

                                //Добавить нередактируемые свойства если они есть в исходном
                                foreach (string dn in propsNotModifiable)
                                {
                                    DataProperty prop = item.PropertyCategories
                                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME, dn);

                                    if (prop!=null)
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
                            }




                            //Переделать все ссылки в соответствии с заполненными строками в окне
                            if (setPropsWindow.OverwriteLinks)//Только если стояла галка в окне!!!
                            {
                                ComApi.InwOpSelection comSelectionOut =
                                        ComApiBridge.ComApiBridge.ToInwOpSelection(new ModelItemCollection() { item });
                                state.SetOverrideURL(comSelectionOut, urlOverride);
                            }

                        }

                    }
                }


            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при заполнении атрибутов в Navis");
            }



            return 0;
        }



    }


}
