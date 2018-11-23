using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Civil3DInfoTools.Geology.GeologyHatch3dWindow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.Geology.GeologyHatch3dCommand))]

namespace Civil3DInfoTools.Geology
{
    public class GeologyHatch3dCommand
    {
        private static PaletteSet ps;
        private static GeologyHatch3dView view;
        private static GeologyHatch3dViewModel viewModel;


        [CommandMethod("S1NF0_GeologyHatch3d", CommandFlags.Modal)]
        public void RaiseNetworkTo3d()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;

            Editor ed = doc.Editor;


            //указать план трассы (полилиния)
            //выбрать штриховки на продольном профиле
            //линейный масштабный коэффициент:
            //- по горизонтали
            //- по вертикали
            //задать базовую отметку для профиля

            try
            {
                if (ps == null)
                {
                    ps = new PaletteSet("Построение 3d профиля геологии");
                    ps.Style = PaletteSetStyles.ShowPropertiesMenu
                        | PaletteSetStyles.ShowCloseButton;

                    view = new GeologyHatch3dView();
                    viewModel = new GeologyHatch3dViewModel(doc, ps);

                    if (GeologyConvertationCommand.ViewModel != null)
                    {
                        viewModel.HorScaling = GeologyConvertationCommand.ViewModel.EndHorScaling;
                        viewModel.VertScaling = GeologyConvertationCommand.ViewModel.EndVertScaling;
                    }


                    view.DataContext = viewModel;
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
                CommonException(ex, "Ошибка при создании 3d профиля геологии");
            }


        }


        private static void PaletteSet_StateChanged(object sender, PaletteSetStateEventArgs e)
        {
            if (e.NewState == StateEventIndex.Hide)
            {
                viewModel.HighlightObjs(false, viewModel.SoilHatchIds);
                viewModel.HighlightObjs(false, viewModel.AlignmentPolyId);
            }
            else if (e.NewState == StateEventIndex.Show)
            {
                viewModel.HighlightObjs(true, viewModel.SoilHatchIds);
                viewModel.HighlightObjs(true, viewModel.AlignmentPolyId);
            }
        }


        public static void ClosePalette(object sender, EventArgs e)
        {
            if (ps != null)
            {
                ps.Visible = false;
                ps = null;
                view = null;
                viewModel = null;
            }
        }

    }
}
