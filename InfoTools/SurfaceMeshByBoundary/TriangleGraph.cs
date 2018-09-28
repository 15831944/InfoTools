using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    /// <summary>
    /// Граф треугольника
    /// Объект, который собирает данные о частях полилиний, которые пересекают один конкретный треугольник поверхности
    /// Главная задача этого объекта - получить полигоны, которые войдут в состав сети, которая будет построена в итоге
    /// </summary>
    public class TriangleGraph
    {
        /// <summary>
        /// Ссылка на дерево полилиний
        /// </summary>
        private PolylineNesting polylineNesting;

        /// <summary>
        /// Ссылка на треугольник поверхности
        /// </summary>
        private TinSurfaceTriangle tinSurfaceTriangle;

        /// <summary>
        /// Последовательно расположенные узлы графа. Точки, расположенные по периметру треугольника от вершины 1 до вершины 3
        /// </summary>
        private LinkedList<GraphNode> graphNodes = new LinkedList<GraphNode>();

        /// <summary>
        /// Узлы графа, расположенные в вершинах треугольника
        /// </summary>
        LinkedListNode<GraphNode>[] vertexNodes = new LinkedListNode<GraphNode>[3];

        /// <summary>
        /// Длины ребер
        /// </summary>
        double[] edgesLength = new double[3];

        /// <summary>
        /// Положения вершин треугольника
        /// </summary>
        Point2d[] vert2dLocs = new Point2d[3];

        /// <summary>
        /// Ссылки на вершины поверхности
        /// </summary>
        TinSurfaceVertex[] verts = new TinSurfaceVertex[3];

        /// <summary>
        /// Точки полилиний, попавшие в этот треугольник
        /// </summary>
        private Dictionary<PolylineNesting.Node, SortedSet<PolylinePt>> pts
            = new Dictionary<PolylineNesting.Node, SortedSet<PolylinePt>>();

        /// <summary>
        /// Участки полилиний, проходящие через этот треугольник
        /// </summary>
        public List<PolylinePart> PolylineParts { get; set; }
            = new List<PolylinePart>();//TODO: Должно быть закрытым полем

        /// <summary>
        /// Список полигонов, которые образует треугольник с пересекаемыми линиями
        /// </summary>
        public List<List<Point3d>> Polygons { get; set; } = new List<List<Point3d>>();

        public TriangleGraph(PolylineNesting polylineNesting, TinSurfaceTriangle triangle)
        {
            this.polylineNesting = polylineNesting;
            tinSurfaceTriangle = triangle;
            verts[0] = tinSurfaceTriangle.Vertex1;
            verts[1] = tinSurfaceTriangle.Vertex2;
            verts[2] = tinSurfaceTriangle.Vertex3;
            vert2dLocs[0] = Utils.Point2DBy3D(tinSurfaceTriangle.Vertex1.Location);
            vert2dLocs[1] = Utils.Point2DBy3D(tinSurfaceTriangle.Vertex2.Location);
            vert2dLocs[2] = Utils.Point2DBy3D(tinSurfaceTriangle.Vertex3.Location);
            edgesLength[0] = (vert2dLocs[1] - vert2dLocs[0]).Length;
            edgesLength[1] = (vert2dLocs[1] - vert2dLocs[2]).Length;
            edgesLength[2] = (vert2dLocs[2] - vert2dLocs[0]).Length;
            //Добавить узлы вершин треугольника
            new VertexGraphNode(this, 0);
            new VertexGraphNode(this, 1);
            new VertexGraphNode(this, 2);

        }

        /// <summary>
        /// Добавить точку полилинии, которая попала в этот треугольник
        /// </summary>
        /// <param name="pt"></param>
        public void AddPolylinePoint(PolylinePt pt)
        {
            SortedSet<PolylinePt> polylinePts = null;
            pts.TryGetValue(pt.Node, out polylinePts);
            if (polylinePts == null)
            {
                polylinePts = new SortedSet<PolylinePt>();
                pts.Add(pt.Node, polylinePts);
            }
            polylinePts.Add(pt);
        }

        /// <summary>
        /// Определить участки полилиний, попавшие в треугольник
        /// </summary>
        public void ResolvePolylineParts()
        {
            foreach (KeyValuePair<PolylineNesting.Node, SortedSet<PolylinePt>> kvp in pts)
            {

                //TEST
                #region MyRegion
                //using (Transaction tr = PolylineNesting.db.TransactionManager.StartTransaction())
                //using (Polyline pline = new Polyline())
                //{
                //    PolylineNesting.ms = tr.GetObject(PolylineNesting.ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                //    Point3d vert13d = TinSurfaceTriangle.Vertex1.Location;
                //    Point3d vert23d = TinSurfaceTriangle.Vertex2.Location;
                //    Point3d vert33d = TinSurfaceTriangle.Vertex3.Location;

                //    pline.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                //    pline.AddVertexAt(0, new Point2d(vert13d.X, vert13d.Y), 0, 0, 0);
                //    pline.AddVertexAt(1, new Point2d(vert23d.X, vert23d.Y), 0, 0, 0);
                //    pline.AddVertexAt(2, new Point2d(vert33d.X, vert33d.Y), 0, 0, 0);
                //    pline.Closed = true;

                //    PolylineNesting.ms.AppendEntity(pline);
                //    tr.AddNewlyCreatedDBObject(pline, true);

                //    tr.Commit();
                //    pline.Draw();
                //    PolylineNesting.ed.Regen();
                //    PolylineNesting.ed.UpdateScreen();
                //}
                #endregion
                //TEST
                //Участок полилинии состоит из точек, параметры которых не перескакивают через целые значения
                //При этом если участок содержит параметр 0,
                //то необходимо состыковать его с замыкающими точками (1 или более)
                //Замыкающие точки - участок без перехода через целые значения с наибольшими параметрами
                //После стыковки участка с параметром 0, удалить все участки, содержащие менее 2 точек
                //Участок полилинии должен начинаться и заканчиваться точкой лежащей на ребре или совпавшей с вершиной,
                //кроме тех случаев, когда вся полилиния находится внутри одного треугольника
                //Не нужны участки, у которых все сегменты лежат на ребрах треугольника

                PolylineNesting.Node node = kvp.Key;
                PolylinePt ptWithStartParam = null;
                SortedSet<PolylinePt> polylinePts = kvp.Value;
                //Последовательности без перехода через  целые значения
                List<LinkedList<PolylinePt>> sequences = new List<LinkedList<PolylinePt>>();
                double? prevParamFloor = null;
                foreach (PolylinePt pt in polylinePts)
                {
                    double parameter = pt.Parameter;
                    if (parameter == node.Polyline.StartParam) //стартовый параметр - всегда 0?
                        ptWithStartParam = pt;

                    if (prevParamFloor != null && sequences.Count > 0 && parameter - prevParamFloor <= 1)
                    {
                        //Продолжить последовательность
                        sequences.Last().AddLast(pt);
                    }
                    else
                    {
                        //Начать новую последовательность
                        LinkedList<PolylinePt> seq = new LinkedList<PolylinePt>();
                        seq.AddLast(pt);
                        sequences.Add(seq);
                    }

                    prevParamFloor = Math.Floor(parameter);
                }

                //Если обнаружен стартовый параметр, то объединить первую и последнюю последовательности
                if (ptWithStartParam != null && sequences.Count > 1)
                {


                    LinkedList<PolylinePt> seq1 = sequences.First();
                    LinkedList<PolylinePt> seq2 = sequences.Last();


                    if (
                        //seq1.First().TinSurfaceEdge==null
                        seq2.Last().Parameter >= node.Polyline.EndParam - 1//Нужно проверить параметр последней точки последей последовательности (что он находится в пределах 1.00 от конечного параметра полилинии)
                        )
                    {
                        for (LinkedListNode<PolylinePt> lln = seq2.Last; lln != null; lln = lln.Previous)
                        {
                            seq1.AddFirst(lln.Value);
                        }
                        sequences.RemoveAt(sequences.Count - 1);
                    }


                }

                //Проверить все участки полилинии и удалить неправильные
                //Tolerance tolerance = Tolerance.Global;
                Point2d vert1 = new Point2d(tinSurfaceTriangle.Vertex1.Location.X, tinSurfaceTriangle.Vertex1.Location.Y);
                Point2d vert2 = new Point2d(tinSurfaceTriangle.Vertex2.Location.X, tinSurfaceTriangle.Vertex2.Location.Y);
                Point2d vert3 = new Point2d(tinSurfaceTriangle.Vertex3.Location.X, tinSurfaceTriangle.Vertex3.Location.Y);
                Vector2d edge1Vector = vert2 - vert1;
                Vector2d edge2Vector = vert3 - vert2;
                Vector2d edge3Vector = vert1 - vert3;


                sequences.RemoveAll(seq =>
                {
                    bool removeThis = seq.Count < 2;//Количество точек не менее двух

                    if (!removeThis)
                    {
                        //Не может быть с одной стороны ребро или вершина а с другой нет
                        bool firstPtOnEdge = seq.First().TinSurfaceEdge != null || seq.First().TinSurfaceVertex != null;
                        bool lastPtOnEdge = seq.Last().TinSurfaceEdge != null || seq.Last().TinSurfaceVertex != null;
                        removeThis = firstPtOnEdge != lastPtOnEdge;//Несоответствие привязки участка к ребрам
                        if (!removeThis)
                        {
                            removeThis = true;
                            //Проверить все ребра на совпадение с ребрами треугольника
                            for (LinkedListNode<PolylinePt> lln = seq.First; lln.Next != null; lln = lln.Next)
                            {
                                Point2d segPt1 = lln.Value.Point2D;
                                Point2d segPt2 = lln.Next.Value.Point2D;
                                if (!(Utils.LinesAreOverlapping(segPt1, segPt2, vert1, vert2)
                                || Utils.LinesAreOverlapping(segPt1, segPt2, vert2, vert3)
                                || Utils.LinesAreOverlapping(segPt1, segPt2, vert3, vert1)))
                                {
                                    //Если сегмент не накладывается ни на одно ребро треугольника, то закончить проверку
                                    removeThis = false;
                                    break;
                                }

                            }
                        }


                    }


                    return removeThis;
                });

                foreach (LinkedList<PolylinePt> seq in sequences)
                {
                    PolylineParts.Add(new PolylinePart(seq));
                }


            }


        }


        /// <summary>
        /// Расчитать полигоны для построения сети
        /// </summary>
        public void CalculatePoligons()
        {
            //Определить внутренние вершины в этом треугольнике
            foreach (LinkedListNode<GraphNode> lln in vertexNodes)
            {
                VertexGraphNode vgn = (VertexGraphNode)lln.Value;
                vgn.IsInnerVertex = polylineNesting.InnerVerts.Contains(verts[vgn.VertNum]);
            }


            List<List<Point3d>> holes = new List<List<Point3d>>();
            //Добавление оставшихся узлов и ребер в граф
            foreach (PolylinePart pp in PolylineParts)
            {
                PolylinePt check = pp.PolylinePts.First();
                if (check.TinSurfaceEdge != null || check.TinSurfaceVertex != null)//Эта полилиния пересекает треугольник?
                {
                    PolylinePt[] nodePts = new PolylinePt[]//Точки присоединения полилинии к границам треугольника
                    {
                        pp.PolylinePts.First(),
                        pp.PolylinePts.Last()
                    };
                    for (short i = 0; i < 2; i++)
                    {
                        PolylinePt pt = nodePts[i];
                        GraphNode graphNode = null;
                        if (pt.TinSurfaceVertex != null)
                        {
                            //Определить номер этой вершины в этом треугольнике и получить ссылку на узел графа 
                            short vertNum = GetVertNum(pt.TinSurfaceVertex);
                            if (vertNum == -1)
                            {
                                throw new Exception();
                            }
                            graphNode = vertexNodes[vertNum].Value;
                        }
                        else
                        {
                            //Определить номер этого ребра, добавить узел этого ребра
                            short edgeNum = GetEdgeNum(pt.TinSurfaceEdge);
                            if (edgeNum == -1)
                            {
                                throw new Exception();
                            }
                            graphNode = new EdgeGraphNode(this, edgeNum, pt.Point2D);
                        }

                        //дополнить свойства graphNode указателями на участок полилинии
                        graphNode.PolylinePart = pp;
                        graphNode.PolylinePartConnectedByStart = i == 0;
                        if (i == 0)
                        {
                            pp.StartNode = graphNode;
                        }
                        else
                        {
                            pp.EndNode = graphNode;
                        }
                    }
                    //Соединения для созданных узлов
                    pp.StartNode.ConnectedLinkedListNode = pp.EndNode.LinkedListNode;
                    pp.EndNode.ConnectedLinkedListNode = pp.StartNode.LinkedListNode;
                }
                else
                {
                    //Эта полилиния полностью находится внутри треугоьника
                    //Если эта полилиния - внешняя граница, то добавить полигон по всем точкам полилинии
                    if (pp.PolylineNestingNode.IsOuterBoundary)
                    {
                        List<Point3d> poligon = new List<Point3d>();
                        Polygons.Add(poligon);
                        foreach (PolylinePt pt in pp.PolylinePts)
                        {
                            poligon.Add(new Point3d(pt.Point2D.X, pt.Point2D.Y, pt.Z));
                        }
                    }
                    else
                    {
                        //Если эта полилиния ограничивает островок, то отложить этот полигон для дальнейшей обработки
                        List<Point3d> hole = new List<Point3d>();
                        holes.Add(hole);
                        foreach (PolylinePt pt in pp.PolylinePts)
                        {
                            hole.Add(new Point3d(pt.Point2D.X, pt.Point2D.Y, pt.Z));
                        }
                    }

                }
            }





            //Составление маршрутов обхода графа
            //Правила
            //- Начинать обход с любого еще не обойденнго узла, из которого исходит участок полилинии. Запомнить ссылку на PolylineNesting.Node
            //- Если из текущего узла исходит участок полилинии, который еще не обойден, то обойти его
            //- Из текущего узла не исходит такого участка полилинии (например, мы только что обошли участок полилинии и пришли в узел на ребре) =>
            //  - Есть 2 варианта куда идти дальше: либо вперед до следующего узла, либо назад 
            //  Проверяются оба варианта. При этом:
            //      - Обходить до тех пор пока не будет дотигнут стартовый узел
            //      - Нельзя заходить в те узлы которые уже посещены (только замыкание со стартовым узлом)
            //      - Нельзя заходить в узлы вершин, которые не являются внутренними
            //  Если в итоге получаются 2 варината обхода, взять точку изнутри каждого из обойденных полигонов и с помощью алгоритма WindingNumber проверить,
            //  попадают ли они внутрь PolylineNesting.Node. Если PolylineNesting.Node.IsOuterBoundary = true,
            //  то принять обход, точка которого попала внутрь PolylineNesting.Node, иначе принять обход, точка которого не попала внутрь PolylineNesting.Node


            //DisplayUtils.Polyline(vert2dLocs, true, 1, SurfaceMeshByBoundaryCommand.DB, null, SurfaceMeshByBoundaryCommand.ED);
            for (LinkedListNode<GraphNode> lln = graphNodes.First; lln != null; lln = lln.Next)
            {
                GraphNode startNode = lln.Value;
                if (!startNode.Visited && startNode.PolylinePart != null)
                {
                    startNode.Visited = true;
                    PolylinePart startPolyPart = startNode.PolylinePart;
                    GraphNode nextNode = startNode.ConnectedLinkedListNode.Value;
                    if (nextNode.Visited || startPolyPart.Visited)
                    {
                        //Такой ситуации не должно быть. Отметить проблемный треугольник
                        DisplayUtils.Polyline(vert2dLocs, true, 1, SurfaceMeshByBoundaryCommand.DB);
                        continue;
                    }

                    PolylineNesting.Node pNNode = startPolyPart.PolylineNestingNode;

                    //Начать составление маршрутов 2 вариантов замкнутого пути. Начинается всегда с прохода по участку полилинии
                    List<PathElement> path1 = new List<PathElement>() { startNode, startPolyPart, nextNode };
                    List<PathElement> path2 = new List<PathElement>() { startNode, startPolyPart, nextNode };

                    startPolyPart.Visited = true;//необходимо для правильной работы PathPreparing
                    bool path1Prepared = PathPreparing(nextNode, startNode, true, path1);
                    bool path2Prepared = PathPreparing(nextNode, startNode, false, path2);


                    List<Point3d> poligon1 = null;
                    List<Point3d> poligon2 = null;
                    if (path1Prepared)
                    {
                        //Заполнить полигон 1
                        poligon1 = GetPoligonFromPath(path1);
                        //DisplayUtils.Polyline(Utils.Poligon3DTo2D(poligon1), true, 1, SurfaceMeshByBoundaryCommand.DB);
                    }
                    if (path2Prepared)
                    {
                        //Заполнить полигон 2
                        poligon2 = GetPoligonFromPath(path2);
                        //DisplayUtils.Polyline(Utils.Poligon3DTo2D(poligon2), true, 1, SurfaceMeshByBoundaryCommand.DB);
                    }

                    List<PathElement> actualPath = null;
                    List<Point3d> actualPoligon = null;
                    if (path1Prepared && path2Prepared)//Если составлено 2 возможных путя
                    {
                        //Для 1-го варианта обхода найти внутреннюю точку (не на границе)
                        IList<Point2d> poligon1_2d = Utils.Poligon3DTo2D(poligon1);
                        //IList<Point2d> poligon2_2d = Utils.Poligon3DTo2D(poligon2);
                        Point2d? p1 = null;
                        try
                        {
                            p1 = Utils.GetAnyPointInsidePoligon(poligon1_2d, Utils.DirectionIsClockwise(poligon1_2d));
                        }
                        catch (Exception)
                        {
                            //Такой ситуации не должно быть. Отметить проблемный треугольник
                            DisplayUtils.Polyline(vert2dLocs, true, 1, SurfaceMeshByBoundaryCommand.DB);
                            continue;
                        }
                        //Point2d p2 = Utils.GetAnyPointInsidePoligon(poligon2_2d, Utils.DirectionIsClockwise(poligon2_2d));

                        if (p1 != null)
                        {
                            //Определить находится ли эта точка внутри полилинии
                            bool p1InsidePolyline = Utils.PointIsInsidePolylineWindingNumber(p1.Value, pNNode.Point2DCollection);

                            //Проверить, подходит ли 1-й вариант с учетом свойства IsOuterBoundary
                            if ((pNNode.IsOuterBoundary && p1InsidePolyline) || (!pNNode.IsOuterBoundary && !p1InsidePolyline))
                            {
                                //Первый вариант правильный
                                actualPoligon = poligon1;
                                actualPath = path1;
                            }
                            else
                            {
                                //Второй вариант правильный
                                actualPoligon = poligon2;
                                actualPath = path2;
                            }
                        }


                    }
                    else if (path1Prepared)
                    {
                        actualPoligon = poligon1;
                        actualPath = path1;
                    }
                    else if (path2Prepared)
                    {
                        actualPoligon = poligon2;
                        actualPath = path2;
                    }

                    if (actualPoligon != null)
                    {
                        //Для принятого пути обхода:
                        // - Добавить полигон в набор
                        // - Присвоить свойству Visited значение true
                        Polygons.Add(actualPoligon);
                        foreach (PathElement pe in actualPath)
                        {
                            pe.Visited = true;
                        }
                    }


                }
            }



            //~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~
            //ОБРАБОТКА ОТВЕРСТИЙ ВНУТРИ ПОЛИГОНОВ
            if (holes.Count > 0)
            {

                //Каждое отверстие находится внутри одного из рассчитанных полигонов. Определить полигон, в который вложено отверстие
                //Эти полигоны будут дополнены участками, включающими в себя отверстия
                //в соответствии с https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf п 3 - 5
                List<PolygonWithNested> poligonsWithHoles = PolygonWithHoles.ResolveHoles(Polygons, holes);

                
                foreach (PolygonWithNested p in poligonsWithHoles)
                {
                    if (p.Polygon == null)
                    {
                        //Если отверстия есть в корневом узле, значит нужно рассматривать полигон равный текущему треугольнику с отверстиями
                        p.Polygon = new List<Point3d>()
                        {
                            tinSurfaceTriangle.Vertex1.Location,
                            tinSurfaceTriangle.Vertex2.Location,
                            tinSurfaceTriangle.Vertex3.Location
                        };
                    }
                    else
                    {
                        //Если оказывается, что полигон включает в себя отверстие, то он должен быть удален из Polygons
                        //Такие полигоны будут разбиваться на треугольники
                        Polygons.Remove(p.Polygon);
                    }


                    p.MakeSimple();
                    //Polygons.Add(p.Polygon);
                    //Нельзя просто добавить простой полигон к общему списку полигонов (SubDMesh создается неправильно).
                    //Необходимо сделать триангуляцию
                    //TEST
                    #region Отрисовка простого полигона
                    //using (Transaction tr
                    //    = SurfaceMeshByBoundaryCommand.DB.TransactionManager.StartTransaction())
                    //using (Polyline pline = new Polyline())
                    //{
                    //    BlockTable bt = tr.GetObject(SurfaceMeshByBoundaryCommand.DB.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    //    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    //    pline.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                    //    foreach (Point3d pt in p.Polygon)
                    //    {
                    //        pline.AddVertexAt(0, new Point2d(pt.X, pt.Y), 0, 0, 0);
                    //    }
                    //    pline.Closed = true;

                    //    ms.AppendEntity(pline);
                    //    tr.AddNewlyCreatedDBObject(pline, true);

                    //    tr.Commit();
                    //}
                    #endregion
                    //TEST


                    EarClippingTriangulator triangulator = new EarClippingTriangulator(p.Polygon);
                    foreach (List<Point3d> tr in triangulator.Triangles)
                    {
                        Polygons.Add(tr);
                    }


                }

            }



            //~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~//~



        }

        /// <summary>
        /// Составление варианта маршрута
        /// </summary>
        /// <param name="startNode"></param>
        /// <param name="endNode"></param>
        /// <param name="forward"></param>
        /// <returns></returns>
        private bool PathPreparing(GraphNode startNode, GraphNode endNode, bool forward, List<PathElement> path)
        {
            List<PolylinePart> polyPartsTraversed = new List<PolylinePart>();
            GraphNode currNode = startNode;
            while (!currNode.Equals(endNode) && !currNode.Visited)
            {
                //- Обходить до тех пор пока не будет дотигнут стартовый узел
                //- Нельзя заходить в те узлы которые уже посещены во время других проходов (только замыкание со стартовым узлом)
                //- Нельзя заходить в узлы вершин, которые не являются внутренними (если вершина находится на границе, то она считается внутренней)
                VertexGraphNode vertexGraphNode = currNode as VertexGraphNode;
                if (vertexGraphNode != null && !vertexGraphNode.IsInnerVertex)
                {
                    break;
                }


                if (currNode.PolylinePart != null && !currNode.PolylinePart.Visited)
                {
                    //Если из текущего узла исходит участок полилинии, который еще не обойден, то обойти его
                    PolylinePart polylinePart = currNode.PolylinePart;
                    currNode = currNode.ConnectedLinkedListNode.Value;
                    path.Add(polylinePart);
                    path.Add(currNode);
                    //Переписать свойство Visited, чтобы не было обратного прохода
                    polylinePart.Visited = true;
                    polyPartsTraversed.Add(polylinePart);
                }
                else//Обход до следующего узла вдоль границы треугольника
                {
                    LinkedListNode<GraphNode> lln = currNode.LinkedListNode;
                    LinkedListNode<GraphNode> nextlln = forward ? lln.Next : lln.Previous;
                    if (nextlln == null)//Если дошли до конца двусвязного списка, то продолжить с противоположной стороны
                    {
                        nextlln = forward ? lln.List.First : lln.List.Last;
                    }

                    currNode = nextlln.Value;
                    path.Add(currNode);
                }
            }

            bool pathFound = false;
            //Если достигнут конечный узел то вернуть true
            if (currNode.Equals(endNode))
                pathFound = true;

            //После завершения составления маршрута сбросить свойство Visited у всех добавленных участков полилинии
            foreach (PolylinePart pp in polyPartsTraversed)
            {
                pp.Visited = false;
            }
            //Если начальный и конечный узлы равны, то удалить последний
            if (path.First().Equals(path.Last()))
            {
                path.RemoveAt(path.Count - 1);
            }

            return pathFound;
        }

        /// <summary>
        /// Получить полигон точек по последовательности узлов графа
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private List<Point3d> GetPoligonFromPath(List<PathElement> path)
        {
            List<Point3d> poligon = new List<Point3d>();
            bool polyPartFromStart = false;
            foreach (PathElement pe in path)
            {
                GraphNode graphNode = pe as GraphNode;
                PolylinePart polylinePart = pe as PolylinePart;
                VertexGraphNode vertexGraphNode = pe as VertexGraphNode;

                if (graphNode != null && graphNode.PolylinePart != null)
                {
                    polyPartFromStart = graphNode.PolylinePartConnectedByStart;
                }
                else if (polylinePart != null)
                {
                    //Для участка полилинии получить все 3d точки в нужном порядке
                    poligon.AddRange(polylinePart.GetPoints3dOrdered(polyPartFromStart));
                }
                if (vertexGraphNode != null && vertexGraphNode.PolylinePart == null)
                {
                    //Если из узла вершины не выходит участок полилинии, то добавить вершину треугольника в полигон
                    poligon.Add(verts[vertexGraphNode.VertNum].Location);
                }
            }

            return poligon;
        }


        /// <summary>
        /// Получить номер вершины в треугольнике
        /// </summary>
        /// <param name="vertex"></param>
        /// <returns></returns>
        private short GetVertNum(TinSurfaceVertex vertex)
        {
            if (vertex.Equals(tinSurfaceTriangle.Vertex1))
            {
                return 0;
            }
            else if (vertex.Equals(tinSurfaceTriangle.Vertex2))
            {
                return 1;
            }
            else if (vertex.Equals(tinSurfaceTriangle.Vertex3))
            {
                return 2;
            }
            return -1;
        }

        /// <summary>
        /// Получить номер ребра в треугольнике
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        private short GetEdgeNum(TinSurfaceEdge edge)
        {
            if (edge.Equals(tinSurfaceTriangle.Edge1))
            {
                return 0;
            }
            else if (edge.Equals(tinSurfaceTriangle.Edge2))
            {
                return 1;
            }
            else if (edge.Equals(tinSurfaceTriangle.Edge3))
            {
                return 2;
            }
            return -1;
        }


        /// <summary>
        /// Элемент замкнутого пути обхода графа - либо GraphNode, либо PolylinePart
        /// Данный класс служит для того чтобы можно было записывать и узлы и участки полилинии в одну коллекцию и отмечать посещенные
        /// </summary>
        public abstract class PathElement
        {
            /// <summary>
            /// Этот элемент графа уже обойден
            /// </summary>
            public bool Visited { get; set; } = false;
        }


        /// <summary>
        /// Узел графа - точка на границе треугольника
        /// </summary>
        public abstract class GraphNode : PathElement
        {
            /// <summary>
            /// Ссылка на граф
            /// </summary>
            public TriangleGraph TriangleGraph { get; set; }
            /// <summary>
            /// LinkedListNode в котором находится этот узел графа
            /// </summary>
            public LinkedListNode<GraphNode> LinkedListNode { get; set; }
            /// <summary>
            /// LinkedListNode, в котором находится узел графа, который соединен с этим через участок полилинии
            /// </summary>
            public LinkedListNode<GraphNode> ConnectedLinkedListNode { get; set; }
            /// <summary>
            /// Участок полилинии, который соединяет этот узел с другим
            /// </summary>
            public PolylinePart PolylinePart { get; set; }
            /// <summary>
            /// Участок полилинии подсоединен к этому началом
            /// </summary>
            public bool PolylinePartConnectedByStart { get; set; }


        }
        /// <summary>
        /// Узел графа, расположенный на вершине треугольника
        /// </summary>
        public class VertexGraphNode : GraphNode
        {
            /// <summary>
            /// Вершина является внутренней
            /// </summary>
            public bool IsInnerVertex { get; set; }
            /// <summary>
            /// Номер вершины треугольника
            /// </summary>
            public short VertNum { get; set; }
            /// <summary>
            /// Узлы вершин должны быть созданы последовательно. Сначала 0, потом 1 и 2
            /// При этом других узлов в графе не должно быть
            /// </summary>
            /// <param name="tg"></param>
            /// <param name="vertNum"></param>
            public VertexGraphNode(TriangleGraph tg, short vertNum)
            {
                if (vertNum > 2 || vertNum < 0)
                {
                    throw new ArgumentException(nameof(vertNum));
                }

                TriangleGraph = tg;
                VertNum = vertNum;

                //Добавить узел в нужное место в графе, заполнить свойство LinkedListNode и поле vertex
                if (tg.vertexNodes.Count(n => n != null) == vertNum
                    && tg.graphNodes.Count == vertNum)
                {
                    LinkedListNode = tg.graphNodes.AddLast(this);
                    tg.vertexNodes[vertNum] = LinkedListNode;
                }
                else
                {
                    throw new ArgumentException(nameof(tg));
                }
            }
        }
        /// <summary>
        /// Узел графа, расположенный на ребре треугольника
        /// </summary>
        private class EdgeGraphNode : GraphNode
        {
            /// <summary>
            /// Номер ребра треугольника
            /// </summary>
            public short EdgeNum { get; set; }
            /// <summary>
            /// Параметр, характеризующий положение узла на ребре
            /// </summary>
            public double Parameter { get; set; }
            /// <summary>
            /// Создание нового объекта возможно только после того как созданы узлы вершин
            /// </summary>
            /// <param name="tg"></param>
            /// <param name="edgeNum"></param>
            public EdgeGraphNode(TriangleGraph tg, short edgeNum, Point2d pt)
            {
                if (edgeNum > 2 || edgeNum < 0)
                {
                    throw new ArgumentException(nameof(edgeNum));
                }

                if (!tg.vertexNodes.All(n => n != null))
                {
                    throw new ArgumentException(nameof(tg));
                }

                TriangleGraph = tg;
                EdgeNum = edgeNum;

                //Расчитать параметр для этого узла
                Parameter = (pt - tg.vert2dLocs[edgeNum]).Length / tg.edgesLength[edgeNum];

                //Добавить узел в нужное место в графе, заполнить свойство LinkedListNode
                LinkedListNode<GraphNode> lln = tg.vertexNodes[edgeNum];
                do
                {
                    lln = lln.Next != null ? lln.Next : lln.List.First;

                    EdgeGraphNode edgeNode = lln.Value as EdgeGraphNode;
                    if (edgeNode != null && edgeNode.Parameter > Parameter)
                    {
                        //Если обнаружен узел с большим параметром, то выход из цикла
                        break;
                    }
                } while (!(lln.Value is VertexGraphNode));

                if (!lln.Equals(tg.vertexNodes[0]))
                {
                    LinkedListNode = tg.graphNodes.AddBefore(lln, this);
                }
                else//????На первом месте в списке всегда должна быть первая вершина. Перед ней ничего не вставляется. Вместо этого в конец списка
                {
                    LinkedListNode = tg.graphNodes.AddLast(this);
                }


            }
        }

        /// <summary>
        /// Участок полилинии проходящий через треугольник
        /// </summary>
        public class PolylinePart : PathElement
        {
            /// <summary>
            /// Ссылка на информацию о полилинии
            /// </summary>
            public PolylineNesting.Node PolylineNestingNode { get; private set; }

            /// <summary>
            /// Точки полилинии
            /// </summary>
            public LinkedList<PolylinePt> PolylinePts { get; private set; }

            /// <summary>
            /// Узел в начале участка полилинии
            /// </summary>
            public GraphNode StartNode { get; set; }

            /// <summary>
            /// Узел в конце участка полилинии
            /// </summary>
            public GraphNode EndNode { get; set; }

            public PolylinePart(LinkedList<PolylinePt> polylinePts)
            {
                if (polylinePts == null || polylinePts.Count == 0)
                {
                    throw new ArgumentException(nameof(polylinePts));
                }
                PolylinePts = polylinePts;
                PolylineNestingNode = polylinePts.First().Node;
            }

            /// <summary>
            /// Получить все точки участка полилинии от начала или от конца
            /// </summary>
            /// <param name="fromStart"></param>
            /// <returns></returns>
            public List<Point3d> GetPoints3dOrdered(bool fromStart)
            {
                List<Point3d> points = new List<Point3d>();
                LinkedListNode<PolylinePt> lln = fromStart ? PolylinePts.First : PolylinePts.Last;
                while (lln != null)
                {
                    points.Add(new Point3d(lln.Value.Point2D.X, lln.Value.Point2D.Y, lln.Value.Z));
                    lln = fromStart ? lln.Next : lln.Previous;
                }

                return points;
            }

        }


    }
}
