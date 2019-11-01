using Autodesk.AutoCAD.Runtime;
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
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Newtonsoft.Json;
using System.IO;

[assembly: CommandClass(typeof(Civil3DInfoTools.AuxiliaryCommands.GenegateUnigineSplineCommand))]

namespace Civil3DInfoTools.AuxiliaryCommands
{

    public class GenegateUnigineSplineCommand
    {
        [CommandMethod("S1NF0_GenegateUnigineSpline", CommandFlags.Modal)]
        public void GenegateUnigineSpline()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            try
            {
                //выбрать 3d полилинию
                PromptEntityOptions peo1 = new PromptEntityOptions("\nВыберите 3D полилинию");
                peo1.SetRejectMessage("\nМожно выбрать только 3D полилинию");
                peo1.AddAllowedClass(typeof(Polyline3d), true);
                PromptEntityResult per1 = ed.GetEntity(peo1);
                if (per1.Status != PromptStatus.OK) return;

                //создание данных для сериализации
                UnigineSpline unigineSpline = new UnigineSpline();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline3d poly = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as Polyline3d;
                    if (poly == null) return;

                    ObjectId[] verts = poly.Cast<ObjectId>().ToArray();



                    List<Point3d> pts = new List<Point3d>(verts.Length);
                    for (int i = 0; i < verts.Length; i++)
                    {
                        PolylineVertex3d vt = tr.GetObject(verts[i], OpenMode.ForRead) as PolylineVertex3d;
                        Point3d point3d = vt.Position;
                        pts.Add(point3d);
                    }


                    List<Vector3d> tangents = new List<Vector3d>(verts.Length);
                    tangents.Add((pts[1] - pts[0]).GetNormal());
                    for (int i = 1; i < verts.Length - 1; i++)
                    {
                        Vector3d tangent = ((pts[i + 1] - pts[i]) + (pts[i] - pts[i - 1])).GetNormal();
                        tangents.Add(tangent);
                    }
                    tangents.Add((pts[verts.Length - 1] - pts[verts.Length - 2]).GetNormal());




                    for (int i = 0; i < verts.Length; i++)
                    {
                        unigineSpline.points.Add(new double[] { pts[i].X, pts[i].Y, pts[i].Z });

                        if (i > 0)
                        {
                            //сегмент
                            UnigineSegment segment = new UnigineSegment()
                            {
                                start_index = i - 1,
                                start_tangent = new double[] { tangents[i - 1].X, tangents[i - 1].Y, tangents[i - 1].Z },
                                start_up = new double[] { 0, 0, 1 },
                                end_index = i,
                                end_tangent = new double[] { -tangents[i].X, -tangents[i].Y, -tangents[i].Z },
                                end_up = new double[] { 0, 0, 1 },
                            };
                            unigineSpline.segments.Add(segment);
                        }
                    }

                    tr.Commit();
                }


                //сериализация
                //TODO: Сделать выбор папки
                string fileName = Common.Utils.GetNonExistentFileName(Path.GetDirectoryName(adoc.Name), "unigineSpline", "spl");
                using (StreamWriter file = System.IO.File.CreateText(fileName))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, unigineSpline);
                }

            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при создании сплайна для UNIGINE");
            }
        }
    }


    public class UnigineSpline
    {
        public List<double[]> points = new List<double[]>();

        public List<UnigineSegment> segments = new List<UnigineSegment>();
    }

    public class UnigineSegment
    {
        public int start_index;
        public double[] start_tangent;
        public double[] start_up;

        public int end_index;
        public double[] end_tangent;
        public double[] end_up;

    }
}
