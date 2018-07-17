using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using AcadDb = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Common.XMLClasses;

[assembly: CommandClass(typeof(Civil3DInfoTools.Spillway.ExtractSpillwayPositionsCommand))]


namespace Civil3DInfoTools.Spillway
{
    //TODO: Проверить как это будет работать не на виде в плане (низкий приоритет)
    //TODO: Переделать на вариант, при котором будет только 3 слоя: КПЧ, ОТК0(для бровки) и ОТК1(Hinge, Daylight)(для всех переломов и выхода на рельеф)
    //TODO: Сделать DrawOverrule - подписи для маркеров водосбросов, соответствующие именам в XML
    class ExtractSpillwayPositionsCommand
    {
        [CommandMethod("S1NF0_ExtractSpillwayPositions", CommandFlags.Modal)]
        public void ExtractSpillwayPositions()
        {
            Document adoc = Application.DocumentManager.MdiActiveDocument;
            if (adoc == null) return;

            Database db = adoc.Database;

            Editor ed = adoc.Editor;

            try
            {
                Plane horizontalPlane = new Plane(Point3d.Origin, Vector3d.ZAxis);


                //Указать линию КПЧ и линии перелома откоса (они должны быть в соответствующих слоях)
                TypedValue[] tv = new TypedValue[] { new TypedValue(0, "POLYLINE") };
                SelectionFilter flt = new SelectionFilter(tv);
                PromptSelectionOptions opts = new PromptSelectionOptions();
                opts.MessageForAdding = "\nВыберите 3d-полилинии, обозначающие край проезжей части и переломы откоса (только с одной стороны дороги)";
                PromptSelectionResult res = ed.GetSelection(opts, flt);
                if (res.Status == PromptStatus.OK)
                {
                    SelectionSet sset = res.Value;
                    //Отобрать только полилинии в нужных слоях
                    Dictionary<string, List<Polyline3dInfo>> slopeLines = new Dictionary<string, List<Polyline3dInfo>>();



                    //Считать направление полилинии соответствующим направлению дороги
                    bool? toTheRight = null;//водосброс справа от КПЧ
                    Polyline3dInfo baseLine = null;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        WrongPolylinesException wrongPolylinesException = new WrongPolylinesException();



                        //Отбор линий в служебных слоях
                        foreach (SelectedObject acSSObj in sset)
                        {
                            Polyline3d currPoly = tr.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as Polyline3d;
                            if (currPoly != null)
                            {
                                if (currPoly.Layer.Equals("КПЧ") || currPoly.Layer.Equals("ОТК_") || Constants.SlopeLayerRegex.IsMatch(currPoly.Layer))
                                {
                                    List<Polyline3dInfo> polylines = null;
                                    slopeLines.TryGetValue(currPoly.Layer, out polylines);
                                    if (polylines == null)
                                    {
                                        polylines = new List<Polyline3dInfo>();
                                        slopeLines.Add(currPoly.Layer, polylines);
                                    }
                                    polylines.Add(new Polyline3dInfo(currPoly.Layer, currPoly));
                                }

                            }

                        }

                        //Проверить, что есть минимальный набор слоев - КПЧ, ОТК0, ОТК_
                        if (!slopeLines.ContainsKey("КПЧ") || !slopeLines.ContainsKey("ОТК0") || !slopeLines.ContainsKey("ОТК_"))
                        {
                            wrongPolylinesException.Mistakes = wrongPolylinesException.Mistakes | Mistake.NotEnoughLayers;
                        }

                        //Проверить, что в слоях КПЧ, ОТК0, ОТК_ находится по одной полилинии
                        List<Polyline3dInfo> checkList1 = null;
                        List<Polyline3dInfo> checkList2 = null;
                        List<Polyline3dInfo> checkList3 = null;
                        slopeLines.TryGetValue("КПЧ", out checkList1);
                        slopeLines.TryGetValue("ОТК0", out checkList2);
                        slopeLines.TryGetValue("ОТК_", out checkList3);
                        if ((checkList1 != null && checkList1.Count != 1)
                            || (checkList2 != null && checkList2.Count != 1)
                            || (checkList3 != null && checkList3.Count != 1))
                        {
                            wrongPolylinesException.Mistakes = wrongPolylinesException.Mistakes | Mistake.TooManyLinesInOneLayer;
                        }

                        #region Проперка непересечения
                        //Проверить что линии откоса не пересекают друг друга в плане
                        //TODO: ВРЕМЕННО отказался от проверки взаимного пересечения линий откоса. Нужно учесть возможность частичного совпадения линий
                        /*
                        List<Polyline3dInfo> slopeLinesList = slopeLines.Values.ToList().Aggregate((l1, l2) =>
                        {
                            return l1.Concat(l2).ToList();
                        });
                        bool exitLoop = false;
                        for (int i = 0; i < slopeLinesList.Count; i++)
                        {
                            for (int j = i + 1; j < slopeLinesList.Count; j++)
                            {
                                Polyline3d poly1 = slopeLinesList[i].Poly3d;
                                Polyline3d poly2 = slopeLinesList[j].Poly3d;
                                Point3dCollection intersectPts = new Point3dCollection();
                                poly1.IntersectWith(poly2, Intersect.OnBothOperands,
                                    horizontalPlane, intersectPts,
                                    new IntPtr(0), new IntPtr(0));

                                //TODO!!!!! Не считать точки пересечения если в точках пересечения происходит полное совпадение вершин двух полилиний
                                //В это случае скорее всего полилинии просто сливаются в одну. Это допустимо для коридора



                                if (intersectPts.Count > 0)
                                {



                                    wrongPolylinesException.Mistakes = wrongPolylinesException.Mistakes | Mistake.LinesAreIntersecting;
                                    exitLoop = true;
                                    break;
                                }
                            }
                            if (exitLoop)
                                break;
                        }
                        */ 
                        #endregion

                        //Проверить, что все точки откоса расположены с одной стороны от КПЧ
                        //Определить водосброс направо или налево
                        //TODO: Проверить сонаправленность линий!



                        //Для всех кодов определить участки КПЧ. Параметры взаимного расположения расчитываются в горизонтальной проекции
                        //По начальным точкам линий определить расположение линии справа или слева от КПЧ

                        //Polyline3dInfo baseLine = slopeLines["КПЧ"].First();
                        baseLine = slopeLines["ОТК0"].First();//базовая линия - бровка откоса

                        foreach (KeyValuePair<string, List<Polyline3dInfo>> kvp in slopeLines)
                        {
                            if (!kvp.Key.Equals(/*"КПЧ"*/"ОТК0"))
                            {
                                foreach (Polyline3dInfo poly3dInfo in kvp.Value)
                                {

                                    poly3dInfo.BaseLine = baseLine.Poly2d;
                                    poly3dInfo.ComputeParameters();
                                    poly3dInfo.ComputeOrientation();
                                    if (!kvp.Key.Equals("КПЧ"))
                                        if (toTheRight != null)
                                        {
                                            if (toTheRight != poly3dInfo.ToTheRightOfBaseLine)
                                            {
                                                wrongPolylinesException.Mistakes = wrongPolylinesException.Mistakes | Mistake.WrongOrientation;
                                            }
                                        }
                                        else
                                        {
                                            toTheRight = poly3dInfo.ToTheRightOfBaseLine;
                                        }
                                }
                            }
                        }
                        //Проверить что КПЧ с проивоположной стороны от ОТК0
                        if (slopeLines["КПЧ"].First().ToTheRightOfBaseLine == toTheRight)
                        {
                            wrongPolylinesException.Mistakes = wrongPolylinesException.Mistakes | Mistake.WrongOrientation;
                        }



                        if (wrongPolylinesException.Mistakes != Mistake.None)
                        {
                            throw wrongPolylinesException;
                        }

                        #region Test
                        //ed.WriteMessage("\nОшибок нет\ntoTheRight = " + toTheRight);
                        ////Начертить круги в точках начала и конца полилиний
                        //BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        //BlockTableRecord ms
                        //        = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        //foreach (KeyValuePair<string, List<Polyline3dInfo>> kvp in slopeLines)
                        //{
                        //    if (!kvp.Key.Equals(/*"КПЧ"*/"ОТК0"))
                        //    {
                        //        foreach (Polyline3dInfo poly3dInfo in kvp.Value)
                        //        {
                        //            Point3d pt1 = poly3dInfo.Poly3d.GetPointAtParameter(poly3dInfo.StartParameter);
                        //            Point3d pt2 = poly3dInfo.Poly3d.GetPointAtParameter(poly3dInfo.EndParameter);
                        //            Point3d pt3 = baseLine.Poly3d.GetPointAtParameter(poly3dInfo.StartParameterBase);
                        //            Point3d pt4 = baseLine.Poly3d.GetPointAtParameter(poly3dInfo.EndParameterBase);

                        //            foreach (Point3d pt in new Point3d[] { pt1, pt2, pt3, pt4 })
                        //            {
                        //                using (Circle circle = new Circle(pt, Vector3d.ZAxis, 1))
                        //                {
                        //                    circle.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                        //                    ms.AppendEntity(circle);
                        //                    tr.AddNewlyCreatedDBObject(circle, true);
                        //                }
                        //            }
                        //            using (Line line = new Line(pt1, pt3))
                        //            {
                        //                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                        //                ms.AppendEntity(line);
                        //                tr.AddNewlyCreatedDBObject(line, true);
                        //            }
                        //            using (Line line = new Line(pt2, pt4))
                        //            {
                        //                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                        //                ms.AppendEntity(line);
                        //                tr.AddNewlyCreatedDBObject(line, true);
                        //            }


                        //        }
                        //    }
                        //}
                        #endregion

                        tr.Commit();
                    }

                    //TODO: Включать подсветку 3d полилиний, которые участвуют в расчете

                    Polyline3dInfo edgeLine = slopeLines["КПЧ"].First();
                    PositionData positionData = new PositionData();
                    while (true)
                    {
                        //Указать точку расположения водосброса
                        PromptPointResult pPtRes;
                        PromptPointOptions pPtOpts = new PromptPointOptions("");
                        pPtOpts.Message = "\nУкажите точку расположения водосброса: ";
                        pPtRes = adoc.Editor.GetPoint(pPtOpts);
                        if (pPtRes.Status == PromptStatus.OK)
                        {
                            //ed.WriteMessage("\nточка введена");

                            Point3d pickedPt = new Point3d(pPtRes.Value.X, pPtRes.Value.Y, 0);

                            Point3d nearestPtOnBasePt = baseLine.Poly2d.GetClosestPointTo(pickedPt, true);//найти ближайшую точку ОТК0

                            double pickedParameterBase = baseLine.Poly2d.GetParameterAtPoint(nearestPtOnBasePt);//параметр базовой линии в этой точке
                            //Найти все линии откоса, которые расположены в районе данного параметра
                            //Предполагается, что для каждого кода есть только одна такая
                            List<Polyline3dInfo> pickedPtSlopeLines = new List<Polyline3dInfo>();
                            bool founded1 = false;
                            bool founded2 = false;
                            foreach (KeyValuePair<string, List<Polyline3dInfo>> kvp in slopeLines)
                            {
                                if (!kvp.Key.Equals("ОТК0"))
                                {
                                    Polyline3dInfo founded
                                        = kvp.Value.Find(l => l.StartParameterBase <= pickedParameterBase && l.EndParameterBase >= pickedParameterBase);
                                    if (founded != null)
                                    {
                                        pickedPtSlopeLines.Add(founded);
                                        if (kvp.Key.Equals("КПЧ"))
                                        {
                                            founded1 = true;
                                        }
                                        if (kvp.Key.Equals("ОТК_"))
                                        {
                                            founded2 = true;
                                        }
                                    }
                                }
                            }


                            if (founded1 && founded2)//Проверить, что найдены КПЧ и ОТК_
                            {
                                //Найти касательную к базовой линии
                                Vector3d tangentVector = baseLine.Poly2d.GetFirstDerivative(pickedParameterBase);

                                double rotateAngle = toTheRight.Value ? -Math.PI / 2 : Math.PI / 2;

                                Vector3d spillWayVector = tangentVector.RotateBy(rotateAngle, Vector3d.ZAxis).GetNormal();
                                Line spillWayAxis = new Line(nearestPtOnBasePt, nearestPtOnBasePt + spillWayVector);
                                Point3dCollection intersections = new Point3dCollection();
                                edgeLine.Poly2d.IntersectWith(spillWayAxis, Intersect.ExtendArgument,
                                    horizontalPlane, intersections,
                                    new IntPtr(0), new IntPtr(0));
                                if (intersections.Count > 0)
                                {
                                    Point3d basePt = intersections[0];//Точка пересечения с КПЧ
                                    //Найти точки пересечения перпендикуляра к ОТК0 и остальными линиями откоса
                                    //Отсортировать все линии по удаленности от КПЧ в этой точке
                                    SortedDictionary<Point3d, Polyline3dInfo> intersectionPts
                                        = new SortedDictionary<Point3d, Polyline3dInfo>(new PtsSortComparer(basePt));
                                    pickedPtSlopeLines.Add(baseLine);
                                    foreach (Polyline3dInfo p3dI in pickedPtSlopeLines)
                                    {
                                        intersections.Clear();
                                        p3dI.Poly2d.IntersectWith(spillWayAxis, Intersect.ExtendArgument,
                                            horizontalPlane, intersections,
                                            new IntPtr(0), new IntPtr(0));
                                        if (intersections.Count > 0)
                                        {
                                            intersectionPts.Add(intersections[0], p3dI);
                                        }
                                    }

                                    if (intersectionPts.Count == pickedPtSlopeLines.Count)//Проверить, что все пересечения найдены
                                    {
                                        //intersectionPts содержит все линии с точками пересечения в нужном порядке,
                                        //но все точки пересечения лежат на плоскости XY
                                        //Расчитать трехмерные точки
                                        Point3dCollection pts = new Point3dCollection();
                                        foreach (KeyValuePair<Point3d, Polyline3dInfo> kvp in intersectionPts)
                                        {
                                            Point3d pt2d = kvp.Key;
                                            pt2d = kvp.Value.Poly2d.GetClosestPointTo(pt2d, false);//по какой-то причине в некоторых случаях без этой строки вылетала ошибка при получении параметра
                                            double param = kvp.Value.Poly2d.GetParameterAtPoint(pt2d);
                                            Point3d pt3d = kvp.Value.Poly3d.GetPointAtParameter(param);
                                            pts.Add(pt3d);
                                        }


                                        using (Transaction tr = db.TransactionManager.StartTransaction())
                                        {
                                            ObjectId layerId = Utils.CreateLayerIfNotExists("ВОДОСБРОС", db, tr, null, 150, LineWeight.LineWeight030);


                                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                                            BlockTableRecord ms
                                                    = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                                            //Вычерчивание вектора
                                            //spillWayAxis.LayerId=layerId;
                                            //ms.AppendEntity(spillWayAxis);
                                            //tr.AddNewlyCreatedDBObject(spillWayAxis, true);

                                            //Вычерчивание 3d полилинии по линии водосброса
                                            //using (Polyline3d poly3d = new Polyline3d())
                                            //{
                                            //    poly3d.LayerId = layerId;
                                            //    poly3d.PolyType = Poly3dType.SimplePoly;
                                            //    ms.AppendEntity(poly3d);
                                            //    tr.AddNewlyCreatedDBObject(poly3d, true);

                                            //    foreach (Point3d pt in pts)
                                            //    {
                                            //        PolylineVertex3d vertex = new PolylineVertex3d(pt);
                                            //        poly3d.AppendVertex(vertex);
                                            //    }

                                            //    //Почему-то не прорисовывается полилиния полностью (не прорисовывается последняя вершина)
                                            //    //ничего не помогает
                                            //    //TODO: Попоробовать решить эту проблему с помощью различных манипуляций (другие транзакции, другие объекты, хз)
                                            //    //Point3d lastPt = pts[pts.Count - 1];
                                            //    //Point3d dummyPt = new Point3d(lastPt.X+1, lastPt.Y+1, lastPt.Z+1);
                                            //    //PolylineVertex3d dummyVertex = new PolylineVertex3d(dummyPt);
                                            //    //poly3d.AppendVertex(dummyVertex);
                                            //    //dummyVertex.Erase();
                                            //    //poly3d.Draw();
                                            //    //ed.Regen();
                                            //    //ed.UpdateScreen();
                                            //}

                                            //Вычерчивание отдельных отрезков
                                            for (int i = 0; i < pts.Count - 1; i++)
                                            {
                                                Point3d pt1 = pts[i];
                                                Point3d pt2 = pts[i + 1];

                                                AcadDb.Entity previous = null;
                                                using (Line line = new Line(pt1, pt2))
                                                {
                                                    line.LayerId = layerId;
                                                    ms.AppendEntity(line);
                                                    tr.AddNewlyCreatedDBObject(line, true);

                                                    //Объединять линии (не работает)
                                                    //if (previous == null)
                                                    //{
                                                    //    previous = line;
                                                    //}
                                                    //else
                                                    //{
                                                    //    previous.JoinEntity(line);
                                                    //}
                                                }
                                            }

                                            tr.Commit();
                                        }


                                        Vector3d baseVector = Vector3d.YAxis;
                                        int sign = Math.Sign(baseVector.CrossProduct(spillWayVector).Z);
                                        double rotation = spillWayVector.GetAngleTo(baseVector) * sign;//в радианах
                                        rotation = rotation * 180 / (Math.PI);//в градусах
                                        List<Slope> slopes = new List<Slope>();
                                        //Сохраниение расположения водосброса и всех уклонов
                                        for (int i = 0; i < pts.Count - 1; i++)
                                        {
                                            Point3d pt1 = pts[i];
                                            Point3d pt2 = pts[i + 1];
                                            Point2d pt1_2d = new Point2d(pt1.X, pt1.Y);
                                            Point2d pt2_2d = new Point2d(pt2.X, pt2.Y);

                                            double len = pt1_2d.GetDistanceTo(pt2_2d);
                                            if (len > 0)
                                            {
                                                double s = (pt2.Z - pt1.Z) / len;
                                                slopes.Add(new Slope() { S = s, Len = len });
                                            }

                                        }

                                        SpillwayPosition spillwayPosition = new SpillwayPosition()
                                        {
                                            X = pts[0].X,
                                            Y = pts[0].Y,
                                            Z = pts[0].Z,
                                            Z_Rotation = rotation,
                                            ToTheRight = toTheRight.Value,
                                            Slopes = slopes
                                        };
                                        positionData.SpillwayPositions.Add(spillwayPosition);
                                    }


                                }


                            }




                        }
                        else
                        {
                            ed.WriteMessage("\nвыбор закончен");
                            break;
                        }

                    }




                    //ed.WriteMessage("\nпродолжение выполнения");
                    //Сериализация расположений. Сохранить xml в папку рядом с файлом
                    if (positionData.SpillwayPositions.Count > 0)
                    {
                        string filename = null;
                        int n = 0;
                        do
                        {
                            filename = Path.Combine(Path.GetDirectoryName(adoc.Name), "SpillwayPositions" + n + ".xml");
                            n++;
                        } while (File.Exists(filename));


                        XmlSerializer xmlSerializer = new XmlSerializer(typeof(PositionData));
                        using (StreamWriter sw = new StreamWriter(filename))
                        {
                            xmlSerializer.Serialize(sw, positionData);
                        }
                        //TODO!!!!!: Добавить сообщение о том, что все выполнено
                    }
                }
            }
            catch (System.Exception ex)
            {
                Utils.ErrorToCommandLine(ed, "Ошибка при извлечении расположений водосбросов", ex);
            }

        }


        /// <summary>
        /// Сортирует точки по удаленности от базовой
        /// </summary>
        private class PtsSortComparer : IComparer<Point3d>
        {
            Point2d basePt;
            public PtsSortComparer(Point3d basePt)
            {
                this.basePt = new Point2d(basePt.X, basePt.Y);
            }

            public int Compare(Point3d pt1_3d, Point3d pt2_3d)
            {
                Point2d pt1 = new Point2d(pt1_3d.X, pt1_3d.Y);
                Point2d pt2 = new Point2d(pt2_3d.X, pt2_3d.Y);

                double dist1 = pt1.GetDistanceTo(basePt);
                double dist2 = pt2.GetDistanceTo(basePt);

                return dist1.CompareTo(dist2);
            }
        }

        class WrongPolylinesException : System.Exception
        {
            public Mistake Mistakes { get; set; } = Mistake.None;

            public List<string> LayersWithTooManyObjects { get; set; } = new List<string>();



            public override string Message
            {
                get
                {
                    string message = "НЕВЕРНЫЙ ВЫБОР ПОЛИЛИНИЙ.";
                    if (Mistakes.HasFlag(Mistake.NotEnoughLayers))
                    {
                        message += "\n - Набор выбора должен содержать служебные слои (минимальный набор \"КПЧ\", \"ОТК0\", \"ОТК_\")."
                            + "Для каждого из переломов откоса должна быть линия в слое \"ОТК##\", где ## - цифра или набор цифр.";
                    }

                    if (Mistakes.HasFlag(Mistake.TooManyLinesInOneLayer))
                    {
                        message += "\n - Набор выбора должен содержать максимум по одной линии в слоях \"КПЧ\", \"ОТК0\", \"ОТК_\"";
                    }

                    if (Mistakes.HasFlag(Mistake.LinesAreIntersecting))
                    {
                        message += "\n - Линии в наборе не должны пересекаться в плане";
                    }

                    if (Mistakes.HasFlag(Mistake.WrongOrientation))
                    {
                        message += "\n - Все линии откоса должны находиться с одной стороны от КПЧ";
                    }

                    return message;
                }
            }



        }

        public enum Mistake
        {
            None = 0,
            NotEnoughLayers = 1,
            TooManyLinesInOneLayer = 2,
            LinesAreIntersecting = 4,
            WrongOrientation = 8
        }



        private class Polyline3dInfo
        {
            public string Code { get; private set; }
            public Polyline3d Poly3d { get; private set; }

            /// <summary>
            /// 2d полилиния для расчета параметров. Нужна для того, чтобы избежать искажения при расчете параметров взаимного расположения 3d полилиний
            /// При этом параметр полученный из 2d полилинии будет соответствовать параметру 3d полилинии в горизонтальной проекции
            /// </summary>
            public Polyline Poly2d { get; private set; }

            public Polyline BaseLine { get; set; }

            /// <summary>
            /// Эта линия справа от BaseLine иначе слева
            /// </summary>
            public bool ToTheRightOfBaseLine { get; private set; }

            //  StartParameterBase        EndParameterBase
            //         ---------|---------|                <---BaseLine
            //
            //                  |---------|-----------     <---эта линия
            //     StartParameter         EndParameter
            //


            /// <summary>
            /// Начальный параметр этой линии
            /// </summary>
            public double StartParameter { get; private set; } = -1;

            /// <summary>
            /// Начальный параметр BaseLine для этой линии
            /// </summary>
            public double StartParameterBase { get; private set; } = -1;

            /// <summary>
            /// Конечный параметр этой линии
            /// </summary>
            public double EndParameter { get; private set; } = -1;

            /// <summary>
            /// Конечный параметр BaseLine для этой линии
            /// </summary>
            public double EndParameterBase { get; private set; } = -1;




            public Polyline3dInfo(string code, Polyline3d poly3d)
            {
                Code = code;
                Poly3d = poly3d;
                Poly2d = new Polyline();
                int vertNum = 0;
                foreach (ObjectId vId in poly3d)
                {
                    PolylineVertex3d v = vId.GetObject(OpenMode.ForRead) as PolylineVertex3d;

                    Point2d position2d = new Point2d(v.Position.X, v.Position.Y);
                    Poly2d.AddVertexAt(vertNum, position2d, 0, 0, 0);

                    vertNum++;
                }
                //Poly2d.ConvertFrom(poly3d, false);//Не работает

            }


            /// <summary>
            /// Расчет параметров взаимного расположения
            /// Этот расчет предполагает, что полилинии направлены в одну сторону
            /// </summary>
            /// <param name="baseLine"></param>
            public void ComputeParameters()
            {
                if (BaseLine == null)
                {
                    throw new System.Exception("Нет ссылки на BaseLine");
                }


                //Начало
                Point3d startPt = Poly2d.GetPoint3dAt(0);//Начальная точка этой полилинии
                Point3d closestPtBase = BaseLine.GetClosestPointTo(startPt, false);
                StartParameterBase
                    = BaseLine.GetParameterAtPoint(closestPtBase);//Параметр BaseLine в точке ближайшей к начальной точке этой полилинии
                Point3d baseLinePt = BaseLine.GetPointAtParameter(StartParameterBase);//точка на BaseLine
                Point3d closestPt = Poly2d.GetClosestPointTo(baseLinePt, false);
                StartParameter
                    = Poly2d.GetParameterAtPoint(closestPt);//Обратный расчет параметра текущей линии

                //Конец
                Point3d endPt = Poly2d.GetPoint3dAt(Poly2d.NumberOfVertices - 1);//Конечная точка этой полилинии
                closestPtBase = BaseLine.GetClosestPointTo(endPt, false);
                EndParameterBase
                    = BaseLine.GetParameterAtPoint(closestPtBase);//Параметр BaseLine в точке ближайшей к конечной точке этой полилинии
                baseLinePt = BaseLine.GetPointAtParameter(EndParameterBase);//точка на BaseLine
                closestPt = Poly2d.GetClosestPointTo(baseLinePt, false);
                EndParameter
                    = Poly2d.GetParameterAtPoint(closestPt);//Обратный расчет параметра текущей линии



            }

            /// <summary>
            /// Определение расположения справа или слева относительно BaseLine
            /// Этот расчет предполагает, что полилинии направлены в одну сторону
            /// </summary>
            public void ComputeOrientation()
            {
                if (BaseLine == null)
                {
                    throw new System.Exception("Нет ссылки на BaseLine");
                }
                if (StartParameterBase < 0 || StartParameter < 0 || EndParameterBase < 0 || EndParameter < 0)
                {
                    throw new System.Exception("Не расcчитано расположение");
                }

                //Получить две точки на BaseLine между которыми находится параметр начала этой линии
                int pt1Index = -1;
                int pt2Index = -1;
                if (StartParameterBase == BaseLine.StartParam)
                {
                    pt1Index = 0;
                    pt2Index = 1;
                }
                else if (StartParameterBase == BaseLine.EndParam)
                {
                    pt1Index = BaseLine.NumberOfVertices - 2;
                    pt2Index = BaseLine.NumberOfVertices - 1;
                }
                else if (Math.Truncate(StartParameterBase) == StartParameterBase)
                {
                    pt1Index = (int)StartParameterBase;
                    pt2Index = (int)StartParameterBase + 1;
                }
                else
                {
                    pt1Index = (int)Math.Floor(StartParameterBase);
                    pt2Index = (int)Math.Ceiling(StartParameterBase);
                }

                Point2d baseLinePt1 = BaseLine.GetPoint2dAt(pt1Index);
                Point2d baseLinePt2 = BaseLine.GetPoint2dAt(pt2Index);
                Point3d thisLineStartPt = Poly2d.GetPointAtParameter(StartParameter);
                ToTheRightOfBaseLine = Utils.IsLeft(baseLinePt1, baseLinePt2, thisLineStartPt) < 0;

            }
        }
    }
}
