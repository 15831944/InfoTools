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
    //[Plugin("SetProperNodeName",
    //    DEVELOPER_ID,
    //    ToolTip = "Задание свойства имени узла",
    //    DisplayName = "Настройка имени узла")]
    public class SetProperNodeName : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                Document doc = Application.ActiveDocument;

                ModelItemCollection currSelectionColl = doc.CurrentSelection.SelectedItems;

                if (currSelectionColl.Count == 0)
                {
                    Win.MessageBox.Show("Перед запуском этой команды выберите объекты, для котороых нужно поменять имя узла",
                       "Подсказка", Win.MessageBoxButton.OK, Win.MessageBoxImage.Information);
                }
                else
                {
                    //foreach ()
                    //{

                    //}
                }

            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка при задании свойства имени узла");
            }

            return 0;
        }
    }
}
