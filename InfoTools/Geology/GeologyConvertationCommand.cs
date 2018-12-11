using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Civil3DInfoTools.Geology.GeologyTrueScalingProfileWindow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.Geology.GeologyConvertationCommand))]

namespace Civil3DInfoTools.Geology
{
    public class GeologyConvertationCommand
    {
        private static PaletteSet ps;
        private static GeologyTrueScalingProfileView2 view;
        public static GeologyTrueScalingProfileViewModel ViewModel { get; private set; }

        /// <summary>
        /// Перевод профиля геологии в масштаб повехности земли
        /// </summary>
        [CommandMethod("S1NF0_GeologyConvertation", CommandFlags.Modal | CommandFlags.Session)]
        public void GeologyTrueScalingProfile()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;

            Editor ed = doc.Editor;

            //ДЛЯ КАЖДОЙ ТОЧКИ ПРОФИЛЯ ГЕОЛОГИИ ЗНАЧЕНИЕ ИМЕЕТ ТОЛЬКО ЗАГЛУБЛЕНИЕ ЭТОЙ ТОЧКИ ОТ ПОВЕРХНОСТИ ЗЕМЛИ!!!!

            //указать линию земли на продольном профиле (полилиния)
            //выбрать штриховки на продольном профиле
            //исходные масштабные коэффициенты (сколько метров в одной единице длины автокада):
            //- по горизонтали
            //- по вертикали
            //- по вертикали грунты
            //требуемые масштабные коэффициенты
            //- по горизонтали
            //- по вертикали 

            try
            {
                if (ps == null)
                {
                    ps = new PaletteSet("Перевод масштаба профиля геологии");
                    ps.Style = PaletteSetStyles.ShowPropertiesMenu
                        //| PaletteSetStyles.ShowAutoHideButton
                        | PaletteSetStyles.ShowCloseButton;

                    view = new GeologyTrueScalingProfileView2();
                    ViewModel = new GeologyTrueScalingProfileViewModel(doc, ps);
                    view.DataContext = ViewModel;
                    ps.AddVisual("ConnectionPaletteControl", view);

                    ps.DockEnabled = DockSides.Left;

                    ps.Visible = true;

                    ps.Size = new System.Drawing.Size(420, 350);
                    ps.Dock = DockSides.Left;

                    ps.StateChanged += PaletteSet_StateChanged;//снимать подсветку объектов про закрытии

                    //панель жестко привязана к одному чертежу
                    //если документ сменяется, то панель должна быть закрыта и удалена!
                    Application.DocumentManager.DocumentToBeDeactivated += ClosePalette;
                    Application.DocumentManager.DocumentToBeDestroyed += ClosePalette;
                }
                else
                {
                    ps.Visible = true;
                }
            }
            catch (System.Exception ex)
            {
                ClosePalette(null, null);
                CommonException(ex, "Ошибка при переводе масштаба профиля геологии");
            }

        }


        private static void PaletteSet_StateChanged(object sender, PaletteSetStateEventArgs e)
        {
            if (e.NewState == StateEventIndex.Hide)
            {
                ViewModel.HighlightObjs(false, ViewModel.SoilHatchIds);
                ViewModel.HighlightObjs(false, ViewModel.GroundSurfPolyId);
            }else if (e.NewState == StateEventIndex.Show)
            {
                ViewModel.HighlightObjs(true, ViewModel.SoilHatchIds);
                ViewModel.HighlightObjs(true, ViewModel.GroundSurfPolyId);
            }
        }


        public static void ClosePalette(object sender, EventArgs e)
        {
            if(ps != null)
            {
                ps.Visible = false;
                ps = null;
                view = null;
                ViewModel = null;
            }
        }
    }
}
