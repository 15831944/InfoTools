using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Civil3DInfoTools
{
    public static class Utils
    {
        /// <summary>
        /// Ошибка в комнадную строку
        /// </summary>
        /// <param name="ed"></param>
        /// <param name="comment"></param>
        /// <param name="ex"></param>
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

        /// <summary>
        /// Первый объект из DBObjectCollection
        /// </summary>
        /// <param name="coll"></param>
        /// <returns></returns>
        public static DBObject GetFirstDBObject(DBObjectCollection coll)
        {
            foreach (DBObject dbo in coll)
            {
                return dbo;
            }
            return null;
        }


        /// <summary>
        /// Найти тип линии - непрерывный (независимо от его названия)
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Последовательность штрихов и пробелов типа линии
        /// </summary>
        /// <param name="ltype"></param>
        /// <param name="ltScale"></param>
        /// <returns></returns>
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
        /// Подходит так же и для имени блока
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetSafeSymbolName(string name)
        {
            return string.Join("_", name.Split(new char[] { '<', '>', '/', '\\', '"', '"', ':', ';', '?', '*', '|', ',', '=', '`' })).Trim();
        }

        /// <summary>
        /// Создать слой если еще нет
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="db"></param>
        /// <param name="tr"></param>
        /// <param name="layerSample"></param>
        /// <param name="color"></param>
        /// <param name="lineWeight"></param>
        /// <returns></returns>
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
            Curve3d curve3D = poly.GetGeCurve();
            CurveCurveIntersector3d intersector = new CurveCurveIntersector3d(curve3D, curve3D, Vector3d.ZAxis);

            return intersector.NumberOfIntersectionPoints > 0;

            /*
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
            */
        }


        /// <summary>
        /// Определение находится ли точка внутри полилинии по методу winding number algorithm
        /// Перегрузка для 3d точек. Точки рассматриваются в плане
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="pointsOfPolyline"></param>
        /// <returns></returns>
        public static bool PointIsInsidePolylineWindingNumber(Point3d pt, IEnumerable<Point3d> points3d)
        {
            Point2dCollection points = new Point2dCollection();
            foreach (Point3d p in points3d)
            {
                points.Add(new Point2d(p.X, p.Y));
            }

            return PointIsInsidePolylineWindingNumber(new Point2d(pt.X, pt.Y), points);
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
        public static bool PointIsInsidePolylineWindingNumber(Point2d pt, Point2dCollection pointsOfPolyline)
        {

            int numVert = pointsOfPolyline.Count;

            int wn = 0;
            for (int i = 0; i < numVert; i++)
            {
                Point2d vert1 = pointsOfPolyline[i];
                Point2d vert2 = pointsOfPolyline[(i + 1) % numVert];

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



        public static bool BarycentricCoordinates(Point2d p,
            Autodesk.Civil.DatabaseServices.TinSurfaceTriangle t,
            out double lambda1, out double lambda2)
        {
            Point2d vert1_2d = new Point2d(t.Vertex1.Location.X, t.Vertex1.Location.Y);
            Point2d vert2_2d = new Point2d(t.Vertex2.Location.X, t.Vertex2.Location.Y);
            Point2d vert3_2d = new Point2d(t.Vertex3.Location.X, t.Vertex3.Location.Y);
            return
                BarycentricCoordinates(p, vert1_2d, vert2_2d,
                vert3_2d, out lambda1, out lambda2);
        }


        /// <summary>
        /// Проверка, что точка внутри треугольника
        /// Расчет барицентрических координат
        /// https://www.youtube.com/watch?v=wSZp8ydgWGw
        /// Если одна координата равна нулю, то точка лежит на ребре
        /// Если две координаты равны нулю, то точка лежит на вершине треугольника
        /// Может возвращать не совсем точное значение если точка лежит на ребре или вершине треугольника,
        /// но более точное чем FindTriangleAtXY
        /// </summary>
        /// <param name="p"></param>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static bool BarycentricCoordinates
            (Point2d p, Point2d p1, Point2d p2, Point2d p3,
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
            (Extents2d ext1_2d, Extents2d ext2_2d)
        {
            Point2d maxPt1 = ext1_2d.MaxPoint;
            Point2d minPt1 = ext1_2d.MinPoint;
            Point2d maxPt2 = ext2_2d.MaxPoint;
            Point2d minPt2 = ext2_2d.MinPoint;
            return BoxesAreSuperimposed(maxPt1, minPt1, maxPt2, minPt2);
        }

        /// <summary>
        /// 2 BoundingBox накладываются друг на друга
        /// </summary>
        /// <param name="maxPt1"></param>
        /// <param name="minPt1"></param>
        /// <param name="maxPt2"></param>
        /// <param name="minPt2"></param>
        /// <returns></returns>
        public static bool BoxesAreSuperimposed
            (Point2d maxPt1, Point2d minPt1, Point2d maxPt2, Point2d minPt2)
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
        public static bool LineSegmentsAreIntersecting(Point2d p1, Point2d p2, Point2d p3, Point2d p4)
        {

            double p3IsLeft = IsLeft(p1, p2, p3);
            double p4IsLeft = IsLeft(p1, p2, p4);
            double p1IsLeft = IsLeft(p3, p4, p1);
            double p2IsLeft = IsLeft(p3, p4, p2);

            int p3IsLeftSign = Math.Sign(p3IsLeft);
            int p4IsLeftSign = Math.Sign(p4IsLeft);
            int p1IsLeftSign = Math.Sign(p1IsLeft);
            int p2IsLeftSign = Math.Sign(p2IsLeft);

            if ((p3IsLeftSign == 0 && p4IsLeftSign == 0) || (p1IsLeftSign == 0 && p2IsLeftSign == 0))
            {
                return false;
            }

            return
            p3IsLeftSign != p4IsLeftSign//Точки второй линии находятся по разные стороны от первой
            && p1IsLeftSign != p2IsLeftSign//Точки первой линии находятся по разные стороны от второй
            ;

        }

        /// <summary>
        /// Пересечение двух бесконечных прямых линий
        /// Данный расчет не учитывает допусков автокада
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



        /// <summary>
        /// Пересечение двух бесконечных прямых линий. Расчет по правилу Крамера
        /// Данный расчет не учитывает допусков автокада
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="p4"></param>
        /// <param name="overlapping"></param>
        /// <returns></returns>
        public static Point2d? GetLinesIntersectionCramer
            (Point2d p1, Point2d p2, Point2d p3, Point2d p4, out bool overlapping)
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
        /// Точка внутри BoundingBox в плане
        ///
        /// </summary>
        /// <param name="maxPt"></param>
        /// <param name="minPt"></param>
        /// <param name="testingPt"></param>
        /// <returns></returns>
        public static bool PointInsideBoundingBox(Point2d maxPt, Point2d minPt, Point2d testingPt)
        {
            return
                (testingPt.X <= maxPt.X)
                && (testingPt.X >= minPt.X)
                &&
                (testingPt.Y <= maxPt.Y)
                && (testingPt.Y >= minPt.Y);
        }



        /// <summary>
        /// Проверка только на наложение двух линий
        /// С учетом допусков автокада
        /// </summary>
        /// <returns></returns>
        public static bool LinesAreOverlapping(Point2d p1, Point2d p2, Point2d p3, Point2d p4)
        {
            Vector2d vector1 = p2 - p1;
            Vector2d vector2 = p4 - p3;
            if (vector1.IsParallelTo(vector2))
            {
                LineSegment2d line1 = new LineSegment2d(p1, p2);
                LineSegment2d line2 = new LineSegment2d(p3, p4);

                //Finds the two closest points between this curve and the input curve
                PointOnCurve2d[] closestPts = line2.GetClosestPointTo(line1);
                if (closestPts[0].Point.IsEqualTo(closestPts[1].Point))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Расчет пересечений двух линий 
        /// С учетом допусков автокада
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="p4"></param>
        /// <returns></returns>
        public static Point2d? GetLinesIntersectionAcad(Point2d p1, Point2d p2, Point2d p3, Point2d p4)
        {
            Point2d? pt = null;
            LineSegment2d line1 = new LineSegment2d(p1, p2);
            LineSegment2d line2 = new LineSegment2d(p3, p4);
            CurveCurveIntersector2d intersector = new CurveCurveIntersector2d(line1, line2);
            if (intersector.NumberOfIntersectionPoints > 0)
            {
                pt = intersector.GetIntersectionPoint(0);
            }
            return pt;
        }


        public static Extents2d Extents2DBy3D(Extents3d ext3d)
        {
            Extents2d ext2d = new Extents2d(new Point2d(ext3d.MinPoint.X, ext3d.MinPoint.Y), new Point2d(ext3d.MaxPoint.X, ext3d.MaxPoint.Y));
            return ext2d;
        }


        public static Point2d Point2DBy3D(Point3d p3d)
        {
            return new Point2d(p3d.X, p3d.Y);
        }


        public static IList<Point2d> Poligon3DTo2D(IList<Point3d> poligon)
        {
            List<Point2d> poligon2d = new List<Point2d>();
            foreach (Point3d p in poligon)
            {
                poligon2d.Add(new Point2d(p.X, p.Y));
            }
            return poligon2d;
        }


        /// <summary>
        /// Порядок точек в наборе соответствует обходу по часовой стрелки
        /// https://stackoverflow.com/a/1165943
        /// </summary>
        /// <param name="poligon"></param>
        /// <returns></returns>
        public static bool DirectionIsClockwise(IList<Point2d> poligon)
        {
            int N = poligon.Count();
            double p = 0;
            for (int i = 0; i < N; i++)
            {
                Point2d p1 = poligon[i];
                Point2d p2 = poligon[(i + 1) % N];
                p += (p2.X - p1.X) * (p2.Y + p1.Y);
            }
            return p > 0;
        }
        /// <summary>
        /// Порядок точек в наборе соответствует обходу по часовой стрелки
        /// Точки рассматриваются в горизонтальной плоскости
        /// https://stackoverflow.com/a/1165943
        /// </summary>
        /// <param name="poligon"></param>
        /// <returns></returns>
        public static bool DirectionIsClockwise(IList<Point3d> poligon)
        {
            int N = poligon.Count();
            double p = 0;
            for (int i = 0; i < N; i++)
            {
                Point3d p1 = poligon[i];
                Point3d p2 = poligon[(i + 1) % N];
                p += (p2.X - p1.X) * (p2.Y + p1.Y);
            }
            return p > 0;
        }


        /// <summary>
        /// Получить произвольную точку, которая гарантировано лежит внутри полигона (не на границе)
        /// http://apodeline.free.fr/FAQ/CGAFAQ/CGAFAQ-3.html - 3.6 : How do I find a single point inside a simple polygon?
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="counterClockwise"></param>
        /// <returns></returns>
        public static Point2d GetAnyPointInsidePoligon(IList<Point2d> polygon, bool clockwise)
        {
            int N = polygon.Count();
            //Найти выпуклую вершину
            int v_index = -1;//выпуклая вершина
            int a_index = -1;//смежная с выпуклой
            int b_index = -1;//смежная с выпуклой
            for (int i = 0; i < N; i++)
            {
                int index1 = i;
                int index2 = (i + 1) % N;
                int index3 = (i + 2) % N;

                Point2d p1 = polygon[index1];
                Point2d p2 = polygon[index2];
                Point2d p3 = polygon[index3];

                if (PolygonVertexIsConvex(p1, p2, p3, clockwise))
                {
                    v_index = index2;
                    a_index = index1;
                    b_index = index3;
                    break;
                }
            }

            if (v_index == -1)
            {
                throw new Exception("Не найдено ни одной выпуклой вершины у полигона");
            }

            Point2d v = polygon[v_index];
            Point2d a = polygon[a_index];
            Point2d b = polygon[b_index];
            //расстояние до точки v расчитывается ортогонально линии ab
            Line2d lineToCalcDistance = new Line2d(v, b - a);

            //Для каждой из остальных вершин
            Point2d? closestPtInsideAVB = null;
            double minDist = double.MaxValue;
            for (int i = (b_index + 1) % N; i != a_index; i = (i + 1) % N)
            {
                Point2d q = polygon[i];
                double l1, l2;
                //Если вершина находится внутри треугольника avb
                if (BarycentricCoordinates(q, a, v, b, out l1, out l2)//Внутри треугольника
                    && l1 != 0 && l2 != 0 && l1 != 1 && l2 != 1)//Не на границе треугольника
                {
                    double distToV = lineToCalcDistance.GetDistanceTo(q);
                    if (distToV < minDist)
                    {
                        minDist = distToV;
                        closestPtInsideAVB = q;
                    }
                }
            }

            if (closestPtInsideAVB != null)
            {
                Point2d p = closestPtInsideAVB.Value;
                return new Point2d((p.X + v.X) / 2, (p.Y + v.Y) / 2);
            }
            else
            {
                return new Point2d((a.X + v.X + b.X) / 3, (a.Y + v.Y + b.Y) / 3);
            }



        }


        public static bool SpecifyWindow(out Point3d? pt1, out Point3d? pt2, Editor adocEd)
        {
            pt1 = null;
            pt2 = null;
            Point3d _pt1 = new Point3d();
            Point3d _pt2 = new Point3d();

            PromptPointOptions ppo = new PromptPointOptions("\n\tУкажите первый угол рамки: ");
            PromptPointResult ppr = adocEd.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return false;
            PromptCornerOptions pco = new PromptCornerOptions("\n\tУкажите второй угол рамки: ", ppr.Value);
            PromptPointResult pcr = adocEd.GetCorner(pco);
            if (pcr.Status != PromptStatus.OK) return false;
            _pt1 = ppr.Value;
            _pt2 = pcr.Value;
            if (_pt1.X == _pt2.X || _pt1.Y == _pt2.Y)
            {
                adocEd.WriteMessage("\nНеправильно указаны точки");
                return false;
            }

            pt1 = _pt1;
            pt2 = _pt2;
            return true;
        }

        public static void ZoomWin(Editor ed, Point3d pt1, Point3d pt2)
        {
            ViewTableRecord view =
              new ViewTableRecord();

            view.CenterPoint = new Point2d((pt1.X + pt2.X) / 2, (pt1.Y + pt2.Y) / 2);
            view.Height = Math.Abs(pt1.Y - pt2.Y);
            view.Width = Math.Abs(pt1.X - pt2.X);
            ed.SetCurrentView(view);
        }


        public static void Highlight(IEnumerable<Entity> selectedPolylines, bool yes)
        {
            if (selectedPolylines != null)
                foreach (Entity p in selectedPolylines)
                {
                    if (yes)
                    {
                        p.Highlight();
                    }
                    else
                    {
                        p.Unhighlight();
                    }
                }
        }

        public static Color GetDimmerColor(Color currColor, double multiplier)
        {
            byte red = Convert.ToByte(currColor.Red * multiplier);
            byte green = Convert.ToByte(currColor.Green * multiplier);
            byte blue = Convert.ToByte(currColor.Blue * multiplier);
            Color dimmerColor = Color.FromRgb(red, green, blue);
            return dimmerColor;
        }

        /// <summary>
        /// Вершина полигона выпуклая
        /// (точки не лежат на одной линии)
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="polygonIsClockwise"></param>
        /// <returns></returns>
        public static bool PolygonVertexIsConvex(Point2d p1, Point2d p2, Point2d p3, bool polygonIsClockwise)
        {
            double isLeft = IsLeft(p1, p2, p3);
            return (polygonIsClockwise && isLeft < 0) || (!polygonIsClockwise && isLeft > 0);
        }
        /// <summary>
        /// Вершина полигона вогнутая
        /// (точки не лежат на одной линии)
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="polygonIsClockwise"></param>
        /// <returns></returns>
        public static bool PolygonVertexIsReflex(Point2d p1, Point2d p2, Point2d p3, bool polygonIsClockwise)
        {
            double isLeft = IsLeft(p1, p2, p3);
            return (polygonIsClockwise && isLeft > 0) || (!polygonIsClockwise && isLeft < 0);
        }

        private static double lengthDelta = Tolerance.Global.EqualPoint;

        /// <summary>
        /// Длины равны с учетом стандартного допуска AutoCAD
        /// </summary>
        /// <param name="len1"></param>
        /// <param name="len2"></param>
        /// <returns></returns>
        public static bool LengthIsEquals(double len1, double len2)
        {
            return Math.Abs(len1 - len2) <= lengthDelta;
        }

    }
}
