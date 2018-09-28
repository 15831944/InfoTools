using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{



    /// <summary>
    /// https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
    /// </summary>
    public static class PolygonWithHoles
    {
        /// <summary>
        /// Определить, какие отверстия относятся к каким полигонам
        /// При этом сами полигоны могут быть вложены друг в друга
        /// </summary>
        /// <param name="polygons"></param>
        /// <param name="holes"></param>
        /// <returns></returns>
        public static List<PolygonWithNested> ResolveHoles
            (List<List<Point3d>> polygons, List<List<Point3d>> holes)
        {
            //Сначала определить вложенность полигонов
            PolygonTree tree = new PolygonTree();
            int i = 0;
            foreach (List<Point3d> p in polygons)
            {
                tree.InsertOuterPolygon(p);
                i++;
            }

            //Для каждого отверстия определить полигон, в который оно вложено, находящийся на наинизшем уровне вложенности
            foreach (List<Point3d> hole in holes)
            {
                tree.InsertHole(hole);
            }

            return tree.GetPolygonsWithHoles();
        }

        private class PolygonTree
        {
            public PolygonWithNested Root { get; private set; }

            public PolygonTree()
            {
                Root = new PolygonWithNested(null);
            }

            public void InsertOuterPolygon(List<Point3d> polygon)
            {
                InsertOuterPolygon(Root, new PolygonWithNested(polygon));
            }

            private void InsertOuterPolygon(PolygonWithNested node, PolygonWithNested insertingNode)
            {
                bool isNested = false;
                //Проверить вложена ли добавляемая полилиния в один из дочерних узлов
                foreach (PolygonWithNested nn in node.NestedOuterPolygons)
                {
                    if (nn.IsNested(insertingNode))
                    {
                        //рекурсия
                        InsertOuterPolygon(nn, insertingNode);
                        isNested = true;
                        break;
                    }
                }

                if (!isNested)
                {
                    //Если полилиния не вложена в дочерние узлы, то проверить не вложены ли дочерние узлы в добавляемую полилинию
                    for (int i = 0; i < node.NestedOuterPolygons.Count;)
                    {
                        PolygonWithNested nn = node.NestedOuterPolygons[i];
                        if (insertingNode.IsNested(nn))
                        {
                            //Если вложена, то убрать из node.NestedPolygons и добавить в insertingNode.NestedPolygons
                            node.NestedOuterPolygons.Remove(nn);
                            insertingNode.NestedOuterPolygons.Add(nn);
                        }
                        else
                        {
                            i++;
                        }
                    }

                    //Добавить insertingNode в node.NestedNodes
                    node.NestedOuterPolygons.Add(insertingNode);
                }
            }

            //Найти полигон, в который вложено отверстие, находящийся на самом низшем уровне вложенности
            public void InsertHole(List<Point3d> hole)
            {
                InsertHole(Root, hole);
            }

            private void InsertHole(PolygonWithNested node, List<Point3d> insertingHole)
            {
                bool isNested = false;
                foreach (PolygonWithNested nn in node.NestedOuterPolygons)
                {
                    if (nn.IsNested(insertingHole))
                    {
                        //рекурсия
                        InsertHole(nn, insertingHole);
                        isNested = true;
                        break;
                    }
                }

                if (!isNested)
                {
                    //Если не вложена в дочерние узлы, значит вложена в текущий
                    node.Holes.Add(insertingHole);
                }
            }

            public List<PolygonWithNested> GetPolygonsWithHoles()
            {
                List<PolygonWithNested> polygonWithHoles = new List<PolygonWithNested>();
                Root.CollectPolygonsWithHoles(polygonWithHoles);
                return polygonWithHoles;
            }

        }
    }

    public class PolygonWithNested
    {
        public List<Point3d> Polygon { get; set; }

        public List<PolygonWithNested> NestedOuterPolygons { get; set; } = new List<PolygonWithNested>();

        public List<List<Point3d>> Holes { get; private set; } = new List<List<Point3d>>();

        public PolygonWithNested(List<Point3d> polygon)
        {
            Polygon = polygon;
        }

        private Point2d GetPt2DAt(int i)
        {
            Point3d pt3d = Polygon[i];
            return new Point2d(pt3d.X, pt3d.Y);
        }

        /// <summary>
        /// Переданный узел вложен в этот
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool IsNested(PolygonWithNested node)
        {
            return Utils.PointIsInsidePolylineWindingNumber(node.Polygon[0], this.Polygon);
        }

        public bool IsNested(List<Point3d> hole)
        {
            return Utils.PointIsInsidePolylineWindingNumber(hole[0], this.Polygon);
        }


        public void CollectPolygonsWithHoles(List<PolygonWithNested> polygonWithHoles)
        {
            if (Holes.Count > 0)
            {
                polygonWithHoles.Add(this);
            }
            foreach (PolygonWithNested nn in NestedOuterPolygons)
            {
                nn.CollectPolygonsWithHoles(polygonWithHoles);
            }
        }


        public void MakeSimple()
        {
            //Настроить порядок обхода полигонов
            SetOppositeVertexOrdering();
            //Отсортировать отверстия по координате X самой правой точки в полигоне отвестия
            SortedSet<HoleInfo> holesByX = new SortedSet<HoleInfo>();
            foreach (List<Point3d> hole in Holes)
            {
                holesByX.Add(new HoleInfo(hole));
            }


            foreach (HoleInfo hi in holesByX)
            {
                //Найти видимую точку
                int visiblePtIndex = GetVisiblePt(hi);
                //Объединить полигоны
                AppendHoleToPolygon(hi, hi.MaxXIndex, visiblePtIndex);
            }


        }

        private int GetVisiblePt(HoleInfo hi)
        {
            //Обозначения точек согласно https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
            Point2d M = hi.MaxXPt;
            Point2d I = Point2d.Origin;
            int[] edge = GetEdgeOnHorizontalRay(M, out I);
            if (edge == null)
            {
                throw new Exception("Ошибка при определении видимой точки. Не найдено ребро, пересекающееся с горизонтальным лучем");
            }
            int vertexOnEdgeIndex = Polygon[edge[0]].X > Polygon[edge[1]].X ? edge[0] : edge[1];
            Point2d P = GetPt2DAt(vertexOnEdgeIndex);

            if (P.IsEqualTo(I))
            {
                //Если точки совпали
                return vertexOnEdgeIndex;
            }

            List<int> reflexPtsIndices = GetReflexVertsInTriangle(M, I, P);
            if (reflexPtsIndices.Count > 0)
                return GetVisiblePtFromReflexPts(M, reflexPtsIndices);
            else
                return vertexOnEdgeIndex;
        }


        /// <summary>
        /// Задание противоположного направления обхода для отверстий
        /// Внешняя полилиния против часовой
        /// Отверстия по часовой
        /// </summary>
        private void SetOppositeVertexOrdering()
        {
            if (Utils.DirectionIsClockwise(Polygon))
            {
                Polygon.Reverse();
            }
            foreach (List<Point3d> hole in Holes)
            {
                if (!Utils.DirectionIsClockwise(hole))
                {
                    hole.Reverse();
                }
            }

        }

        /// <summary>
        /// Найти ближайшее ребро, которое пересекает горизонтальный луч, направленный вправо из точки
        /// </summary>
        /// <param name="M"></param>
        /// <param name="ptOnEdge"></param>
        /// <returns></returns>
        private int[] GetEdgeOnHorizontalRay(Point2d M, out Point2d ptOnEdge)
        {
            int[] edge = null;
            Point2d? _ptOnEdge = null;
            double minDistToM = double.MaxValue;
            int numVert = Polygon.Count;

            Action<int[], Point2d, Point2d> onIntersectionFind = (currEdge, vert1, vert2) =>
            {
                Point2d currPtOnEdge = ptOnHorizontalRay(M.Y, vert1, vert2);
                double currDistToM = currPtOnEdge.GetDistanceTo(M);
                if (currDistToM < minDistToM)
                {
                    minDistToM = currDistToM;
                    edge = currEdge;
                    _ptOnEdge = currPtOnEdge;
                }
            };


            for (int i = 0; i < numVert; i++)
            {
                int[] currEdge = new int[] { i, (i + 1) % numVert };
                Point2d vert1 = GetPt2DAt(currEdge[0]);
                Point2d vert2 = GetPt2DAt(currEdge[1]);

                double isLeftVal = double.MinValue;
                if (vert1.Y <= M.Y)
                {
                    if (vert2.Y > M.Y)
                    {
                        //Переход снизу вверх
                        isLeftVal = Utils.IsLeft(vert1, vert2, M);
                        if (isLeftVal > 0)
                        {
                            onIntersectionFind.Invoke(currEdge, vert1, vert2);
                        }

                    }
                }
                else if (vert2.Y <= M.Y)
                {
                    //Переход сверху вниз
                    isLeftVal = Utils.IsLeft(vert1, vert2, M);
                    if (isLeftVal < 0)
                    {
                        onIntersectionFind.Invoke(currEdge, vert1, vert2);
                    }

                }

            }
            if (_ptOnEdge.Value != null)
                ptOnEdge = _ptOnEdge.Value;
            else
                ptOnEdge = Point2d.Origin;
            return edge;
        }


        /// <summary>
        /// Точка пересечения прямой и горизонтальной линии
        /// </summary>
        /// <param name="y"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        private Point2d ptOnHorizontalRay(double y, Point2d p1, Point2d p2)
        {
            double den = (p2.Y - p1.Y);
            //Уравнение x от y
            if (den != 0)
            {
                double x = (y - p1.Y) * (p2.X - p1.X) / den + p1.X;
                return new Point2d(x, y);
            }
            else
            {
                return p1.X > p2.X ? p1 : p2;
            }

        }

        /// <summary>
        /// Находит все вогнутые вершины попадающие в заданный треугольник
        /// </summary>
        /// <param name="trp1"></param>
        /// <param name="trp2"></param>
        /// <param name="trp3"></param>
        /// <returns></returns>
        private List<int> GetReflexVertsInTriangle(Point2d trp1, Point2d trp2, Point2d trp3)
        {
            List<int> reflexIndexes = new List<int>();
            int N = Polygon.Count;
            for (int i = 0; i < N; i++)
            {
                int index1 = i;
                int index2 = (i + 1) % N;
                int index3 = (i + 2) % N;

                Point2d pt1 = GetPt2DAt(index1);
                Point2d pt2 = GetPt2DAt(index2);
                Point2d pt3 = GetPt2DAt(index3);

                //double isLeft = Utils.IsLeft(pt1, pt2, pt3);
                if (Utils.PolygonVertexIsReflex(pt1, pt2, pt3, false) /*isLeft < 0*/)//Условие вогнутости вершины для полигона с направлением обхода против часовой стрелки
                {
                    //pt2 - вогнутая вершина
                    double lambda1, lambda2;
                    if (Utils.BarycentricCoordinates(pt2, trp1, trp2, trp3, out lambda1, out lambda2))
                    {
                        reflexIndexes.Add(index2);
                    }

                }
            }


            return reflexIndexes;
        }

        /// <summary>
        /// Если вогнутые вершины попали в треугольник MIP
        /// выбирает видимую точку
        /// </summary>
        /// <param name="M"></param>
        /// <param name="candidateReflexPoints"></param>
        /// <returns></returns>
        private int GetVisiblePtFromReflexPts(Point2d M, List<int> candidateReflexPoints)
        {
            int ptIndex = -1;
            double angleToHor = double.MaxValue;
            double distToM = double.MaxValue;
            foreach (int i in candidateReflexPoints)
            {
                Point2d pt = GetPt2DAt(i);
                Vector2d currVector = pt - M;
                double currAngleToHor = currVector.GetAngleTo(Vector2d.XAxis);
                double currDistToM = pt.GetDistanceTo(M);
                if ((currAngleToHor < angleToHor) || (currAngleToHor == angleToHor && currDistToM < distToM))
                {
                    ptIndex = i;
                    angleToHor = currAngleToHor;
                    distToM = currDistToM;
                }
            }
            return ptIndex;
        }

        /// <summary>
        /// Присоединяет отверстие к полигону
        /// </summary>
        /// <param name="hi"></param>
        /// <param name="ptInHole"></param>
        /// <param name="ptInPolygon"></param>
        private void AppendHoleToPolygon(HoleInfo hi, int ptInHole, int ptInPolygon)
        {
            List<Point3d> newPlygon = new List<Point3d>();
            for (int i = 0; i <= ptInPolygon; i++)//Добавить все точки полигона до точки присоединения
            {
                newPlygon.Add(Polygon[i]);
            }

            int currHoleIndex = ptInHole;
            do
            {
                newPlygon.Add(hi.Hole[currHoleIndex]);
                currHoleIndex = (currHoleIndex + 1) % hi.Hole.Count;
            }
            while (currHoleIndex != ptInHole);//Добавлять точки до полного обхода отверстия

            newPlygon.Add(hi.Hole[ptInHole]);//Еще раз добавить эту же точку отверстия

            for (int i = ptInPolygon; i < Polygon.Count; i++)//Добавить оставшиеся точки полигона
            {
                newPlygon.Add(Polygon[i]);
            }

            Polygon = newPlygon;

        }

        private class HoleInfo : IComparable<HoleInfo>
        {
            public int MaxXIndex { get; set; }

            public List<Point3d> Hole { get; set; }

            public Point2d MaxXPt
            {
                get
                {
                    Point3d pt3d = Hole[MaxXIndex];
                    return new Point2d(pt3d.X, pt3d.Y);
                }
            }
            public HoleInfo(List<Point3d> hole)
            {
                Point3d ptMaxX = hole[0];
                MaxXIndex = 0;
                for (int i = 1; i < hole.Count; i++)
                {
                    Point3d pt = hole[i];
                    if (pt.X > ptMaxX.X)
                    {
                        ptMaxX = pt;
                        MaxXIndex = i;
                    }
                }
                Hole = hole;
            }

            public int CompareTo(HoleInfo other)
            {
                return MaxXPt.X.CompareTo(other.MaxXPt.X) * (-1);
            }
        }


    }


}
