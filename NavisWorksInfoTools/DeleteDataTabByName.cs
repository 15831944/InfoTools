using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using WinForms = System.Windows.Forms;
using System.IO;
using Autodesk.Navisworks.Api;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;

namespace NavisWorksInfoTools
{
    public class DeleteDataTabByName
    {
        public static int Execute()
        {
            try
            {
                Document doc = Application.ActiveDocument;

                ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;

                DataTabToDeleteWindow dataTabToDeleteWindow = new DataTabToDeleteWindow();
                bool? result = dataTabToDeleteWindow.ShowDialog();
                if (result != null && result.Value 
                    && !String.IsNullOrWhiteSpace(dataTabToDeleteWindow.DataTabName))
                {
                    Search searchForDataTabs = new Search();
                    searchForDataTabs.Selection.SelectAll();
                    searchForDataTabs.PruneBelowMatch = false;
                    SearchCondition hasDataTabCondition = SearchCondition//.HasCategoryByDisplayName(dataTabToDeleteWindow.DataTabName);
                        .HasCategoryByCombinedName(new NamedConstant("LcOaPropOverrideCat",
                        dataTabToDeleteWindow.DataTabName));
                    searchForDataTabs.SearchConditions.Add(hasDataTabCondition);
                    ModelItemCollection hasDataTabs = searchForDataTabs.FindAll(doc, false);

                    foreach (ModelItem item in hasDataTabs)
                    {
                        ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);

                        ComApi.InwGUIPropertyNode2 propn
                        = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);
                        int i = 1;
                        foreach (ComApi.InwGUIAttribute2 attr in propn.GUIAttributes())
                        {
                            if (!attr.UserDefined) continue;

                            if (attr.ClassUserName.Equals(dataTabToDeleteWindow.DataTabName))
                            {
                                propn.RemoveUserDefined(i);
                                break;
                            }
                            i++;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при удалении панели в Navis");
            }

            return 0;
        }
    }
}
