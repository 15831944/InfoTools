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
                Point3d center = Point3d.Origin;
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

                    //найти среднюю точку
                    foreach (Point3d pt in pts)
                    {
                        center = center.Add(new Vector3d(pt.X, pt.Y, pt.Z));
                    }
                    center = center.DivideBy(pts.Count);

                    for (int i = 0; i < pts.Count; i++)
                    {
                        pts[i] = pts[i].Subtract(new Vector3d(center.X, center.Y, center.Z));
                    }


                    List<Vector3d> tangents = new List<Vector3d>(verts.Length);
                    tangents.Add((pts[1] - pts[0]).GetNormal());
                    for (int i = 1; i < verts.Length - 1; i++)
                    {
                        Vector3d tangent = ((pts[i + 1] - pts[i]).GetNormal()
                            + (pts[i] - pts[i - 1]).GetNormal()).GetNormal();
                        tangents.Add(tangent);
                    }
                    tangents.Add((pts[verts.Length - 1] - pts[verts.Length - 2]).GetNormal());

                    for (int i = 0; i < verts.Length; i++)
                    {
                        unigineSpline.points.Add(new double[] { pts[i].X, pts[i].Y, pts[i].Z });

                        if (i > 0)
                        {
                            //тангенсы должны быть по модулю примерно 1/4 от расстояния между точками
                            double mult = (pts[i] - pts[i - 1]).Length / 4;
                            Vector3d tan0 = tangents[i - 1] * mult;
                            Vector3d tan1 = -tangents[i] * mult;

                            //сегмент
                            UnigineSegment segment = new UnigineSegment()
                            {
                                start_index = i - 1,
                                start_tangent = new double[] { tan0.X, tan0.Y, tan0.Z },
                                start_up = new double[] { 0, 0, 1 },
                                end_index = i,
                                end_tangent = new double[] { tan1.X, tan1.Y, tan1.Z },
                                end_up = new double[] { 0, 0, 1 },
                            };
                            unigineSpline.segments.Add(segment);
                        }
                    }

                    if (poly.Closed || pts[0].IsEqualTo(pts[verts.Length - 1]))
                    {
                        UnigineSegment lastSeg = unigineSpline.segments.Last();
                        UnigineSegment firstSeg = unigineSpline.segments.First();

                        if (pts[0].IsEqualTo(pts[verts.Length - 1]))
                        {
                            //замыкающий сегмент привязывается к стартовой точке
                            
                            lastSeg.end_index = 0;

                            //тангенсы в стартовой точке
                            Vector3d tan = ((pts[1] - pts[0]).GetNormal()
                                + (pts[verts.Length - 1] - pts[verts.Length - 2]).GetNormal()).GetNormal();
                            Vector3d tanLast = -tan * (pts[lastSeg.end_index] - pts[lastSeg.start_index]).Length / 4;
                            lastSeg.end_tangent = new double[] { tanLast.X, tanLast.Y, tanLast.Z };

                            
                            Vector3d tanFirst = tan * (pts[firstSeg.end_index] - pts[firstSeg.start_index]).Length / 4;
                            firstSeg.start_tangent = new double[] { tanFirst.X, tanFirst.Y, tanFirst.Z };
                        }
                        else
                        {
                            //тангенсы в стартовой и последней точке
                            Point3d lastPt = pts[verts.Length - 1];
                            Point3d firstPt = pts[0];
                            Vector3d tanStart = ((pts[1] - firstPt).GetNormal()
                                + (firstPt - lastPt).GetNormal()).GetNormal();
                            Vector3d tanEnd = ((firstPt - lastPt).GetNormal()
                                + (lastPt - pts[verts.Length - 2]).GetNormal()).GetNormal();

                            Vector3d tanLast = -tanEnd * (pts[lastSeg.end_index] - pts[lastSeg.start_index]).Length / 4;
                            lastSeg.end_tangent = new double[] { tanLast.X, tanLast.Y, tanLast.Z };

                            Vector3d tanFirst = tanStart * (pts[firstSeg.end_index] - pts[firstSeg.start_index]).Length / 4;
                            firstSeg.start_tangent = new double[] { tanFirst.X, tanFirst.Y, tanFirst.Z };

                            //замыкающий сегмент
                            double mult = (firstPt - lastPt).Length / 4;
                            Vector3d tan0 = tanEnd * mult;
                            Vector3d tan1 = -tanStart * mult;

                            UnigineSegment segment = new UnigineSegment()
                            {
                                start_index = verts.Length - 1,
                                start_tangent = new double[] { tan0.X, tan0.Y, tan0.Z },
                                start_up = new double[] { 0, 0, 1 },
                                end_index = 0,
                                end_tangent = new double[] { tan1.X, tan1.Y, tan1.Z },
                                end_up = new double[] { 0, 0, 1 },
                            };
                            unigineSpline.segments.Add(segment);

                            
                        }

                    }


                    tr.Commit();
                }


                //сериализация
                //TODO: Сделать выбор папки
                if (unigineSpline.segments.Count > 0)
                {
                    string fileName = Common.Utils.GetNonExistentFileName(Path.GetDirectoryName(adoc.Name),
                    center.ToString().Replace(',', '_').Substring(1, center.ToString().Length - 2), "spl");
                    using (StreamWriter file = System.IO.File.CreateText(fileName))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(file, unigineSpline);
                    }
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
