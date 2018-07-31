using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    //TODO: Обратить внимание на то, что должна быть расчитана координата Z для всех найденных точек

    /// <summary>
    /// Дерево для хранения данных о вложенности полилиний
    /// Предполагается, что все линии замкнуты, не имеют самопересечений и пересечений друг с другом
    /// </summary>
    public class PolylineNesting
    {
        /// <summary>
        /// Корневой узел - фиктивный, не имеет полилинии
        /// Никогда не меняется
        /// </summary>
        public Node Root { get; private set; }

        /// <summary>
        /// Трансформация для точек полилиний
        /// </summary>
        //public Matrix3d Transform { get; private set; }

        /// <summary>
        /// Поверхность по которой строится сеть
        /// </summary>
        public TinSurface TinSurf { get; private set; }

        /// <summary>
        /// Внутренние вершины
        /// </summary>
        public HashSet<TinSurfaceVertex> InnerVerts = new HashSet<TinSurfaceVertex>();

        /// <summary>
        /// Внутренние треугольники
        /// </summary>
        public HashSet<TinSurfaceTriangle> InnerTriangles = new HashSet<TinSurfaceTriangle>();

        /// <summary>
        /// Графы треугольников
        /// </summary>
        public Dictionary<TinSurfaceTriangle, TriangleGraph> TriangleGraphs
            = new Dictionary<TinSurfaceTriangle, TriangleGraph>();


        public Editor ed;//TEST
        public Database db;//TEST
        public BlockTableRecord ms;//TEST

        public PolylineNesting(/*Matrix3d transform,*/ TinSurface tinSurf,
            Database db,//TEST
            Editor ed//TEST
            )
        {
            Root = new Node(null, this);
            //Transform = transform;
            TinSurf = tinSurf;

            //TEST
            this.db = db;
            this.ed = ed;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                tr.Commit();
            }

            //TEST
        }

        /// <summary>
        /// Вставка новой полилинии
        /// </summary>
        /// <param name="polyline"></param>
        public void Insert(Polyline polyline)
        {
            Insert(Root, new Node(polyline, this));
        }

        private void Insert(Node node, Node insertingNode)
        {
            bool isNested = false;
            //Проверить вложена ли добавляемая полилиния в один из дочерних узлов
            foreach (Node nn in node.NestedNodes)
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
                    Node nn = node.NestedNodes[i];
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


        /// <summary>
        /// Расчет граней для построения сети по обертывающей к поверхности TIN
        /// </summary>
        /// <param name="tinSurf"></param>
        public void CalculatePoligons()
        {
            //Определить какие узлы представляют внешнюю границу, а какие внутреннюю
            ResolveOuterBoundary(Root, false);
            //Определить внутренние вершины и внутренние треугольники поверхности
            //Найти наборы внутренних вершин для внешних контуров

            foreach (Node node in Root.NestedNodes)
            {
                Extents3d? ext = node.Polyline.Bounds;
                if (ext != null)
                {
                    //TinSurfaceVertex[] verts = TinSurf.GetVerticesInsideBorder(node.Point3DCollection);
                    //К сожалению GetVerticesInsideBorder недостаточно производителен. Необходимо использовать R-tree
                    IReadOnlyList<TinSurfaceVertexS> verts = SurfaceMeshByBoundaryCommand.TreesCurrDoc[TinSurf.Handle.Value]
                        .Search(new RBush.Envelope()
                        {
                            MinX = node.MinX,
                            MinY = node.MinY,
                            MaxX = node.MaxX,
                            MaxY = node.MaxY,
                        });
                    foreach (TinSurfaceVertexS vertexS in verts)
                    {
                        TinSurfaceVertex vertex = vertexS.TinSurfaceVertex;
                        Point3d vertLoc = vertex.Location;

                        if (
                            //Проверить находится ли вершина в пределах BoundingBox полилинии
                            (vertLoc.X <= node.MaxX) && (vertLoc.X >= node.MinX) && (vertLoc.Y <= node.MaxY) && (vertLoc.Y >= node.MinY)
                            //Проверить, что точка точно находится внутри контура
                            && Utils.PointIsInsidePolylineWindingNumber(vertLoc, node.Point3DCollection)
                            )
                        {
                            InnerVerts.Add(vertex);
                        }

                        //if()

                    }
                }


            }
            //Затем для контуров внутренних границ найти те вершины, которые попадают в вырезы в контуре
            HashSet<TinSurfaceVertex> outerVerts = new HashSet<TinSurfaceVertex>();
            GetOuterVerts(Root, InnerVerts, outerVerts);
            foreach (TinSurfaceVertex ov in outerVerts)
            {
                InnerVerts.Remove(ov);
            }
            //Определить внутренние треугольники. На данном этапе считать, что внутренние треугольники - те, у которых все вершины внутренние
            //TODO: Но такие треугольники могут пересекаться с полилиниями. Пересекающиеся треугольники должны быть удалены на следующем этапе
            //HashSet<TinSurfaceTriangle> ch
            foreach (TinSurfaceVertex iv in InnerVerts)
            {
                foreach (TinSurfaceTriangle triangle in iv.Triangles)
                {
                    if (InnerVerts.Contains(triangle.Vertex1)
                        && InnerVerts.Contains(triangle.Vertex2)
                        && InnerVerts.Contains(triangle.Vertex3))
                    {
                        InnerTriangles.Add(triangle);
                    }
                }
            }


            //Определить пересечения всех полилиний с ребрами поверхностей. Добавление точек полилиний в графоы треугольников
            TraversePolylines(Root);

            //Графы треугольников
            foreach (TriangleGraph trGr in TriangleGraphs.Values)
            {
                trGr.PolylineParts();
            }

        }

        /// <summary>
        /// Присвоение значений свойству IsOuterBoundary
        /// </summary>
        /// <param name="node"></param>
        /// <param name="thisNodeIsOutsideBorder"></param>
        private void ResolveOuterBoundary(Node node, bool thisNodeIsOutsideBorder)
        {
            node.IsOuterBoundary = thisNodeIsOutsideBorder;
            foreach (Node nestedNode in node.NestedNodes)
            {
                ResolveOuterBoundary(nestedNode, !thisNodeIsOutsideBorder);
            }
        }

        /// <summary>
        /// Удалить из набора вершины, которые попали в контура вырезов
        /// </summary>
        /// <param name="node"></param>
        /// <param name="innerVerts"></param>
        private void GetOuterVerts(Node node, IEnumerable<TinSurfaceVertex> innerVerts, HashSet<TinSurfaceVertex> outerVerts)
        {
            //Набор тех вершин, которые находятся внутри node
            List<TinSurfaceVertex> innerVertsToRecursCall = new List<TinSurfaceVertex>();

            foreach (TinSurfaceVertex vert in innerVerts)
            {
                if (Utils.PointIsInsidePolylineWindingNumber(vert.Location, node.Point3DCollection))
                {
                    //Вершина внутри текущего узла
                    innerVertsToRecursCall.Add(vert);

                    if (!node.IsOuterBoundary)//Этот узел - внутренняя граница
                    {
                        bool insideInner = false;
                        foreach (Node nn in node.NestedNodes)
                        {
                            if (Utils.PointIsInsidePolylineWindingNumber(vert.Location, nn.Point3DCollection))
                            {
                                insideInner = true;
                                break;
                            }
                        }
                        //Если точка находится внутри node, но не находится внутри одного из node.NestedNodes, то она находится в вырезе
                        if (!insideInner)
                        {
                            outerVerts.Add(vert);
                        }
                    }

                }
            }

            foreach (Node nn in node.NestedNodes)
            {
                GetOuterVerts(nn, innerVertsToRecursCall, outerVerts);
            }
        }


        private void TraversePolylines(Node node)
        {
            if (node.Polyline != null)
            {
                //Обход полилинии
                //Учитываются только те случаи, когда в границах поверхности есть хотябы 1 вершина полилинии 
                //Для каждой вершины полилинии искать треугольник поверхности FindTriangleAtXY
                //Образуются последовательности вершин, которые находятся на поверхности (сохраняются индексы начала и конца последовательности и треугольник для каждой вершины)
                //!Несколько вершин может лежать в одном треугольнике
                //!Если все вершины лежат на поверхности - это не гарантирует того, что полилиния не выходит за границы поверхности
                //Если не все вершины попали в единую последовательность, то
                //обход каждой последовательности начинать с обратного прохода линии от первой точки последовательности до предыдущей точки полилинии
                //далее проход каждого сегмента полилинии (цикл for) от первой до последней точек в последовательности + следующая точка полилинии (в любом случае)
                //проход сегмента полилинии выполняется в цикле while - ищется пересечение со всеми ребрами треугольника пока не будет найдено,
                //далее переход к следующему треугольнику через пересекаемое ребро
                //!Особый случай - точка пересечения с треугольником совпадает с вершиной треугольника
                //!Особый случай - вершина полилинии лежит на ребре треугольника
                //!Особый случай - вершина полилинии совпала с вершиной треугольника
                //!Особый случай - сегмент полилинии совпал с ребром поверхности!!!


                List<LinkedList<PolylinePt>> vertSequences = new List<LinkedList<PolylinePt>>();

                for (int i = 0; i < node.Point3DCollection.Count; i++)
                {
                    Point3d pt = node.Point3DCollection[i];
                    Point2d pt2d = new Point2d(pt.X, pt.Y);
                    TinSurfaceTriangle triangle = null;
                    try
                    {
                        triangle = TinSurf.FindTriangleAtXY(pt.X, pt.Y);
                    }
                    catch (System.ArgumentException) { }//Если точка за пределами поверхности, то выбрасывается исключение
                    if (triangle != null)
                    {
                        //Расчет барицентрических координат для определения координаты Z
                        Point2d v1_2d = new Point2d(triangle.Vertex1.Location.X, triangle.Vertex1.Location.Y);
                        Point2d v2_2d = new Point2d(triangle.Vertex2.Location.X, triangle.Vertex2.Location.Y);
                        Point2d v3_2d = new Point2d(triangle.Vertex3.Location.X, triangle.Vertex3.Location.Y);
                        double lambda1 = 0;
                        double lambda2 = 0;
                        Utils.BarycentricCoordinates(pt2d, v1_2d, v2_2d, v3_2d, out lambda1, out lambda2);
                        double lambda3 = 1 - lambda1 - lambda2;
                        //TODO: Расчет координаты Z

                        //Вершина находится на поверхности. Добавить ее в набор
                        PolylinePt polyPt = new PolylinePt(node, pt2d) { VertNumber = i };
                        if (vertSequences.Count > 0 && vertSequences.Last().Last().VertNumber == i - 1)
                        {
                            //Продолжить заполнение последовательности
                            vertSequences.Last().AddLast(polyPt);
                        }
                        else
                        {
                            //Начать заполнение новой последовательности
                            LinkedList<PolylinePt> seq = new LinkedList<PolylinePt>();
                            seq.AddLast(polyPt);
                            vertSequences.Add(seq);
                        }

                        //Определить расположение вершины - внутри треугольника, на ребре или на вершине
                        PtPositionInTriangle(pt2d, triangle, polyPt);

                        //Добавление в граф треугольника
                        AddPolylinePt(polyPt);

                    }
                }

                if (vertSequences.Count == 0)
                {
                    //Еслиполилиния полностью за пределами поверхности, то прервать выполнение
                    return;
                }


                //Проверить, не требуется ли объединить первую и последнюю последовательности
                {
                    LinkedList<PolylinePt> firstSeq = vertSequences.First();
                    LinkedList<PolylinePt> lastSeq = vertSequences.Last();
                    if (vertSequences.Count > 1 &&
                        firstSeq.First().VertNumber
                        == (lastSeq.Last().VertNumber + 1) % node.Point3DCollection.Count)
                    {
                        //Объединить первую и последнюю последовательности
                        for (LinkedListNode<PolylinePt> lln = lastSeq.Last; lln != null; lln = lln.Previous)
                        {
                            firstSeq.AddFirst(lln.Value);
                        }
                        vertSequences.RemoveAt(vertSequences.Count - 1);
                    }
                }


                //Все вершины попали на поверхность
                bool allVertsOnSurface = vertSequences.Count == 1 && vertSequences.First().Count == node.Point3DCollection.Count;


                //Обход последовательностей
                foreach (LinkedList<PolylinePt> seq in vertSequences)
                {
                    if (!allVertsOnSurface)
                    {
                        TraversePolylineSegment(node, seq.First, false);
                    }

                    bool prevTraversed = true;
                    for (LinkedListNode<PolylinePt> lln = seq.First; lln != null; lln = lln.Next)
                    {
                        if (!prevTraversed)
                        {
                            //Сделать обратный проход, а затем прямой
                            TraversePolylineSegment(node, lln, false);
                        }
                        prevTraversed = TraversePolylineSegment(node, lln, true);

                    }

                }
            }

            //Рекурсивный вызов для вложенных полилиний
            foreach (Node nn in node.NestedNodes)
            {
                TraversePolylines(nn);
            }

        }

        /// <summary>
        /// Определение положения точки внутри треугольника, на ребре или на вершине
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="triangle"></param>
        /// <param name="polyPt"></param>
        private void PtPositionInTriangle(Point2d pt, TinSurfaceTriangle triangle, PolylinePt polyPt)
        {
            TinSurfaceVertex[] vertices = new TinSurfaceVertex[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };
            TinSurfaceEdge[] edges = new TinSurfaceEdge[] { triangle.Edge1, triangle.Edge2, triangle.Edge3 };
            Point2d[] vert2dLocs = new Point2d[3];
            for (int i = 0; i < 3; i++)
            {
                vert2dLocs[i] = new Point2d(vertices[i].Location.X, vertices[i].Location.Y);
            }
            //Проверить, совпадает ли точка с одной из вершин или попадет на ребро
            for (int i = 0; i < 3; i++)
            {
                Point2d vert2dLoc = vert2dLocs[i];

                if (pt.IsEqualTo(vert2dLoc))
                {
                    //Точка лежит на вершине
                    polyPt.TinSurfaceVertex = vertices[i];
                    return;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                using (Polyline line = new Polyline())
                {
                    line.AddVertexAt(0, vert2dLocs[i], 0, 0, 0);
                    line.AddVertexAt(0, vert2dLocs[(i + 1) % 3], 0, 0, 0);
                    Point3d pt3d = new Point3d(pt.X, pt.Y, 0);
                    Point3d closestPtOnEdge = line.GetClosestPointTo(pt3d, false);

                    Tolerance tolerance = /*Tolerance.Global;*/new Tolerance(0.001, 0.001);
                    if (pt3d.IsEqualTo(closestPtOnEdge, tolerance))//Здесь должен быть допуск
                    {
                        //Точка лежит на ребре
                        polyPt.TinSurfaceEdge = edges[i];
                        return;
                    }
                }
            }
            //Если не на ребре и не на вершине, то внутри треугольника
            polyPt.TinSurfaceTriangle = triangle;
        }


        private const bool testDisplay = false;

        //TODO: НЕОБХОДИМО УЧЕСТЬ ВСЕ ЧАСТНЫЕ СЛУЧАИ
        /// <summary>
        /// Поиск пересечений ребер поверхности с сегментом полилинии
        /// Возврат false если при проходе ребра был выход за границу поверхности
        /// </summary>
        /// <param name="polyline"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="forvard"></param>
        private bool TraversePolylineSegment(Node node,
            LinkedListNode<PolylinePt> lln,
            //PolylinePt start, PolylinePt end = null,
            bool forvard = true)
        {
            PolylinePt start = lln.Value;
            PolylinePt next = lln.Next?.Value;
            PolylinePt prev = lln.Previous?.Value;
            Polyline polyline = node.Polyline;
            bool segmentTraversedAsPossible = true;


            Point2d startPt2d = start.Point2D;//первая точка сегмента
            Point2d endPt2d = Point2d.Origin;//вторая точка сегмента
            if (forvard)
            {
                if (next != null)
                {
                    endPt2d = next.Point2D;
                }
                else
                {
                    int endNum = (start.VertNumber + 1) % polyline.NumberOfVertices;
                    endPt2d = polyline.GetPoint2dAt(endNum);
                }
            }
            else
            {
                if (prev != null)
                {
                    endPt2d = prev.Point2D;
                }
                else
                {
                    int endNum = start.VertNumber != 0 ? start.VertNumber - 1 : polyline.NumberOfVertices - 1;
                    endPt2d = polyline.GetPoint2dAt(endNum);
                }

            }


            ObjectId ptId = ObjectId.Null;//TEST

            try
            {
                //TEST
                #region MyRegion
                if (testDisplay)
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    using (Line line = new Line(new Point3d(startPt2d.X, startPt2d.Y, 0), new Point3d(endPt2d.X, endPt2d.Y, 0)))
                    using (Circle circle1 = new Circle(new Point3d(startPt2d.X, startPt2d.Y, 0), Vector3d.ZAxis, 0.3))
                    {
                        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 4);
                        ms.AppendEntity(line);
                        tr.AddNewlyCreatedDBObject(line, true);

                        circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 8);
                        ptId = ms.AppendEntity(circle1);
                        tr.AddNewlyCreatedDBObject(circle1, true);

                        tr.Commit();
                        line.Draw();
                        ed.Regen();
                        ed.UpdateScreen();
                    }
                #endregion
                //TEST




                //Треугольники, в котороых будет искаться пересечение. Набор треугольников должен обновляться полностью каждую итерацию
                List<TinSurfaceTriangle> trianglesToSearchIntersection = new List<TinSurfaceTriangle>();
                //Ребра, которые не подлежат проверке на пересечение и наложение.
                //Набор ребер дополняется новыми ребрами каждую итерацию
                HashSet<TinSurfaceEdge> edgesAlreadyIntersected = new HashSet<TinSurfaceEdge>();
                HashSet<TinSurfaceEdge> edgesAlreadyOverlapped = new HashSet<TinSurfaceEdge>();
                if (start.TinSurfaceTriangle != null)
                {
                    trianglesToSearchIntersection.Add(start.TinSurfaceTriangle);
                }
                else if (start.TinSurfaceEdge != null)
                {
                    trianglesToSearchIntersection.Add(start.TinSurfaceEdge.Triangle1);
                    trianglesToSearchIntersection.Add(start.TinSurfaceEdge.Triangle2);
                    edgesAlreadyIntersected.Add(start.TinSurfaceEdge);
                }
                else
                {
                    trianglesToSearchIntersection.AddRange(start.TinSurfaceVertex.Triangles);
                    foreach (TinSurfaceEdge edge in start.TinSurfaceVertex.Edges)
                    {
                        edgesAlreadyIntersected.Add(edge);
                    }

                }

                //Основной цикл прохода сегмента полилинии
                //В каждой итерации требуется найти пересечение с ребром
                bool intersectionFound = false;
                while (true)
                {
                    intersectionFound = false;
                    foreach (TinSurfaceTriangle triangle in trianglesToSearchIntersection)
                    {
                        if (triangle == null)
                        {
                            continue;
                        }

                        ObjectId plineId = ObjectId.Null;//TEST
                        try
                        {
                            //TEST
                            #region MyRegion
                            if (testDisplay)
                                using (Transaction tr = db.TransactionManager.StartTransaction())
                                using (Polyline pline = new Polyline())
                                {
                                    ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                    Point3d vert1 = triangle.Vertex1.Location;
                                    Point3d vert2 = triangle.Vertex2.Location;
                                    Point3d vert3 = triangle.Vertex3.Location;

                                    pline.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                                    pline.AddVertexAt(0, new Point2d(vert1.X, vert1.Y), 0, 0, 0);
                                    pline.AddVertexAt(1, new Point2d(vert2.X, vert2.Y), 0, 0, 0);
                                    pline.AddVertexAt(2, new Point2d(vert3.X, vert3.Y), 0, 0, 0);
                                    pline.Closed = true;

                                    plineId = ms.AppendEntity(pline);
                                    tr.AddNewlyCreatedDBObject(pline, true);

                                    tr.Commit();
                                    pline.Draw();
                                    ed.Regen();
                                    ed.UpdateScreen();
                                }
                            #endregion
                            //TEST


                            //Искать пересечения с каждым ребром
                            TinSurfaceEdge[] edges = new TinSurfaceEdge[] { triangle.Edge1, triangle.Edge2, triangle.Edge3 };

                            foreach (TinSurfaceEdge edge in edges)
                            {
                                ObjectId lineId = ObjectId.Null;//TEST


                                try
                                {
                                    if (!edgesAlreadyOverlapped.Contains(edge))
                                    {
                                        //Необходимо проверить каждое ребро на наложение даже если с ним не может быть пересечения
                                        Point3d edgePt1 = edge.Vertex1.Location;
                                        Point3d edgePt2 = edge.Vertex2.Location;


                                        //TEST
                                        #region MyRegion
                                        if (testDisplay)
                                            using (Transaction tr = db.TransactionManager.StartTransaction())
                                            using (Line line = new Line(edge.Vertex1.Location, edge.Vertex2.Location))
                                            {
                                                ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                                line.LineWeight = LineWeight.LineWeight040;
                                                lineId = ms.AppendEntity(line);
                                                tr.AddNewlyCreatedDBObject(line, true);

                                                tr.Commit();
                                                line.Draw();
                                                ed.Regen();
                                                ed.UpdateScreen();
                                            }
                                        #endregion
                                        //TEST



                                        Point2d edgePt1_2d = new Point2d(edgePt1.X, edgePt1.Y);
                                        Point2d edgePt2_2d = new Point2d(edgePt2.X, edgePt2.Y);
                                        bool overlapping = false;
                                        Point2d? intersection = Utils//Расчет точки пересечения с помощью AutoCAD
                                            .GetLinesIntersectionAcad(startPt2d, endPt2d, edgePt1_2d, edgePt2_2d, out overlapping);


                                        //if (intersection == null && !overlapping//В некоторых случаях автокад не просчитывает пересечение, хотя оно есть!
                                        //    && Utils.LineSegmentsAreIntersecting(startPt2d, endPt2d, edgePt1_2d, edgePt2_2d))
                                        //{
                                        //    //Тогда расчитать точку пересечения по правилу Крамера
                                        //    intersection = Utils.GetLinesIntersectionCramer(startPt2d, endPt2d, edgePt1_2d, edgePt2_2d, out overlapping);
                                        //}


                                        if (overlapping)
                                        {
                                            //Это ребро больше не должно подвергаться проверке
                                            //edgesAlreadyIntersected.Add(edge);
                                            edgesAlreadyOverlapped.Add(edge);
                                            //Если обнаружилось, что сегмент полилинии совпал с ребром поверхности
                                            //то нужно взять точку ребра дальнюю от начала сегмента и переходить к следующей итерации
                                            //как если бы произошло пересечение в вершине поверхности
                                            double dist1 = startPt2d.GetDistanceTo(edgePt1_2d);
                                            double dist2 = startPt2d.GetDistanceTo(edgePt2_2d);
                                            double maxDist = dist1 > dist2 ? dist1 : dist2;
                                            double distNextPt = startPt2d.GetDistanceTo(endPt2d);
                                            if (distNextPt > maxDist)//Сегмент полилинии проходит через все ребро?
                                            {
                                                TinSurfaceVertex vertex = dist1 > dist2 ? edge.Vertex1 : edge.Vertex2;
                                                //Проверка на совпадение с конечной точкой сегмента.
                                                Point2d vertexLoc = new Point2d(vertex.Location.X, vertex.Location.Y);
                                                if (!vertexLoc.IsEqualTo(endPt2d))
                                                {
                                                    trianglesToSearchIntersection = vertex.Triangles.ToList();
                                                    foreach (TinSurfaceEdge edge1 in vertex.Edges)
                                                    {
                                                        edgesAlreadyIntersected.Add(edge1);
                                                    }
                                                    AddPolylinePt(new PolylinePt(node, vertexLoc) { TinSurfaceVertex = vertex });
                                                    //TEST
                                                    #region MyRegion
                                                    if (testDisplay)
                                                        using (Transaction tr = db.TransactionManager.StartTransaction())
                                                        using (Circle circle1 = new Circle(edgePt1, Vector3d.ZAxis, 0.1))
                                                        using (Circle circle2 = new Circle(edgePt2, Vector3d.ZAxis, 0.1))
                                                        {
                                                            ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;


                                                            circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                                            circle2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                                            ms.AppendEntity(circle1);
                                                            ms.AppendEntity(circle2);
                                                            tr.AddNewlyCreatedDBObject(circle1, true);
                                                            tr.AddNewlyCreatedDBObject(circle2, true);

                                                            tr.Commit();
                                                            circle1.Draw();
                                                            circle2.Draw();
                                                            ed.Regen();
                                                            ed.UpdateScreen();
                                                        }
                                                    #endregion
                                                    //TEST
                                                    intersectionFound = true;
                                                    break;
                                                }

                                            }


                                        }
                                        else if (!edgesAlreadyIntersected.Contains(edge) && intersection != null)
                                        {
                                            //Это ребро больше не должно подвергаться проверке
                                            edgesAlreadyIntersected.Add(edge);
                                            //Если обнарушено пересечение, то расчитать точку пересечения
                                            //Если она совпала с вершиной поверхности, то в следующей итерации проверять все треугольники, примыкающие к этой вершине
                                            //ИЛИ нужно как-то найти точно тот треугольник, в котором будет продолжение линии????
                                            //Если пересечение не совпало с вершиной поверхности, то переход к соседнему треугольнику
                                            //!!!Здесь должен быть обнаружен выход за пределы поверхности
                                            Point2d intersectionPt = intersection.Value;
                                            if (!intersectionPt.IsEqualTo(endPt2d))//Проверка на совпадение с конечной точкой сегмента
                                            {
                                                if (intersectionPt.IsEqualTo(edgePt1_2d))//пересечение совпало с 1-й вершиной ребра
                                                {
                                                    trianglesToSearchIntersection = edge.Vertex1.Triangles.ToList();
                                                    foreach (TinSurfaceEdge edge1 in edge.Vertex1.Edges)
                                                    {
                                                        edgesAlreadyIntersected.Add(edge1);
                                                    }
                                                    AddPolylinePt(new PolylinePt(node, intersectionPt) { TinSurfaceVertex = edge.Vertex1 });
                                                    //TEST
                                                    #region MyRegion
                                                    if (testDisplay)
                                                        using (Transaction tr = db.TransactionManager.StartTransaction())
                                                        using (Circle circle1 = new Circle(edgePt1, Vector3d.ZAxis, 0.1))
                                                        {
                                                            ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                                            circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                                            ms.AppendEntity(circle1);
                                                            tr.AddNewlyCreatedDBObject(circle1, true);

                                                            tr.Commit();
                                                            circle1.Draw();
                                                            ed.Regen();
                                                            ed.UpdateScreen();
                                                        }
                                                    #endregion
                                                    //TEST
                                                }
                                                else if (intersectionPt.IsEqualTo(edgePt2_2d))//пересечение совпало с 2-й вершиной ребра
                                                {
                                                    trianglesToSearchIntersection = edge.Vertex2.Triangles.ToList();
                                                    foreach (TinSurfaceEdge edge1 in edge.Vertex2.Edges)
                                                    {
                                                        edgesAlreadyIntersected.Add(edge1);
                                                    }
                                                    AddPolylinePt(new PolylinePt(node, intersectionPt) { TinSurfaceVertex = edge.Vertex2 });
                                                    //TEST
                                                    #region MyRegion
                                                    if (testDisplay)
                                                        using (Transaction tr = db.TransactionManager.StartTransaction())
                                                        using (Circle circle1 = new Circle(edgePt2, Vector3d.ZAxis, 0.1))
                                                        {
                                                            ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                                            circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                                            ms.AppendEntity(circle1);
                                                            tr.AddNewlyCreatedDBObject(circle1, true);

                                                            tr.Commit();
                                                            circle1.Draw();
                                                            ed.Regen();
                                                            ed.UpdateScreen();
                                                        }
                                                    #endregion
                                                    //TEST
                                                }
                                                else//пересечение не совпало с вершиной
                                                {
                                                    trianglesToSearchIntersection = new List<TinSurfaceTriangle>();
                                                    //Взять соседний треугольник если он есть
                                                    TinSurfaceTriangle nextTriangle =
                                                    !edge.Triangle1.Equals(triangle) ? edge.Triangle1 : edge.Triangle2;
                                                    if (nextTriangle != null)
                                                    {
                                                        trianglesToSearchIntersection.Add(nextTriangle);
                                                    }
                                                    else
                                                    {
                                                        if (next != null && forvard)
                                                        {
                                                            //Если при прямом проходе встречена граница поверхности и известно, что следующая вершина полилинии лежит на поверхности, то использовать метод
                                                            //TinSurf.FindTriangleAtXY для точек на ребре с шагом 1 ед длины пока не будет найден треугольник
                                                            //Если треугольник найден, то сначала выполнить обратный проход от него а затем продолжить прямой проход от него
                                                            //Для этого в последовательность вставляется новый узел после переданного lln если только найденная точка не совпала с конечной или не перескочила ее
                                                            //TODO?: КАК ОТСЛЕДИТЬ ВЫХОД ЗА ГРАНИЦУ ПОВЕРХНОСТИ ЧЕРЕЗ ВЕРШИНУ ПОВЕРХНОСТИ???
                                                            Vector2d segmentVector = endPt2d - startPt2d;
                                                            double overalLength = segmentVector.Length;
                                                            segmentVector = segmentVector.GetNormal();
                                                            int n = 1;
                                                            Point2d testingPt = Point2d.Origin;
                                                            TinSurfaceTriangle otherTriangle = null;
                                                            bool overrun = false;
                                                            do
                                                            {
                                                                testingPt = intersectionPt + segmentVector * n;
                                                                n++;
                                                                overrun = (testingPt - startPt2d).Length > overalLength;
                                                                try { otherTriangle = TinSurf.FindTriangleAtXY(testingPt.X, testingPt.Y); }
                                                                catch (System.ArgumentException) { }
                                                            }
                                                            while (otherTriangle == null && !overrun);

                                                            if (otherTriangle != null && !overrun)
                                                            {
                                                                PolylinePt polyPt = new PolylinePt(node, testingPt);
                                                                PtPositionInTriangle(testingPt, otherTriangle, polyPt);

                                                                lln.List.AddAfter(lln, polyPt);


                                                                segmentTraversedAsPossible = false;
                                                            }
                                                        }

                                                    }
                                                    AddPolylinePt(new PolylinePt(node, intersectionPt) { TinSurfaceEdge = edge });
                                                    //TEST
                                                    #region MyRegion
                                                    if (testDisplay)
                                                        using (Transaction tr = db.TransactionManager.StartTransaction())
                                                        using (Circle circle1 = new Circle(new Point3d(intersectionPt.X, intersectionPt.Y, 0), Vector3d.ZAxis, 0.1))
                                                        {
                                                            ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                                            circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                                                            ms.AppendEntity(circle1);
                                                            tr.AddNewlyCreatedDBObject(circle1, true);

                                                            tr.Commit();
                                                            circle1.Draw();
                                                            ed.Regen();
                                                            ed.UpdateScreen();
                                                        }
                                                    #endregion
                                                    //TEST
                                                }


                                                //переход к следующей итерации цикла while
                                                intersectionFound = true;
                                                break;
                                            }

                                        }
                                    }


                                }
                                finally
                                {
                                    //TEST
                                    #region MyRegion
                                    if (testDisplay)
                                        if (!lineId.IsNull)
                                            using (Transaction tr = db.TransactionManager.StartTransaction())
                                            {
                                                Line line = tr.GetObject(lineId, OpenMode.ForWrite) as Line;
                                                line.Erase();
                                                tr.Commit();
                                                ed.Regen();
                                                ed.UpdateScreen();
                                            }
                                    #endregion
                                    //TEST
                                }

                            }
                            if (intersectionFound)
                                break;
                        }
                        finally
                        {
                            //TEST
                            #region MyRegion
                            if (testDisplay)
                                if (!plineId.IsNull)
                                    using (Transaction tr = db.TransactionManager.StartTransaction())
                                    {
                                        Polyline pline = tr.GetObject(plineId, OpenMode.ForWrite) as Polyline;
                                        pline.Erase();
                                        tr.Commit();
                                        ed.Regen();
                                        ed.UpdateScreen();
                                    }
                            #endregion
                            //TEST
                        }
                    }
                    if (intersectionFound)
                        continue;
                    else
                    {
                        //выход из цикла если при обходе всех треугольников, подлежащих проверке пересечений не обнаружено.
                        break;
                    }

                }
            }
            finally
            {
                //TEST
                #region MyRegion
                if (testDisplay)
                    if (!ptId.IsNull)
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Circle pt = tr.GetObject(ptId, OpenMode.ForWrite) as Circle;
                            pt.Erase();
                            tr.Commit();
                            ed.Regen();
                            ed.UpdateScreen();
                        }
                #endregion
                //TEST
            }

            return segmentTraversedAsPossible;

        }



        /// <summary>
        /// Точка полилинии совпавшая с вершиной треугольника
        /// </summary>
        private void AddPolylinePt(PolylinePt pt)
        {
            if (pt.TinSurfaceVertex != null)
            {
                //Добавить точку во все примыкающие треугольники
                foreach (TinSurfaceTriangle triangle in pt.TinSurfaceVertex.Triangles)
                {
                    AddPolylinePt1(triangle, pt);
                }
            }
            else if (pt.TinSurfaceEdge != null)
            {
                //Добавить точку в 2 треугольника (или 1 если это граница поверхности)
                TinSurfaceTriangle triangle1 = pt.TinSurfaceEdge.Triangle1;
                if (triangle1 != null)
                {
                    AddPolylinePt1(triangle1, pt);
                }
                TinSurfaceTriangle triangle2 = pt.TinSurfaceEdge.Triangle2;
                if (triangle2 != null)
                {
                    AddPolylinePt1(triangle2, pt);
                }
            }
            else if (pt.TinSurfaceTriangle != null)
            {
                AddPolylinePt1(pt.TinSurfaceTriangle, pt);
            }
            else
            {
                throw new Exception("Точка полилинии не привязана к треугольнику");
            }

        }


        /// <summary>
        /// Точка полилинии внутри треугольника
        /// </summary>
        /// <param name="triangle"></param>
        /// <param name="pt"></param>
        private void AddPolylinePt1(TinSurfaceTriangle triangle, PolylinePt pt)
        {
            TriangleGraph triangleGraph = null;
            //Добавить в один треугольник
            TriangleGraphs.TryGetValue(triangle, out triangleGraph);
            if (triangleGraph == null)
            {
                triangleGraph = new TriangleGraph(this, triangle);
                TriangleGraphs.Add(triangle, triangleGraph);
            }
            triangleGraph.AddPolylinePoint(pt);
        }


        /// <summary>
        /// Узел дерева - 1 полилиния
        /// </summary>
        public class Node
        {
            /// <summary>
            /// Ссылка на дерево
            /// </summary>
            public PolylineNesting PolylineNesting { get; private set; }

            /// <summary>
            /// Полилиния
            /// </summary>
            public Polyline Polyline { get; private set; }

            /// <summary>
            /// Точки полилинии. Точки пересчитаны в глобальную систему координат
            /// </summary>
            public Point3dCollection Point3DCollection { get; private set; } = new Point3dCollection();
            public double MinX { get; private set; } = double.MaxValue;
            public double MinY { get; private set; } = double.MaxValue;
            public double MaxX { get; private set; } = double.MinValue;
            public double MaxY { get; private set; } = double.MinValue;

            /// <summary>
            /// Внешняя граница области
            /// </summary>
            public bool IsOuterBoundary { get; set; } = false;

            /// <summary>
            /// Вложенные узлы
            /// </summary>
            public List<Node> NestedNodes { get; private set; } = new List<Node>();

            public Node(Polyline polyline, PolylineNesting polylineNesting)
            {
                Polyline = polyline;
                PolylineNesting = polylineNesting;
                //Заполнить набор точек полилинии
                //Получить минимальные и максимальные значения X и Y
                if (polyline != null)
                {
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        Point3d pt = polyline.GetPoint3dAt(i);
                        if (pt.X < MinX)
                        {
                            MinX = pt.X;
                        }
                        if (pt.Y < MinY)
                        {
                            MinY = pt.Y;
                        }
                        if (pt.X > MaxX)
                        {
                            MaxX = pt.X;
                        }
                        if (pt.Y > MaxY)
                        {
                            MaxY = pt.Y;
                        }


                        //Если последняя точка равна первой, то не добавлять ее
                        //if (i == polyline.NumberOfVertices - 1 && pt.IsEqualTo(Point3DCollection[0]))
                        //{
                        //    //Вместо этого отредактировать полилинию, убрав из нее лишнюю точку
                        //    polyline.RemoveVertexAt(i);
                        //    polyline.Closed = true;
                        //}
                        //else
                        //{
                        Point3DCollection.Add(pt);
                        //}

                    }
                }

            }

            /// <summary>
            /// Переданный узел вложен в вызывающий узел
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            public bool IsNested(Node node)
            {
                //Проверка по одной точке, так как предполагается, что полилинии не пересекаются
                return Utils.PointIsInsidePolylineWindingNumber(node.Point3DCollection[0], this.Point3DCollection);
            }

            public override int GetHashCode()
            {
                return Polyline.Id.GetHashCode();
            }
        }


    }
}
