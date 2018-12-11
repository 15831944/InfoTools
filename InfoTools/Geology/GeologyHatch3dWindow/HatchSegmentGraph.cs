using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.Geology.GeologyHatch3dWindow
{
    public partial class GeologyHatch3dViewModel : INotifyPropertyChanged
    {
        private class HatchSegmentGraph
        {
            public List<List<Point2d>> Result { get; private set; } = new List<List<Point2d>>();

            private HatchData hd;
            private double start, end;
            //private Database db;

            private LinearEntity2d startVert;
            private LinearEntity2d endVert;

            private List<HatchNestingNode> boundariesWithNoIntersections = new List<HatchNestingNode>();

            //точки пересечения с вертикалью вначале
            private SortedSet<Node> startVertSorted = new SortedSet<Node>(new NodeYComparer());//сортировка по Y
            //точки пересечения с вертикалью в конце
            private SortedSet<Node> endVertSorted = new SortedSet<Node>(new NodeYComparer());//сортировка по Y
            //точки пересечения с каждым из контуров штриховки
            private Dictionary<HatchNestingNode, SortedSet<Node>> boundariesSorted = new Dictionary<HatchNestingNode, SortedSet<Node>>();

            //не обойденные узлы
            private HashSet<Node> notVisited = new HashSet<Node>();

            //точки пересечения с вертикалью вначале
            private LinkedList<Node> startVertLinkedList = new LinkedList<Node>();
            //точки пересечения с вертикалью в конце
            private LinkedList<Node> endVertLinkedList = new LinkedList<Node>();
            //точки пересечения с каждым из контуров штриховки
            private Dictionary<HatchNestingNode, LinkedList<Node>> boundariesLinkedLists = new Dictionary<HatchNestingNode, LinkedList<Node>>();



            public HatchSegmentGraph(double start, double end, HatchData hd, Editor ed )
            {
                this.hd = hd;
                this.start = start;
                this.end = end;
                //this.db = db;
                //получить все точки пересечений полигонов с вертикалями в начале и в конце (интересуют только полноценные пересечения, не касания)
                //все полученные точки пересечений поместить в несколько отсортированных коллекций:
                //по координате Y начало (должно быть четное количество),
                //по координате Y конец (должно быть четное количество),
                //для каждого полигона контуров штриховки - по параметру композитной кривой
                startVert = new Line2d(new Point2d(start, 0), Vector2d.YAxis);
                endVert = new Line2d(new Point2d(end, 0), Vector2d.YAxis);
                GetIntersections(hd.Root);//первоначально заполняются SortedSet
                //если на одной из вертикалей только одна точка пересечения, то не учитывать ее
                //if (startVertSorted.Count==1)
                //{
                //    Node node = startVertSorted.First();
                //    notVisited.Remove(node);
                //    boundariesSorted[node.Boundary].Remove(node);
                //    startVertSorted.Clear();
                //}
                //if (endVertSorted.Count == 1)
                //{
                //    Node node = endVertSorted.First();
                //    notVisited.Remove(node);
                //    boundariesSorted[node.Boundary].Remove(node);
                //    endVertSorted.Clear();
                //}

                //переместить узлы в LinkedList и заполнить свойство VerticalJunction узлов (нужен переход между соседними узлами в списках)
                MoveToLinkedLists();


                //получить набор полигонов, которые должны быть вставлены в 3d профиль на текущем отрезке
                //для этого совершается обход точек пересечения
                while (notVisited.Count > 1)//В обходе может быть не менее 2 узлов
                {
                    try
                    {
                        Traverse();
                    }
                    catch(Exception ex)
                    {
                        Utils.ErrorToCommandLine(ed,
                            "Ошибка при при обходе полигона", ex);
                        break;
                    }
                }

                //Контуры, котрые полностью находятся внутри текущего диапазона добавляются в набор без изменений
                //(скорее всего для создания М-полигона вложенность запоминать не нужно)

                foreach (HatchNestingNode boundaryWithNoIntersections in boundariesWithNoIntersections)
                {
                    Result.Add(new List<Point2d>(boundaryWithNoIntersections.Point2dCollection.ToArray()));
                }

            }


            //1 проход дает 1 полигон в результат
            private void Traverse()
            {
                List<Point2d> polygon = new List<Point2d>();
                HashSet<Node> visitedThisTraverse = new HashSet<Node>();
                //выбрать и запомнить стартовую точку. Это может быть любой непосещенный узел графа
                Node startNode = notVisited.First();
                Node currNode = startNode;
                bool currTraverseType = true;//true - проход по вертикали, false - проход вдоль контура штриховки
                do
                {
                    //при проходе в полигон и посещенные узлы не добавляется только последняя точка в проходе
                    if (currTraverseType) //проход между 2 точек пересечения соседних по координате Y.
                    {
                        currNode = VertTraverse(currNode, polygon, visitedThisTraverse);
                    }
                    else //проход вдоль контура штриховки
                    {
                        currNode = BoundaryTraverse(currNode, polygon, visitedThisTraverse);
                    }
                    currTraverseType = !currTraverseType;

                } while (currNode != startNode);//пришли в стартовую точку?




                //Обновить все коллекции узлов в соответствии с посещенными
                //(Обойденные точки пересечений удаляются из всех наборов)
                foreach (Node visitedNode in visitedThisTraverse)
                {
                    notVisited.Remove(visitedNode);

                    //visitedNode.VerticalJunction.List.Remove(visitedNode.VerticalJunction);
                    //visitedNode.BoundaryJunction.List.Remove(visitedNode.BoundaryJunction);
                }

                if (polygon.Count > 0)
                {
                    Result.Add(polygon);
                }
            }

            /// <summary>
            /// Следующий узел на вертикали при обходе графа
            /// </summary>
            /// <param name="currNode"></param>
            /// <returns></returns>
            private Node VertTraverse(Node currNode, List<Point2d> polygon, HashSet<Node> visitedThisTraverse)
            {
                if (visitedThisTraverse.Contains(currNode))
                {
                    throw new Exception("Ошибка логики обхода полигона");
                }

                try
                {
                    LinkedListNode<Node> vertLLCurrNode = currNode.VerticalJunction;
                    //ВЫБОР НАПРАВЛЕНИЯ ОБХОДА - приращение по Y в 2 раза меньше чем до соседней точки пересечения
                    //=> эта точка находится внутри текущей штриховки? необходимо учитывать вложенность полигонов, то есть островки!
                    LinkedListNode<Node> nextNode = vertLLCurrNode.Next;
                    LinkedListNode<Node> prevNode = vertLLCurrNode.Previous;
                    if (nextNode == null && prevNode == null)
                    {
                        throw new Exception("Ошибка логики обхода полигона");
                    }

                    if (nextNode == null || prevNode == null)//если обход возможен только в одну сторону, то выбор не нужен
                        return prevNode == null ? nextNode.Value : prevNode.Value;
                    Vector2d nextCheckVec = (nextNode.Value.LocationVector + vertLLCurrNode.Value.LocationVector) / 2;
                    Point2d nextCheckPt = new Point2d(nextCheckVec.X, nextCheckVec.Y);
                    Node result = null;
                    if (hd.PointIsInsideHatch(nextCheckPt))
                        result = nextNode.Value;
                    else
                        result = prevNode.Value;

                    return result;
                }
                finally
                {
                    polygon.Add(currNode.Location);
                    visitedThisTraverse.Add(currNode);
                }

            }


            private Node BoundaryTraverse(Node currNode, List<Point2d> polygon, HashSet<Node> visitedThisTraverse)
            {
                if (visitedThisTraverse.Contains(currNode))
                {
                    throw new Exception("Ошибка логики обхода полигона");
                }

                //Определить к какому контуру штриховки относится точка в которую пришли
                //затем выбор направления обхода вдоль контура полигона так чтобы обходить внутри текущего диапазона
                //(использовать прирощение параметра в 2 раза меньшее чем длина текущего диапазона и определять находится ли точка контура с данным прирощением в текущем диапазоне)
                //обход вдоль контура до следующей точки пересечения в соответствии с коллекцией по параметру композитной кривой для текущего контура и выбранным  направлением обхода

                HatchNestingNode hnn = currNode.Boundary;
                CompositeCurve2d boundaryCurve = currNode.Boundary.Boundary;
                LinkedListNode<Node> currLLNode = currNode.BoundaryJunction;
                //Соседние узлы должны браться как будто это циклический список!!!
                LinkedListNode<Node> nextLLNode = Common.Utils.NextVertCycled(currLLNode);//следующий узел в направлении возрастания параметра кривой (возможен переход через 0)
                LinkedListNode<Node> prevLLNode = Common.Utils.PreviousVertCycled(currLLNode);//следующий узел в направлении убывания параметра кривой (возможен переход через 0)
                if (nextLLNode == null || prevLLNode == null || nextLLNode == currLLNode || prevLLNode == currLLNode)
                {
                    throw new Exception("Ошибка логики обхода полигона");
                }

                double currParam = currNode.CurveParameter;
                double nextParam = nextLLNode.Value.CurveParameter;
                double maxParam = boundaryCurve.GetInterval().UpperBound;

                double nextCheckParam = currParam < nextParam ? //в списке параметр идет по возрастанию
                    (currParam + nextParam) / 2
                    ://если следующий параметр меньше текущего, значит происходит переход через ноль
                    (currParam + (maxParam + nextParam - currParam) / 2) % maxParam;//TODO: ПРОВЕРИТЬ ПРАВИЛЬНОСТЬ!!!
                //(как вариант - за средний параметр можно просто взять ноль (но нужно учесть, что 0 может быть одной из границ интервала))

                Point2d nextCheckPt = boundaryCurve.EvaluatePoint(nextCheckParam);
                Node result = null;
                bool orderForward = true;//проход вдоль контура в направлении возрастания параметра

                //Если nextCheckPt попадает точно на границу интервала, то считать, что она попала в интервал
                //(ТОЛЬКО ЕСЛИ НЕ СОВПАДАЕТ ПО X С currNode!!!)
                if ((start </*=*/ nextCheckPt.X || Utils.LengthIsEquals(start, nextCheckPt.X))
                    && (end >/*=*/ nextCheckPt.X || Utils.LengthIsEquals(end, nextCheckPt.X))
                    && !Utils.LengthIsEquals(currNode.Location.X, nextCheckPt.X))
                    //if (start < nextCheckPt.X && end > nextCheckPt.X)
                    result = nextLLNode.Value;
                else
                {
                    result = prevLLNode.Value;
                    orderForward = false;//точки добавляются в полигон в порядке убывания параметра
                }


                //все вершины, лежащие внутри интервала должны перейти в полигон в нужном порядке
                double intervalStartParam = orderForward ? currParam : result.CurveParameter;
                double intervalEndParam = orderForward ? result.CurveParameter : currParam;
                List<Point2d> addToPolygon = hnn.GetPointsInInterval(intervalStartParam, intervalEndParam);
                if (!orderForward)
                    addToPolygon.Reverse();


                polygon.Add(currNode.Location);
                polygon.AddRange(addToPolygon);
                visitedThisTraverse.Add(currNode);
                return result;
            }




            private void GetIntersections(HatchNestingNode hatchNode)
            {
                //НЕВЕРНАЯ РАБОТА CurveCurveIntersector2d и CurveCurveIntersector3d!!!
                //ВЫДАЮТСЯ АБСОЛЮТНО НЕПРАВИЛЬНЫЕ ЗНАЧЕНИЯ ПРИ ПЕРЕСЕЧЕНИИ ВЕРТИКАЛЬНОЙ ЛИНИИ И КОМПОЗИТНОЙ КРИВОЙ!!!!
                //CurveCurveIntersector3d startIntersector = new CurveCurveIntersector3d(startVert, boundary3d/*hatchNode.Boundary*/, Vector3d.ZAxis);
                //bool hasIntersections0 = AddIntersections(startIntersector, hatchNode, startVertSorted);

                //CurveCurveIntersector3d endIntersector = new CurveCurveIntersector3d(endVert, boundary3d/*hatchNode.Boundary*/, Vector3d.ZAxis);
                //bool hasIntersections1 = AddIntersections(endIntersector, hatchNode, endVertSorted);

                bool hasIntersections0 = AddIntersections(start, end, hatchNode);


                if (!hasIntersections0
                    && (hatchNode.Extents.MinPoint.X >/*=*/ start
                    || Utils.LengthIsEquals(hatchNode.Extents.MinPoint.X, start))//допуск
                    && (hatchNode.Extents.MaxPoint.X </*=*/ end
                    || Utils.LengthIsEquals(hatchNode.Extents.MaxPoint.X, end)))
                {
                    boundariesWithNoIntersections.Add(hatchNode);
                }

                //recurse
                foreach (HatchNestingNode nestedNode in hatchNode.NestedNodes)
                {
                    GetIntersections(nestedNode);
                }
            }


            private bool AddIntersections(double start, double end, HatchNestingNode hatchNode)
            {
                //расчет пересечений с помощью логики сканирующей линии
                //сформировать очередь событий начала и конца каждого сегмента (вертикальные сегменты при этом не брать в расчет!)
                //перейти по очереди до start, получить пересекаемые отрезки и точки пересечения,
                //затем до end, получить пересекаемые отрезки и точки пересечения
                //БУДУТ ПОЛУЧЕНЫ ТОЧКИ ПЕРЕСЕЧЕНИЯ ТАМ, ГДЕ ВЕРТИКАЛЬНЫЙ СЕГМЕНТ СТЫКУЕТСЯ С НЕВЕРТИКАЛЬНЫМ
                //БУДУТ ДУБЛИРОВАТЬСЯ ТОЧКИ ПЕРЕСЕЧЕНИЙ В ТОЧКАХ СТЫКОВКИ ДВУХ СОСЕДНИХ СЕГМЕНТОВ КОНТУРА
                C5.IntervalHeap<SweepLineEvent> queue = new C5.IntervalHeap<SweepLineEvent>();
                foreach (Curve2d curve2d in hatchNode.Boundary.GetCurves())
                {
                    LineSegment2d lineSegment2D = (LineSegment2d)curve2d;
                    double p0x = lineSegment2D.StartPoint.X;
                    double p1x = lineSegment2D.EndPoint.X;


                    if (Math.Abs(p0x - p1x) < Tolerance.Global.EqualPoint)
                        continue;//вертикальные сегменты не интересуют

                    double segStart = double.NegativeInfinity;
                    double segEnd = double.NegativeInfinity;
                    if (p0x < p1x)
                    {
                        segStart = p0x;
                        segEnd = p1x;
                    }
                    else
                    {
                        segStart = p1x;
                        segEnd = p0x;
                    }
                    queue.Add(new SweepLineEvent(segStart, true, lineSegment2D));
                    queue.Add(new SweepLineEvent(segEnd, false, lineSegment2D));
                }

                HashSet<LineSegment2d> currSegments = new HashSet<LineSegment2d>();
                Action goThoughEvent = () =>
                {
                    SweepLineEvent e = queue.DeleteMin();
                    if (e.Start)
                    {
                        currSegments.Add(e.LineSegment2D);
                    }
                    else
                    {
                        currSegments.Remove(e.LineSegment2D);
                    }
                };

                while (queue.Count > 0 && queue.FindMin().Position < start)//пройти все события до start
                {
                    goThoughEvent();
                }
                //TODO: В ТЕКУЩЕМ НАБОРЕ СЕГМЕНТОВ МОГУТ НАХОДИТЬСЯ 2 СТЫКУЮЩИХСЯ СЕГМЕНТА, ДАЮЩИЕ В РЕЗУЛЬТАТЕ ОДНУ И ТУ ЖЕ ТОЧКУ ПЕРЕСЕЧЕНИЯ
                //если на одной из вертикалей только одна точка пересечения, то не учитывать ее
                bool result0 = false;
                foreach (LineSegment2d seg in currSegments)//получить пересечения
                {
                    result0 = AddIntersection(seg, startVert, hatchNode, startVertSorted);
                }

                while (queue.Count > 0 && queue.FindMin().Position < end)//пройти все события до end
                {
                    goThoughEvent();
                }
                bool result1 = false;
                foreach (LineSegment2d seg in currSegments)//получить пересечения
                {
                    result1 = AddIntersection(seg, endVert, hatchNode, endVertSorted);
                }

                return result0 || result1;
            }

            private bool AddIntersection(LineSegment2d intersectionSeg, LinearEntity2d vert, HatchNestingNode hatchNode, SortedSet<Node> vertSorted)
            {
                bool result = false;
                CurveCurveIntersector2d intersector = new CurveCurveIntersector2d(intersectionSeg, vert);
                if (intersector.NumberOfIntersectionPoints > 0)
                {
                    Point2d intersectionPt = intersector.GetPointOnCurve1(0).Point;
                    //если точка пересечения контура и вертикали имеет ту же координату X,
                    //что и начало или конец контура, то это пересечение не учитывается!
                    if (!Utils.LengthIsEquals(intersectionPt.X, hatchNode.Extents.MinPoint.X)
                        && !Utils.LengthIsEquals(intersectionPt.X, hatchNode.Extents.MaxPoint.X))//допуск
                    {
                        double param = hatchNode.Boundary.GetParameterOf(intersectionPt);

                        Node newIntersectionNode = new Node(hatchNode, param, intersectionPt);
                        if (!vertSorted.Contains(newIntersectionNode))//TODO: Нужен учет допуска???
                        {
                            result = true;
                            notVisited.Add(newIntersectionNode);

                            vertSorted.Add(newIntersectionNode);
                            SortedSet<Node> boundarySorted = null;
                            boundariesSorted.TryGetValue(hatchNode, out boundarySorted);
                            if (boundarySorted == null)
                            {
                                boundarySorted = new SortedSet<Node>(new NodeParamComparer());//сортировка по параметру!!!
                                boundariesSorted.Add(hatchNode, boundarySorted);
                            }

                            if (boundarySorted.Contains(newIntersectionNode))
                            {
                                throw new Exception("Ошибка логики обхода полигона. Получены 2 точки с одинаковым параметром на одной кривой");
                            }

                            boundarySorted.Add(newIntersectionNode);
                        }
                    }

                }
                return result;
            }


            private class SweepLineEvent : IComparable<SweepLineEvent>
            {
                public double Position { get; private set; }
                public bool Start { get; private set; }
                public LineSegment2d LineSegment2D { get; private set; }

                public SweepLineEvent(double position, bool start, LineSegment2d lineSegment2D)
                {
                    Position = position;
                    Start = start;
                    LineSegment2D = lineSegment2D;
                }

                public int CompareTo(SweepLineEvent other)
                {
                    return this.Position.CompareTo(other.Position);
                }
            }



            //private bool AddIntersections(CurveCurveIntersector3d intersector, HatchNestingNode hatchNode, SortedSet<Node> vertSorted)
            //{
            //    bool hasIntersections = false;
            //    for (int i = 0; i < intersector.NumberOfIntersectionPoints; i++)
            //    {
            //        if (intersector.IsTransversal(i))
            //        {
            //            hasIntersections = true;
            //            //PointOnCurve2d pt = intersector.GetPointOnCurve1(i);
            //            PointOnCurve3d _pt = intersector.GetPointOnCurve2(i);
            //            //ОЧЕНЬ СТРАННО. ОШИБКА API? - для получения правильного параметра нужно использовать GetPointOnCurve1, а не GetPointOnCurve2
            //            //ПРИ ЭТОМ ПРАВИЛЬНУЮ ТОЧКУ НЕ ДАЕТ НИ ОДИН ИЗ ВАРИАНТОВ!!!
            //            //ОШИБКА ВОЗНИКАЕТ ТОЛЬКО ПРИ ПЕРЕСЕЧЕНИИ С БЕСКОНЕЧНОЙ ЛИНИЕЙ???? ВОЗНИКАЕТ ВСЕГДА!!!!!
            //            //ВЫДАЮТСЯ АБСОЛЮТНО НЕПРАВИЛЬНЫЕ ЗНАЧЕНИЯ ПРИ ПЕРЕСЕЧЕНИИ ВЕРТИКАЛЬНОЙ ЛИНИИ И КОМПОЗИТНОЙ КРИВОЙ!!!!
            //            //ЭТО НЕВЕРНАЯ РАБОТА CurveCurveIntersector2d!!!
            //            //ДЛЯ 3D ЛИНИЙ??? ТО ЖЕ САМОЕ!!!
            //            //ВАРИАНТЫ
            //            //- ИСП ЛОГИКУ СКАНИРУЮЩЕЙ ЛИНИИ
            //            //- СОЗДАВАТЬ POLYLINE И ИСКАТЬ ПЕРЕСЕЧЕНИЕ С НЕЙ

            //            PointOnCurve3d pt = intersector.GetPointOnCurve1(i);

            //            //double param = hatchNode.Boundary.GetParameterOf(_pt.Point);
            //            Node newIntersectionNode = new Node(hatchNode, /*param*/pt.Parameter, Utils.Point2DBy3D(pt.Point));
            //            notVisited.Add(newIntersectionNode);

            //            if (vertSorted.Contains(newIntersectionNode))
            //            {
            //                throw new Exception("Ошибка логики обхода полигона. Получены 2 точки с одинаковой высотой на одной верикали");
            //            }

            //            vertSorted.Add(newIntersectionNode);
            //            SortedSet<Node> boundarySorted = null;
            //            boundariesSorted.TryGetValue(hatchNode, out boundarySorted);
            //            if (boundarySorted == null)
            //            {
            //                boundarySorted = new SortedSet<Node>(new NodeParamComparer());//сортировка по параметру!!!
            //                boundariesSorted.Add(hatchNode, boundarySorted);
            //            }

            //            if (boundarySorted.Contains(newIntersectionNode))
            //            {
            //                throw new Exception("Ошибка логики обхода полигона. Получены 2 точки с одинаковым параметром на одной кривой");
            //            }

            //            boundarySorted.Add(newIntersectionNode);
            //        }
            //        else
            //        {

            //        }
            //    }


            //    return hasIntersections;
            //}

            private void MoveToLinkedLists()
            {
                foreach (Node intersectionNode in startVertSorted)
                {
                    intersectionNode.VerticalJunction = startVertLinkedList.AddLast(intersectionNode);
                }
                foreach (Node intersectionNode in endVertSorted)
                {
                    intersectionNode.VerticalJunction = endVertLinkedList.AddLast(intersectionNode);
                }
                foreach (KeyValuePair<HatchNestingNode, SortedSet<Node>> kvp in boundariesSorted)
                {
                    LinkedList<Node> nodesLl = new LinkedList<Node>();
                    foreach (Node intersectionNode in kvp.Value)
                    {
                        intersectionNode.BoundaryJunction = nodesLl.AddLast(intersectionNode);
                    }
                    boundariesLinkedLists.Add(kvp.Key, nodesLl);
                }
            }

            /// <summary>
            /// Узел графа - точка пересечения вертикалей начала и конца с контурами штриховки
            /// </summary>
            private class Node
            {
                /// <summary>
                /// Контур штриховки
                /// </summary>
                public HatchNestingNode Boundary { get; private set; }

                /// <summary>
                /// Параметр контура штриховки
                /// </summary>
                public double CurveParameter { get; private set; }

                /// <summary>
                /// Точка пересечения
                /// </summary>
                public Point2d Location { get; private set; }
                /// <summary>
                /// Точка пересечения как вектор
                /// </summary>
                public Vector2d LocationVector { get; private set; }

                /// <summary>
                /// ссылка на вертикаль (на конкретный узел двусвязного списка)
                /// </summary>
                public LinkedListNode<Node> VerticalJunction { get; set; }

                /// <summary>
                /// ссылка на границу штриховки (на конкретный узел двусвязного списка)
                /// </summary>
                public LinkedListNode<Node> BoundaryJunction { get; set; }

                public Node(HatchNestingNode boundary, double curveParameter, Point2d location)
                {
                    Boundary = boundary;
                    CurveParameter = curveParameter;
                    Location = location;
                    LocationVector = new Vector2d(location.X, location.Y);
                }
            }

            private class NodeYComparer : IComparer<Node>
            {
                public int Compare(Node n1, Node n2)
                {
                    return n1.Location.Y.CompareTo(n2.Location.Y);
                }
            }

            private class NodeParamComparer : IComparer<Node>
            {
                public int Compare(Node n1, Node n2)
                {
                    return n1.CurveParameter.CompareTo(n2.CurveParameter);
                }
            }



            /*
             private void DrawState()
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    //начертить пересекаемые полигоны
                    //начертить полигоны в результате

                    tr.Commit();
                }
                
            }
                 */

        }
    }

}
