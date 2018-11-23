using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static Common.ExceptionHandling.ExeptionHandlingProcedures;

namespace Civil3DInfoTools.Geology.GeologyTrueScalingProfileWindow
{
    public partial class GeologyTrueScalingProfileViewModel : INotifyPropertyChanged
    {
        private void CreateProfile(object arg)
        {
            try
            {
                if (doc != null)
                {
                    using (doc.LockDocument())
                    {
                        Editor ed = doc.Editor;
                        Database db = doc.Database;


                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            //выбрать базовую точку снизу слева
                            double minx = double.PositiveInfinity;
                            double miny = double.PositiveInfinity;
                            List<ObjectId> allSelected = new List<ObjectId>(soilHatchIds);
                            allSelected.Add(groundSurfPolyId);
                            foreach (ObjectId id in allSelected)
                            {
                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                Extents3d? ext = ent.Bounds;
                                if (ext != null)
                                {
                                    Point3d minPt = ext.Value.MinPoint;
                                    if (minx > minPt.X) minx = minPt.X;
                                    if (miny > minPt.Y) miny = minPt.Y;
                                }
                            }
                            Point2d basePt = new Point2d(minx, miny);


                            //для трансформации координат
                            //Vector2d baseVector = new Vector2d(minx, miny);
                            Matrix2d transform =  
                                new Matrix2d(new double[]
                                {
                                    StartHorScaling, 0,                0,
                                    0,               StartVertScaling, 0,
                                    0,               0,                1
                                })
                                * Matrix2d.Displacement(new Vector2d(-minx, -miny));

                            //полилиния поверхности земли 
                            //???????координаты точек полилинии расчитываются относительно базовой точки (и пересчитываются в метры согласно введенным коэффициентам?)
                            //отсортировать список сточек по координате X.
                            //ПОИСК ПО ДИАПАЗОНУ:
                            //Использовать бинарный поиск (https://www.baeldung.com/java-binary-search) для нахождения хотябы одной точки в заданном диапазоне
                            //затем проверять соседние точки на нахождение в диапазоне
                            Polyline poly = (Polyline)tr.GetObject(groundSurfPolyId, OpenMode.ForRead);
                            ObjectId groundSurfLayerId = poly.LayerId;
                            List<Point2d> polyPts = new List<Point2d>();
                            for (int i = 0; i < poly.NumberOfVertices; i++)
                            {
                                Point2d pt = poly.GetPoint2dAt(i);
                                pt = transform * pt;
                                //pt = pt - baseVector;
                                //pt = new Point2d(pt.X * StartHorScaling, pt.Y * StartVertScaling);
                                polyPts.Add(pt);
                            }
                            polyPts.Sort((a, b) =>
                            {
                                return a.X.CompareTo(b.X);
                            });



                            double depthMultipier = StartVertSoilScaling / StartVertScaling;
                            //для каждой штриховки получить все контуры
                            //любые контуры должны быть переведены в полигоны без кривизны
                            //???????координаты точек полигонов расчитываются относительно базовой точки (и пересчитываются в метры согласно введенным коэффициентам?)
                            //для каждой точки полигона должно быть расчитано заглубление относительно поверхности земли
                            //если между двумя точками полигона по гоизонтали есть точки перелома поверхности земли,
                            //то для этих точек должны быть добавлены соответствующие точки полигона
                            List<HatchData> hatchData = new List<HatchData>();
                            foreach (ObjectId id in soilHatchIds)
                            {
                                Hatch hatch = (Hatch)tr.GetObject(id, OpenMode.ForRead);
                                HatchData hd = new HatchData(hatch);
                                hatchData.Add(hd);
                                for (int i = 0; i < hatch.NumberOfLoops; i++)
                                {
                                    HatchLoop hl = hatch.GetLoopAt(i);

                                    if (!hl.LoopType.HasFlag(HatchLoopTypes.SelfIntersecting)
                                        && !hl.LoopType.HasFlag(HatchLoopTypes.Textbox)
                                        && !hl.LoopType.HasFlag(HatchLoopTypes.TextIsland)
                                        && !hl.LoopType.HasFlag(HatchLoopTypes.NotClosed))
                                    {
                                        List<Point2d> polygon = new List<Point2d>();
                                        hd.Polygons.Add(polygon);

                                        List<Curve2d> curves = Utils.GetHatchLoopCurves(hl);


                                        foreach (Curve2d c in curves)
                                        {
                                            if (!(c is LineSegment2d))
                                            {
                                                Interval interval = c.GetInterval();
                                                PointOnCurve2d[] samplePts = c.GetSamplePoints(interval.LowerBound, interval.UpperBound, 0.02);
                                                for (int n = 0; n < samplePts.Length - 1; n++)
                                                {
                                                    ProcessLineSegmentOfHatch(samplePts[n].Point, samplePts[n + 1].Point,
                                                        polygon, polyPts, transform, /*baseVector, StartHorScaling, StartVertScaling,*/ depthMultipier);
                                                }
                                            }
                                            else
                                            {
                                                ProcessLineSegmentOfHatch(c.StartPoint, c.EndPoint,
                                                    polygon, polyPts, transform, /*baseVector, StartHorScaling, StartVertScaling,*/ depthMultipier);
                                            }
                                        }
                                    }

                                }
                            }


                            //указание пользователем точки вставки нового профиля
                            //создание полилиний
                            //координаты точек пересчитываются заданным масштабным коэффициентам
                            //
                            PromptPointOptions pPtOpts = new PromptPointOptions("\nУкажите точку вставки");
                            PromptPointResult pPtRes = doc.Editor.GetPoint(pPtOpts);
                            if (pPtRes.Status == PromptStatus.OK)
                            {
                                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                                BlockTableRecord ms
                                    = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                                DrawOrderTable drawOrder = tr.GetObject(ms.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;

                                ObjectId continLtId = Utils.GetContinuousLinetype(db);

                                Point2d insertPt = Utils.Point2DBy3D(pPtRes.Value);
                                //Vector2d insertVec = new Vector2d(insertPt.X, insertPt.Y);

                                Matrix2d insertTransform =
                                    Matrix2d.Displacement(new Vector2d(insertPt.X, insertPt.Y))
                                * new Matrix2d(new double[]
                                {
                                    1/EndHorScaling, 0,                0,
                                    0,               1/EndVertScaling, 0,
                                    0,               0,                1
                                });

                                //полилиния поверхности земли
                                using (Polyline surfGroundPoly = new Polyline())
                                {
                                    surfGroundPoly.LayerId = groundSurfLayerId;
                                    surfGroundPoly.ColorIndex = 256;
                                    surfGroundPoly.LinetypeId = continLtId;
                                    for (int i = 0; i < polyPts.Count; i++)
                                    {
                                        Point2d pt = polyPts[i];
                                        //Point2d convertedPt = new Point2d(pt.X / EndHorScaling, pt.Y / EndVertScaling);
                                        //convertedPt = convertedPt + insertVec;
                                        Point2d convertedPt = insertTransform * pt;

                                        surfGroundPoly.AddVertexAt(i, convertedPt, 0, 0, 0);
                                    }
                                    ms.AppendEntity(surfGroundPoly);
                                    tr.AddNewlyCreatedDBObject(surfGroundPoly, true);

                                }

                                //полилинии штриховок
                                HashSet<ObjectId> createdLayers = new HashSet<ObjectId>();
                                ObjectIdCollection hatchIds = new ObjectIdCollection();
                                foreach (HatchData hd in hatchData)
                                {
                                    double patternScale = hd.Hatch.PatternScale;
                                    string layerName = patternScale != 1 ? hd.Hatch.PatternName + "_" + patternScale.ToString("f2") : hd.Hatch.PatternName;

                                    ObjectId hatchLayerId = Utils.CreateLayerIfNotExists(layerName, db, tr,
                                            lineWeight: LineWeight.LineWeight030);
                                    createdLayers.Add(hatchLayerId);


                                    ObjectIdCollection PolyIds = new ObjectIdCollection();
                                    foreach (List<Point2d> polygon in hd.Polygons)
                                    {
                                        if (polygon.Count > 0)
                                        {
                                            using (Polyline geologPoly = new Polyline())
                                            {
                                                geologPoly.LayerId = hatchLayerId;
                                                geologPoly.ColorIndex = 256;
                                                geologPoly.LinetypeId = continLtId;

                                                for (int i = 0; i < polygon.Count; i++)
                                                {
                                                    Point2d pt = polygon[i];
                                                    //Point2d convertedPt = new Point2d(pt.X / EndHorScaling, pt.Y / EndVertScaling);
                                                    //convertedPt = convertedPt + insertVec;
                                                    Point2d convertedPt = insertTransform * pt;

                                                    geologPoly.AddVertexAt(i, convertedPt, 0, 0, 0);
                                                }

                                                geologPoly.Closed = true;

                                                ObjectId id = ms.AppendEntity(geologPoly);
                                                tr.AddNewlyCreatedDBObject(geologPoly, true);

                                                if (!id.IsNull && id.IsValid)
                                                {
                                                    PolyIds.Add(id);
                                                }
                                            }
                                        }

                                    }

                                    if (PolyIds.Count > 0)
                                    {
                                        try
                                        {
                                            using (Hatch oHatch = /*(Hatch)hd.Hatch.Clone()*/ new Hatch())
                                            {
                                                oHatch.LayerId = hatchLayerId;


                                                Vector3d normal = new Vector3d(0.0, 0.0, 1.0);

                                                oHatch.Normal = normal;
                                                oHatch.Elevation = 0.0;

                                                oHatch.PatternScale = hd.Hatch.PatternScale;
                                                //oHatch.SetHatchPattern(hd.Hatch.PatternType, hd.Hatch.PatternName);
                                                oHatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                                oHatch.ColorIndex = 256;

                                                //while (oHatch.NumberOfLoops>0)
                                                //{
                                                //    oHatch.RemoveLoopAt(0);
                                                //}

                                                ObjectId hatchId = ms.AppendEntity(oHatch);
                                                tr.AddNewlyCreatedDBObject(oHatch, true);

                                                oHatch.Associative = true;


                                                foreach (ObjectId polyId in PolyIds)
                                                {
                                                    oHatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection() { polyId });
                                                }

                                                oHatch.EvaluateHatch(true);

                                                if (!hatchId.IsNull && hatchId.IsValid)
                                                    hatchIds.Add(hatchId);
                                            }
                                        }
                                        catch { }
                                    }

                                }

                                short colorIndex = 1;
                                foreach (ObjectId layerId in createdLayers)
                                {
                                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);

                                    colorIndex = Convert.ToByte((colorIndex + 1) % 256);
                                }


                                drawOrder.MoveToBottom(hatchIds);


                                //GeologyConvertationCommand.ClosePalette(null, null);
                                ps.Visible = false;
                            }
                            tr.Commit();
                        }
                    }

                }
            }
            catch (System.Exception ex)
            {
                GeologyConvertationCommand.ClosePalette(null, null);
                CommonException(ex, "Ошибка при переводе масштаба профиля геологии");
            }
        }



        /// <summary>
        /// Добавление точек в полигон ограничивающий геологический элемент в новом масштабе
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="hatchPolygon"></param>
        /// <param name="groundSurfPoly"></param>
        /// <param name="baseVector"></param>
        /// <param name="startHorScaling"></param>
        /// <param name="startVertScaling"></param>
        /// <param name="depthMultipier"></param>
        private static void ProcessLineSegmentOfHatch(Point2d p0, Point2d p1, List<Point2d> hatchPolygon,
            List<Point2d> groundSurfPoly, /*Vector2d baseVector, double startHorScaling, double startVertScaling,*/
            Matrix2d transform, double depthMultipier)
        {
            //пересчет точек относительно базовой точки + масштабные коэффициенты
            //p0 = p0 - baseVector;
            //p0 = new Point2d(p0.X * startHorScaling, p0.Y * startVertScaling);
            //p1 = p1 - baseVector;
            //p1 = new Point2d(p1.X * startHorScaling, p1.Y * startVertScaling);
            p0 = transform * p0;
            p1 = transform * p1;

            //для рассчета заглублений сначала сориентировать точки по возрастанию координаты X
            bool swapped = false;
            if (p1.X < p0.X)
            {
                Point2d temp = p1;
                p1 = p0;
                p0 = temp;
                swapped = true;
            }

            //найти минимальный интервал точек полилинии, в который попадает интевал p0 - p1
            int[] interval = GetSurfPolyInterval(p0.X, p1.X, groundSurfPoly);
            //расчитать точки геологии по заглублению относительно линии поверхности земли!
            //расчет заглубления должен учитывать StartVertSoilScaling (depthMultipier)
            List<Point2d> polygonPts = CalcGeologPtsByDepth(p0, p1, interval, groundSurfPoly, depthMultipier);

            if (polygonPts.Count > 0)
            {
                //если точки меняли местами, то развернуть список расчитанных точек
                if (swapped)
                {
                    polygonPts.Reverse();
                }
                //Если последняя точка в полигоне равна первой в добавляемом наборе, то убрать из добавляемого набора первую точку
                if (hatchPolygon.Count > 0 && hatchPolygon.Last().IsEqualTo(polygonPts.First()))
                {
                    polygonPts = polygonPts.GetRange(1, polygonPts.Count - 1);
                }

                hatchPolygon.AddRange(polygonPts);
            }

        }

        /// <summary>
        /// Нахождение интервала точек полилинии поверхности в который попадает указанный интервал
        /// с помощью бинарного поиска
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="groundSurfPoly"></param>
        /// <returns></returns>
        private static int[] GetSurfPolyInterval(double start, double end, List<Point2d> groundSurfPoly)
        {
            int low = 0;
            int high = groundSurfPoly.Count;

            //либо хотябы одна точка полилинии находится между start и end и тогла нужно найти ее, а затем последовательно перебрать соседние справа и слева
            //либо обе точки start и end находятся между 2-х соседних точек полилинии и тогда low становится больше или равен high
            int mid = -1;
            while (low < high)
            {
                mid = (low + high) / 2;
                if (groundSurfPoly[mid].X < start)
                {
                    low = mid + 1;
                }
                else if (groundSurfPoly[mid].X > end)
                {
                    high = mid - 1;
                }
                else
                {
                    //найдена точка полилинии между start и end
                    low = mid;
                    high = mid;
                    break;
                }
            }

            if (low > high)
            {
                int temp = low;
                low = high;
                high = temp;
            }


            if (low < 0) low = 0;
            if (low == 0 && high <= low) high = low + 1;

            if (high > groundSurfPoly.Count - 1) high = groundSurfPoly.Count - 1;
            if (high == groundSurfPoly.Count - 1 && low >= high) low = high - 1;

            while (low > 0 && groundSurfPoly[low].X > start) low--;
            while (high < groundSurfPoly.Count - 1 && groundSurfPoly[high].X < end) high++;

            return new int[] { low, high };
        }

        private static List<Point2d> CalcGeologPtsByDepth(Point2d p0, Point2d p1, int[] interval,
            List<Point2d> groundSurfPoly, double depthMultipier)
        {
            List<Point2d> polygonPts = new List<Point2d>();

            LineSegment2d geologSegment = new LineSegment2d(p0, p1);

            //начало отрезка
            LineSegment2d surfSegment0 = new LineSegment2d(groundSurfPoly[interval[0]], groundSurfPoly[interval[0] + 1]);
            Line2d vert0 = new Line2d(p0, Vector2d.YAxis);
            CurveCurveIntersector2d intersector0 = new CurveCurveIntersector2d(vert0, surfSegment0);
            if (intersector0.NumberOfIntersectionPoints > 0)
            {
                Point2d surfPt0 = intersector0.GetPointOnCurve2(0).Point;
                double depth0 = (surfPt0.Y - p0.Y) * depthMultipier;
                Point2d convertedGeologPt0 = surfPt0 - Vector2d.YAxis * depth0;
                polygonPts.Add(convertedGeologPt0);
            }

            //все точки поверхности между началом и концом отрезка
            if (!geologSegment.Direction.IsParallelTo(Vector2d.YAxis))
            {
                for (int i = interval[0]; i <= interval[1]; i++)
                {
                    Point2d surfPt = groundSurfPoly[i];
                    Line2d vert = new Line2d(surfPt, Vector2d.YAxis);
                    CurveCurveIntersector2d intersector = new CurveCurveIntersector2d(vert, geologSegment);
                    if (intersector.NumberOfIntersectionPoints > 0)
                    {
                        Point2d geologPt = intersector.GetPointOnCurve2(0).Point;
                        double depth = (surfPt.Y - geologPt.Y) * depthMultipier;
                        Point2d convertedGeologPt = surfPt - Vector2d.YAxis * depth;
                        polygonPts.Add(convertedGeologPt);
                    }
                }
            }


            //конец отрезка
            LineSegment2d surfSegment1 = new LineSegment2d(groundSurfPoly[interval[1] - 1], groundSurfPoly[interval[1]]);
            Line2d vert1 = new Line2d(p1, Vector2d.YAxis);
            CurveCurveIntersector2d intersector1 = new CurveCurveIntersector2d(vert1, surfSegment1);
            if (intersector1.NumberOfIntersectionPoints > 0)
            {
                Point2d surfPt1 = intersector1.GetPointOnCurve2(0).Point;
                double depth1 = (surfPt1.Y - p1.Y) * depthMultipier;
                Point2d convertedGeologPt1 = surfPt1 - Vector2d.YAxis * depth1;
                polygonPts.Add(convertedGeologPt1);
            }

            return polygonPts;
        }



        private class HatchData
        {
            public List<List<Point2d>> Polygons { get; private set; } = new List<List<Point2d>>();

            public Hatch Hatch { get; private set; }

            public HatchData(Hatch hatch)
            {
                Hatch = hatch;
            }
        }
    }
}
