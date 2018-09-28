using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    public class EarClippingTriangulator
    {
        private List<Point3d> polygon;
        public List<List<Point3d>> Triangles { get; private set; } = new List<List<Point3d>>();

        private LinkedList<int> polygonLl = new LinkedList<int>();

        //Коллекции хранят ссылки на узлы списка polygonLl, чтобы можно было быстро найти соседние узлы
        private HashSet<LinkedListNode<int>> convexVerts = new HashSet<LinkedListNode<int>>(new LinkedListNodeComparer());
        private HashSet<LinkedListNode<int>> reflexVerts = new HashSet<LinkedListNode<int>>(new LinkedListNodeComparer());
        private HashSet<LinkedListNode<int>> ears = new HashSet<LinkedListNode<int>>(new LinkedListNodeComparer());

        public EarClippingTriangulator(List<Point3d> polygon)
        {
            this.polygon = polygon;
            int N = polygon.Count();
            for (int i = 0; i < polygon.Count; i++)
            {
                int index1 = i;
                int index2 = (i + 1) % N;
                int index3 = (i + 2) % N;

                LinkedListNode<int> currVertNode = polygonLl.AddLast(index2);

                Point2d p1 = Utils.Point2DBy3D(polygon[index1]);
                Point2d p2 = Utils.Point2DBy3D(polygon[index2]);
                Point2d p3 = Utils.Point2DBy3D(polygon[index3]);


                if (Utils.PolygonVertexIsConvex(p1, p2, p3, false))
                {
                    convexVerts.Add(currVertNode);
                }
                else if (Utils.PolygonVertexIsReflex(p1, p2, p3, false))
                {
                    reflexVerts.Add(currVertNode);
                }

            }
            //Какие из выпуклых вершин являются ear ()
            foreach (LinkedListNode<int> convVertNode in convexVerts)
            {
                if (IsEar(convVertNode))
                {
                    ears.Add(convVertNode);
                }

            }

            //Выполнить триангуляцию
            Triangulate();
        }

        /// <summary>
        /// Выпуклая вершина является Ear
        /// </summary>
        /// <param name="convVertNode"></param>
        /// <returns></returns>
        private bool IsEar(LinkedListNode<int> convVertNode)
        {
            int index1 = PreviousVertCycled(convVertNode).Value;
            int index2 = convVertNode.Value;
            int index3 = NextVertCycled(convVertNode).Value;

            Point2d p1 = Utils.Point2DBy3D(this.polygon[index1]);
            Point2d p2 = Utils.Point2DBy3D(this.polygon[index2]);
            Point2d p3 = Utils.Point2DBy3D(this.polygon[index3]);

            return IsEar(p1, p2, p3);
        }


        private bool IsEar(Point2d p1, Point2d p2, Point2d p3)
        {
            foreach (LinkedListNode<int> reflVertNode in reflexVerts)
            {
                Point2d reflVert = Utils.Point2DBy3D(this.polygon[reflVertNode.Value]);
                double lambda1, lambda2;
                if (!reflVert.IsEqualTo(p1) && !reflVert.IsEqualTo(p2) && !reflVert.IsEqualTo(p3)
                    //Если одна из точек точно совпала с другой, значит это дублирующиеся точки, появившиеся при объединении с отверстиями
                    //Эти точки не должны влиять отбор ear
                    && Utils.BarycentricCoordinates(reflVert, p1, p2, p3, out lambda1, out lambda2))
                {
                    //Вогнутая вершина попала в треугольник. Это не Ear
                    return false;
                }
            }
            return true;
        }

        private void Triangulate()
        {
            //Отнимать по одному ear, сохранять как треугольники
            while (polygonLl.Count > 3)
            {
                //Изъять первое ear
                LinkedListNode<int> earTip = ears.First();
                LinkedListNode<int> earVert1 = PreviousVertCycled(earTip);
                LinkedListNode<int> earVert2 = NextVertCycled(earTip);
                ears.Remove(earTip);//Удалить из коллекции ears
                convexVerts.Remove(earTip); //Удалить из коллекции выпуклых
                polygonLl.Remove(earTip);//Удалить из общего списка полигона

                //Получить треугольник
                int index1 = earVert1.Value;
                int index2 = earTip.Value;
                int index3 = earVert2.Value;
                Point3d p1 = this.polygon[index1];
                Point3d p2 = this.polygon[index2];
                Point3d p3 = this.polygon[index3];
                Triangles.Add(new List<Point3d>() { p1, p2, p3 });

                //Две соседние вершины могут поменять свой статус
                //Выпуклые вершины остаются выпуклыми
                //Ear может перестать быть ear
                //Невыпуклые вершины могут стать выпуклыми и, возможно, ear
                ChekVert(earVert1);
                ChekVert(earVert2);

                //TEST
                #region Отрисовать текущее состояние полигона
                //using (Transaction tr
                //    = SurfaceMeshByBoundaryCommand.DB.TransactionManager.StartTransaction())
                //using (Polyline pline = new Polyline())
                //{
                //    BlockTable bt = tr.GetObject(SurfaceMeshByBoundaryCommand.DB.BlockTableId, OpenMode.ForWrite) as BlockTable;
                //    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                //    pline.ColorIndex = 5;
                //    foreach (int ptInd in polygonLl)
                //    {
                //        Point3d pt = polygon[ptInd];
                //        pline.AddVertexAt(0, new Point2d(pt.X, pt.Y), 0, 0, 0);
                //    }
                //    pline.Closed = true;

                //    ms.AppendEntity(pline);
                //    tr.AddNewlyCreatedDBObject(pline, true);

                //    tr.Commit();
                //    pline.Draw();
                //    SurfaceMeshByBoundaryCommand.ED.Regen();
                //    SurfaceMeshByBoundaryCommand.ED.UpdateScreen();
                //}
                #endregion
                //TEST
            }

            //Добавить последний треугольник
            List<Point3d> lastTriangle = new List<Point3d>();
            foreach (int i in polygonLl)
            {
                lastTriangle.Add(polygon[i]);
            }
            Triangles.Add(lastTriangle);


        }

        /// <summary>
        /// Выпуклые вершины остаются выпуклыми, но могут стать ear
        /// Ear может перестать быть ear
        /// Невыпуклые вершины могут стать выпуклыми и, возможно, ear
        /// </summary>
        /// <param name="vertToCheck"></param>
        private void ChekVert(LinkedListNode<int> vertToCheck)
        {
            LinkedListNode<int> adjacent1 = PreviousVertCycled(vertToCheck);
            LinkedListNode<int> adjacent2 = NextVertCycled(vertToCheck);

            int index1 = adjacent1.Value;
            int index2 = vertToCheck.Value;
            int index3 = adjacent2.Value;

            Point2d p1 = Utils.Point2DBy3D(this.polygon[index1]);
            Point2d p2 = Utils.Point2DBy3D(this.polygon[index2]);
            Point2d p3 = Utils.Point2DBy3D(this.polygon[index3]);



            if (!convexVerts.Contains(vertToCheck))//Если эта вершина не выпуклая
            {
                //Невыпуклые вершины могут стать выпуклыми и, возможно, ear
                if (Utils.PolygonVertexIsConvex(p1, p2, p3, false))
                {
                    reflexVerts.Remove(vertToCheck);
                    convexVerts.Add(vertToCheck);
                    if (IsEar(p1, p2, p3))
                    {
                        ears.Add(vertToCheck);
                    }

                }

            }
            else /*if (ears.Contains(vertToCheck))*/
            {
                //Ear может перестать быть ear
                if (IsEar(p1, p2, p3))
                {
                    ears.Add(vertToCheck);
                }
                else
                {
                    ears.Remove(vertToCheck);
                }
            }

        }

        /// <summary>
        /// Для использования 2-связного списка как циклического списка
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private LinkedListNode<int> NextVertCycled(LinkedListNode<int> current)
        {
            LinkedListNode<int> next = current.Next;
            if (next != null)
            {
                return next;
            }
            else
            {
                return polygonLl.First;
            }
        }
        /// <summary>
        /// Для использования 2-связного списка как циклического списка
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private LinkedListNode<int> PreviousVertCycled(LinkedListNode<int> current)
        {
            LinkedListNode<int> next = current.Previous;
            if (next != null)
            {
                return next;
            }
            else
            {
                return polygonLl.Last;
            }
        }

        private class LinkedListNodeComparer : IEqualityComparer<LinkedListNode<int>>
        {
            public bool Equals(LinkedListNode<int> x, LinkedListNode<int> y)
            {
                return x.Value == y.Value;
            }

            public int GetHashCode(LinkedListNode<int> obj)
            {
                return obj.Value.GetHashCode();
            }
        }
    }



}
