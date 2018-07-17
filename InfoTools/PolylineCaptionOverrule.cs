using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools
{
    class PolylineCaptionOverrule : DrawableOverrule
    {
        public Dictionary<ObjectId, Point3d> position = null;

        /// <summary>
        /// Добавляет подписи к полилиниям, которые созданы из CSV (названия файлов CSV)
        /// </summary>
        /// <param name="drawable"></param>
        /// <param name="wd"></param>
        /// <returns></returns>
        public override bool WorldDraw(Drawable drawable, WorldDraw wd)
        {
            // draw the base class
            bool ret = base.WorldDraw(drawable, wd);
            try
            {
                Polyline3d poly = drawable as Polyline3d;
                if (poly != null)
                {
                    //Проверить наличие XData
                    string xdata = Utils.GetCaptionFromXData(poly);
                    if (xdata != null)
                    {

                        //Добавить отображение подписи возле первой точки полилинии
                        //poly.DowngradeOpen();
                        //poly.Database
                        Point3d position = Point3d.Origin;


                        Database db = poly.Database;
                        if (db != null)
                        {
                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {

                                ObjectId[] verts = poly.Cast<ObjectId>().ToArray();

                                PolylineVertex3d vtStart = tr.GetObject(verts[0], OpenMode.ForRead) as PolylineVertex3d;
                                position = vtStart.Position;

                                tr.Commit();
                            }
                        }
                        else
                        {
                            try
                            {
                                position = poly.StartPoint;
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                            {

                            }
                        }




                        Vector3d normal = Vector3d.ZAxis;
                        Vector3d direction = Vector3d.XAxis;

                        wd.Geometry.Text(position, normal, direction, 5, 1, 0, xdata);
                    }
                }
            }
            catch (Exception ex)
            {
                CommonException(ex, "Ошибка PolylineCaptionOverrule");
                ret = false;
            }

            // return the base
            return ret;
            //return base.WorldDraw(drawable, wd);
        }
    }
}
