using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Win = System.Windows;
using static NavisWorksInfoTools.Constants;

namespace NavisWorksInfoTools
{
    /// <summary>
    /// Команда для раздачи уникальных идентификаторов всем объектам
    /// ОБЯЗАТЕЛЬНО НУЖНО УЧЕСТЬ, что свойства,
    /// содержащие идентификаторы не должны перезаписываться другими командами
    /// </summary>
    //[Plugin("SetIds",
    //    "S-Info",
    //    ToolTip = "Раздать идентификаторы",
    //    DisplayName = "Раздать идентификаторы")]
    public class SetIds : AddInPlugin
    {
        private static int idCount = 0;

        public override int Execute(params string[] parameters)
        {
            try
            {
                //Все элементы модели получают свойство Id и Id материала (пустое)
                ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;

                Document doc = Application.ActiveDocument;
                ModelItemEnumerableCollection rootItems = doc.Models.RootItems;
                idCount = 0;
                SetIdsRecurse(rootItems, oState);

                Win.MessageBox.Show("Всего привязано уникальных id - "+ idCount, "Готово", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при раздаче идентификаторов в Navis");
            }

            return 0;
        }


        private void SetIdsRecurse(IEnumerable<ModelItem> items, ComApi.InwOpState3 oState)
        {
            foreach (ModelItem item in items)
            {
                //SetIdToItem(oState, item);

                SetIdsRecurse(item.Children, oState);
            }
        }

        /// <summary>
        /// Добавляет id и id материала к объекту если еще нет
        /// </summary>
        /// <param name="oState"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        //public static void SetIdToItem(ComApi.InwOpState3 oState, ModelItem item)
        //{

        //    ComApi.InwOaPropertyVec propsToSet
        //                            = oState.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec);

        //    //convert the .NET collection to COM object
        //    ComApi.InwOaPath oPath = ComApiBridge.ComApiBridge.ToInwOaPath(item);
        //    //Получить текущие свойства элемента
        //    ComApi.InwGUIPropertyNode2 propertyNode
        //        = (ComApi.InwGUIPropertyNode2)oState.GetGUIPropertyNode(oPath, true);
        //    //Поиск панели Id
        //    int indexToSet = 0;
        //    int i = 1;
        //    bool needToOverwriteId = true;
        //    bool needToOverwriteMaterialId = true;
        //    foreach (ComApi.InwGUIAttribute2 attr in propertyNode.GUIAttributes())
        //    {
        //        if (attr.UserDefined)
        //        {
        //            if (attr.ClassUserName.Equals(S1NF0_DATA_TAB_DISPLAY_NAME))
        //            {
        //                indexToSet = i;
        //                //Если панель уже содержит свойство Id и Id материала, то перезаписывать ее не нужно
        //                //Если одно из этих свойств отсутствует (или значение Id равно пустой строке)
        //                //то панель нужно будет перезаписать

        //                foreach (ComApi.InwOaProperty prop in attr.Properties())
        //                {
        //                    if (prop.UserName.Equals(ID_PROP_DISPLAY_NAME) /*&& prop.value is string && !String.IsNullOrWhiteSpace(prop.value)*/)
        //                    {
        //                        //Панель свойств содержит Id
        //                        needToOverwriteId = false;
        //                    }
        //                    else if (prop.UserName.Equals(MATERIAL_ID_PROP_DISPLAY_NAME))
        //                    {
        //                        //панель свойств содержит Id материала
        //                        needToOverwriteMaterialId = false;
        //                    }
        //                    propsToSet.Properties().Add(Utils.CopyProp(oState, prop));
        //                }
        //                break;
        //            }
        //            else
        //            {
        //                i++;
        //            }
        //        }
        //    }

        //    if (needToOverwriteId)
        //    {
        //        propsToSet.Properties().Add(Utils.CreateNewUserProp(oState, ID_PROP_DISPLAY_NAME, Guid.NewGuid().ToString()));
        //        idCount++;
        //    }
        //    if (needToOverwriteMaterialId)
        //    {
        //        propsToSet.Properties().Add(Utils.CreateNewUserProp(oState, MATERIAL_ID_PROP_DISPLAY_NAME, "_"));
        //    }

        //    if (needToOverwriteId || needToOverwriteMaterialId)
        //    {
        //        propertyNode.SetUserDefined(indexToSet, S1NF0_DATA_TAB_DISPLAY_NAME, "S1NF0", propsToSet);
        //    }
        //}
    }
}
