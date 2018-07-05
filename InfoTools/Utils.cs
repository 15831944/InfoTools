using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            return pattern;
        }

    }
}
