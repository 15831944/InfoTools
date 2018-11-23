using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;


namespace Civil3DInfoTools.Geology.GeologyHatch3dWindow
{
    public partial class GeologyHatch3dViewModel : INotifyPropertyChanged
    {
        //TODO: использовать м-полигон https://adndevblog.typepad.com/autocad/2012/08/create-a-mpolygon-in-autocad-using-net.html?
        private void Create3dProfile(object arg)
        {
            try
            {
                if (doc != null)
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
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
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
                                    HorScaling, 0,                0,
                                    0,               VertScaling, 0,
                                    0,               0,                1
                                })
                                * Matrix2d.Displacement(new Vector2d(-minx, ElevBasePoint.Value.Y));

                        C5.IntervalHeap<HatchEvent> eventQueue = new C5.IntervalHeap<HatchEvent>();
                        List<HatchData> hatchData = new List<HatchData>();
                        foreach (ObjectId id in soilHatchIds)
                        {
                            //получить все точки штриховок с учетом возможных дуг, сплайнов и проч

                            //Для каждой штриховки создается набор композитных кривых, состоящих из линейных сегментов
                            Hatch hatch = (Hatch)tr.GetObject(id, OpenMode.ForRead);
                            List<CompositeCurve2d> boundaries = new List<CompositeCurve2d>();
                            List<Extents2d> extends = new List<Extents2d>();

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

                                    //List<Point2d> polygonPts = new List<Point2d>();
                                    foreach (Curve2d c in curves)
                                    {
                                        if (!(c is LineSegment2d))
                                        {
                                            Interval interval = c.GetInterval();
                                            PointOnCurve2d[] samplePts = c.GetSamplePoints(interval.LowerBound, interval.UpperBound, 0.02);

                                            Point2d[] pts = samplePts.Select(p => transform * p.Point).ToArray();
                                            for (int n = 0; n < pts.Length - 1; n++)
                                            {
                                                compositeCurveElems.Add(new LineSegment2d(pts[n], pts[n + 1]));
                                                //polygonPts.Add(pts[n + 1]);
                                                updateExtends(pts[n + 1]);
                                            }
                                        }
                                        else
                                        {
                                            c.TransformBy(transform);
                                            compositeCurveElems.Add(c);
                                            //polygonPts.Add(c.EndPoint);
                                            updateExtends(c.EndPoint);
                                        }
                                    }
                                    CompositeCurve2d boundary = new CompositeCurve2d(compositeCurveElems.ToArray());
                                    Extents2d ext = new Extents2d(_minx, _miny, _maxx, _maxy);
                                    boundaries.Add(boundary);
                                    extends.Add(ext);
                                }
                            }

                            //контуры штриховок не могут иметь пересечений друг с другом или самопересечений
                            //(м-полигон не может быть построен таким образом!!!) - такие штриховки не обрабатываются
                            //TODO: на будущее - самопересекающиеся полигоны можно разбить на отдельные по количеству
                            //самопересечний. Не знаю как быть с остальными пересечениями.

                            //проверка на самопересечения
                            bool badBoundaries = false;
                            foreach (CompositeCurve2d b in boundaries)
                            {
                                CurveCurveIntersector2d intersector = new CurveCurveIntersector2d(b, b);
                                if (intersector.NumberOfIntersectionPoints>0)
                                {
                                    badBoundaries = true;
                                    break;
                                }
                            }
                            if (badBoundaries) continue;
                            //проверка на взаимные пересечения. Исп RBush для того, чтобы не было квадратичной сложности


                            //определяется вложенность контуров штриховки
                            //ЕСЛИ ШТРИХОВКА СОСТОИТ ИЗ 2 И БОЛЕЕ КОНТУРОВ, КОТОРЫЕ НЕ ВЛОЖЕНЫ ДРУГ В ДРУГА, 
                            //ТО ЭТИ КОНТУРА ДОЛЖНЫ РАССМАТРИВАТЬСЯ КАК ОТДЕЛЬНЫЕ ШТРИХОВКИ!!!
                            HatchNestingTree hatchNestingTree = new HatchNestingTree(boundaries);
                            List<HatchData> currHatchData = hatchNestingTree.GetHatchData();
                            hatchData.AddRange(currHatchData);

                            //Каждая штриховка имеет диапазон по X от начала до конца по оси.
                            //В общую очередь событий сохраняются события начала и конца штриховки
                            foreach (HatchData hd in currHatchData)
                            {
                                hd.AddEvents(eventQueue);
                            }
                        }


                        //Трассу разбить на отрезки, на которых будут вствалены прямолинейные сегменты штриховок


                        //проход по каждому отрезку трассы (диапазон отрезка - диапазон длины полилинии)
                        //Получить те штриховки, диапазон по X которых пересекается с диапазоном текущего отрезка (СКАНИРУЮЩАЯ ЛИНИЯ: события - начало, конец штриховки)
                        //для каждой штриховки получить все точки пересечений полигонов с вертикалями в начале и в конце (интересуют только полноценные пересечения, не касания)
                        //все полученные точки пересечений поместить в несколько отсортированных коллекций:
                        //по координате Y начало (должно быть четное количество),
                        //по координате Y конец (должно быть четное количество),
                        //для каждого полигона контуров штриховки - по параметру композитной кривой

                        //для каждой штриховки получить набор полигонов, которые должны быть вставлены в 3d профиль на текущем отрезке
                        //для этого совершается обход точек пересечения
                        //запомнить стартовую точку
                        //сначала - проход между 2 точек пересечения соседних по координате Y (по возрастанию Y НЕ ПРАВИЛЬНО!!!!!!!!!! НУЖЕН ВЫБОР НАПРАВЛЕНИЯ ОБХОДА!!!).
                        //ВЫБОР НАПРАВЛЕНИЯ ОБХОДА - приращение по Y в 2 раза меньше чем до соседней точки пересечение => эта точка находится внутри текущей штриховки? необходимо учитывать вложенность полигонов, то есть островки!
                        //Определить к какому котруру штриховки относится точка в которую пришли
                        //затем выбор направления обхода вдоль контура полигона так чтобы обходить внутри текущего диапазона
                        //(использовать прирощение параметра в 2 раза меньшее чем длина текущего диапазона и определять находится ли точка контура с данным прирощением в текущем диапазоне)
                        //обход вдоль контура до следующей точки пересечения в соответствии с коллекцией по параметру композитной кривой для текущего контура и выбранным  направлением обхода
                        //проход между 2 точек пересечения соседних по координате Y (по убыванию Y (то есть идет чередование - НЕ ПРАВИЛЬНО!!!!!!!!!! НУЖЕН ВЫБОР НАПРАВЛЕНИЯ ОБХОДА!!!))
                        //и так далее до тех пор пока не будет достигнута стартовая точка
                        //при обходе собираются точки для полигона, который сохраняется в набор полигонов. Обойденные точки пересечений удаляются из всех наборов
                        //Контуры, котрые полностью находятся внутри текущего диапазона добавляются в набор без изменений
                        //сохранить наборы полигонов для текущего диапазона


                        //для каждого диапазона создать полученные полигоны в 3d (создается MPolygon и пересчитывается в нужное положение)

                        tr.Commit();
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


        private class HatchNestingTree
        {
            //корневой узел фиктивный
            private HatchNestingNode root = new HatchNestingNode(null);


            public HatchNestingTree(List<CompositeCurve2d> boundaries)
            {
                foreach (CompositeCurve2d boundary in boundaries)
                {
                    HatchNestingNode insertingNode = new HatchNestingNode(boundary);
                    Insert(root, insertingNode);
                }
            }

            private void Insert(HatchNestingNode node, HatchNestingNode insertingNode)
            {
                //TODO: вставка контура в дерево вложенности!

                bool isNested = false;
                //Проверить вложена ли добавляемая полилиния в один из дочерних узлов
                foreach (HatchNestingNode nn in node.NestedNodes)
                {
                    if (nn.IsNested(insertingNode))
                    {
                        //рекурсия
                        Insert(nn, insertingNode);
                        isNested = true;
                        break;
                    }
                }

                if (!isNested)
                {
                    //Если полилиния не вложена в дочерние узлы, то проверить не вложены ли дочерние узлы в добавляемую полилинию
                    for (int i = 0; i < node.NestedNodes.Count;)
                    {
                        HatchNestingNode nn = node.NestedNodes[i];
                        if (insertingNode.IsNested(nn))
                        {
                            //Если вложена, то убрать из node.NestedNodes и добавить в insertingNode.NestedNodes
                            node.NestedNodes.Remove(nn);
                            insertingNode.NestedNodes.Add(nn);
                        }
                        else
                        {
                            i++;
                        }
                    }

                    //Добавить insertingNode в node.NestedNodes
                    node.NestedNodes.Add(insertingNode);
                }

            }

            public List<HatchData> GetHatchData()
            {
                return root.NestedNodes.Select(hnn => new HatchData(hnn)).ToList();
            }
        }


        /// <summary>
        /// Узел вложенности штриховок
        /// </summary>
        private class HatchNestingNode
        {
            public List<HatchNestingNode> NestedNodes { get; private set; } = new List<HatchNestingNode>();

            public CompositeCurve2d Boundary { get; private set; }

            private Point2dCollection point2dCollection = new Point2dCollection();

            public HatchNestingNode(CompositeCurve2d boundary)
            {
                Boundary = boundary;

                foreach(Curve2d c in boundary.GetCurves())
                {
                    point2dCollection.Add(c.EndPoint);
                }
            }


            /// <summary>
            /// Переданный узел вложен в вызывающий узел
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            public bool IsNested(HatchNestingNode node)
            {
                //Проверка по одной точке, так как предполагается, что полилинии не пересекаются
                return Utils.PointIsInsidePolylineWindingNumber(node.point2dCollection[0], this.point2dCollection);
            }
        }


        /// <summary>
        /// Класс, содержащий информацию о штриховке:
        ///  - Все контуры и их вложенность
        ///  - Диапазон расположения штриховки по X
        /// </summary>
        private class HatchData
        {
            public HatchNestingNode Root { get; private set; }

            public double Start { get; private set; }
            public double End { get; private set; }

            public HatchData(HatchNestingNode root)
            {
                Root = root;
                //TODO: получение минимума и максимума X
            }

            public void AddEvents(C5.IntervalHeap<HatchEvent> eventQueue)
            {
                eventQueue.Add(new HatchEvent(true, this));
                eventQueue.Add(new HatchEvent(false, this));
            }
        }

    }
}
