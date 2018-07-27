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

        /// <summary>
        /// Убрать все недопустимые символы из имени слоя
        /// </summary>
        /// <param name="layername"></param>
        /// <returns></returns>
        public static string GetSafeLayername(string layername)
        {
            return string.Join("_", layername.Split(new char[] { '<', '>', '/', '\\', '"', '"', ':', ';', '?', '*', '|', ',', '=', '`' }));
        }

        public static ObjectId CreateLayerIfNotExists(string layerName, Database db, Transaction tr,
            LayerTableRecord layerSample = null, Color color = null,/*short colorIndex = -1,*/ LineWeight lineWeight = LineWeight.ByLayer)
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
                    if (color != null/*colorIndex != -1*/)
                    {
                        //Color color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                        ltrNew.Color = color;
                    }
                    if (lineWeight != LineWeight.ByLayer)
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
        public static double IsLeft(Point2d pt0, Point2d pt1, Point2d pt2)
        {
            return ((pt1.X - pt0.X) * (pt2.Y - pt0.Y) - (pt2.X - pt0.X) * (pt1.Y - pt0.Y));
        }
        public static double IsLeft(Point2d pt0, Point2d pt1, Point3d pt2)
        {
            return ((pt1.X - pt0.X) * (pt2.Y - pt0.Y) - (pt2.X - pt0.X) * (pt1.Y - pt0.Y));
        }
        public static double IsLeft(Point3d pt0, Point3d pt1, Point3d pt2)
        {
            return ((pt1.X - pt0.X) * (pt2.Y - pt0.Y) - (pt2.X - pt0.X) * (pt1.Y - pt0.Y));
        }

        /// <summary>
        /// Регистрация приложения для записи расширенных данных есл еще не рарегестрировано
        /// </summary>
        /// <param name="db"></param>
        /// <param name="tr"></param>
        public static void RegisterApp(Database db, Transaction tr)
        {
            //Таблица приложений
            RegAppTable regTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);

            //Создать имя приложения в таблице если еще нет
            if (!regTable.Has(Constants.AppName))
            {
                regTable.UpgradeOpen();

                RegAppTableRecord app = new RegAppTableRecord();
                app.Name = Constants.AppName;
                regTable.Add(app);
                tr.AddNewlyCreatedDBObject(app, true);
            }
        }

        /// <summary>
        /// Получить расширенные данные подписи объекта
        /// Предполагается, что в самом объекте будет храниться только одна строковая ячейка расширенных данных
        /// Если потребуется хранение большего объема информации необходимо использовать CreateExtensionDictionary и Xrecord
        /// (http://forums.augi.com/showthread.php?107513-C-sample-attaching-XRecord-to-a-graphical-object)
        /// </summary>
        /// <param name="ent"></param>
        /// <returns></returns>
        public static string GetCaptionFromXData(Entity ent)
        {
            string returnVal = null;
            ResultBuffer buffer = ent.GetXDataForApplication(Constants.AppName);
            if (buffer != null)
            {
                List<TypedValue> xdataList = new List<TypedValue>(buffer.AsArray());
                List<TypedValue> xdataStringList = xdataList.FindAll(x => x.TypeCode == 1000);
                if (xdataStringList.Count > 0)
                {
                    returnVal = (string)xdataStringList[0].Value;
                }
            }
            return returnVal;
        }


        /// <summary>
        /// Полилиния пересекает сама себя
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static bool PolylineIsSelfIntersecting(Polyline poly)
        {
            Dictionary<Point3d, int?> polyPts = new Dictionary<Point3d, int?>();
            int vertCount = poly.NumberOfVertices;
            for (int i = 0; i < vertCount; i++)
            {
                Point3d pt3d = poly.GetPoint3dAt(i);
                //Если точки повторяются, то есть самопересечение
                //Но допускается повторение первой и последней точек
                int? existPtNum = null;
                polyPts.TryGetValue(pt3d, out existPtNum);
                if (existPtNum == null//Такой точки не было
                    || (i == vertCount - 1 && existPtNum == 0))//Такая тока была, но это замыкание, а не самопересечение
                {
                    polyPts[pt3d] = i;
                }
                else
                    return true;
            }
            //Использование метода IntersectWith
            Point3dCollection intersectionPts = new Point3dCollection();
            poly.IntersectWith(poly, Intersect.OnBothOperands, intersectionPts, IntPtr.Zero, IntPtr.Zero);

            foreach (Point3d intersectionPt in intersectionPts)
            {
                if (!polyPts.ContainsKey(intersectionPt))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Определение находится ли точка внутри полилинии по методу winding number algorithm
        /// Не учитывается кривизна сегментов
        /// Основа взята отсюда - https://forums.autodesk.com/t5/net/check-if-the-point-inside-or-outside-the-polyline/m-p/5483878#M43117
        /// Подробности алгоритма - http://geomalgorithms.com/a03-_inclusion.html
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static bool PointIsInsidePolylineWindingNumber(Point3d pt, Point3dCollection pointsOfPolyline)
        {

            int numVert = pointsOfPolyline.Count;

            int wn = 0;
            for (int i = 0; i < numVert; i++)
            {
                Point3d vert1 = pointsOfPolyline[i];
                Point3d vert2 = pointsOfPolyline[(i + 1) % numVert];

                double isLeftVal = double.MinValue;
                //Проверка точек сегмента на переход через координату Y проверяемой точки
                if (vert1.Y <= pt.Y)
                {
                    if (vert2.Y > pt.Y)
                    {
                        //Переход снизу вверх
                        isLeftVal = IsLeft(vert1, vert2, pt);
                        if (isLeftVal > 0)//В случае перехода снизу вверх учитываем только те случаи, когда точка расположена слева от сегмента
                        {
                            wn++;
                        }
                        if (isLeftVal == 0)//Если оказывается, что точка находится на линии, то считать, что точка попала в область полилинии
                        {
                            return true;
                        }
                    }
                }
                else if (vert2.Y <= pt.Y)
                {
                    //Переход сверху вниз
                    isLeftVal = IsLeft(vert1, vert2, pt);
                    if (isLeftVal < 0)//В случае перехода сверху вниз учитываем только те случаи, когда точка расположена справа от сегмента
                    {
                        wn--;
                    }
                    if (isLeftVal == 0)
                    {
                        return true;
                    }
                }

            }

            return wn != 0;
        }


        /// <summary>
        /// Проверка, что точка внутри треугольника
        /// Расчет барицентрических координат
        /// https://www.youtube.com/watch?v=wSZp8ydgWGw
        /// Если одна координата равна нулю, то точка лежит на ребре
        /// Если две координаты равны нулю, то точка лежит на вершине треугольника
        /// </summary>
        /// <param name="p"></param>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static bool BarycentricCoordinates
            (Point3d p, Point3d p1, Point3d p2, Point3d p3,
            out double lambda1, out double lambda2)
        {
            double y2_y3 = p2.Y - p3.Y;
            double x1_x3 = p1.X - p3.X;
            double x3_x2 = p3.X - p2.X;
            double y1_y3 = p1.Y - p3.Y;
            double denominator = y2_y3 * x1_x3 + x3_x2 * y1_y3;

            double x_x3 = p.X - p3.X;
            double y_y3 = p.Y - p3.Y;
            lambda1 = (y2_y3 * x_x3 + x3_x2 * y_y3) / denominator;

            double y3_y1 = p3.Y - p1.Y;
            lambda2 = (y3_y1 * x_x3 + x1_x3 * y_y3) / denominator;


            if (lambda1 != 0 && Math.Abs(lambda1) < 0.000000001)
            {
                lambda1 = 0;
            }
            if (lambda2 != 0 && Math.Abs(lambda2) < 0.000000001)
            {
                lambda2 = 0;
            }
            if (lambda1 + lambda2 != 1 && Math.Abs(lambda1 + lambda2 - 1) < 0.000000001)
            {
                lambda2 = 1 - lambda1;
            }


            return
                lambda1 >= 0
                && lambda2 >= 0
                && lambda1 + lambda2 <= 1;
        }



        /// <summary>
        /// 2 BoundingBox накладываются друг на друга в плане
        /// </summary>
        /// <param name="maxPt1"></param>
        /// <param name="minPt1"></param>
        /// <param name="maxPt2"></param>
        /// <param name="minPt2"></param>
        /// <returns></returns>
        public static bool BoxesAreSuperimposed
            (Point3d maxPt1, Point3d minPt1, Point3d maxPt2, Point3d minPt2)
        {
            //Характеристики двух прямоугольников
            double dX1 = maxPt1.X - minPt1.X;
            double dY1 = maxPt1.Y - minPt1.Y;
            Point2d midPt1 = new Point2d
                (
                    (maxPt1.X + minPt1.X) / 2,
                    (maxPt1.Y + minPt1.Y) / 2
                );

            double dX2 = maxPt2.X - minPt2.X;
            double dY2 = maxPt2.Y - minPt2.Y;
            Point2d midPt2 = new Point2d
                (
                    (maxPt2.X + minPt2.X) / 2,
                    (maxPt2.Y + minPt2.Y) / 2
                );
            //Проверка их перекрытия
            if
                (
                Math.Abs(midPt1.X - midPt2.X) <= dX1 / 2 + dX2 / 2 &&
                Math.Abs(midPt1.Y - midPt2.Y) <= dY1 / 2 + dY2 / 2
                )
            {
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Проверяет два отрезка на наличие пересечений
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="p4"></param>
        /// <returns></returns>
        public static bool LineSegmentsAreIntersecting(Point2d p1, Point2d p2, Point2d p3, Point2d p4/*, out bool overlaying*/)
        {
            //overlaying = false;

            double p3IsLeft = IsLeft(p1, p2, p3);
            double p4IsLeft = IsLeft(p1, p2, p4);
            double p1IsLeft = IsLeft(p3, p4, p1);
            double p2IsLeft = IsLeft(p3, p4, p2);

            //Невозможно определить допуск
            //p3IsLeft = Math.Abs(p3IsLeft) > 1.0E-9 ? p3IsLeft : 0;
            //p4IsLeft = Math.Abs(p4IsLeft) > 1.0E-9 ? p4IsLeft : 0;
            //p1IsLeft = Math.Abs(p1IsLeft) > 1.0E-9 ? p1IsLeft : 0;
            //p2IsLeft = Math.Abs(p2IsLeft) > 1.0E-9 ? p2IsLeft : 0;


            int p3IsLeftSign = Math.Sign(p3IsLeft);
            int p4IsLeftSign = Math.Sign(p4IsLeft);
            int p1IsLeftSign = Math.Sign(p1IsLeft);
            int p2IsLeftSign = Math.Sign(p2IsLeft);

            if ((p3IsLeftSign == 0 && p4IsLeftSign == 0) || (p1IsLeftSign == 0 && p2IsLeftSign == 0))
            {
                //overlaying = true;
                return false;
            }

            return
            p3IsLeftSign != p4IsLeftSign//Точки второй линии находятся по разные стороны от первой
            && p1IsLeftSign != p2IsLeftSign//Точки первой линии находятся по разные стороны от второй
            ;

        }

        /// <summary>
        /// Пересечение двух бесконечных прямых линий
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="p4"></param>
        public static Point2d? GetLinesIntersection(Point2d p1, Point2d p2,
            Point2d p3, Point2d p4)
        {
            Point2d? intersectionPt = null;
            double intersectX = 0;
            double intersectY = 0;

            double x1_x2 = p1.X - p2.X;
            double y3_y4 = p3.Y - p4.Y;
            double y1_y2 = p1.Y - p2.Y;
            double x3_x4 = p3.X - p4.X;
            double denominator = x1_x2 * y3_y4 - y1_y2 * x3_x4;
            if (denominator != 0)
            {
                double x1y2_y1x2 = p1.X * p2.Y - p1.Y * p2.X;
                double x3y4_y3x4 = p3.X * p4.Y - p3.Y * p4.X;
                intersectX = (x1y2_y1x2 * x3_x4 - x1_x2 * x3y4_y3x4)
                    / denominator;
                intersectY = (x1y2_y1x2 * y3_y4 - y1_y2 * x3y4_y3x4)
                    / denominator;
                intersectionPt = new Point2d(intersectX, intersectY);
            }

            return intersectionPt;
        }




        public static Point2d? GetLinesIntersectionCramer(Point2d p1, Point2d p2, Point2d p3, Point2d p4, out bool overlapping)
        {

            Point2d? pt = null;
            overlapping = false;

            double a1 = p1.Y - p2.Y;
            double b1 = p2.X - p1.X;
            double c1 = p2.X * p1.Y - p1.X * p2.Y;
            double a2 = p3.Y - p4.Y;
            double b2 = p4.X - p3.X;
            double c2 = p4.X * p3.Y - p3.X * p4.Y;

            double determinant1 = a1 * b2 - a2 * b1;
            double determinantX = c1 * b2 - c2 * b1;
            double determinantY = a1 * c2 - a2 * c1;
            if (determinant1 != 0)
            {
                pt = new Point2d(determinantX / determinant1, determinantY / determinant1);
            }

            if (determinant1 == 0 && determinantX == 0 && determinantY == 0)
            {
                overlapping = true;
            }

            return pt;
        }


        /// <summary>
        /// Проверка двух линий на пересечение с использованием методов API автокада
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="p4"></param>
        /// <param name="overlapping"></param>
        /// <returns></returns>
        public static Point2d? GetLinesIntersectionAcad(Point2d p1, Point2d p2, Point2d p3, Point2d p4, out bool overlapping)
        {
            Point2d? pt = null;
            overlapping = false;

            using (Line line1 = new Line(new Point3d(p1.X, p1.Y, 0), new Point3d(p2.X, p2.Y, 0)))
            using (Line line2 = new Line(new Point3d(p3.X, p3.Y, 0), new Point3d(p4.X, p4.Y, 0)))
            {
                Point3dCollection intersectPts = new Point3dCollection();
                line1.IntersectWith(line2, Intersect.ExtendBoth, intersectPts, IntPtr.Zero, IntPtr.Zero);
                if (intersectPts.Count == 0)
                {
                    //Линии параллельны
                    //Проверить расстояние между линиями
                    double dist = line2.GetClosestPointTo(line1.StartPoint, true).DistanceTo(line1.StartPoint);
                    if (dist == 0)
                    {
                        overlapping = true;
                    }
                }
                else
                {

                    //Бесконечные линии пересекаются, но нужно проверить, что они пересекаются без продления
                    //Для этого точка пересечения должна попадать в BoudingBox обоих линий
                    Point2d testingPt = new Point2d(intersectPts[0].X, intersectPts[0].Y);
                    Extents3d? ext1 = line1.Bounds;
                    Extents3d? ext2 = line2.Bounds;
                    if (ext1!=null && ext2!=null
                        && PointInsideBoundingBox(ext1.Value.MaxPoint, ext1.Value.MinPoint, testingPt)
                        && PointInsideBoundingBox(ext2.Value.MaxPoint, ext2.Value.MinPoint, testingPt))
                    {
                        pt = testingPt;
                    }



                    //intersectPts = new Point3dCollection();
                    //line1.IntersectWith(line2, Intersect.OnBothOperands, intersectPts, IntPtr.Zero, IntPtr.Zero);
                    //if (intersectPts.Count > 0)
                    //{
                    //    pt = new Point2d(intersectPts[0].X, intersectPts[0].Y);
                    //}
                }
            }

            return pt;
        }


        /// <summary>
        /// Точка внутри BoundingBox в плане
        ///
        /// </summary>
        /// <param name="maxPt"></param>
        /// <param name="minPt"></param>
        /// <param name="testingPt"></param>
        /// <returns></returns>
        public static bool PointInsideBoundingBox(Point3d maxPt, Point3d minPt, Point2d testingPt)
        {
            return
                (testingPt.X <= maxPt.X)
                && (testingPt.X >= minPt.X)
                &&
                (testingPt.Y <= maxPt.Y)
                && (testingPt.Y >= minPt.Y);
        }

    }
}
