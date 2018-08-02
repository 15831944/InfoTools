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
        /// Точки полилиний, попавшие в этоттреугольник
        /// </summary>
        private Dictionary<PolylineNesting.Node, SortedSet<PolylinePt>> pts
            = new Dictionary<PolylineNesting.Node, SortedSet<PolylinePt>>();


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
        }


        /// <summary>
        /// Добавить узлы внутрненних вершин в граф
        /// </summary>
        public void InnerVerts()
        {
            //Внутренние вершины треугольника добавить в граф (узлы вершин треугольника)
            //Если вершина треугольника внутренняя, то в граф добавляются смежные с ней ребра (узлы сторон треугольника) и ребра от вершины к смежным ребрам
            TinSurfaceVertex[] verts = new TinSurfaceVertex[] { TinSurfaceTriangle.Vertex1, TinSurfaceTriangle.Vertex2, TinSurfaceTriangle.Vertex3 };
            TinSurfaceEdge[] edges = new TinSurfaceEdge[] { TinSurfaceTriangle.Edge1, TinSurfaceTriangle.Edge2, TinSurfaceTriangle.Edge3 };
            bool[] triangSideNodesAdded = new bool[] { false, false, false };
            for (int i = 0; i < 3; i++)
            {
                TinSurfaceVertex v = verts[i];
                if (PolylineNesting.InnerVerts.Contains(verts[i]))
                {
                    int e1Index = (i + 1) % 3;
                    int e2Index = i == 0 ? 2 : i - 1;

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




        public List<LinkedList<PolylinePt>> PolylineParts { get; set; } = new List<LinkedList<PolylinePt>>();
        /// <summary>
        /// Добавить узлы участков поллиний в граф
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

                    //Нужно проверить параметр последней точки последей последовательности (что он находится в пределах 1.00 от конечного параметра полилинии)
                    if (seq2.Last().Parameter >= node.Polyline.EndParam - 1)
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
                Point2d vert1 = new Point2d(TinSurfaceTriangle.Vertex1.Location.X, TinSurfaceTriangle.Vertex1.Location.Y);
                Point2d vert2 = new Point2d(TinSurfaceTriangle.Vertex2.Location.X, TinSurfaceTriangle.Vertex2.Location.Y);
                Point2d vert3 = new Point2d(TinSurfaceTriangle.Vertex3.Location.X, TinSurfaceTriangle.Vertex3.Location.Y);
                Vector2d edge1Vector = vert2- vert1;
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

                PolylineParts.AddRange(sequences);

                //Создать ребра и узлы графа

            }


        }


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


        public override int GetHashCode()
        {
            return TinSurfaceTriangle.GetHashCode();
        }

    }
}
