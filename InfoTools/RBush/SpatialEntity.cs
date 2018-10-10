using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using RBush;

namespace Civil3DInfoTools.RBush
{
    public class SpatialEntity : ISpatialData
    {
        public ObjectId ObjectId { get; set; }

        private Envelope _envelope;
        public ref readonly Envelope Envelope => ref _envelope;

        public SpatialEntity(ObjectId objectId)
        {
            if (objectId.IsNull)
            {
                throw new ArgumentException(nameof(objectId));
            }

            Entity ent = objectId.GetObject(OpenMode.ForRead) as Entity;

            if (ent == null)
            {
                throw new ArgumentException(nameof(objectId));
            }

            Extents3d? ext = ent.Bounds;

            if (ext == null)
            {
                throw new ArgumentException(nameof(objectId));
            }

            ObjectId = objectId;
            _envelope = GetEnvelope(ext.Value);
        }

        public static Envelope GetEnvelope(Extents3d extents3D)
        {
            return new Envelope
            (
                minX: extents3D.MinPoint.X,
                minY: extents3D.MinPoint.Y,
                maxX: extents3D.MaxPoint.X,
                maxY: extents3D.MaxPoint.Y
            );
        }
    }
}
