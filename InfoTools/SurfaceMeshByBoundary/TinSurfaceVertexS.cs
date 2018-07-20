//using Autodesk.Civil.DatabaseServices;
//using RBush;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Civil3DInfoTools.SurfaceMeshByBoundary
//{
//    /// <summary>
//    /// Обертка для вершины поверхности TIN для хранения в R-tree
//    /// </summary>
//    public class TinSurfaceVertexS : ISpatialData
//    {
//        public TinSurfaceVertex TinSurfaceVertex { get; private set; }
//        public Envelope Envelope { get; private set; }

//        public TinSurfaceVertexS(TinSurfaceVertex tinSurfaceVertex)
//        {
//            TinSurfaceVertex = tinSurfaceVertex;

//            Envelope = new Envelope
//            {
//                MinX = tinSurfaceVertex.Location.X,
//                MinY = tinSurfaceVertex.Location.Y,
//                MaxX = tinSurfaceVertex.Location.X,
//                MaxY = tinSurfaceVertex.Location.Y
//            };
//        }
//    }
//}
