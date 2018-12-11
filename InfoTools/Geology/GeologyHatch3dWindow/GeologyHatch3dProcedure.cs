using Autodesk.AutoCAD.DatabaseServices;
using AcadDB = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Civil3DInfoTools.RBush;
using RBush;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using Autodesk.AutoCAD.Colors;

namespace Civil3DInfoTools.Geology.GeologyHatch3dWindow
{
    public partial class GeologyHatch3dViewModel : INotifyPropertyChanged
    {
        private void Create3dProfile(object arg)
        {
            try
            {
                if (doc != null)
                {

                    List<ObjectId> toHighlight = new List<ObjectId>();

                    using (doc.LockDocument())
                    {
                        Editor ed = doc.Editor;
                        Database db = doc.Database;

                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            //найти координату X начала профиля (крайнюю левую)
                            double minx = double.PositiveInfinity;
                            List<ObjectId> allSelected = new List<ObjectId>(soilHatchIds);
                            foreach (ObjectId id in allSelected)
                            {
                                Entity ent = null;
                                try
                                {
                                    ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                }
                                catch (Autodesk.AutoCAD.Runtime.Exception) { continue; }
                                Extents3d? ext = ent.Bounds;
                                if (ext != null)
                                {
                                    Point3d minPt = ext.Value.MinPoint;
                                    if (minx > minPt.X) minx = minPt.X;
                                }

                            }


                            //Штриховки должны быть заранее раскиданы по слоям в соответствии с ИГЭ!

                            //пересчет всех точек штриховок в координаты:
                            //X - положение отностиельно начала профиля с учетом горизонтального масштаба профиля,
                            //Y - отметка расчитанная согласно базовой отметке с учетом вертикального масштаба профиля
                            Matrix2d transform =
                                    new Matrix2d(new double[]
                                    {
                                    HorScaling,      0,           0,
                                    0,               VertScaling, 0,
                                    0,               0,           1
                                    })
                                    * Matrix2d.Displacement(new Vector2d(-minx, -ElevBasePoint.Value.Y + ElevationInput / VertScaling));

                            C5.IntervalHeap<HatchEvent> eventQueue = new C5.IntervalHeap<HatchEvent>();
                            List<HatchData> allHatchData = new List<HatchData>();
                            List<Point2dCollection> selfintersectingLoops = new List<Point2dCollection>();
                            foreach (ObjectId id in soilHatchIds)
                            {
                                //получить все точки штриховок с учетом возможных дуг, сплайнов и проч
                                //Для каждой штриховки создается набор композитных кривых, состоящих из линейных сегментов
                                Hatch hatch = null;
                                try
                                {
                                    hatch = (Hatch)tr.GetObject(id, OpenMode.ForRead);
                                }
                                catch (Autodesk.AutoCAD.Runtime.Exception) { continue; }
                                List<CompositeCurve2d> boundaries = new List<CompositeCurve2d>();
                                List<Extents2d> extends = new List<Extents2d>();
                                List<Point2dCollection> ptsCollList = new List<Point2dCollection>();
                                List<List<double>> ptParamsList = new List<List<double>>();
                                for (int i = 0; i < hatch.NumberOfLoops; i++)
                                {
                                    HatchLoop hl = hatch.GetLoopAt(i);
                                    if (!hl.LoopType.HasFlag(HatchLoopTypes.SelfIntersecting)
                                            && !hl.LoopType.HasFlag(HatchLoopTypes.Textbox)
                                            && !hl.LoopType.HasFlag(HatchLoopTypes.TextIsland)
                                            && !hl.LoopType.HasFlag(HatchLoopTypes.NotClosed))
                                    {
                                        List<Curve2d> curves = Utils.GetHatchLoopCurves(hl);
                                        List<Curve2d> compositeCurveElems = new List<Curve2d>();
                                        double _minx = double.PositiveInfinity;
                                        double _miny = double.PositiveInfinity;
                                        double _maxx = double.NegativeInfinity;
                                        double _maxy = double.NegativeInfinity;
                                        Action<Point2d> updateExtends = new Action<Point2d>(p =>
                                        {
                                            _minx = p.X < _minx ? p.X : _minx;
                                            _miny = p.Y < _miny ? p.Y : _miny;
                                            _maxx = p.X > _maxx ? p.X : _maxx;
                                            _maxy = p.Y > _maxy ? p.Y : _maxy;
                                        });

                                        Point2dCollection ptsColl = new Point2dCollection();
                                        List<double> ptParams = new List<double>();
                                        double currParam = 0;
                                        foreach (Curve2d c in curves)
                                        {
                                            if (!(c is LineSegment2d))
                                            {
                                                Interval interval = c.GetInterval();
                                                PointOnCurve2d[] samplePts = c.GetSamplePoints(interval.LowerBound, interval.UpperBound, 0.02);

                                                Point2d[] pts = samplePts.Select(p => transform * p.Point).ToArray();
                                                for (int n = 0; n < pts.Length - 1; n++)
                                                {
                                                    LineSegment2d lineSeg = new LineSegment2d(pts[n], pts[n + 1]);
                                                    compositeCurveElems.Add(lineSeg);

                                                    ptsColl.Add(pts[n]);
                                                    ptParams.Add(currParam);
                                                    updateExtends(pts[n]);
                                                    currParam += lineSeg.Length;

                                                }
                                            }
                                            else
                                            {
                                                LineSegment2d lineSeg = (LineSegment2d)c;
                                                lineSeg.TransformBy(transform);
                                                compositeCurveElems.Add(lineSeg);

                                                ptsColl.Add(lineSeg.StartPoint);
                                                ptParams.Add(currParam);
                                                updateExtends(lineSeg.StartPoint);
                                                currParam += lineSeg.Length;
                                            }
                                        }


                                        CompositeCurve2d boundary = new CompositeCurve2d(compositeCurveElems.ToArray());
                                        Extents2d ext = new Extents2d(_minx, _miny, _maxx, _maxy);
                                        boundaries.Add(boundary);
                                        ptsCollList.Add(ptsColl);
                                        ptParamsList.Add(ptParams);
                                        extends.Add(ext);
                                    }
                                }

                                //контуры штриховок не могут иметь самопересечений!
                                #region Проверка на пересечения
                                //проверка на самопересечения
                                //bool badBoundaries = false;
                                HashSet<int> badBoundaries = new HashSet<int>();
                                HashSet<int> splitBoundaries = new HashSet<int>();//Если 2 контура в одной штриховке пересекаются, то разносить их по разным штриховкам
                                //List<HatchData> decomposeHatchData = new List<HatchData>();//TODO: самопересекающиеся полигоны нужно разбить на отдельные по количеству самопересечний.

                                for (int i = 0; i < boundaries.Count; i++)
                                {
                                    CompositeCurve2d b = boundaries[i];
                                    CurveCurveIntersector2d intersector = new CurveCurveIntersector2d(b, b);
                                    if (intersector.NumberOfIntersectionPoints > 0)
                                    {
                                        //если происходит только наложение???
                                        badBoundaries.Add(i);
                                        selfintersectingLoops.Add(ptsCollList[i]);
                                    }
                                }

                                if (boundaries.Count > 1)
                                {
                                    //проверка на взаимные пересечения.
                                    //Исп RBush для того чтобы избежать проверки на пересечение каждого с каждым и квадратичной сложности
                                    //(работает только если контуры разнесены)
                                    //Не брать в расчет пересечения по касательной
                                    RBush<Spatial> boundariesRBush = new RBush<Spatial>();
                                    List<Spatial> spatialData = new List<Spatial>();
                                    for (int i = 0; i < extends.Count; i++)
                                    {
                                        spatialData.Add(new Spatial(extends[i], i));
                                    }
                                    boundariesRBush.BulkLoad(spatialData);
                                    foreach (Spatial s in spatialData)
                                    {
                                        IReadOnlyList<Spatial> nearestNeighbors = boundariesRBush.Search(s.Envelope);
                                        if (nearestNeighbors.Count > 1)
                                        {
                                            CompositeCurve2d thisCurve = boundaries[(int)s.Obj];
                                            foreach (Spatial n in nearestNeighbors)
                                            {
                                                if (!s.Equals(n))
                                                {
                                                    CompositeCurve2d otherCurve = boundaries[(int)n.Obj];
                                                    CurveCurveIntersector2d intersector
                                                        = new CurveCurveIntersector2d(thisCurve, otherCurve);
                                                    if (intersector.NumberOfIntersectionPoints > 0 ||
                                                        intersector.OverlapCount > 0)
                                                    {
                                                        bool matches = false;
                                                        //Проверить, что кривые не накладываются друг на друга по всей длине (то есть полностью совпадают)
                                                        if (intersector.OverlapCount > 0)
                                                        {
                                                            //сумма длин всех интервалов перекрытия равна общей длине кривой
                                                            double thisCurveOverlapLength = 0;
                                                            double otherCurveOverlapLength = 0;
                                                            for (int i = 0; i < intersector.OverlapCount; i++)
                                                            {
                                                                Interval[] intervals = intersector.GetOverlapRanges(i);
                                                                Interval thisOverlapInterval = intervals[0];
                                                                thisCurveOverlapLength += thisOverlapInterval.Length;
                                                                Interval otherOverlapInterval = intervals[1];
                                                                otherCurveOverlapLength += otherOverlapInterval.Length;
                                                            }

                                                            Interval thisCurveInterval = thisCurve.GetInterval();
                                                            Interval otherCurveInterval = otherCurve.GetInterval();

                                                            if (Utils.LengthIsEquals(thisCurveOverlapLength, thisCurveInterval.Length)
                                                                && Utils.LengthIsEquals(otherCurveOverlapLength, otherCurveInterval.Length))
                                                            {
                                                                matches = true;
                                                            }

                                                        }

                                                        if (!matches)
                                                        {
                                                            splitBoundaries.Add((int)s.Obj);
                                                            splitBoundaries.Add((int)n.Obj);
                                                        }
                                                        else
                                                        {
                                                            badBoundaries.Add((int)s.Obj);
                                                            badBoundaries.Add((int)n.Obj);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                splitBoundaries.ExceptWith(badBoundaries);
                                List<HatchData> splitHatchData = new List<HatchData>();
                                if (badBoundaries.Count > 0 || splitBoundaries.Count > 0)
                                {
                                    List<CompositeCurve2d> boundariesClear = new List<CompositeCurve2d>();
                                    List<Extents2d> extendsClear = new List<Extents2d>();
                                    List<Point2dCollection> ptsCollListClear = new List<Point2dCollection>();
                                    List<List<double>> ptParamsListClear = new List<List<double>>();

                                    for (int i = 0; i < boundaries.Count; i++)
                                    {
                                        if (!badBoundaries.Contains(i) && !splitBoundaries.Contains(i))
                                        {
                                            boundariesClear.Add(boundaries[i]);
                                            extendsClear.Add(extends[i]);
                                            ptsCollListClear.Add(ptsCollList[i]);
                                            ptParamsListClear.Add(ptParamsList[i]);
                                        }
                                    }

                                    foreach (int index in splitBoundaries)
                                    {
                                        splitHatchData.Add(new HatchData(
                                            new HatchNestingNode(
                                            boundaries[index],
                                            extends[index],
                                            ptsCollList[index],
                                            ptParamsList[index], hatch)));
                                    }


                                    boundaries = boundariesClear;
                                    extends = extendsClear;
                                    ptsCollList = ptsCollListClear;
                                    ptParamsList = ptParamsListClear;
                                }
                                #endregion

                                //определяется вложенность контуров штриховки
                                //ЕСЛИ ШТРИХОВКА СОСТОИТ ИЗ 2 И БОЛЕЕ КОНТУРОВ, КОТОРЫЕ НЕ ВЛОЖЕНЫ ДРУГ В ДРУГА, 
                                //ТО ЭТИ КОНТУРЫ ДОЛЖНЫ РАССМАТРИВАТЬСЯ КАК ОТДЕЛЬНЫЕ ШТРИХОВКИ!!!
                                HatchNestingTree hatchNestingTree
                                    = new HatchNestingTree(boundaries, extends, ptsCollList, ptParamsList, hatch);
                                List<HatchData> currHatchData = hatchNestingTree.GetHatchData();
                                currHatchData.AddRange(splitHatchData);//добавить контуры, полученные из взаимно пересекающихся контуров
                                //currHatchData.AddRange(decomposeHatchData);

                                allHatchData.AddRange(currHatchData);

                                //Каждая штриховка имеет диапазон по X от начала до конца по оси.
                                //В общую очередь событий сохраняются события начала и конца штриховки
                                foreach (HatchData hd in currHatchData)
                                {
                                    hd.AddEventsToQueue(eventQueue);
                                }

                            }




                            //Трассу разбить на отрезки, на которых будут вставлены прямолинейные сегменты штриховок
                            Polyline alignmentPoly = null;
                            try
                            {
                                alignmentPoly = (Polyline)tr.GetObject(AlignmentPolyId, OpenMode.ForRead);
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception)
                            {
                                return;
                            }
                            int segments = alignmentPoly.NumberOfVertices - 1;
                            List<AlignmentSegment> alignmentSegments = new List<AlignmentSegment>();
                            Action<Point2d, Point2d> addAlignmentSegment = new Action<Point2d, Point2d>((p0, p1) =>
                            {
                                double start = alignmentPoly.GetDistAtPoint(new Point3d(p0.X, p0.Y, 0));
                                double end = alignmentPoly.GetDistAtPoint(new Point3d(p1.X, p1.Y, 0));
                                if (Math.Abs(start - end) > Tolerance.Global.EqualPoint)//TODO: Это спорный момент - ведь может быть большое множество очень коротких участков подряд!
                                {
                                    Vector2d startDir = p1 - p0;
                                    alignmentSegments.Add(new AlignmentSegment(start, end, p0, startDir));
                                }
                            });
                            for (int i = 0; i < segments; i++)
                            {
                                SegmentType segmentType = alignmentPoly.GetSegmentType(i);
                                Point2d startLoc = alignmentPoly.GetPoint2dAt(i);
                                Point2d endLoc = alignmentPoly.GetPoint2dAt(i + 1);
                                switch (segmentType)
                                {
                                    case SegmentType.Line:
                                        addAlignmentSegment(startLoc, endLoc);
                                        break;
                                    case SegmentType.Arc:
                                        CircularArc2d arc = new CircularArc2d(startLoc, endLoc, alignmentPoly.GetBulgeAt(i), false);
                                        Interval interval = arc.GetInterval();
                                        PointOnCurve2d[] samplePts = arc.GetSamplePoints(interval.LowerBound, interval.UpperBound, 0.1);
                                        for (int n = 0; n < samplePts.Length - 1; n++)
                                        {
                                            addAlignmentSegment(samplePts[n].Point, samplePts[n + 1].Point);
                                        }
                                        break;
                                }
                            }


                            //проход по каждому отрезку трассы (диапазон отрезка - диапазон длины полилинии)
                            HashSet<HatchData> currentHatchData = new HashSet<HatchData>();
                            foreach (AlignmentSegment alignmentSegment in alignmentSegments)
                            {
                                if (eventQueue.Count == 0) break;//штриховки закончились

                                //Получить те штриховки, диапазон по X которых пересекается с диапазоном текущего отрезка
                                //(СКАНИРУЮЩАЯ ЛИНИЯ: события - начало, конец штриховки)
                                HashSet<HatchData> intervalHatchData = new HashSet<HatchData>(currentHatchData);//штриховки пришедшие из предыдущего участка остаются все

                                //Собрать все события до конца сегмента
                                //Если при проходе от начала до конца сегмента какая-то штриховка проходится полностью от начала до конца, то 
                                //все ее контуры без изменений должны быть переданы для создания М-полигона!!! (для них обход графа не нужен!)
                                HashSet<HatchData> startedInsideInterval = new HashSet<HatchData>();
                                List<HatchData> hatchesCompletelyInsideInterval = new List<HatchData>();
                                while (eventQueue.Count > 0)
                                {
                                    HatchEvent nextEvent = eventQueue.FindMin();
                                    if (nextEvent.Position > alignmentSegment.End)
                                    {
                                        break;
                                    }
                                    else if (nextEvent.Start)
                                    {
                                        //добавить штриховку в текущий набор
                                        HatchData hd = eventQueue.DeleteMin().HatchData;
                                        currentHatchData.Add(hd);
                                        //добавлять в набор текущего интервла только в том случае,
                                        //если сканирующая линия еще не дошла до конца интервала
                                        if (nextEvent.Position < alignmentSegment.End
                                            && !Utils.LengthIsEquals(nextEvent.Position, alignmentSegment.End))//Допуск нужен
                                        {
                                            startedInsideInterval.Add(hd);
                                            intervalHatchData.Add(hd);
                                        }

                                    }
                                    else
                                    {
                                        //убрать штриховку из текущего набора
                                        HatchData hd = eventQueue.DeleteMin().HatchData;
                                        currentHatchData.Remove(hd);

                                        if (startedInsideInterval.Contains(hd))
                                            hatchesCompletelyInsideInterval.Add(hd);
                                    }

                                }

                                foreach (HatchData hd in hatchesCompletelyInsideInterval)
                                {
                                    HatchSegmentData hsd = new HatchSegmentData(hd);
                                    alignmentSegment.HatchSegmentData.Add(hsd);

                                    hsd.Polygons = hd.GetAllBoundaries();
                                }


                                intervalHatchData.ExceptWith(hatchesCompletelyInsideInterval);
                                foreach (HatchData hd in intervalHatchData)
                                {
                                    HatchSegmentData hsd = new HatchSegmentData(hd);
                                    alignmentSegment.HatchSegmentData.Add(hsd);
                                    //для каждой штриховки выполнить построение и обход графа сегмента штриховки
                                    HatchSegmentGraph graph = new HatchSegmentGraph(alignmentSegment.Start, alignmentSegment.End, hd, doc.Editor);

                                    //сохранить наборы полигонов для текущего диапазона
                                    hsd.Polygons = graph.Result;

                                }

                            }



                            //для каждого диапазона создать полученные полигоны в 3d
                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                            BlockTableRecord ms
                                = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                            BlockTableRecord btr = new BlockTableRecord();//Каждый профиль в отдельный блок
                            btr.Name = Guid.NewGuid().ToString();
                            ObjectId btrId = bt.Add(btr);
                            tr.AddNewlyCreatedDBObject(btr, true);

                            foreach (AlignmentSegment alignmentSegment in alignmentSegments)
                            {
                                List<Entity> flatObjs = new List<Entity>();

                                foreach (HatchSegmentData hsd in alignmentSegment.HatchSegmentData)
                                {
                                    //PlaneSurface
                                    hsd.GetNesting();
                                    Dictionary<Point2dCollection, List<Point2dCollection>> bwhs = hsd.GetBoundariesWithHoles();
                                    foreach (KeyValuePair<Point2dCollection, List<Point2dCollection>> bwh in bwhs)
                                    {
                                        //создать Region
                                        Region region = null;
                                        using (Polyline poly = new Polyline())
                                        {
                                            for (int i = 0; i < bwh.Key.Count; i++)
                                            {
                                                poly.AddVertexAt(i, bwh.Key[i], 0, 0, 0);
                                            }
                                            poly.Closed = true;
                                            DBObjectCollection coll = new DBObjectCollection();
                                            coll.Add(poly);
                                            try
                                            {
                                                DBObjectCollection regionColl = Region.CreateFromCurves(coll);
                                                foreach (DBObject dbo in regionColl)
                                                {
                                                    region = (Region)dbo;
                                                    break;
                                                }
                                            }
                                            catch { }
                                        }

                                        //из Region создать PlaneSurface
                                        if (region != null)
                                        {
                                            using (PlaneSurface planeSurface = new PlaneSurface())
                                            {
                                                planeSurface.CreateFromRegion(region);
                                                planeSurface.LayerId = hsd.Hatch.LayerId;
                                                planeSurface.ColorIndex = 256;




                                                ObjectId planeSurfaceId = ms.AppendEntity(planeSurface);
                                                tr.AddNewlyCreatedDBObject(planeSurface, true);


                                                //вырезать отверстия в PlaneSurface

                                                foreach (Point2dCollection holePts2d in bwh.Value)
                                                {
                                                    using (Polyline poly = new Polyline())
                                                    {
                                                        for (int i = 0; i < holePts2d.Count; i++)
                                                        {
                                                            poly.AddVertexAt(i, holePts2d[i], 0, 0, 0);
                                                        }
                                                        poly.Closed = true;

                                                        ObjectIdCollection trimPolyColl = new ObjectIdCollection();
                                                        trimPolyColl.Add(ms.AppendEntity(poly));
                                                        tr.AddNewlyCreatedDBObject(poly, true);

                                                        List<Point2d> ptsList = new List<Point2d>(holePts2d.ToArray());
                                                        Point2d pickPt2d = Utils.GetAnyPointInsidePoligon(ptsList,
                                                            Utils.DirectionIsClockwise(ptsList));

                                                        try
                                                        {
                                                            AcadDB.Surface.TrimSurface(planeSurfaceId, new ObjectIdCollection(), trimPolyColl,
                                                                                                                new Vector3dCollection() { Vector3d.ZAxis }, new Point3d(pickPt2d.X, pickPt2d.Y, 0),
                                                                                                                -Vector3d.ZAxis, false, false);
                                                        }
                                                        catch /*(Exception ex)*/
                                                        {
                                                            //Вывод в командную строку
                                                            Utils.ErrorToCommandLine(doc.Editor,
                                                                "Ошибка при попытке вырезания отверстия в поверхности"/*, ex*/);
                                                        }

                                                        //Удалить все объекты, добавленные в чертеж!
                                                        poly.Erase();
                                                    }
                                                }


                                                flatObjs.Add((Entity)planeSurface.Clone());

                                                //Удалить все объекты, добавленные в чертеж!
                                                planeSurface.Erase();

                                            }
                                            region.Dispose();
                                        }



                                    }

                                }



                                foreach (Entity ent in flatObjs)
                                {
                                    ent.TransformBy(alignmentSegment.Transform);
                                    /*ms*/
                                    btr.AppendEntity(ent);
                                    tr.AddNewlyCreatedDBObject(ent, true);
                                    ent.Dispose();
                                }

                            }

                            BlockReference br = new BlockReference(Point3d.Origin, btrId);
                            ms.AppendEntity(br);
                            tr.AddNewlyCreatedDBObject(br, true);


                            if (selfintersectingLoops.Count > 0)
                            {
                                Utils.ErrorToCommandLine(doc.Editor,
                                    "Отбраковано самопересекающихся контуров штриховок - " + selfintersectingLoops.Count + " (отмечены на профиле)");

                                ObjectId layerId = Utils.CreateLayerIfNotExists("САМОПЕРЕСЕКАЮЩИЙСЯ КОНТУР ШТРИХОВКИ", db, tr,
                                    color: Color.FromColorIndex(ColorMethod.ByAci, 1), lineWeight: LineWeight.LineWeight200);
                                Matrix2d returnTransform = transform.Inverse();
                                foreach (Point2dCollection pts in selfintersectingLoops)
                                {
                                    using (Polyline selfIntersectingPoly = new Polyline())
                                    {
                                        selfIntersectingPoly.LayerId = layerId;
                                        selfIntersectingPoly.ColorIndex = 256;
                                        for (int i = 0; i < pts.Count; i++)
                                        {
                                            Point2d pt = pts[i].TransformBy(returnTransform);
                                            selfIntersectingPoly.AddVertexAt(i, pt, 0, 0, 0);
                                        }

                                        toHighlight.Add(ms.AppendEntity(selfIntersectingPoly));
                                        tr.AddNewlyCreatedDBObject(selfIntersectingPoly, true);

                                        //selfIntersectingPoly.Highlight();
                                    }
                                }
                            }


                            tr.Commit();
                        }
                    }
                    ps.Visible = false;

                    if (toHighlight.Count > 0)
                    {
                        using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            foreach (ObjectId id in toHighlight)
                            {
                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                ent.Highlight();
                            }
                            tr.Commit();
                        }                            
                    }

                }

            }
            catch (System.Exception ex)
            {
                GeologyConvertationCommand.ClosePalette(null, null);
                CommonException(ex, "Ошибка при создании 3d профиля геологии");
            }
        }




        private class HatchEvent : IComparable<HatchEvent>
        {
            public bool Start { get; private set; }

            public HatchData HatchData { get; private set; }

            public double Position
            {
                get
                {
                    return Start ? HatchData.Start : HatchData.End;
                }
            }
            public HatchEvent(bool start, HatchData hatchData)
            {
                Start = start;
                HatchData = hatchData;
            }

            public int CompareTo(HatchEvent other)
            {
                return this.Position.CompareTo(other.Position);
            }
        }


        private class HatchNestingTree : PolygonNestingTree
        {
            public HatchNestingTree(List<CompositeCurve2d> boundaries,
                List<Extents2d> extends, List<Point2dCollection> pts, List<List<double>> ptParams, Hatch hatch)
            {
                for (int i = 0; i < boundaries.Count; i++)
                {
                    HatchNestingNode insertingNode
                        = new HatchNestingNode(boundaries[i], extends[i], pts[i], ptParams[i], hatch);
                    Insert(root, insertingNode);
                }
            }

            public List<HatchData> GetHatchData()
            {
                return root.NestedNodes.Select(hnn => new HatchData((HatchNestingNode)hnn)).ToList();
            }
        }


        /// <summary>
        /// Узел вложенности штриховок
        /// </summary>
        private class HatchNestingNode : PolygonNestingNode
        {
            //public List<HatchNestingNode> NestedNodes { get; private set; } = new List<HatchNestingNode>();

            public CompositeCurve2d Boundary { get; private set; }

            public Extents2d Extents { get; private set; }

            //public Point2dCollection Point2dCollection { get; private set; }

            private List<double> ptParams;

            public Hatch Hatch { get; private set; }

            public HatchNestingNode(CompositeCurve2d boundary, Extents2d extends,
                Point2dCollection ptsColl, List<double> ptParams, Hatch hatch) : base(ptsColl)
            {
                Boundary = boundary;
                Extents = extends;
                //base.Point2dCollection = ptsColl;
                this.ptParams = ptParams;
                Hatch = hatch;
            }




            public bool PointIsInside(Point2d pt)
            {
                return Utils.PointIsInsidePolylineWindingNumber(pt, this.Point2dCollection);
            }


            /// <summary>
            /// Предполагает, что начало и конец находятся в порядке возрастания параметра
            /// (но при этом может быть переход через ноль - в этом случае конечный параметр меньше начального)
            /// В таком же порядке возвращаются точки
            /// </summary>
            /// <param name="startParam"></param>
            /// <param name="endParam"></param>
            /// <returns></returns>
            public List<Point2d> GetPointsInInterval(double startParam, double endParam)
            {
                if (startParam > ptParams.Last() && endParam > ptParams.Last())
                {
                    //если и startParam и endParam находятся на замыкающем звене - возникнет ошибка
                    //Можно сразу возвратить пустой список
                    return new List<Point2d>();
                }

                bool goThroughZero = startParam > endParam;
                if (goThroughZero)
                {
                    //проверить на попадание в интервал точек в начале и в конце списка
                    List<Point2d> ptsFromStart = new List<Point2d>();
                    int i = 0;
                    while (ptParams[i] < endParam)
                    {
                        ptsFromStart.Add(Point2dCollection[i]);
                        i++;
                    }
                    List<Point2d> ptsFromEnd = new List<Point2d>();
                    i = ptParams.Count - 1;
                    while (ptParams[i] > startParam)
                    {
                        ptsFromEnd.Add(Point2dCollection[i]);
                        i--;
                    }

                    List<Point2d> result = new List<Point2d>(ptsFromEnd);
                    result.Reverse();
                    result.AddRange(ptsFromStart);


                    return result;
                }
                else
                {
                    //бинарный поиск точки, которая попадает в указанный интервал
                    //и затем если найдена взять все соседние, попадающие в интервал
                    //простейший пример бинарного поиска - https://www.baeldung.com/java-binary-search
                    int low = 0;
                    int high = ptParams.Count;

                    int index = -1;
                    while (low <= high)
                    {
                        int mid = (low + high) / 2;
                        if (ptParams[mid] < startParam)
                        {
                            low = mid + 1;
                        }
                        else if (ptParams[mid] > endParam)
                        {
                            high = mid - 1;
                        }
                        else
                        {
                            index = mid;
                            break;
                        }
                    }
                    if (index >= 0)
                    {
                        int baseIndex = index;
                        LinkedList<Point2d> resultLl = new LinkedList<Point2d>();
                        resultLl.AddFirst(Point2dCollection[index]);

                        while (ptParams[--index] > startParam)
                            resultLl.AddFirst(Point2dCollection[index]);
                        index = baseIndex;
                        while (++index < ptParams.Count && ptParams[index] < endParam)//endParam может находиться на замыкающем сегменте контура
                            resultLl.AddLast(Point2dCollection[index]);


                        return resultLl.ToList();
                    }
                    else
                    {
                        return new List<Point2d>();
                    }
                }
            }
        }


        /// <summary>
        /// Класс, содержащий информацию о штриховке:
        ///  - Один внешний контур
        ///  - Все контуры вложенные в него
        ///  - Диапазон расположения штриховки по X
        /// </summary>
        private class HatchData
        {
            public HatchNestingNode Root { get; private set; }

            public double Start
            {
                get
                {
                    return Root.Extents.MinPoint.X;
                }
            }
            public double End
            {
                get
                {
                    return Root.Extents.MaxPoint.X;
                }
            }

            public HatchData(HatchNestingNode root)
            {
                Root = root;
            }

            public void AddEventsToQueue(C5.IntervalHeap<HatchEvent> eventQueue)
            {
                eventQueue.Add(new HatchEvent(true, this));
                eventQueue.Add(new HatchEvent(false, this));
            }

            public bool PointIsInsideHatch(Point2d pt)
            {
                if (Root.PointIsInside(pt))
                    return PointIsInsideHatch(pt, Root, true);
                else
                    return false;
            }

            private bool PointIsInsideHatch(Point2d pt, HatchNestingNode node, bool isOuter)
            {
                foreach (HatchNestingNode nn in node.NestedNodes)
                {
                    if (nn.PointIsInside(pt))
                        return PointIsInsideHatch(pt, nn, !isOuter);
                }
                //точка находится внутри текущего узла вложенности контуров. Текущий узел является конечным
                return isOuter;
            }


            public List<List<Point2d>> GetAllBoundaries()
            {
                List<List<Point2d>> result = new List<List<Point2d>>();
                GetAllBoundaries(Root, result);

                return result;
            }

            private void GetAllBoundaries(HatchNestingNode node, List<List<Point2d>> result)
            {
                result.Add(new List<Point2d>(node.Point2dCollection.ToArray()));
                foreach (HatchNestingNode nestedNode in node.NestedNodes)
                {
                    GetAllBoundaries(nestedNode, result);
                }
            }
        }


        private class AlignmentSegment
        {
            public double Start { get; private set; }
            public double End { get; private set; }

            public List<HatchSegmentData> HatchSegmentData { get; private set; }
                = new List<HatchSegmentData>(0);

            public Matrix3d Transform { get; private set; }

            public AlignmentSegment(double start, double end, Point2d startLocation, Vector2d startDirection)
            {
                Start = start;
                End = end;

                Vector3d targetPos = new Vector3d(startLocation.X, startLocation.Y, 0);
                Point3d _startPos = new Point3d(Start, 0, 0);
                Vector3d startPos = new Vector3d(Start, 0, 0);

                Matrix3d transf1 = Matrix3d.Rotation(Math.PI / 2, Vector3d.XAxis, Point3d.Origin);//1 - поворот относительно оси X на 90 градусов
                Matrix3d transf2 = Matrix3d.Rotation(startDirection.Angle, Vector3d.ZAxis, _startPos);//2 - поворот согласно направлению оси
                Matrix3d transf3 = Matrix3d.Displacement(targetPos - startPos);//3 - перемещение к оси

                Transform = transf3 * transf2 * transf1;
            }
        }

        private class HatchSegmentData : PolygonNestingTree
        {
            public List<List<Point2d>> Polygons { get; set; } = null;

            public Hatch Hatch { get; private set; }


            public HatchSegmentData(HatchData hatchData)
            {
                Hatch = hatchData.Root.Hatch;
            }


            public void GetNesting()
            {
                foreach (List<Point2d> polygon in Polygons)
                {
                    PolygonNestingNode nestingNode
                        = new PolygonNestingNode(new Point2dCollection(polygon.ToArray()));
                    Insert(root, nestingNode);
                }
            }


            public Dictionary<Point2dCollection, List<Point2dCollection>> GetBoundariesWithHoles()
            {
                Dictionary<Point2dCollection, List<Point2dCollection>> result
                    = new Dictionary<Point2dCollection, List<Point2dCollection>>();

                GetBoundariesWithHoles(result, root, false);

                return result;
            }


            private void GetBoundariesWithHoles
                (Dictionary<Point2dCollection, List<Point2dCollection>> result,
                PolygonNestingNode nestingNode, bool outerBoundary)
            {
                if (outerBoundary)
                {
                    result.Add(nestingNode.Point2dCollection,
                        nestingNode.NestedNodes.Select(nn => nn.Point2dCollection).ToList());
                }

                foreach (PolygonNestingNode nestedNode in nestingNode.NestedNodes)
                {
                    GetBoundariesWithHoles(result, nestedNode, !outerBoundary);
                }
            }

        }




    }
}
