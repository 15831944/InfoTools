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
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace NavisWorksInfoTools
{
    //[Plugin("SetCustomNesting",
    //    "S-Info",
    //    ToolTip = "Задание свойства вложенности объектов",
    //    DisplayName = "Настройка вложенности")]
    public class SetCustomNesting : AddInPlugin
    {
        //TODO: Есть возможность, что пользователь выберет объекты, потом откроет др
        private enum SetNestingStage
        {
            SetChildren,
            SetParent
        }

        private List<ModelItem> children = null;

        private SetNestingStage currStage = SetNestingStage.SetChildren;

        private bool additionalHintShowed = false;

        public override int Execute(params string[] parameters)
        {
            try
            {
                Document doc = Application.ActiveDocument;
                //Application.
                //doc.
                ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;

                if (currSelectionColl.Count == 0)
                {
                    Win.MessageBox.Show("Сначала выберите элементы, для которых нужно задать родителя и запустите эту команду, "
                        + "затем выберите 1 елемент модели, который будет задан как родитель, для выбранных в предыдущий раз и запустите эту команду еще раз",
                       "Подсказка", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                }
                else
                {
                    switch (currStage)
                    {
                        case SetNestingStage.SetChildren:
                            children = currSelectionColl.ToList();//Скопировать содержимое набора выбора
                            if (!additionalHintShowed)
                            {
                                Win.MessageBox.Show("Выбрано " + currSelectionColl.Count
                                    + " объектов. Теперь выберите 1 объект-родитель и запустите эту команду еще раз", "Подсказка",
                                        Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                            }
                            currStage = SetNestingStage.SetParent;
                            break;
                        case SetNestingStage.SetParent:
                            if (currSelectionColl.Count == 1 && children != null && children.Count() > 0)
                            {
                                //Назначить родителя детям, поменять стадию
                                ModelItem parent = currSelectionColl.First;
                                DataProperty parentIdProp = parent.PropertyCategories.FindPropertyByDisplayName(S1NF0_DATA_TAB_DISPLAY_NAME, ID_PROP_DISPLAY_NAME);
                                if (parentIdProp != null)
                                {
                                    object parentId = Utils.GetUserPropValue(parentIdProp.Value);
                                    ComApi.InwOpState3 oState = ComApiBridge.ComApiBridge.State;
                                    foreach (ModelItem child in children)
                                    {
                                        Utils.SetS1NF0PropsToItem(oState, child, new Dictionary<string, object>()
                                {
                                    {PARENT_PROP_DISPLAY_NAME, parentId }
                                }, true);
                                    }
                                    if (!additionalHintShowed)
                                    {
                                        Win.MessageBox.Show("Родитель назначен", "Готово",
                                                Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                                        additionalHintShowed = true;
                                    }
                                }
                                else
                                {
                                    Win.MessageBox.Show("У выбранного объекта нет id. Используйте команду добавления служебных свойств", "Отмена",
                                        Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                                }


                                currStage = SetNestingStage.SetChildren;
                            }
                            else
                            {
                                //Переназначить выбранных детей, оставить стадию
                                children = currSelectionColl.ToList();//Скопировать содержимое набора выбора
                                additionalHintShowed = false;//Еще раз показать подсказку
                                Win.MessageBox.Show("Выбрано " + currSelectionColl.Count
                                        + " объектов. Теперь выберите 1 объект-родитель и запустите эту команду еще раз", "Подсказка",
                                            Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                            }
                            break;
                    }
                }


            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при задании свойства вложенности объектов");
            }

            return 0;
        }
    }
}
