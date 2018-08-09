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


        public PolylinePt(PolylineNesting.Node node, Point2d pt)
        {

            Node = node;
            Point2D = pt;
            //Расчет параметра
            Point3d nearestPt = node.Polyline.GetClosestPointTo(new Point3d(pt.X, pt.Y, 0), false);//иначе может ошибка при расчете параметра
            Parameter = node.Polyline.GetParameterAtPoint(nearestPt);
            VertNumber = Convert.ToInt32(Math.Floor(Parameter));
        }

        /// <summary>
        /// Расчет координаты Z по барицентрическим координатам
        /// </summary>
        public void CalculateZ()
        {
            if (TinSurfaceVertex != null)
            {
                Z = TinSurfaceVertex.Location.Z;
                return;
            }

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

        public int CompareTo(PolylinePt other)
        {
            return Parameter.CompareTo(other.Parameter);
        }
    }
}
