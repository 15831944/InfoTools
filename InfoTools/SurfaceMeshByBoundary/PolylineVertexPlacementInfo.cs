using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{
    /// <summary>
    /// Информация о расположении вершины полилинии отностиельно элементов поверхности TIN
    /// Она может быть расположена внутри треугольника, на ребре или на вершине поверхности
    /// </summary>
    public class PolylineVertexPlacementInfo : IComparable<PolylineVertexPlacementInfo>
    {
        public int VertNumber { get; set; }

        public TinSurfaceTriangle TinSurfaceTriangle { get; set; }

        public TinSurfaceEdge TinSurfaceEdge { get; set; }

        public TinSurfaceVertex TinSurfaceVertex { get; set; }


        public int CompareTo(PolylineVertexPlacementInfo other)
        {
            return this.VertNumber.CompareTo(other.VertNumber);
            
        }
    }
}
