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
                TinSurfaceVertex[] verts = TinSurf.GetVerticesInsideBorder(node.Point3DCollection);
                foreach (TinSurfaceVertex vertex in verts)
                {
                    InnerVerts.Add(vertex);
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
                    if (InnerVerts.Contains( triangle.Vertex1)
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
            if (node.Polyline!=null)
            {
                //Обход полилинии
                //Учитываются только те случаи, когда в границах поверхности есть хотябы 1 вершина полилинии 
                //Для каждой вершины полилинии искать треугольник поверхности FindTriangleAtXY
                //Образуются последовательности вершин, которые находятся на поверхности (сохраняются индексы начала и конца последовательности и треугольник для каждой вершины)
                //!Несколько вершин может лежать в одном треугольнике
                //!Если все вершины лежат на поверхности - это не гарантирует того, что полилиния не выходит за границы поверхности
                //Если не все вершины попали в единую последовательность, то
                //обход каждой последовательности начинать обратного прохода линии от первой точки последовательности до предыдущей точки полилинии
                //далее проход каждого сегмента полилинии (цикл for) от первой до последней точек в последовательности + следующая точка полилинии (в любом случае)
                //проход сегмента полилинии выполняется в цикле while - ищется пересечение со всеми ребрами треугольника пока не будет найдено,
                //далее переход к следующему треугольнику через пересекаемое ребро
                //!Особый случай - точка пересечения с треугольником совпадает с вершиной треугольника
                //!Особый случай - вершина полилинии лежит на ребре треугольника
                //!Особый случай - вершина полилинии совпала с вершиной треугольника
                //!Особый случай - сегмент полилинии совпал с ребром поверхности!!!
            }

            foreach (Node nn in node.NestedNodes)
            {
                TraversePolylines(nn);
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
                if (polyline!=null)
                {
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        Point3d pt = polyline.GetPoint3dAt(i)
                            //.TransformBy(polylineNesting.Transform)//точка переводится в мировую систему
                            ;
                        //Если последняя точка равна первой, то не добавлять ее
                        if (i != polyline.NumberOfVertices - 1 || !pt.Equals(Point3DCollection[0]))
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
