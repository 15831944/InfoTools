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
//using RBush;

[assembly: CommandClass(typeof(Civil3DInfoTools.SurfaceMeshByBoundary.SurfaceMeshByBoundaryCommand))]

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    //TODO: Команду можно вызывать только из пространства модели документа

    public class SurfaceMeshByBoundaryCommand
    {
        #region MyRegion
        /// <summary>
        /// Все 2dTree во всех открытых документах
        /// Ключ - имя документа
        /// </summary>
        //public static Dictionary<string, Dictionary<long, RBush<TinSurfaceVertexS>>> Trees
        //    = new Dictionary<string, Dictionary<long, RBush<TinSurfaceVertexS>>>();

        /// <summary>
        /// 2dTree в текущем документе
        /// Ключ - Handle поверхности
        /// </summary>
        //public static Dictionary<long, RBush<TinSurfaceVertexS>> TreesCurrDoc = null; 
        #endregion


        [CommandMethod("SurfaceMeshByBoundary", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SurfaceMeshByBoundary()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            #region MyRegion
            //Обновление ссылки на набор 2dTree текущего документа
            //TreesCurrDoc = null;
            //Trees.TryGetValue(adoc.Name, out TreesCurrDoc);
            //if (TreesCurrDoc == null)
            //{
            //    TreesCurrDoc = new Dictionary<long, RBush<TinSurfaceVertexS>>();
            //    Trees.Add(adoc.Name, TreesCurrDoc);
            //} 
            #endregion

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
                        tinSurf = tr.GetObject(per1.ObjectId, OpenMode.ForWrite) as TinSurface;

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
                                    using (Transaction tr1 = db.TransactionManager.StartTransaction())
                                    {
                                        //блок внутри набора выбора
                                        BlockReference blockReference = tr1.GetObject(acSSObj.ObjectId, OpenMode.ForWrite) as BlockReference;
                                        Matrix3d transform = Matrix3d.Identity;
                                        if (blockReference != null)
                                        {
                                            //трансформация из системы координат блока в мировую систему координат
                                            transform = blockReference.BlockTransform;

                                            //Перебор всех объектов внутри блока
                                            //Найти все правильные полилинии в блоке
                                            BlockTableRecord blockTableRecord = tr1.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                            foreach (ObjectId id in blockTableRecord)
                                            {
                                                AcadDb.Entity ent = tr1.GetObject(id, OpenMode.ForWrite) as AcadDb.Entity;
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

                                            if (polylines.Count > 0)
                                            {
                                                #region MyRegion
                                                //Проверить, что структура данных вершин выбранной поверхности уже существует
                                                //if (!TreesCurrDoc.ContainsKey(tinSurf.Handle.Value))
                                                //{
                                                //    RBush<TinSurfaceVertexS> tree = new RBush<TinSurfaceVertexS>();
                                                //    List<TinSurfaceVertexS> list = new List<TinSurfaceVertexS>();
                                                //    TinSurfaceVertexCollection vc = tinSurf.Vertices;
                                                //    foreach (TinSurfaceVertex v in vc)
                                                //    {

                                                //        list.Add(new TinSurfaceVertexS(v));
                                                //    }

                                                //    tree.BulkLoad(list);

                                                //    TreesCurrDoc.Add(tinSurf.Handle.Value, tree);
                                                //    //При изменении поверхности удалять R-tree
                                                //    tinSurf.Modified += SurfModified_EventHandler;
                                                //    //При закрытии чертежа удалять R-tree
                                                //    Application.DocumentManager.DocumentDestroyed += DocDestroyed;
                                                //} 
                                                #endregion
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

                                                //Аппроксимация всех полилиний, которые имеют кривизну
                                                List<Polyline> polylinesWithNoBulges = new List<Polyline>();
                                                foreach (Polyline poly in polylinesWithNoIntersections)
                                                {
                                                    polylinesWithNoBulges.Add(ApproximatePolyBulges(poly, 0.02));
                                                }


                                                //Построение дерева вложенности полилиний
                                                PolylineNesting polylineNesting = new PolylineNesting(/*transform,*/ tinSurf);
                                                foreach (Polyline poly in polylinesWithNoBulges)
                                                {
                                                    poly.TransformBy(transform);
                                                    polylineNesting.Insert(poly);
                                                }

                                                polylineNesting.CalculatePoligons();
                                            }
                                        }

                                        tr1.Commit();
                                    }
                                }
                            }
                        }

                        tr.Commit();
                    }
                }

            }
            catch (System.Exception ex)
            {
                CommonException(ex, "Ошибка при создании сетей по участкам поверхности");
            }

        }

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
                            if (currDist < endDist)
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
                        approxPoly.AddVertexAt(n, poly.GetPoint2dAt(i), 0, 0, 0);
                        n++;
                    }
                }

                return approxPoly;
            }
            else
            {
                return (Polyline)poly.Clone();
            }
        }


        #region MyRegion
        ///// <summary>
        ///// При изменении поверхности удалить ее старое R-tree 
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //public void SurfModified_EventHandler(object sender, EventArgs e)
        //{
        //    TinSurface tinSurf = sender as TinSurface;
        //    TreesCurrDoc.Remove(tinSurf.Handle.Value);
        //}

        ///// <summary>
        ///// При закрытии документа удалить все R-tree, относящиеся к нему
        ///// </summary>
        ///// <param name="obj"></param>
        ///// <param name="acDocDesEvtArgs"></param>
        //public static void DocDestroyed(object obj, DocumentDestroyedEventArgs acDocDesEvtArgs)
        //{
        //    Trees.Remove(acDocDesEvtArgs.FileName);
        //} 
        #endregion
    }
}
