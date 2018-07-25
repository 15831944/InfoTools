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

        public PolylineNesting(/*Matrix3d transform,*/ TinSurface tinSurf)
        {
            Root = new Node(null, this);
            //Transform = transform;
            TinSurf = tinSurf;
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


            //Определить пересечения всех полилиний с ребрами поверхностей. Построение графов треугольников (добавление ребер участков полилиний)
            TraversePolylines(Root);

            //Обход графов треугольников
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


                List<LinkedList<PolylineVertexPlacementInfo>> vertSequences = new List<LinkedList<PolylineVertexPlacementInfo>>();

                for (int i = 0; i < node.Point3DCollection.Count; i++)
                {
                    Point3d pt = node.Point3DCollection[i];
                    TinSurfaceTriangle triangle = null;
                    try
                    {
                        triangle = TinSurf.FindTriangleAtXY(pt.X, pt.Y);
                    }
                    catch (System.ArgumentException) { }//Если точка за пределами поверхности, то выбрасывается исключение
                    if (triangle != null)
                    {
                        //Вершина находится на поверхности. Добавить ее в набор
                        PolylineVertexPlacementInfo pi = new PolylineVertexPlacementInfo() { VertNumber = i };
                        if (vertSequences.Count > 0 && vertSequences.Last().Last().VertNumber == i - 1)
                        {
                            //Продолжить заполнение последовательности
                            vertSequences.Last().AddLast(pi);
                        }
                        else
                        {
                            //Начать заполнение новой последовательности
                            LinkedList<PolylineVertexPlacementInfo> seq = new LinkedList<PolylineVertexPlacementInfo>();
                            seq.AddLast(pi);
                            vertSequences.Add(seq);
                        }

                        //Определить расположение вершины - внутри треугольника, на ребре или на вершине
                        double lambda1 = 0;
                        double lambda2 = 0;
                        bool isInside = Utils.BarycentricCoordinates(pt,
                            triangle.Vertex1.Location, triangle.Vertex2.Location, triangle.Vertex3.Location,
                            out lambda1, out lambda2);
                        if (!isInside)
                        {
                            throw new Exception("Положение вершины полилинии относительно поверхности не определено");
                        }

                        double lambda3 = 1 - lambda1 - lambda2;

                        //Проверить, равна ли хоть одна координата нулю или единице,
                        double[] coords = new double[] { lambda1, lambda2, lambda3 };
                        TinSurfaceVertex[] vertices = new TinSurfaceVertex[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };
                        TinSurfaceEdge[] edges = new TinSurfaceEdge[] { triangle.Edge1, triangle.Edge2, triangle.Edge3 };


                        int oneIndex = -1;
                        int zeroIndex = -1;
                        for (int cn = 0; cn < 3; cn++)
                        {
                            if (coords[cn] == 1)
                            {
                                oneIndex = cn;
                            }
                            else
                            if (coords[cn] == 0)
                            {
                                zeroIndex = cn;
                            }
                        }
                        if (oneIndex != -1)
                        {
                            //Точка лежит на вершине
                            pi.TinSurfaceVertex = vertices[oneIndex];
                        }
                        else if (zeroIndex != -1)
                        {
                            //Точка лежит на ребре
                            pi.TinSurfaceEdge = edges[zeroIndex];
                        }
                        else
                        {
                            //Точка лежит внутри треугольника
                            pi.TinSurfaceTriangle = triangle;
                        }

                    }
                }

                //Проверить, не требуется ли объединить первую и последнюю последовательности
                {
                    LinkedList<PolylineVertexPlacementInfo> firstSeq = vertSequences.First();
                    LinkedList<PolylineVertexPlacementInfo> lastSeq = vertSequences.Last();
                    if (vertSequences.Count > 1 &&
                        firstSeq.First().VertNumber
                        == (lastSeq.Last().VertNumber + 1) % node.Point3DCollection.Count)
                    {
                        //Объединить первую и последнюю последовательности
                        for (LinkedListNode<PolylineVertexPlacementInfo> lln = lastSeq.Last; lln != null; lln = lln.Previous)
                        {
                            firstSeq.AddFirst(lln.Value);
                        }
                        vertSequences.RemoveAt(vertSequences.Count - 1);
                    }
                }


                //Все вершины попали на поверхность
                bool allVertsOnSurface = vertSequences.Count == 1 && vertSequences.First().Count == node.Point3DCollection.Count;


                //Обход последовательностей
                foreach (LinkedList<PolylineVertexPlacementInfo> ll in vertSequences)
                {
                    if (!allVertsOnSurface)
                    {
                        TraversePolylineSegment(node.Polyline, ll.First(), null, false);
                    }

                    for (LinkedListNode<PolylineVertexPlacementInfo> lln = ll.First; lln != null; lln = lln.Next)
                    {
                        TraversePolylineSegment(node.Polyline, lln.Value, lln.Next.Value, true);
                    }

                }

            }

            foreach (Node nn in node.NestedNodes)
            {
                TraversePolylines(nn);
            }
        }


        private void TraversePolylineSegment(Polyline polyline, PolylineVertexPlacementInfo start,
            PolylineVertexPlacementInfo end = null, bool forvard = true)
        {
            //TODO: НЕОБХОДИМО УЧЕСТЬ ВСЕ ЧАСТНЫЕ СЛУЧАИ
            Point2d startPt2d = polyline.GetPoint2dAt(start.VertNumber);//первая точка сегмента
            Point2d endPt2d = Point2d.Origin;//вторая точка сегмента
            if (end != null)
            {
                endPt2d = polyline.GetPoint2dAt(end.VertNumber);
            }
            else
            {
                int endNum = forvard ? (start.VertNumber + 1) % polyline.NumberOfVertices
                    :
                    start.VertNumber != 0 ? start.VertNumber - 1 : polyline.NumberOfVertices - 1;
                endPt2d = polyline.GetPoint2dAt(endNum);
            }
            //Point3d startPt = new Point3d(startPt2d.X, startPt2d.Y, 0);
            //Point3d endPt = new Point3d(endPt2d.X, endPt2d.Y, 0);

            //Треугольники, в котороых будет искаться пересечение. Набор треугольников должен обновляться полностью каждую итерацию
            List<TinSurfaceTriangle> trianglesToSearchIntersection = new List<TinSurfaceTriangle>();
            //Ребра, которые не подлежат проверке на пересечение (НО ПОДЛЕЖАТ ПРОВЕРКЕ НА СОВПАДЕНИЕ С СЕГМЕНТОМ).
            //Набор ребер дополняется новыми ребрами каждую итерацию
            HashSet<TinSurfaceEdge> edgesNotChecking = new HashSet<TinSurfaceEdge>();
            //Ребра, которые не подлежат проверке ни на наложение, ни на пересечение
            HashSet<TinSurfaceEdge> edgesNotChecking1 = new HashSet<TinSurfaceEdge>();
            if (start.TinSurfaceTriangle != null)
            {
                trianglesToSearchIntersection.Add(start.TinSurfaceTriangle);
            }
            else if (start.TinSurfaceEdge != null)
            {
                trianglesToSearchIntersection.Add(start.TinSurfaceEdge.Triangle1);
                trianglesToSearchIntersection.Add(start.TinSurfaceEdge.Triangle2);
                edgesNotChecking.Add(start.TinSurfaceEdge);
            }
            else
            {
                trianglesToSearchIntersection.AddRange(start.TinSurfaceVertex.Triangles);
                //нельзя исключать из рассмотрения ни одного из ребер
                foreach (TinSurfaceEdge edge in start.TinSurfaceVertex.Edges)
                {
                    edgesNotChecking.Add(edge);
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
                    //Искать пересечения с каждым ребром
                    TinSurfaceEdge[] edges = new TinSurfaceEdge[] { triangle.Edge1, triangle.Edge2, triangle.Edge3 };

                    foreach (TinSurfaceEdge edge in edges)
                    {
                        if (!edgesNotChecking1.Contains(edge))
                        {
                            //Необходимо проверить каждое ребро на наложение даже если с ним не может быть пересечения
                            Point3d edgePt1 = edge.Vertex1.Location;
                            Point3d edgePt2 = edge.Vertex2.Location;
                            Point2d edgePt1_2d = new Point2d(edgePt1.X, edgePt1.Y);
                            Point2d edgePt2_2d = new Point2d(edgePt2.X, edgePt2.Y);
                            bool overlaying = false;
                            bool intersecting
                                = Utils.LineSegmentsAreIntersecting(startPt2d, endPt2d, edgePt1_2d, edgePt2_2d, out overlaying);

                            //Это ребро больше не должно подвергаться проверке
                            edgesNotChecking.Add(edge);
                            edgesNotChecking1.Add(edge);

                            if (overlaying)
                            {
                                //Если обнаружилось, что сегмент полилинии совпал с ребром поверхности
                                //то нужно взять точку ребра дальнюю от начала сегмента и переходить к следующей итерации
                                //как если бы произошло пересечение в вершине поверхности
                                double dist1 = startPt2d.GetDistanceTo(edgePt1_2d);
                                double dist2 = startPt2d.GetDistanceTo(edgePt2_2d);
                                TinSurfaceVertex vertex = dist1 > dist2 ? edge.Vertex1 : edge.Vertex2;
                                trianglesToSearchIntersection = vertex.Triangles.ToList();
                                foreach (TinSurfaceEdge edge1 in vertex.Edges)
                                {
                                    edgesNotChecking.Add(edge1);
                                }
                                intersectionFound = true;
                                break;
                            }
                            else if (!edgesNotChecking.Contains(edge) && intersecting)
                            {
                                //Если обнарушено пересечение, то расчитать точку пересечения
                                //Если она совпала с вершиной поверхности, то в следующей итерации проверять все треугольники, примыкающие к этой вершине
                                //ИЛИ нужно как-то найти точно тот треугольник, в котором будет продолжение линии????
                                //Если пересечение не совпало с вершиной поверхности, то переход к соседнему треугольнику
                                //!!!Здесь должен быть обнаружен выход за пределы поверхности
                                //!!
                                //переход к следующей итерации цикла while
                                intersectionFound = true;
                                break;
                            }
                        }

                    }
                    if (intersectionFound)
                        break;
                }
                if (intersectionFound)
                    continue;
                else
                    //Если пролистали все треугольники и не нашли пересечений с ребрами, значит вышли за границы поверхности через вершину поверхности
                    //Можно прерывать цикл while
                    break;
            }

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
                        if (i == polyline.NumberOfVertices - 1 && pt.Equals(Point3DCollection[0]))
                        {
                            //Вместо этого отредактировать полилинию, убрав из нее лишнюю точку
                            polyline.RemoveVertexAt(i);
                            polyline.Closed = true;
                        }
                        else
                        {
                            Point3DCollection.Add(pt);
                        }

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
        }


    }
}
