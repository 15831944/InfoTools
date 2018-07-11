using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CommandClass(typeof(Civil3DInfoTools.Spillway.ExtractSpillwayPositionsCommand))]

namespace Civil3DInfoTools.Spillway
{
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
                //Указать линию КПЧ и линии перелома откоса (они должны быть в соответствующих слоях)
                TypedValue[] tv = new TypedValue[] { new TypedValue(0, "POLYLINE") };
                SelectionFilter flt = new SelectionFilter(tv);
                PromptSelectionOptions opts = new PromptSelectionOptions();
                opts.MessageForAdding = "\nВыберите 3d-полилинии, обозначающие край проезжей части и переломы откоса";
                PromptSelectionResult res = ed.GetSelection(opts, flt);
                if (res.Status == PromptStatus.OK)
                {
                    SelectionSet sset = res.Value;
                    //Отобрать только полилинии в нужных слоях
                    Dictionary<string, List<Polyline3dInfo>> slopeLines = new Dictionary<string, List<Polyline3dInfo>>();



                    //Считать направление полилинии соответствующим направлению дороги


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

                        //Проверить что линии откоса не пересекают друг друга в плане
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
                                    new Plane(Point3d.Origin, Vector3d.ZAxis), intersectPts,
                                    new IntPtr(0), new IntPtr(0));
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

                        //Проверить, что все точки откоса расположены с одной стороны от КПЧ
                        //Определить водосброс направо или налево
                        bool? toTheRight = null;//водосброс справа от КПЧ


                        //Для всех кодов определить участки КПЧ. Параметры взаимного расположения расчитываются в горизонтальной проекции
                        //По начальным точкам линий определить расположение линии справа или слева от КПЧ

                        //Polyline3dInfo baseLine = slopeLines["КПЧ"].First();
                        Polyline3dInfo baseLine = slopeLines["ОТК0"].First();

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


                        if (wrongPolylinesException.Mistakes != Mistake.None)
                        {
                            throw wrongPolylinesException;
                        }

                        #region Test
                        ed.WriteMessage("\nОшибок нет\ntoTheRight = " + toTheRight);
                        //Начертить круги в точках начала и конца полилиний
                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord ms
                                = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        foreach (KeyValuePair<string, List<Polyline3dInfo>> kvp in slopeLines)
                        {
                            if (!kvp.Key.Equals(/*"КПЧ"*/"ОТК0"))
                            {
                                foreach (Polyline3dInfo poly3dInfo in kvp.Value)
                                {
                                    Point3d pt1 = poly3dInfo.Poly3d.GetPointAtParameter(poly3dInfo.StartParameter);
                                    Point3d pt2 = poly3dInfo.Poly3d.GetPointAtParameter(poly3dInfo.EndParameter);
                                    Point3d pt3 = baseLine.Poly3d.GetPointAtParameter(poly3dInfo.StartParameterBase);
                                    Point3d pt4 = baseLine.Poly3d.GetPointAtParameter(poly3dInfo.EndParameterBase);

                                    foreach (Point3d pt in new Point3d[] { pt1, pt2, pt3, pt4 })
                                    {
                                        using (Circle circle = new Circle(pt, Vector3d.ZAxis, 1))
                                        {
                                            circle.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                            ms.AppendEntity(circle);
                                            tr.AddNewlyCreatedDBObject(circle, true);
                                        }
                                    }
                                    using (Line line = new Line(pt1, pt3))
                                    {
                                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        ms.AppendEntity(line);
                                        tr.AddNewlyCreatedDBObject(line, true);
                                    }
                                    using (Line line = new Line(pt2, pt4))
                                    {
                                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        ms.AppendEntity(line);
                                        tr.AddNewlyCreatedDBObject(line, true);
                                    }


                                }
                            }
                        }
                        #endregion

                        tr.Commit();
                    }

                    while (true)
                    {
                        break;
                        //Указать точку расположения водосброса
                        PromptPointResult pPtRes;
                        PromptPointOptions pPtOpts = new PromptPointOptions("");
                        pPtOpts.Message = "\nHiveMind: Укажите точку расположения водосброса: ";

                        //Отсортировать все линии по удаленности от КПЧ в этой точке
                        //Сохраниение расположения водосброса и всех уклонов
                        //Вычерчивание 3d полилинии по линии водосброса 
                    }





                    //Сериализация расположений
                }
            }
            catch (System.Exception ex)
            {
                Utils.ErrorToCommandLine(ed, "Ошибка при извлечении расположений водосбросов", ex);
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
                    throw new System.Exception("Не расчитано расположение");
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
