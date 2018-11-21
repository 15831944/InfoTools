using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;

using Autodesk.AutoCAD.DatabaseServices;

using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Civil3DInfoTools.PipeNetworkCreating.ConfigureNetworkCreationWindow2;

[assembly: CommandClass(typeof(Civil3DInfoTools.PipeNetworkCreating.RaiseNetworkTo3dCommand))]

namespace Civil3DInfoTools.PipeNetworkCreating
{

    /// <summary>
    /// Команда для поднятия инженерных сетей в 3d
    /// ПРЕДПОЛАГАЕТ, ЧТО ИСХОДНЫЕ ДАННЫЕ ПОЛНОСТЬЮ СООТВЕТСТВУЮТ КОДИФИКАТОРУ ПО СПБ (ПО КОТОРОМУ РАБОТАЕТ БЕНТА (2018 год))
    /// допускаются изменения названий слоев и блоков
    /// обязательна точная стыковка всех линий сетей
    /// </summary>
    public class RaiseNetworkTo3dCommand
    {
        #region Мысли
        //ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION
        //ПРЕДПОЛАГАЕТСЯ, ЧТО ВСЯ СЕТЬ СОСТОИТ ТОЛЬКО ИЗ ПОЛИЛИНИЙ (МАЛОВЕРОЯТНО, ЧТО МОГУТ БЫТЬ ДУГОВЫЕ ВСТАВКИ, СЧИТАТЬ ЧТО ВСЕ СЕГМЕНТЫ ПРЯМЫЕ).
        //ОТДЕЛЬНЫЕ ПОЛИЛИНИИ СТЫКУЮТСЯ МЕЖДУ СОБОЙ (С УЧЕТОМ ДОПУСКОВ АВТОКАДА). ВОЗМОЖНА Т-ОБРАЗНАЯ СТЫКОВКА?
        //В ТОЧКАХ СТЫКОВОК МОЖЕТ БЫТЬ БЛОК КОЛОДЦА
        //(С ТОЧНЫМ СООТВЕТСТВИЕМ ТОЧКИ ВСТАВКИ БЛОКА (С УЧЕТОМ ДОПУСКОВ АВТОКАДА))
        //КОЛОДЕЦ МОЖЕТ БЫТЬ В ОТДЕЛЬНОМ ЗАДАННОМ СЛОЕ ЛИБО В СЛОЕ САМОЙ СЕТИ (ВОЗМОЖНЫЕ ТИПЫ БЛОКОВ СТРОГО ЗАДАЮТСЯ).
        //ПРИ ЭТОМ КОЛОДЦА МОЖЕТ И НЕ БЫТЬ. НА СЕТИ МОГУТ БЫТЬ И ДРУГИЕ БЛОКИ ПОМИМО КОЛОДЦЕВ, НО ОНИ ПОКА НИКАК НЕ ВЛИЯЮТ НА МОДЕЛЬ 
        //РЯДОМ С СЕТЬЮ ЕСТЬ РАЗЛИЧНЫЕ ПОДПИСИ В ТОМ ЖЕ СЛОЕ, ЧТО И СЕТЬ:
        //- НОМЕРА ПРИМЫКАНИЙ К КОЛОДЦАМ (ПРОСТО ЦИФРА, ОРИЕНТАЦИЯ - ГОРИЗОНТАЛЬНАЯ)
        //- ДИАМЕТР ТРУБЫ (НАЧИНАЕТСЯ С СОКРАЩЕННО МАТЕРИАЛ, ПРОБЕЛ, ДИАМЕТР В МИЛИМЕТРАХ, ОРИЕНТАЦИЯ ВДОЛЬ ПОЛИЛИНИИ (НЕТОЧНО!))
        //- ПОДПИСЬ ВЛАДЕЛЬЦА(НАЧИНАЕТСЯ С "вл.", ОРИЕНТАЦИЯ ВДОЛЬ ПОЛИЛИНИИ (НЕТОЧНО!))
        //- ДРУГАЯ ИНФОРМАЦИЯ (НАПРИМЕР ПОДПИСЬ "не действ.", подписи кабелей, ОРИЕНТАЦИЯ ВДОЛЬ ПОЛИЛИНИИ (НЕТОЧНО!))
        //ВИДЫ МАТЕРИАЛОВ - "ст.","бет.","чуг.","плм",
        //ПЕРЕД МАТЕРИАЛОМ МОЖЕТ БЫТЬ
        //"ф-р"  ---  футляр (сами футляры обычно находятся в отдельном слое (и подписи к ним тоже))
        //цифра  ---  количество
        //цифра/цифра  ---  какое-то количество для кабелей

        //ОТДЕЛЬНАЯ ИСТОРИЯ - ЗОНЫ КАБЕЛЕЙ. ОНИ НЕ УКЛАДЫВАЮТСЯ В ОБЩУЮ СХЕМУ
        //НУЖНА ОТДЕЛЬНАЯ УТИЛИТА ДЛЯ ПЕРЕВОДА ЗОН КАБЕЛЕЙ В ОТДЕЛЬНЫЕ ЛИНИИ КАБЕЛЕЙ

        //УЧЕСТЬ ВОЗМОЖНОСТЬ СУЩЕСТВОВАНИЯ Т-ОБРАЗНЫХ СТЫКОВОК ЛИНИЙ СЕТЕЙ!
        //ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION//ASSUMPTION 
        #endregion


        [CommandMethod("S1NF0_RaiseNetworkTo3d", CommandFlags.Modal)]
        public void RaiseNetworkTo3d()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;

            Editor ed = doc.Editor;

            CivilDocument cdok = CivilDocument.GetCivilDocument(doc.Database);

            try
            {
                ConfigureNetworkCreationView configView = new ConfigureNetworkCreationView();
                ConfigureNetworkCreationViewModel viewModel = new ConfigureNetworkCreationViewModel(doc, cdok, configView);
                configView.DataContext = viewModel;
                Application.ShowModalWindow(configView);
                //if (viewModel.AcceptBtnIsEnabled && viewModel.ConfigurationsAccepted)
                //{}

            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при создании модели сети");
            }
        }
    }
}
