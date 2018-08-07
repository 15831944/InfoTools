using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools
{
    /// <summary>
    /// Методы для вычерчивания различных вспомогательных примитивов для отображения промежуточных результатов работы
    /// </summary>
    public static class DisplayUtils
    {
        public static ObjectId Polyline(IEnumerable<Point2d> pts, bool closed, short colorIndex, Database db,
            Transaction tr = null, Editor ed = null)
        {
            ObjectId objectId = ObjectId.Null;

            Transaction usingTr = null;
            if (tr == null)
            {
                usingTr = db.TransactionManager.StartTransaction();
            }
            else
            {
                usingTr = tr;
            }

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            BlockTable bt = usingTr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
            BlockTableRecord ms = (BlockTableRecord)usingTr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            using (Polyline poly = new Polyline())
            {
                for (int i = 0; i < pts.Count(); i++)
                {
                    poly.AddVertexAt(i, pts.ElementAt(i), 0, 0, 0);
                }
                poly.Closed = closed;
                poly.ColorIndex = colorIndex;

                objectId = ms.AppendEntity(poly);
                usingTr.AddNewlyCreatedDBObject(poly, true);
            }
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////
            if (tr == null)
            {
                usingTr.Commit();
                usingTr.Dispose();
            }
                

            if (ed != null)
            {
                ed.Regen();
                ed.UpdateScreen();
            }

            return objectId;
        }
    }
}
