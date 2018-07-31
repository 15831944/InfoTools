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

[assembly: CommandClass(typeof(Civil3DInfoTools.SurfaceMeshByBoundary.SurfaceMeshByBoundaryCommand))]

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    //TODO: Команду можно вызывать только из пространства модели документа
    //TODO: Выдавать предупреждение если обнаружены пересекающиеся прямые
    //TODO: Сеть создавать не внутри блока, а в пространстве модели. Если у блока были привязаны данные Map3d, то привязать их к сети

    public class SurfaceMeshByBoundaryCommand
    {

        /// <summary>
        /// Все R-Tree во всех открытых документах
        /// Ключ - имя документа
        /// </summary>
        public static Dictionary<string, Dictionary<long, RBush<TinSurfaceVertexS>>> Trees
            = new Dictionary<string, Dictionary<long, RBush<TinSurfaceVertexS>>>();

        /// <summary>
        /// R-Tree в текущем документе
        /// Ключ - Handle поверхности
        /// </summary>
        public static Dictionary<long, RBush<TinSurfaceVertexS>> TreesCurrDoc = null;



        [CommandMethod("SurfaceMeshByBoundary", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SurfaceMeshByBoundary()
        {

            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            //Обновление ссылки на набор 2dTree текущего документа
            TreesCurrDoc = null;
            Trees.TryGetValue(adoc.Name, out TreesCurrDoc);
            if (TreesCurrDoc == null)
            {
                TreesCurrDoc = new Dictionary<long, RBush<TinSurfaceVertexS>>();
                Trees.Add(adoc.Name, TreesCurrDoc);
            }

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

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
                        PromptSelectionOptions pso = new PromptSelectionOptions();

                        acSSPrompt = adoc.Editor.GetSelection();
                        if (acSSPrompt.Status == PromptStatus.OK)
                        {
                            acSSet = acSSPrompt.Value;
                        }
                    }

                    if (acSSet != null)
                    {
                        foreach (SelectedObject acSSObj in acSSet)
                        {
                            if (acSSObj != null)
                            {
                                //полилинии внутри блока
                                List<Polyline> polylines = new List<Polyline>();
                                BlockReference blockReference = null;
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
                                        BlockTableRecord blockTableRecord = tr.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
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
                                        //Проверить, что структура данных вершин выбранной поверхности уже существует
                                        if (!TreesCurrDoc.ContainsKey(tinSurf.Handle.Value))
                                        {
                                            RBush<TinSurfaceVertexS> tree = new RBush<TinSurfaceVertexS>();
                                            List<TinSurfaceVertexS> list = new List<TinSurfaceVertexS>();
                                            TinSurfaceVertexCollection vc = tinSurf.Vertices;
                                            foreach (TinSurfaceVertex v in vc)
                                            {

                                                list.Add(new TinSurfaceVertexS(v));
                                            }

                                            tree.BulkLoad(list);

                                            TreesCurrDoc.Add(tinSurf.Handle.Value, tree);
                                            using (Transaction tr = db.TransactionManager.StartTransaction())
                                            {

                                                tinSurf = tr.GetObject(tinSurf.Id, OpenMode.ForWrite) as TinSurface;
                                                //При изменении поверхности удалять R-tree
                                                tinSurf.Modified += SurfModified_EventHandler;
                                                //При закрытии чертежа удалять R-tree
                                                Application.DocumentManager.DocumentDestroyed += DocDestroyed;
                                                tr.Commit();
                                            }
                                        }

                                        #region MyRegion
                                        //Test///////////////////////////////
                                        ////!!!нахождение треугольника в точке
                                        //TinSurfaceTriangle tinSurfaceTriangle = tinSurf.FindTriangleAtXY(blockReference.Position.X, blockReference.Position.Y);

                                        ////Polyline borderPoly = (Polyline)polylines.First().Clone();


                                        //Extents3d? ext = blockReference.Bounds;
                                        //if (ext != null)
                                        //{
                                        //    Point3dCollection border = new Point3dCollection();
                                        //    border.Add(ext.Value.MinPoint);
                                        //    border.Add(new Point3d(ext.Value.MinPoint.X, ext.Value.MaxPoint.Y, 0));
                                        //    border.Add(ext.Value.MaxPoint);
                                        //    border.Add(new Point3d(ext.Value.MaxPoint.X, ext.Value.MinPoint.Y, 0));
                                        //    border.Add(ext.Value.MinPoint);

                                        //    //!!!Нахождение точек внутри границ
                                        //    TinSurfaceVertex[] v1 = tinSurf.GetVerticesInsideBorder(border);
                                        //    TinSurfaceVertex[] v2 = tinSurf.GetVerticesInsideBorderRandom(border, 2);
                                        //}
                                        //Test/////////////////////////////// 
                                        #endregion

                                        //Проверить все линии на пересечение друг с другом. Удалить из списка те, которые имеют пересечения
                                        HashSet<Polyline> polylinesWithNoIntersections = new HashSet<Polyline>(polylines);
                                        for (int i = 0; i < polylines.Count; i++)
                                        {
                                            for (int j = i + 1; j < polylines.Count; j++)
                                            {
                                                Extents3d? ext1 = polylines[i].Bounds;
                                                Extents3d? ext2 = polylines[j].Bounds;
                                                if (ext1 != null && ext2 != null
                                                    && Utils
                                                    .BoxesAreSuperimposed(ext1.Value.MaxPoint, ext1.Value.MinPoint, ext2.Value.MaxPoint, ext2.Value.MinPoint))
                                                {
                                                    //Сначала проверяем перекрываются ли BoundingBox.
                                                    //Во многих случаях это гораздо производительнее и затем считаем пеерсечения
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

                                        //Аппроксимация всех полилиний, которые имеют кривизну
                                        List<Polyline> polylinesWithNoBulges = new List<Polyline>();
                                        foreach (Polyline poly in polylinesWithNoIntersections)
                                        {
                                            polylinesWithNoBulges.Add(ApproximatePolyBulges(poly, 0.02));
                                        }

                                        //Удалить все повторяющиеся подряд точки полилинии
                                        foreach (Polyline poly in polylinesWithNoBulges)
                                        {
                                            for (int i =0;i< poly.NumberOfVertices;)
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
                                        PolylineNesting polylineNesting = new PolylineNesting(/*transform,*/ tinSurf
                                            , db, ed//TEST
                                            );
                                        foreach (Polyline poly in polylinesWithNoBulges)
                                        {
                                            poly.TransformBy(transform);
                                            polylineNesting.Insert(poly);
                                        }

                                        //Расчет полигонов
                                        polylineNesting.CalculatePoligons();

                                        //TEST
                                        using (Transaction tr = db.TransactionManager.StartTransaction())
                                        {
                                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                                            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                            int colorIndex = 1;
                                            foreach (TriangleGraph tg in polylineNesting.TriangleGraphs.Values)
                                            {
                                                foreach (LinkedList<PolylinePt> seq in tg.Sequences)
                                                {
                                                    using (Polyline poly = new Polyline())
                                                    {
                                                        int n = 0;
                                                        foreach (PolylinePt pt in seq)
                                                        {
                                                            poly.AddVertexAt(n, pt.Point2D, 0, 0, 0);
                                                            n++;
                                                        }
                                                        poly.ColorIndex = colorIndex;
                                                        colorIndex = (colorIndex + 1) % 6 + 1;
                                                        poly.LineWeight = LineWeight.LineWeight030;
                                                        ms.AppendEntity(poly);
                                                        tr.AddNewlyCreatedDBObject(poly, true);
                                                    }
                                                }

                                            }

                                            tr.Commit();
                                        }
                                        //TEST


                                    }
                                }



                            }
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
                            if (currDist < endDist || (endDist==0 && currDist < poly.Length))
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


        /// <summary>
        /// При изменении поверхности удалить ее старое R-tree 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SurfModified_EventHandler(object sender, EventArgs e)
        {
            TinSurface tinSurf = sender as TinSurface;
            TreesCurrDoc.Remove(tinSurf.Handle.Value);
        }

        /// <summary>
        /// При закрытии документа удалить все R-tree, относящиеся к нему
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="acDocDesEvtArgs"></param>
        public static void DocDestroyed(object obj, DocumentDestroyedEventArgs acDocDesEvtArgs)
        {
            Trees.Remove(acDocDesEvtArgs.FileName);
        }

    }
}
