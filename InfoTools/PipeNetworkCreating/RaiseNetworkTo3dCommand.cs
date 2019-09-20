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
