using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CommandClass(typeof(Civil3DInfoTools.RoadMarking.CreateBoundariesCommand))]

namespace Civil3DInfoTools.RoadMarking
{
    class CreateBoundariesCommand
    {
        [CommandMethod("CreateBoundaries", CommandFlags.Modal)]
        public void CreateBoundaries()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;


            try
            {
                //Запрос у пользователя выбора слоев, объекты в которых нужно подвергнуть обработке
                //Подсветка объектов, подлежащих обработке (линии с глобальной шириной и штриховки)
                PromptSelectionOptions options = new PromptSelectionOptions();
                options.SingleOnly = true;
                options.MessageForAdding = "\nУкажите объекты для выбора слоев разметки";
                ed.GetSelection(options);


                //Создание новой базы данных для записи обработанных объектов и создания нового чертежа

                //Обработка объектов

                //Создание нового чертежа и открытие его
            }
            catch (System.Exception ex)
            {
                Utils.ErrorToCommandLine(ed, "Ошибка при выполнении команды InsertByCoordinates", ex);
            }
        }


        private void ProcessPolyline(Curve curve)
        {
            //Перенос полилинии в отдельный блок с сохранением координат
            //Анализ типа линии. Скорее всего можно получить длину штриха и длину пробела
            //Затем использовать метод Curve.GetSplitCurves для создания сегментов
            //Затем использовать метод Curve.GetOffsetCurves
            //для создания линий границ справа и слева в соответствии с глобальной шириной
            //Создание замкнутого контура

        }

        private void ProcessHatch(Hatch hatch)
        {
            //Для создания контура штриховки
            //Hatch.NumberOfLoops
            //Hatch.GetLoopAt
            //HatchLoop.Polyline!!!
        }
    }
}
