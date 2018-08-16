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

        public Envelope Envelope { get; set; }

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
            Envelope = GetEnvelope(ext.Value);
        }

        public static Envelope GetEnvelope(Extents3d extents3D)
        {
            return new Envelope()
            {
                MinX = extents3D.MinPoint.X,
                MinY = extents3D.MinPoint.Y,
                MaxX = extents3D.MaxPoint.X,
                MaxY = extents3D.MaxPoint.Y
            };
        }
    }
}
