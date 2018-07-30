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

        private const string defTabName = "АТРИБУТЫ";

        public override int Execute(params string[] parameters)
        {
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

                    foreach (PropertyCategory oPC in sample.PropertyCategories)
                    {
                        if (oPC.Name.Equals("LcOaPropOverrideCat"))
                        {
                            foreach (DataProperty dp in oPC.Properties)
                            {
                                if (dp.Value.IsDisplayString || dp.Value.IsIdentifierString)
                                {
                                    props.Add(new DisplayProperty() { DisplayName = dp.DisplayName, Value = dp.Value.ToDisplayString() });
                                }
                            }
                            break;
                        }
                    }

                    SetPropsWindow setPropsWindow = new SetPropsWindow(props);
                    bool? result = setPropsWindow.ShowDialog();
                    if (result != null && result.Value)
                    {

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



                        foreach (ModelItem item in selection.DescendantsAndSelf)
                        {
                            //Переделать панель атрибутов в соответствии с заполненными строками в окне

                            //convert the .NET collection to COM object
                            ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);
                            // get properties collection of the path
                            ComApi.InwGUIPropertyNode2 propn = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);
                            try
                            { propn.RemoveUserDefined(0); }
                            catch (System.Runtime.InteropServices.COMException) { }
                            string tabName = defTabName;
                            if (!String.IsNullOrEmpty(setPropsWindow.TabName))
                                tabName = setPropsWindow.TabName;
                            //propn.SetUserDefined(0, "АТРИБУТЫ", tabName, newPvec);
                            propn.SetUserDefined(0, tabName, "S1NF0", newPvec);
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
}
