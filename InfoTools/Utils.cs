using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;

namespace Civil3DInfoTools
{
    public static class Utils
    {

        public static void ErrorToCommandLine(Editor ed, string comment, Exception ex = null)
        {
            ed.WriteMessage("\n~~~~~~~|Ошибка InfoTools|~~~~~~~");
            ed.WriteMessage("\n" + comment);
            if (ex != null)
            {
                string errMessage = ex.Message;
                string stackTrace = ex.StackTrace;
                ed.WriteMessage("\nMessage: {0}\nStackTrace: {1}", errMessage, stackTrace);
            }

            ed.WriteMessage("\n~~~~~~~|Ошибка InfoTools|~~~~~~~");
        }

        public static DBObject GetFirstDBObject(DBObjectCollection coll)
        {
            foreach (DBObject dbo in coll)
            {
                return dbo;
            }
            return null;
        }


        public static ObjectId GetContinuousLinetype(Database db)
        {


            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LinetypeTable lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                foreach (ObjectId ltrId in lt)
                {
                    LinetypeTableRecord ltr = (LinetypeTableRecord)tr.GetObject(ltrId, OpenMode.ForRead);
                    List<double> pattern = GetLinePattern(ltr);
                    if (pattern.Count == 1 && pattern[0] > 0)
                    {
                        return ltrId;
                    }
                }

                tr.Commit();
            }
            return ObjectId.Null;

        }


        public static List<double> GetLinePattern(LinetypeTableRecord ltype, double ltScale = 1)
        {
            List<double> pattern = new List<double>();
            for (int i = 0; i < ltype.NumDashes; i++)
            {
                //TODO: Учесть возможность вставки подряд двух пробелов или двух штрихов
                //(если это вообще возможно сделать)
                double dash = ltype.DashLengthAt(i);
                if (dash != 0)
                {
                    pattern.Add(dash * ltScale);//домножить на масштаб типа линии
                }

            }


            if (pattern.Count == 0)//если данные не получены, то выдать патерн для сплошной линии 
            {
                pattern.Add(1);
            }

            return pattern;
        }

        public static ObjectId CreateLayerIfNotExists(string layerName, Database db, Transaction tr,
            LayerTableRecord layerSample = null, short colorIndex = -1, LineWeight lineWeight = LineWeight.ByLayer)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            ObjectId layerId = ObjectId.Null;
            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltrNew = null;
                if (layerSample != null)
                {
                    ltrNew = (LayerTableRecord)layerSample.Clone();
                }
                else
                {
                    ltrNew = new LayerTableRecord();
                    if (colorIndex != -1)
                    {
                        Color color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                        ltrNew.Color = color;
                    }
                    if (lineWeight!= LineWeight.ByLayer)
                    {
                        ltrNew.LineWeight = lineWeight;
                    }
                    
                }

                ltrNew.Name = layerName;
                layerId = lt.Add(ltrNew);
                tr.AddNewlyCreatedDBObject(ltrNew, true);
            }
            else
            {
                layerId = lt[layerName];
            }

            return layerId;
        }


        /// <summary>
        /// http://geomalgorithms.com/a01-_area.html
        /// test if a point is Left|On|Right of an infinite 2D line.
        /// Input:  three points P0, P1, and P2
        /// Return:
        /// >0 for P2 left of the line through P0 to P1
        /// =0 for P2 on the line
        /// <0 for P2 right of the line
        /// </summary>
        /// <param name="pt0"></param>
        /// <param name="pt1"></param>
        /// <param name="pt2"></param>
        /// <returns></returns>
        public static double IsLeft(Point2d pt0, Point2d pt1, Point3d pt2)
        {
            return ((pt1.X - pt0.X) * (pt2.Y - pt0.Y) - (pt2.X - pt0.X) * (pt1.Y - pt0.Y));
        }

    }
}
