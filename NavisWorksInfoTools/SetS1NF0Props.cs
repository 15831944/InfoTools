﻿using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NavisWorksInfoTools.Constants;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComApiBridge = Autodesk.Navisworks.Api.ComApi;
using Win = System.Windows;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

namespace NavisWorksInfoTools
{
    [Plugin("SetS1NF0Props",
        "S-Info",
        ToolTip = "Добавить служебные свойства S1NF0 со значениями по умолчанию",
        DisplayName = "Добавить служебные свойства")]
    public class SetS1NF0Props : AddInPlugin
    {
        private static int editedCount = 0;
        /// <summary>
        /// Раздать свойства родитель и правильное имя узла
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public override int Execute(params string[] parameters)
        {
            try
            {
                //Все элементы модели получают свойство Id и Id материала (пустое)
                ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;

                Document doc = Application.ActiveDocument;
                ModelItemEnumerableCollection rootItems = doc.Models.RootItems;
                editedCount = 0;
                SetTreePropsRecurse(rootItems, oState, "ROOT");

                Win.MessageBox.Show("Всего объектов с добавленными свойствами - " + editedCount,
                    "Готово", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при раздаче служебных свойств S1NF0 в Navis");
            }

            return 0;
        }


        private void SetTreePropsRecurse(IEnumerable<ModelItem> items, ComApi.InwOpState3 oState, object parentId)
        {
            foreach (ModelItem item in items)
            {
                string defProperName = item.DisplayName;
                if (String.IsNullOrEmpty(defProperName))
                {
                    defProperName = item.ClassDisplayName;
                }

                if (Utils.SetS1NF0PropsToItem(oState, item,
                    new Dictionary<string, object>()
                    {
                    { ID_PROP_DISPLAY_NAME, Utils.S1NF0PropSpecialValue.RandomGUID},
                    { MATERIAL_ID_PROP_DISPLAY_NAME, "_"},
                    { PROPER_NAME_PROP_DISPLAY_NAME, defProperName},
                    { PARENT_PROP_DISPLAY_NAME, parentId},
                    }))
                {
                    editedCount++;
                }
                
                DataProperty idProp = item.PropertyCategories
                    .FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME, ID_PROP_DISPLAY_NAME);//Ссылка на id этого элемента для передачи детям
                SetTreePropsRecurse(item.Children, oState, Utils.GetUserPropValue(idProp.Value));
            }
        }


    }
}
