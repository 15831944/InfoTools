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
    /// Информация о расположении точки полилинии отностиельно элементов поверхности TIN
    /// Она может быть расположена внутри треугольника, на ребре или на вершине поверхности
    /// </summary>
    public class PolylinePt : IComparable<PolylinePt>
    {
        /// <summary>
        /// Номер вершины, с которой совпадает точка, или номер ближайшей меньшей вершины если точка лежит на ребре 
        /// </summary>
        public int VertNumber { get; set; }

        /// <summary>
        /// Положение точки на плане
        /// </summary>
        public Point2d Point2D { get; set; }

        /// <summary>
        /// Отметка поверхности
        /// </summary>
        public double Z { get; set; }

        public PolylineNesting.Node Node { get; set; }

        public double Parameter { get; set; }

        public TinSurfaceTriangle TinSurfaceTriangle { get; set; }

        public TinSurfaceEdge TinSurfaceEdge { get; set; }

        public TinSurfaceVertex TinSurfaceVertex { get; set; }

        private PolylineNesting.BorderPart borderPart = null;

        public PolylinePt(PolylineNesting.Node node, Point2d pt)
        {
            Node = node;
            Point2D = pt;
            //Расчет параметра
            Point3d ptOnPoly = node.Polyline.GetClosestPointTo(new Point3d(pt.X, pt.Y, 0), false);//ptOnPoly по сути всегда равна pt. Однако без данной строки может быть ошибка при расчете параметра
            Parameter = node.Polyline.GetParameterAtPoint(ptOnPoly);
            VertNumber = Convert.ToInt32(Math.Floor(Parameter));

            //Сохранить ссылку на созданный объект в общую коллекцию
            Node.PolylineNesting.PolylinePts.Add(this);
        }

        /// <summary>
        /// Расчет координаты Z поверхности по барицентрическим координатам
        /// </summary>
        public void CalculateZ()
        {
            if (TinSurfaceVertex != null)
            {
                Z = TinSurfaceVertex.Location.Z;
            }
            else
            {
                TinSurfaceTriangle triangle = null;
                if (TinSurfaceEdge != null)
                {
                    triangle = TinSurfaceEdge.Triangle1 != null ? TinSurfaceEdge.Triangle1 : TinSurfaceEdge.Triangle2;
                }
                else
                {
                    triangle = TinSurfaceTriangle;
                }
                double lambda1;
                double lambda2;
                Utils.BarycentricCoordinates(Point2D, triangle, out lambda1, out lambda2);

                double lambda3 = 1 - lambda1 - lambda2;

                Z = lambda1 * triangle.Vertex1.Location.Z + lambda2 * triangle.Vertex2.Location.Z + lambda3 * triangle.Vertex3.Location.Z;
            }
        }



        /// <summary>
        /// Добавление точки в список точек участка
        /// </summary>
        public void AddToPolylinePart()
        {
            borderPart = Node.BorderParts.Find(t =>
               (t.StartParam < t.EndParam && t.StartParam <= Parameter && t.EndParam >= Parameter)
               || (t.StartParam > t.EndParam && (t.StartParam <= Parameter || t.EndParam >= Parameter)));

            if (borderPart != null)
            {
                //Добавить эту точку в набор 3d точек этой полилинии
                borderPart.PointsOrderedByParameter[Parameter] = this;
            }
        }

        /// <summary>
        /// Этот метод должен выстраивать точки полилинии в правильном порядке от начала к концу
        /// Если полилиния разделена на несколько участков, то учитывается ситуация, когда на участке происходит переход через 0
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PolylinePt other)
        {
            //if (partStart > 0 && partEnd > 0 && partStart > partEnd)//Если на участке происходит переход серез ноль
            //{

            //}
            //else
            //{
            return Parameter.CompareTo(other.Parameter);
            //}
        }

        //public override int GetHashCode()
        //{
        //    return Point2D.GetHashCode();
        //}
    }
}
