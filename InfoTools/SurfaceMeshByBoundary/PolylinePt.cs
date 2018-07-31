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

        public Point2d Point2D { get; set; }

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

        public int CompareTo(PolylinePt other)
        {
            return Parameter.CompareTo(other.Parameter);
        }
    }
}
