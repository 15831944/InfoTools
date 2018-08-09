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

namespace NavisWorksInfoTools
{
    /// <summary>
    /// http://adndevblog.typepad.com/aec/2013/03/add-custom-properties-to-all-desired-model-items.html
    /// </summary>
    [Plugin("SetProps",
        "S-Info",
        ToolTip = "Заполнить атрибуты",
        DisplayName = "Заполнить атрибуты")]
    public class SetProps : AddInPlugin
    {

        public static string DefTabName {get;} = "АТРИБУТЫ";

        private string existantTabName = null;

        public override int Execute(params string[] parameters)
        {
            existantTabName = null;
            try
            {
                //get state object of COM API
                ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;


                Document doc = Application.ActiveDocument;

                ModelItemCollection selection = doc.CurrentSelection.SelectedItems;

                if (selection.Count > 0)
                {
                    //Получить список свойств из первого выбранного объекта из пользовательской панели если она есть
                    ModelItem sample = selection.First;


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




                    List<DisplayProperty> props = new List<DisplayProperty>();
                    List<DisplayURL> urls = new List<DisplayURL>();

                    //Поиск среди выбранных объектов и их потомков первого объекта,
                    //1 - который содержит пользовательские свойства
                    //2 - который содержит ссылки
                    bool userDataFound = false;
                    bool linksFound = false;
                    foreach (ModelItem item in selection.DescendantsAndSelf)
                    {
                        //Поиск категории пользовательских данных
                        if (!userDataFound)
                        {
                            PropertyCategory userDataCat
                            = item.PropertyCategories.FindCategoryByName("LcOaPropOverrideCat");
                            if (userDataCat != null)
                            {
                                foreach (DataProperty dp in userDataCat.Properties)
                                {
                                    if (dp.Value.IsDisplayString || dp.Value.IsIdentifierString)
                                    {
                                        existantTabName = userDataCat.DisplayName;
                                        props.Add(new DisplayProperty() { DisplayName = dp.DisplayName, Value = dp.Value.ToDisplayString() });
                                    }
                                }
                                userDataFound = true;
                            }
                        }
                        //Поиск категории ссылок
                        if (!linksFound)
                        {
                            PropertyCategory linksCat
                               = item.PropertyCategories.FindCategoryByName("LcOaExURLAttribute");
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
                                        DisplayURL displayURL = new DisplayURL()
                                        {
                                            DisplayName = nameProp.Value.ToDisplayString(),
                                            URL = urlProp.Value.ToDisplayString()
                                        };
                                        urls.Add(displayURL);
                                    }
                                }

                                linksFound = true;
                            }

                        }
                        if (userDataFound && linksFound)
                            break;
                    }

                    SetPropsWindow setPropsWindow = new SetPropsWindow(props, urls);
                    if (existantTabName != null)
                    {
                        setPropsWindow.TabName = existantTabName;
                    }

                    bool? result = setPropsWindow.ShowDialog();
                    if (result != null && result.Value)
                    {
                        //Удалить пустые строки из наборов
                        setPropsWindow.Props.RemoveAll(dp => String.IsNullOrEmpty(dp.DisplayName));
                        setPropsWindow.URLs.RemoveAll(dUrl => 
                        String.IsNullOrEmpty(dUrl.DisplayName) || String.IsNullOrEmpty(dUrl.URL));

                        //Заполнены ли списки свойств и гиперссылок?
                        //Если список пустой, то соответствующая панель должна быть удалена
                        bool userPropsDefined = setPropsWindow.Props.Count > 0;
                        //bool urlsDefined = setPropsWindow.URLs.Count > 0;

                        // create new property category
                        // (new tab in the properties dialog)
                        ComApi.InwOaPropertyVec newPvec = (ComApi.InwOaPropertyVec)oState
                            .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

                        foreach (DisplayProperty dp in setPropsWindow.Props)
                        {
                            if (!String.IsNullOrEmpty(dp.DisplayName))
                            {
                                // create new property
                                ComApi.InwOaProperty newP = (ComApi.InwOaProperty)oState
                                    .ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaProperty, null, null);

                                // set the name, username and value of the new property
                                newP.name = Guid.NewGuid().ToString();
                                newP.UserName = dp.DisplayName;
                                if (String.IsNullOrEmpty(dp.Value))
                                    dp.Value = "_";
                                newP.value = dp.Value;

                                // add the new property to the new property category
                                newPvec.Properties().Add(newP);
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

                        foreach (ModelItem item in selection.DescendantsAndSelf)
                        {
                            //convert the .NET collection to COM object
                            ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);


                            //Переделать панель атрибутов в соответствии с заполненными строками в окне
                            if (setPropsWindow.OverwriteUserAttr)//Только если стояла галка в окне!!!
                            {
                                // get properties collection of the path
                                ComApi.InwGUIPropertyNode2 propn = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);
                                try
                                { propn.RemoveUserDefined(0); }
                                catch (System.Runtime.InteropServices.COMException) { }
                                if (userPropsDefined)
                                {
                                    string tabName = DefTabName;
                                    if (!String.IsNullOrEmpty(setPropsWindow.TabName))
                                        tabName = setPropsWindow.TabName;
                                    //propn.SetUserDefined(0, "АТРИБУТЫ", tabName, newPvec);
                                    propn.SetUserDefined(0, tabName, "S1NF0", newPvec);
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

    public class DisplayProperty
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }
    }

    public class DisplayURL
    {
        public string DisplayName { get; set; }

        public string URL { get; set; }
    }
}
