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
    /// </summary>
    public class TriangleGraph
    {
        /// <summary>
        /// Ссылка на дерево полилиний
        /// </summary>
        public PolylineNesting PolylineNesting { get; private set; }

        /// <summary>
        /// Ссылка на треугольник поверхности
        /// </summary>
        public TinSurfaceTriangle TinSurfaceTriangle { get; private set; }

        /// <summary>
        /// Узлы графа
        /// </summary>
        private List<Node> Nodes { get; set; }

        /// <summary>
        /// Ребра графа
        /// </summary>
        private List<Edge> Edges { get; set; }

        public TriangleGraph(PolylineNesting polylineNesting, TinSurfaceTriangle triangle)
        {
            PolylineNesting = polylineNesting;
            TinSurfaceTriangle = triangle;

            //Внутренние вершины треугольника добавить в граф (узлы вершин треугольника)
            //Если вершина треугольника внутренняя, то в граф добавляются смежные с ней ребра (узлы сторон треугольника) и ребра от вершины к смежным ребрам
            TinSurfaceVertex[] verts = new TinSurfaceVertex[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };
            TinSurfaceEdge[] edges = new TinSurfaceEdge[] { triangle.Edge1, triangle.Edge2, triangle.Edge3 };
            bool[] triangSideNodesAdded = new bool[] { false, false, false };
            for (int i = 0; i < 3; i++)
            {
                TinSurfaceVertex v = verts[i];
                if (polylineNesting.InnerVerts.Contains(verts[i]))
                {
                    int e1Index = (i + 1) % 3;
                    int e2Index = (i - 1) % 3;

                    TinSurfaceEdge e1 = edges[e1Index];
                    TinSurfaceEdge e2 = edges[e2Index];

                    if (!triangSideNodesAdded[e1Index])
                    {
                        //TODO добавить узел ребра 1
                    }
                    if (!triangSideNodesAdded[e2Index])
                    {
                        //TODO добавить узел ребра 2
                    }

                    //TODO: Добавить ребра графа
                }
            }
        }


        //public SortedDictionary<double, >



        /// <summary>
        /// Узел графа - это либо ребро треугольника, либо его вершина
        /// </summary>
        abstract class Node
        {
            abstract public Point3d Location { get; set; }

            abstract public bool Visited { get; set; }
        }

        /// <summary>
        /// Вершина треугольника
        /// </summary>
        class VertexNode : Node
        {
            public TinSurfaceVertex TinSurfaceVertex { get; private set; }
            public override Point3d Location { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override bool Visited { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }

        /// <summary>
        /// Сторона треугольника
        /// </summary>
        class TriangSideNode : Node
        {
            public TinSurfaceEdge TinSurfaceEdge { get; private set; }
            public override Point3d Location { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override bool Visited { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }


        /// <summary>
        /// Ребро графа - это либо участок полилинии, проходящей через треугольник, либо участок ребра треугольника смежный его вершине
        /// </summary>
        abstract class Edge
        {
            public abstract Node Node1 { get; set; }

            public abstract Node Node2 { get; set; }
        }

        /// <summary>
        /// Участок полилинии
        /// </summary>
        class PolyEdge : Edge
        {
            public override Node Node1 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override Node Node2 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }

        /// <summary>
        /// Участок стороны треугольника от вершины
        /// </summary>
        class SideEdge : Edge
        {
            public override Node Node1 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override Node Node2 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }

    }
}
