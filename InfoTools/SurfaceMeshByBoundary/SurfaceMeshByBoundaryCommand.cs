using Autodesk.AutoCAD.ApplicationServices;
using AcadDb = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.BoundaryRepresentation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using RBush;
using Autodesk.AutoCAD.Colors;

[assembly: CommandClass(typeof(Civil3DInfoTools.SurfaceMeshByBoundary.SurfaceMeshByBoundaryCommand))]

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    //TODO: Попробовать разобраться с созданием островков внутри треугольников --- https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
    //ГОТОВО//TODO: Если блок имеет более одного вхождения, то сеть создавать не внутри блока, а в пространстве модели. Но при этом слой использовать такой же как у полилинии
    //ГОТОВО//TODO: Сеть создавать в слое как у первой полилинии, находящейся на самом высоком уровне дерева вложенности полилиний
    //ГОТОВО//TODO: Добавить ввод возвышения над поверхностью (запоминать ввод)
    //TODO: Выдавать предупреждение если обнаружены пересекающиеся прямые
    //TODO: Выдавать предупреждение если возникли ошибки при обходе треугольника
    //TODO: Добавить запрос у пользователя создавать ли все сети в пространстве модели или по возможности создавать внутри блоков
    //ГОТОВО//TODO: Добавить фильтр при выборе блоков

    //TODO: Команду можно вызывать только из пространства модели документа

    //TODO: Подробно изучить CurveCurveIntersector2d Class для расчета пересечений между кривыми!  IsTangential если линии только касаются в точке пересечения?
    //Подумать об изменении процедуры проверки на пересечение с учетом того, что я узнал о Tolerance  Curve2d

    //TODO: Учесть  eCannotScaleNonUniformly из-за растянутых блоков. Сначала нужно аппроксимировать полилинии с учетом самого большого из коэффициентов масштабирования (по X и Y)
    //Затем точки полилинии пересчитать по трансформации блока

    //TODO: Учесть динамические блоки с несколькими состояниями видимости



    public class SurfaceMeshByBoundaryCommand
    {
        public static Database DB { get; private set; }
        public static Editor ED { get; private set; }

        public static double MeshElevation { get; private set; } = 0.05;

        [CommandMethod("S1NF0_SurfaceMeshByBoundary", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SurfaceMeshByBoundary()
        {

            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;
            DB = db;

            Editor ed = adoc.Editor;
            ED = ed;

            CivilDocument cdok = CivilDocument.GetCivilDocument(adoc.Database);


            try
            {
                TinSurface tinSurf = null;
                //BlockReference blockReference = null;

                //Выбрать поверхность
                PromptEntityOptions peo1 = new PromptEntityOptions("\nУкажите поверхность для построения 3d тела по обертывающей");
                peo1.SetRejectMessage("\nМожно выбрать только поверхность TIN");
                peo1.AddAllowedClass(typeof(TinSurface), true);
                PromptEntityResult per1 = ed.GetEntity(peo1);
                if (per1.Status == PromptStatus.OK)
                {
                    //Запрос возвышения создаваемой сети над поверхностью
                    PromptDoubleOptions pdo = new PromptDoubleOptions("Введите требуемое возвышение над поверхностью TIN");
                    pdo.AllowArbitraryInput = false;
                    pdo.AllowNegative = true;
                    pdo.AllowNone = true;
                    pdo.AllowZero = true;
                    pdo.DefaultValue = MeshElevation;
                    PromptDoubleResult pdr = ed.GetDouble(pdo);
                    if (pdr.Status == PromptStatus.OK)
                    {
                        MeshElevation = pdr.Value;

                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            tinSurf = tr.GetObject(per1.ObjectId, OpenMode.ForRead) as TinSurface;
                            tr.Commit();
                        }
                        //Проверка текущего набора выбора
                        SelectionSet acSSet = null;
                        PromptSelectionResult acSSPrompt;
                        acSSPrompt = ed.SelectImplied();
                        if (acSSPrompt.Status == PromptStatus.OK)
                        {
                            acSSet = acSSPrompt.Value;
                        }
                        else
                        {
                            //Множественный выбор блоков
                            TypedValue[] tv = new TypedValue[]
                            {
                            new TypedValue(0, "INSERT")
                            };
                            SelectionFilter flt = new SelectionFilter(tv);

                            PromptSelectionOptions pso = new PromptSelectionOptions();
                            pso.MessageForAdding = "\nВыберите блоки участков";

                            acSSPrompt = adoc.Editor.GetSelection(pso, flt);
                            if (acSSPrompt.Status == PromptStatus.OK)
                            {
                                acSSet = acSSPrompt.Value;
                            }
                        }

                        if (acSSet != null)
                        {
                            foreach (SelectedObject acSSObj in acSSet)
                            {

                                string blockName = null;
                                try
                                {
                                    if (acSSObj != null)
                                    {
                                        //полилинии внутри блока
                                        List<Polyline> polylines = new List<Polyline>();
                                        BlockReference blockReference = null;
                                        ObjectId btrId = ObjectId.Null;
                                        using (Transaction tr = db.TransactionManager.StartTransaction())
                                        {
                                            //блок внутри набора выбора
                                            blockReference = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as BlockReference;
                                            tr.Commit();
                                        }
                                        Matrix3d transform = Matrix3d.Identity;
                                        if (blockReference != null)
                                        {
                                            //трансформация из системы координат блока в мировую систему координат
                                            transform = blockReference.BlockTransform;

                                            //Перебор всех объектов внутри блока
                                            //Найти все правильные полилинии в блоке
                                            using (Transaction tr = db.TransactionManager.StartTransaction())
                                            {
                                                btrId = blockReference.BlockTableRecord;
                                                BlockTableRecord blockTableRecord = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;

                                                if (blockTableRecord.XrefStatus != XrefStatus.NotAnXref)
                                                {
                                                    //Если это внешняя ссылка, то не интересно
                                                    continue;
                                                }


                                                blockName = blockTableRecord.Name;
                                                foreach (ObjectId id in blockTableRecord)
                                                {
                                                    AcadDb.Entity ent = tr.GetObject(id, OpenMode.ForRead) as AcadDb.Entity;
                                                    if (ent is Polyline)
                                                    {
                                                        Polyline poly = ent as Polyline;
                                                        if (poly.Closed || poly.GetPoint2dAt(0).Equals(poly.GetPoint2dAt(poly.NumberOfVertices - 1))//Полилиния замкнута
                                                            && !Utils.PolylineIsSelfIntersecting(poly)//Не имеет самопересечений
                                                                                                      //&& !poly.HasBulges//Не имеет криволинейных сегментов
                                                            )
                                                        {
                                                            polylines.Add(ent as Polyline);
                                                        }

                                                    }
                                                }


                                                tr.Commit();
                                            }



                                            if (polylines.Count > 0)
                                            {
                                                //Проверить все линии на пересечение друг с другом. Удалить из списка те, которые имеют пересечения
                                                HashSet<Polyline> polylinesWithNoIntersections = new HashSet<Polyline>(polylines);
                                                for (int i = 0; i < polylines.Count; i++)
                                                {
                                                    for (int j = i + 1; j < polylines.Count; j++)
                                                    {
                                                        Extents3d? ext1 = polylines[i].Bounds;
                                                        Extents3d? ext2 = polylines[j].Bounds;
                                                        if (ext1 != null && ext2 != null)
                                                        {
                                                            Extents2d ext1_2d = Utils.Extents2DBy3D(ext1.Value);
                                                            Extents2d ext2_2d = Utils.Extents2DBy3D(ext2.Value);

                                                            if (Utils.BoxesAreSuperimposed(ext1_2d, ext2_2d))
                                                            {
                                                                //Сначала проверяем перекрываются ли BoundingBox.
                                                                //Во многих случаях это гораздо производительнее и затем считаем персечения
                                                                Point3dCollection intersectionPts = new Point3dCollection();
                                                                polylines[i].IntersectWith(polylines[j], Intersect.OnBothOperands,
                                                                new Plane(Point3d.Origin, Vector3d.ZAxis),
                                                                intersectionPts, IntPtr.Zero, IntPtr.Zero);
                                                                if (intersectionPts.Count > 0)
                                                                {
                                                                    polylinesWithNoIntersections.Remove(polylines[i]);
                                                                    polylinesWithNoIntersections.Remove(polylines[j]);
                                                                }
                                                            }

                                                        }


                                                    }
                                                }

                                                //Аппроксимация всех полилиний, которые имеют кривизну
                                                List<Polyline> polylinesWithNoBulges = new List<Polyline>();
                                                foreach (Polyline poly in polylinesWithNoIntersections)
                                                {
                                                    polylinesWithNoBulges.Add(ApproximatePolyBulges(poly, 0.02));
                                                }

                                                //Удалить все повторяющиеся подряд точки полилинии
                                                foreach (Polyline poly in polylinesWithNoBulges)
                                                {
                                                    for (int i = 0; i < poly.NumberOfVertices;)
                                                    {
                                                        Point2d curr = poly.GetPoint2dAt(i);
                                                        int nextIndex = (i + 1) % poly.NumberOfVertices;
                                                        Point2d next = poly.GetPoint2dAt(nextIndex);

                                                        if (next.IsEqualTo(curr))
                                                        {
                                                            poly.RemoveVertexAt(nextIndex);
                                                        }
                                                        else
                                                        {
                                                            i++;
                                                        }
                                                    }
                                                }


                                                //Построение дерева вложенности полилиний
                                                PolylineNesting polylineNesting = new PolylineNesting(tinSurf);
                                                foreach (Polyline poly in polylinesWithNoBulges)
                                                {
                                                    poly.TransformBy(transform);
                                                    polylineNesting.Insert(poly);
                                                }

                                                //Расчет полигонов
                                                polylineNesting.CalculatePoligons();

                                                //Построение сети
                                                SubDMesh sdm = polylineNesting.CreateSubDMesh();//Сеть постоена в координатах пространства модели
                                                if (sdm != null)
                                                {
                                                    using (Transaction tr = db.TransactionManager.StartTransaction())
                                                    {
                                                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
                                                        if (btr.GetBlockReferenceIds(true, false).Count > 1)
                                                        {
                                                            //Если у блока несколько вхождений, то создавать сеть в пространстве модели
                                                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                                                            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                                            ms.AppendEntity(sdm);
                                                            tr.AddNewlyCreatedDBObject(sdm, true);
                                                        }
                                                        else
                                                        {
                                                            //Если у блока только одно вхождение, то создавать сеть внутри блока
                                                            sdm.TransformBy(transform.Inverse());
                                                            btr.AppendEntity(sdm);
                                                            tr.AddNewlyCreatedDBObject(sdm, true);
                                                        }
                                                        tr.Commit();
                                                    }
                                                }



                                                //TEST
                                                //using (Transaction tr = db.TransactionManager.StartTransaction())
                                                //{
                                                //    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                                                //    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                                //    int colorIndex = 1;
                                                //    foreach (TriangleGraph tg in polylineNesting.TriangleGraphs.Values)
                                                //    {
                                                //        foreach (TriangleGraph.PolylinePart pp in tg.PolylineParts)
                                                //        {
                                                //            LinkedList<PolylinePt> seq = pp.PolylinePts;
                                                //            using (Polyline poly = new Polyline())
                                                //            {
                                                //                int n = 0;
                                                //                foreach (PolylinePt pt in seq)
                                                //                {
                                                //                    poly.AddVertexAt(n, pt.Point2D, 0, 0, 0);
                                                //                    n++;
                                                //                }
                                                //                poly.ColorIndex = colorIndex;
                                                //                colorIndex = (colorIndex + 1) % 6 + 1;
                                                //                poly.LineWeight = LineWeight.LineWeight030;
                                                //                ms.AppendEntity(poly);
                                                //                tr.AddNewlyCreatedDBObject(poly, true);
                                                //            }
                                                //        }

                                                //        foreach (List<Point3d> poligon in tg.Polygons)
                                                //        {
                                                //            using (Polyline poly = new Polyline())
                                                //            {
                                                //                for (int i = 0; i < poligon.Count; i++)
                                                //                {
                                                //                    poly.AddVertexAt(i, Utils.Point2DBy3D(poligon[i]), 0, 0, 0);
                                                //                }
                                                //                poly.Closed = true;
                                                //                poly.ColorIndex = 6;

                                                //                ms.AppendEntity(poly);
                                                //                tr.AddNewlyCreatedDBObject(poly, true);
                                                //            }
                                                //        }

                                                //    }



                                                //    foreach (TinSurfaceVertex vert in polylineNesting.InnerVerts)
                                                //    {
                                                //        using (Circle circle1 = new Circle(vert.Location, Vector3d.ZAxis, 0.3))
                                                //        {
                                                //            circle1.ColorIndex = 7;
                                                //            ms.AppendEntity(circle1);
                                                //            tr.AddNewlyCreatedDBObject(circle1, true);
                                                //        }
                                                //    }

                                                //    foreach (TinSurfaceTriangle triangle in polylineNesting.InnerTriangles)
                                                //    {
                                                //        using (Polyline poly = new Polyline())
                                                //        {
                                                //            poly.AddVertexAt(0, new Point2d(triangle.Vertex1.Location.X, triangle.Vertex1.Location.Y), 0, 0, 0);
                                                //            poly.AddVertexAt(1, new Point2d(triangle.Vertex2.Location.X, triangle.Vertex2.Location.Y), 0, 0, 0);
                                                //            poly.AddVertexAt(2, new Point2d(triangle.Vertex3.Location.X, triangle.Vertex3.Location.Y), 0, 0, 0);
                                                //            poly.Closed = true;
                                                //            poly.ColorIndex = 6;
                                                //            ms.AppendEntity(poly);
                                                //            tr.AddNewlyCreatedDBObject(poly, true);
                                                //        }
                                                //    }

                                                //    tr.Commit();
                                                //}
                                                //TEST


                                            }
                                        }



                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    string message = "Возникла ошибка при обработке одного из выбранных объектов";
                                    if (!String.IsNullOrEmpty(blockName))
                                    {
                                        message = "Возникла ошибка при обработке вхождения блока " + blockName;
                                    }
                                    Utils.ErrorToCommandLine(ed, message, ex);
                                }
                            }

                            ed.Regen();
                        }

                    }

                    


                }

            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при создании сетей по участкам поверхности");
            }

        }


        /// <summary>
        /// Аппроксимация дуг полилинии прямыми вставками
        /// Только для замкнутой полилинии
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        private static Polyline ApproximatePolyBulges(Polyline poly, double delta)
        {


            if (poly.HasBulges)
            {
                Polyline approxPoly = new Polyline();
                approxPoly.LayerId = poly.LayerId;
                int numVert = poly.NumberOfVertices;
                int n = 0;
                for (int i = 0; i < numVert; i++)
                {

                    double bulge = poly.GetBulgeAt(i);
                    if (bulge != 0)
                    {
                        double r = Math.Abs(1 / bulge);
                        double maxArcLength = Math.Acos((r - delta) / r) * r * 2;
                        double startDist = poly.GetDistanceAtParameter(i);
                        double endDist = poly.GetDistanceAtParameter((i + 1) % numVert);
                        double currDist = startDist;

                        bool exitLoop = false;
                        while (!exitLoop)
                        {
                            currDist += maxArcLength;
                            if (currDist < endDist || (endDist == 0 && currDist < poly.Length))
                            {
                                Point3d ptOnArc = poly.GetPointAtDist(currDist);
                                approxPoly.AddVertexAt(n, new Point2d(ptOnArc.X, ptOnArc.Y), 0, 0, 0);
                                n++;
                            }
                            else
                            {
                                Point3d ptOnArc = poly.GetPointAtDist(endDist);
                                approxPoly.AddVertexAt(n, new Point2d(ptOnArc.X, ptOnArc.Y), 0, 0, 0);
                                n++;
                                exitLoop = true;
                            }
                        }

                    }
                    else
                    {
                        approxPoly.AddVertexAt(n, poly.GetPoint2dAt((i + 1) % numVert), 0, 0, 0);
                        n++;
                    }
                }
                approxPoly.Closed = true;
                return approxPoly;
            }
            else
            {
                Polyline polyClone = (Polyline)poly.Clone();
                polyClone.Closed = true;
                return polyClone;
            }
        }

    }
}
