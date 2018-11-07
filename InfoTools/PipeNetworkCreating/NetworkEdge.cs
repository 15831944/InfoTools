using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using CivilDB = Autodesk.Civil.DatabaseServices;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    public partial class PipeNetworkGraph
    {
        /// <summary>
        /// Ребро графа
        /// </summary>
        private class NetworkEdge
        {
            public NetworkNode StartNode { get; set; }

            public NetworkNode EndNode { get; set; }

            //данные из Excel если есть для 1-го присоединения
            public PipeJunctionData StartPipeJunctionData { get; set; }

            //данные из Excel если есть для 2-го присоединения
            public PipeJunctionData EndPipeJunctionData { get; set; }

            //точки полилинии
            public List<Point2d> PositionList { get; private set; }

            //2d кривая, состоящая из линейных сегментов согласно точкам полилинии 
            public CompositeCurve2d PositionCurve { get; private set; }

            //точки для создания трубопровода
            public List<XYZ> PipePositionList { get; set; } = new List<XYZ>();

            private double pipeHorizontalLength = 0;

            public ObjectId PolyId { get; private set; }

            public NetworkEdge(List<Point2d> polyPts, ObjectId polyId)
            {
                PositionList = polyPts;
                PolyId = polyId;

                CreatePositionCurve(polyPts);
            }

            private void CreatePositionCurve(List<Point2d> polyPts)
            {
                List<Curve2d> segments = new List<Curve2d>();

                for (int i = 0; i < polyPts.Count - 1; i++)
                {
                    Point2d p0 = polyPts[i];
                    Point2d p1 = polyPts[i + 1];

                    LineSegment2d segment = new LineSegment2d(p0, p1);
                    segments.Add(segment);
                }
                PositionCurve = new CompositeCurve2d(segments.ToArray());
            }

            /// <summary>
            /// Сращивание двух ребер, стыкующихся в одном узле
            /// </summary>
            /// <param name="nn"></param>
            public NetworkEdge(NetworkNode nn/*, Transaction tr, BlockTableRecord ms*/)
            {
                if (nn.AttachedEdges.Count != 2)
                {
                    throw new ArgumentException(nameof(nn));
                }

                NetworkEdge edge1 = nn.AttachedEdges[0];
                NetworkEdge edge2 = nn.AttachedEdges[1];
                bool edge1Start = edge1.StartNode == nn;
                bool edge2Start = edge2.StartNode == nn;

                StartNode = edge1Start ? edge1.EndNode : edge1.StartNode;
                EndNode = edge2Start ? edge2.EndNode : edge2.StartNode;

                StartPipeJunctionData = edge1Start ? edge1.EndPipeJunctionData : edge1.StartPipeJunctionData;
                EndPipeJunctionData = edge2Start ? edge2.EndPipeJunctionData : edge2.StartPipeJunctionData;

                //PositionList
                List<Point2d> posList1 = new List<Point2d>(edge1.PositionList);
                if (edge1Start)
                    posList1.Reverse();
                posList1.RemoveAt(posList1.Count - 1);
                List<Point2d> posList2 = new List<Point2d>(edge2.PositionList);
                if (!edge2Start)
                    posList2.Reverse();
                PositionList = posList1.Concat(posList2).ToList();

                CreatePositionCurve(PositionList);



                //переназначить ссылки на примыкающие ребра у соседних узлов!
                StartNode.AttachedEdges.Remove(edge1);
                StartNode.AttachedEdges.Add(this);
                EndNode.AttachedEdges.Remove(edge2);
                EndNode.AttachedEdges.Add(this);

            }

            //private bool joinedEdge = false;//test

            public void CalcPipePosition(Transaction tr, ObjectId tinSurfId, double defaultDepth, bool sameDepthEveryPt, BlockTableRecord ms)
            {
                //Нужно учитывать размеры колодцев при расчете положения ребра!
                //Колодцы бывают просто цилиндрические и просто коробчатые
                //Учитывать Rotation, BoundingShape, InnerDiameterOrWidth, InnerLength
                //Уточнить кривую в плане по которой идет труба с учетом размеров колодца

                //TODO: Как учесть, что блок прямоугольного колодца может не соответствовать колодцу по направлениям длины и ширины????

                PointOnCurve2d startSplitPt = null;
                PointOnCurve2d endSplitPt = null;
                CivilDB.Structure strStart = null;
                CivilDB.Structure strEnd = null;
                if (!StartNode.StructId.IsNull)
                {
                    strStart = (CivilDB.Structure)tr.GetObject(StartNode.StructId, OpenMode.ForRead);
                    //уточнить положение начала кривой в начале с учетом размеров колодца!
                    startSplitPt = GetSplitPt(strStart, true);
                }
                if (!EndNode.StructId.IsNull)
                {
                    strEnd = (CivilDB.Structure)tr.GetObject(EndNode.StructId, OpenMode.ForRead);
                    //уточнить положение конца кривой в начале с учетом размеров колодца!
                    endSplitPt = GetSplitPt(strEnd, false);
                }

                if (startSplitPt != null && endSplitPt != null && startSplitPt.Parameter >= endSplitPt.Parameter)
                {
                    //колодцы стоят вплотную друг к другу или залезают друг на друга. Места для трубы на остается
                    return;
                }

                //проход по составляющим кривой с отбрасыванием частей, отсекаемых колодцами
                //Curve2d.GetSplitCurves работает неправильно
                //Curve2d.Explode тоже не помогает
                if (startSplitPt == null)
                {
                    AddPtToPipePositionList(new XYZ(PositionCurve.StartPoint));
                }
                Curve2d[] segments = PositionCurve.GetCurves();
                double currParam = 0;
                foreach (Curve2d seg in segments)
                {
                    Interval interval = seg.GetInterval();
                    double len = seg.GetLength(interval.LowerBound, interval.UpperBound);

                    currParam += len;

                    //Если есть точка разбиения в начале и она еще не достигнута
                    if (startSplitPt != null)
                    {
                        if (startSplitPt.Parameter < currParam)
                        {
                            //точка разбиения находится на этой кривой. Ее нужно добавить в список
                            AddPtToPipePositionList(new XYZ(startSplitPt.Point));
                            startSplitPt = null;
                        }
                        else
                        {
                            //точка отсечения начала еще не достигнута, переход к следующей кривой
                            continue;
                        }

                    }


                    if (endSplitPt != null && endSplitPt.Parameter < currParam)
                    {
                        //точка разбиения находится на этой кривой. Ее нужно добавить в список
                        AddPtToPipePositionList(new XYZ(endSplitPt.Point));
                        endSplitPt = null;
                        break;//обход точек заканчивается
                    }

                    AddPtToPipePositionList(new XYZ(seg.EndPoint));

                }


                //Задание глубин заложения ребер по концам
                //- если не задана глубина заложения на одном из концов, сделать их равными
                //- если не задана глубина на обоих концах задать обоим концам глубину по умолчанию согласно вводу в окне
                CivilDB.TinSurface tinSurf = (CivilDB.TinSurface)tr.GetObject(tinSurfId, OpenMode.ForRead);
                double startElevByData = double.NegativeInfinity;
                double endElevByData = double.NegativeInfinity;

                if (StartPipeJunctionData != null && StartPipeJunctionData.JunctionLevel != double.NegativeInfinity)
                {
                    startElevByData = StartPipeJunctionData.JunctionLevel;
                    XYZ xyz = PipePositionList.First();
                    xyz.Z = startElevByData;
                }
                if (EndPipeJunctionData != null && EndPipeJunctionData.JunctionLevel != double.NegativeInfinity)
                {
                    endElevByData = EndPipeJunctionData.JunctionLevel;
                    XYZ xyz = PipePositionList.Last();
                    xyz.Z = endElevByData;
                }

                


                if (startElevByData != double.NegativeInfinity && endElevByData == double.NegativeInfinity)
                {
                    XYZ xyz = PipePositionList.Last();
                    xyz.Z = startElevByData;
                }
                else if (startElevByData == double.NegativeInfinity && endElevByData != double.NegativeInfinity)
                {
                    XYZ xyz = PipePositionList.First();
                    xyz.Z = endElevByData;
                }
                else if (startElevByData == double.NegativeInfinity && endElevByData == double.NegativeInfinity)
                {
                    XYZ xyz1 = PipePositionList.First();
                    SetElevBySurf(defaultDepth, tinSurf, xyz1);

                    XYZ xyz2 = PipePositionList.Last();
                    SetElevBySurf(defaultDepth, tinSurf, xyz2);
                }


                //- но не допускать, чтобы труба опускалась ниже дна колодца
                double sartElevByStr = double.NegativeInfinity;
                double endElevByStr = double.NegativeInfinity;
                if (strStart != null && strStart.SumpElevation > PipePositionList.First().Z)
                {
                    sartElevByStr = strStart.SumpElevation;
                    PipePositionList.First().Z = sartElevByStr;
                }

                if (strEnd != null && strEnd.SumpElevation > PipePositionList.Last().Z)
                {
                    endElevByStr = strEnd.SumpElevation;
                    PipePositionList.Last().Z = endElevByStr;
                }
                //после корректировки уточнить отметку соседней точки если по ней нет данных
                if (sartElevByStr != double.NegativeInfinity
                    && endElevByData == double.NegativeInfinity)
                {
                    XYZ xyz = PipePositionList.Last();
                    xyz.Z = sartElevByStr;
                }
                else if (startElevByData == double.NegativeInfinity
                    && endElevByStr != double.NegativeInfinity)
                {
                    XYZ xyz = PipePositionList.First();
                    xyz.Z = endElevByStr;
                }


                //Убедиться, что если в одном узле без колодца стыкуются несколько ребер, 
                //то в месте стыковки обязательно у всех ребер должна быть одинаковая отметка
                double neighborJunctElev = GetNeigborJuncElev(StartNode);
                XYZ startPos = PipePositionList.First();
                if (neighborJunctElev != double.NegativeInfinity && startPos.Z != neighborJunctElev)
                {
                    startPos.Z = neighborJunctElev;
                }

                neighborJunctElev = GetNeigborJuncElev(EndNode);
                XYZ endPos = PipePositionList.Last();
                if (neighborJunctElev != double.NegativeInfinity && endPos.Z != neighborJunctElev)
                {
                    endPos.Z = neighborJunctElev;
                }


                //Задание отметок промежуточных точек на ребрах сети (интерполяция либо относительно поверхности)
                if (PipePositionList.Count > 2)
                {
                    if (sameDepthEveryPt)
                    {

                        //одинаковая глубина относительно поверхности земли
                        //метод TinSurface.SampleElevations работает не так как надо! Он не дает подробного учета рельефа!
                        //точки полилинии
                        for (int i = 1; i < PipePositionList.Count - 1; i++)
                        {
                            XYZ xyz = PipePositionList[i];
                            SetElevBySurf(defaultDepth, tinSurf, xyz);
                        }


                        //Помимо углов поворотов нужно добавить промежуточные точки через 1 м для учета рельефа!


                        List<XYZ> positionListExtended = new List<XYZ>();
                        positionListExtended.Add(PipePositionList.First());
                        for (int i = 1; i < PipePositionList.Count; i++)
                        {
                            XYZ xyz0 = PipePositionList[i - 1];
                            XYZ xyz1 = PipePositionList[i];
                            double len = xyz1.Position2d.GetDistanceTo(xyz0.Position2d);
                            Vector2d vector = (xyz1.Position2d - xyz0.Position2d).GetNormal();
                            double currLen = 1;
                            while (currLen < len)
                            {
                                //добавление промежуточных точек
                                Point2d pt = xyz0.Position2d + vector * currLen;
                                XYZ intermediateXYZ = new XYZ(pt);
                                SetElevBySurf(defaultDepth, tinSurf, intermediateXYZ);
                                positionListExtended.Add(intermediateXYZ);

                                currLen += 1;
                            }
                            positionListExtended.Add(xyz1);
                        }
                        PipePositionList = positionListExtended;
                    }
                    else
                    {
                        //интерполяция между началом и концом
                        double startElev = startPos.Z;
                        double endElev = endPos.Z;
                        double elevDiff = endElev - startElev;
                        double currLength = 0;
                        for (int i = 1; i < PipePositionList.Count - 1; i++)
                        {
                            XYZ xyz = PipePositionList[i];

                            Point2d prevPt = PipePositionList[i - 1].Position2d;
                            currLength += prevPt.GetDistanceTo(xyz.Position2d);


                            xyz.Z = startElev + (elevDiff * currLength / pipeHorizontalLength);
                        }

                    }
                }

            }

            private void AddPtToPipePositionList(XYZ xyz)
            {
                if (PipePositionList.Count > 0)
                {
                    pipeHorizontalLength += PipePositionList.Last().Position2d.GetDistanceTo(xyz.Position2d);
                }
                PipePositionList.Add(xyz);
            }


            private double GetNeigborJuncElev(NetworkNode nn)
            {
                double neighborJunctElev = double.NegativeInfinity;

                if (nn.AttachedEdges.Count > 1 && nn.StructId.IsNull)
                {
                    //взять отметку из первого стыкующегося присоединения
                    foreach (NetworkEdge ne in
                        nn.AttachedEdges.Where(e => e.PipePositionList.Count > 0 && e != this))
                    {
                        bool start = ne.StartNode == nn;
                        neighborJunctElev = (start ? ne.PipePositionList[0] : ne.PipePositionList[ne.PipePositionList.Count - 1]).Z;
                    }
                }

                return neighborJunctElev;
            }

            private static void SetElevBySurf(double defaultDepth, CivilDB.TinSurface tinSurf, XYZ xyz)
            {
                double surfElev = 0;
                try
                {
                    surfElev = tinSurf.FindElevationAtXY(xyz.Position2d.X, xyz.Position2d.Y);
                }
                catch { }
                xyz.Z = surfElev - defaultDepth;
            }

            private PointOnCurve2d GetSplitPt(CivilDB.Structure str, bool start)
            {
                Curve2d strPosCurve = StructurePosCurve(str);
                CurveCurveIntersector2d intersector = new CurveCurveIntersector2d(PositionCurve, strPosCurve);
                if (intersector.NumberOfIntersectionPoints > 0)
                {
                    intersector.OrderWithRegardsTo1();
                    //взять точку пересечения, которая имеет имеет наибольший параметр PositionCurve для начальной точки
                    //и наименьший для конечной точки
                    return start ? intersector.GetPointOnCurve1(intersector.NumberOfIntersectionPoints - 1)
                        : intersector.GetPointOnCurve1(0);
                }
                return null;
            }

            private Curve2d StructurePosCurve(CivilDB.Structure str)
            {
                switch (str.BoundingShape)
                {
                    case CivilDB.BoundingShapeType.Cylinder:
                        return new CircularArc2d(new Point2d(str.Location.X, str.Location.Y), str.InnerDiameterOrWidth / 2);

                    case CivilDB.BoundingShapeType.Box:
                        //создать прямоугольник с учетом поворота и точки вставки
                        Vector2d loc = new Vector2d(str.Location.X, str.Location.Y);
                        Matrix2d rotation = Matrix2d.Rotation(str.Rotation, Point2d.Origin);
                        Point2d p0 = (new Point2d(-str.InnerDiameterOrWidth / 2, -str.InnerLength / 2)).TransformBy(rotation) + loc;
                        Point2d p1 = (new Point2d(-str.InnerDiameterOrWidth / 2, str.InnerLength / 2)).TransformBy(rotation) + loc;
                        Point2d p2 = (new Point2d(str.InnerDiameterOrWidth / 2, str.InnerLength / 2)).TransformBy(rotation) + loc;
                        Point2d p3 = (new Point2d(str.InnerDiameterOrWidth / 2, -str.InnerLength / 2)).TransformBy(rotation) + loc;

                        LineSegment2d side0 = new LineSegment2d(p0, p1);
                        LineSegment2d side1 = new LineSegment2d(p1, p2);
                        LineSegment2d side2 = new LineSegment2d(p2, p3);
                        LineSegment2d side3 = new LineSegment2d(p3, p0);

                        return new CompositeCurve2d(new Curve2d[] { side0, side1, side2, side3 });

                    default:
                        return null;
                }
            }

        }
    }
}
