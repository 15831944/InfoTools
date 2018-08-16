using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WF = System.Windows.Forms;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.AddPointToSurfaceWithoutDeformCommand))]




namespace Civil3DInfoTools.AuxiliaryCommands
{
    public class AddPointToSurfaceWithoutDeformCommand
    {
        [CommandMethod("S1NF0_AddPointToSurfaceWithoutDeform", CommandFlags.Modal)]
        public void AddPointToSurfaceWithoutDeform()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            CivilDocument cdok = CivilDocument.GetCivilDocument(adoc.Database);

            try
            {
                PromptEntityOptions peo1 = new PromptEntityOptions("\nУкажите поверхность для вставки дополнительной точки");
                peo1.SetRejectMessage("\nМожно выбрать только поверхность TIN");
                peo1.AddAllowedClass(typeof(TinSurface), true);
                PromptEntityResult per1 = ed.GetEntity(peo1);
                if (per1.Status == PromptStatus.OK)
                {

                    //Указание точки внутри треугольника для встваки точки
                    while (true)
                    {
                        PromptPointOptions ppo = new PromptPointOptions("Укажите точку внутри одного из треугольников поверхности");
                        PromptPointResult ppr = ed.GetPoint(ppo);
                        if (ppr.Status == PromptStatus.OK)
                        {
                            Point3d pt = ppr.Value;

                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                TinSurface tinSurf = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as TinSurface;

                                TinSurfaceTriangle triangle = tinSurf.FindTriangleAtXY(pt.X, pt.Y);
                                if (triangle != null)
                                {
                                    tinSurf.AddLine(triangle.Vertex1, triangle.Vertex2);
                                }

                                tr.Commit();
                            }
                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                TinSurface tinSurf = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as TinSurface;

                                TinSurfaceTriangle triangle = tinSurf.FindTriangleAtXY(pt.X, pt.Y);
                                if (triangle != null)
                                {
                                    tinSurf.AddLine(triangle.Vertex2, triangle.Vertex3);
                                }

                                tr.Commit();
                            }
                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                TinSurface tinSurf = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as TinSurface;

                                TinSurfaceTriangle triangle = tinSurf.FindTriangleAtXY(pt.X, pt.Y);
                                if (triangle != null)
                                {
                                    tinSurf.AddLine(triangle.Vertex3, triangle.Vertex1);
                                }

                                tr.Commit();
                            }
                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                TinSurface tinSurf = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as TinSurface;

                                TinSurfaceTriangle triangle = tinSurf.FindTriangleAtXY(pt.X, pt.Y);
                                if (triangle != null)
                                {
                                    tinSurf.AddVertex(new Point2d(pt.X, pt.Y));
                                }
                                tr.Commit();
                            }
                            ed.Regen();
                        }
                        else
                        {
                            return;
                        }
                    }

                }
            }
            catch (Autodesk.Civil.SurfaceException)
            {
                WF.MessageBox.Show("Невозможно редактировать поверхность. Если это быстрая ссылка, то ее нужно освободить");
            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при вставке точки в поверхность");
            }
        }
    }
}
