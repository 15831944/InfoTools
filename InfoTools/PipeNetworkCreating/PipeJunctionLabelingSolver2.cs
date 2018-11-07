using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    public partial class PipeNetworkGraph
    {
        private class PipeJunctionLabelingSolver2
        {

            public Dictionary<NetworkEdge, TextPosition> LabelingResult { get; private set; }
                = null;

            private double sumDist = double.PositiveInfinity;

            private NetworkNode nn;
            List<TextPosition> lblsNearNN;

            private Dictionary<NetworkEdge, NetworkEdgeWrapper> newrs
                = new Dictionary<NetworkEdge, NetworkEdgeWrapper>();

            private Dictionary<TextPosition, TextPositionWrapper> txtwrs
                = new Dictionary<TextPosition, TextPositionWrapper>();


            public PipeJunctionLabelingSolver2(NetworkNode nn,
                List<TextPosition> lblsNearNN)
            {
                this.nn = nn;
                this.lblsNearNN = new List<TextPosition>(lblsNearNN);

                //Формирование списков приоритетных соотношений
                //между присоединениями к колодцу и подписями присоединений
                foreach (NetworkEdge ne in nn.AttachedEdges)
                {
                    bool start = ne.StartNode == nn;
                    NetworkEdgeWrapper newr = new NetworkEdgeWrapper(ne, start);
                    newrs.Add(ne, newr);

                    foreach (TextPosition txt in lblsNearNN)
                    {
                        Relation relation = new Relation(ne, start, txt);
                        newr.RelationsPriority.Add(relation);
                        TextPositionWrapper txtWr = null;
                        txtwrs.TryGetValue(txt, out txtWr);
                        if (txtWr == null)
                        {
                            txtWr = new TextPositionWrapper(txt);
                            txtwrs[txt] = txtWr;
                        }
                        txtWr.RelationsPriority.Add(relation);
                    }

                    newr.RelationsPriority.Sort();
                }

                foreach (KeyValuePair<TextPosition, TextPositionWrapper> kvp in txtwrs)
                {
                    kvp.Value.RelationsPriority.Sort();
                }

                //Определить количество наиболее вероятных конкурирующих вариантов
                //Если обнаружено, что 1 текстовая метка является ближайшей к двум присоединениям,
                //то у этих присоединений количество конкурирующих вариантов подписей +1
                //Для текстовой метки количество конкурирующих вариантов равно количеству присоединений,
                //для которых она является ближайшей
                Dictionary<TextPosition, HashSet<NetworkEdgeWrapper>> txtPossibleOwners
                    = new Dictionary<TextPosition, HashSet<NetworkEdgeWrapper>>();
                foreach (NetworkEdgeWrapper newr in newrs.Values)
                {
                    //NetworkEdge edge = newr.NetworkEdge;

                    //
                    foreach (Relation relation in newr.RelationsPriority)
                    {
                        //принимать как конкурирующие все тексты на определеннном расстоянии,
                        //но не менее 1
                        if (relation.Distance > JUNCTION_LBL_COMPATITORS_DISTANCE && newr.CompCount > 0)
                        {
                            break;
                        }


                        newr.CompCount++;

                        TextPosition txtKey = relation.TextPosition;

                        HashSet<NetworkEdgeWrapper> possibleOwners = null;
                        txtPossibleOwners.TryGetValue(txtKey, out possibleOwners);
                        bool hasCompatitors = possibleOwners != null;
                        if (!hasCompatitors)
                        {
                            possibleOwners = new HashSet<NetworkEdgeWrapper>();
                            txtPossibleOwners.Add(txtKey, possibleOwners);
                        }

                        if (!possibleOwners.Contains(newr))
                            possibleOwners.Add(newr);

                        if (hasCompatitors)
                        {
                            txtwrs[txtKey].CompCount = possibleOwners.Count;

                            foreach (NetworkEdgeWrapper comp in possibleOwners)
                            {
                                comp.CompCount++;
                            }
                        }
                    }


                }


                //Поиск перебором сопоставления пар присоединение-текстовая метка,
                //которое дает наименьшее суммарное расстояние от текста до присоединения

                if (lblsNearNN.Count >= nn.AttachedEdges.Count)//зависит от того, чего больше: подписей или присоединений
                {
                    List<ISolverWrapper> edgeTxtSearchSeq
                    = newrs.Values.Cast<ISolverWrapper>().ToList();
                    edgeTxtSearchSeq.Sort();
                    invalidNumberCombinationResults = 0;
                    BruteForceSearch(edgeTxtSearchSeq, 0, new HashSet<NetworkEdge>(),
                        new HashSet<TextPosition>(), new HashSet<int>(),
                        new Dictionary<NetworkEdge, TextPosition>(), 0);
                }
                else
                {
                    List<ISolverWrapper> txtEdgeSearchSeq
                    = txtwrs.Values.Cast<ISolverWrapper>().ToList();
                    txtEdgeSearchSeq.Sort();
                    invalidNumberCombinationResults = 0;
                    BruteForceSearch(txtEdgeSearchSeq, 0, new HashSet<NetworkEdge>(),
                        new HashSet<TextPosition>(), new HashSet<int>(),
                        new Dictionary<NetworkEdge, TextPosition>(), 0);
                }

            }


            //TODO: Подумать. Это очень сомнительное решение, но оно вроде работает:
            //Если в процессе перебора какие-то варианты были отбракованы из-за неправильного сочетания цифр,
            //то прибавлять их количество к CompCount на всех уровнях рекурсии
            private int invalidNumberCombinationResults = 0;

            private void BruteForceSearch(List<ISolverWrapper> searchSeq, int n,
                HashSet<NetworkEdge> edgesOut, HashSet<TextPosition> txtOut,
                HashSet<int> txtContentOut, Dictionary<NetworkEdge, TextPosition> result,
                double sumDist)
            {
                //NetworkEdgeWrapper newr = searchSeq[n];
                ISolverWrapper w = searchSeq[n];
                List<Relation> priorityList = w.RelationsPriority;

                bool possiblePairFound = false;
                int i = 0;

                
                //перебрать все конкурирующие варианты,
                //если допустимая пара не найдена, то все оставшиеся
                //до тех пор пока допустимая пара не будет найдена 
                while ((i < w.CompCount + invalidNumberCombinationResults && i < priorityList.Count)//если какие-то варианты были отбракованы из-за неправильного сочетания цифр, то прибавлять их количество к CompCount
                    || (!possiblePairFound && i < priorityList.Count))
                {
                    Relation relation = priorityList[i];
                    NetworkEdge edge = relation.NetworkEdge;
                    TextPosition txt = relation.TextPosition;
                    int txtContent = Convert.ToInt32(txt.TextContent.Trim());
                    if (!txtOut.Contains(txt) //не допускать один и тот же текст
                    && !txtContentOut.Contains(txtContent)//не допускать 2 одинаковые цифры
                    && !edgesOut.Contains(edge) //не допускать повторение ребер
                    )
                    {
                        possiblePairFound = true;
                        HashSet<NetworkEdge> edgesOutNextStep
                            = new HashSet<NetworkEdge>(edgesOut);
                        HashSet<TextPosition> txtOutNextStep
                            = new HashSet<TextPosition>(txtOut);
                        HashSet<int> txtContentOutNextStep
                            = new HashSet<int>(txtContentOut);
                        Dictionary<NetworkEdge, TextPosition> resultNextStep
                            = new Dictionary<NetworkEdge, TextPosition>(result);


                        edgesOutNextStep.Add(edge);
                        txtOutNextStep.Add(txt);
                        txtContentOutNextStep.Add(txtContent);
                        resultNextStep.Add(edge, txt);
                        double sumDistNextStep = sumDist + relation.Distance;

                        if (n != searchSeq.Count - 1)
                        {
                            BruteForceSearch(searchSeq, n + 1, edgesOutNextStep,
                                txtOutNextStep, txtContentOutNextStep, resultNextStep, sumDistNextStep);
                        }
                        else
                        {
                            CheckCandidateResult(resultNextStep, sumDistNextStep);
                        }
                    }



                    i++;
                }

                if (!possiblePairFound)
                {
                    //данный проход завершен
                    CheckCandidateResult(result, sumDist);
                }
            }

            private bool CheckCandidateResult(
                Dictionary<NetworkEdge, TextPosition> result,
                double sumDist)
            {
                if (LabelingResult != null && LabelingResult.Count > result.Count)
                {
                    return false;//если уже есть вариант с большим количеством сопоставленных меток, то новый вариант не подходит
                }

                //Отбраковывать варианты в которых не идут подряд цифры
                //от единицы до какой-то другой без повторений и разрывов
                SortedSet<int> numbers = new SortedSet<int>();
                foreach (KeyValuePair<NetworkEdge, TextPosition> kvp in result)
                {
                    int n = Convert.ToInt32(kvp.Value.TextContent);
                    if (numbers.Contains(n))
                    {
                        invalidNumberCombinationResults++;
                        return false;//повторение двух одинаковых цифр
                    } 
                    numbers.Add(n);
                }
                int checkN = 1;
                foreach (int i in numbers)
                {
                    if (i != checkN)
                    {
                        invalidNumberCombinationResults++;
                        return false;//цифра пропущена
                    }
                    checkN++;
                }


                if (sumDist < this.sumDist)
                {
                    LabelingResult = result;
                    this.sumDist = sumDist;
                    return true;
                }
                return false;
            }

            private interface ISolverWrapper : IComparable<ISolverWrapper>
            {
                List<Relation> RelationsPriority { get; set; }

                int CompCount { get; set; }
            }

            private class NetworkEdgeWrapper : ISolverWrapper, IComparable<ISolverWrapper>
            {
                public NetworkEdge NetworkEdge { get; private set; }

                public bool Start { get; private set; }

                public List<Relation> RelationsPriority { get; set; } = new List<Relation>();

                private int textCompCount = 0;
                public int CompCount
                {
                    get
                    {
                        return textCompCount;
                    }
                    set
                    {
                        if (value > 1 && value > RelationsPriority.Count)
                            value = RelationsPriority.Count;
                        if (value < 1)
                            value = 1;
                        textCompCount = value;
                    }
                }

                public NetworkEdgeWrapper(NetworkEdge edge, bool start)
                {
                    NetworkEdge = edge;
                    Start = start;
                }

                public override int GetHashCode()
                {
                    return NetworkEdge.GetHashCode();
                }

                public int CompareTo(ISolverWrapper other)
                {
                    return this.CompCount.CompareTo(other.CompCount);
                }
            }

            private class TextPositionWrapper : ISolverWrapper, IComparable<ISolverWrapper>
            {
                public TextPosition TextPosition { get; private set; }

                public List<Relation> RelationsPriority { get; set; } = new List<Relation>();

                private int edgeCompCount = 0;
                public int CompCount
                {
                    get
                    {
                        return edgeCompCount;
                    }
                    set
                    {
                        if (value > 1 && value > RelationsPriority.Count)
                            value = RelationsPriority.Count;
                        if (value < 1)
                            value = 1;
                        edgeCompCount = value;
                    }
                }

                public TextPositionWrapper(TextPosition txt)
                {
                    TextPosition = txt;
                }

                public override int GetHashCode()
                {
                    return TextPosition.GetHashCode();
                }

                public int CompareTo(ISolverWrapper other)
                {
                    return this.CompCount.CompareTo(other.CompCount);
                }
            }


            /// <summary>
            /// Соотношение между присоединением к колодцу и подписью присоединения
            /// к колодцу
            /// </summary>
            private class Relation : IComparable<Relation>
            {
                public NetworkEdge NetworkEdge { get; private set; }

                public bool NetworkEdgeByStart { get; private set; }

                public TextPosition TextPosition { get; private set; }

                public double Distance { get; set; }

                public double CenterDistance { get; set; }

                public bool HasNormalPriority { get; set; } = true;

                public Relation(NetworkEdge edge, bool start, TextPosition txt)
                {
                    NetworkEdge = edge;
                    NetworkEdgeByStart = start;
                    TextPosition = txt;

                    Point3d txtPt = TextPosition.GetCenter();
                    Polyline poly = (Polyline)NetworkEdge.PolyId.GetObject(OpenMode.ForRead);
                    using (Polyline polyClone = (Polyline)poly.Clone())
                    {
                        polyClone.Elevation = 0;//на случай если у полилинии задана высота

                        //расчитать расстояние до коробки от полилинии с помощью API AutoCAD
                        Distance = DistanceFromPolyToTxtBox(polyClone, TextPosition);

                        Point3d closestPt = polyClone.GetClosestPointTo(txtPt, false);
                        double param = polyClone.GetParameterAtPoint(closestPt);
                        double startParam = polyClone.StartParam;
                        double endParam = polyClone.EndParam;
                        double averageParam = (startParam + endParam) / 2;
                        bool inclinesToStartOfPolyline = param <= averageParam;

                        NetworkNode oppositeNode = NetworkEdgeByStart ? 
                            NetworkEdge.EndNode : NetworkEdge.StartNode;
                        
                        bool oppositeNodeHaveSeveralAttachedEdges = oppositeNode.AttachedEdges.Count>1;

                        if ((NetworkEdgeByStart && param == startParam) || (!NetworkEdgeByStart && param == endParam)//текст находится c другой стороны колодца
                        || Distance > LBL_TOO_FAR_FROM_LINE)//расстояние до текста явно слишком большое
                            HasNormalPriority = false;
                        else if (
                            //polyClone.Length > EDGE_LENGTH_LBL_LONGITUDINAL_POSITION_MATTERS//если полилиния не слишком короткая
                            //&&
                            oppositeNodeHaveSeveralAttachedEdges//к соседнему узлу подходит более 1 ребра
                            )
                            HasNormalPriority = NetworkEdgeByStart == inclinesToStartOfPolyline;//текстовая метка ближе к текущему узлу, чем к соседнему

                        //расстояние до центра текстового примитива
                        CenterDistance = closestPt.DistanceTo(txtPt);
                    }

                }



                private double DistanceFromPolyToTxtBox(Polyline poly, TextPosition txtPos)
                {
                    Point2d maxPt = new Point2d(txtPos.Envelope.MaxX, txtPos.Envelope.MaxY);
                    Point2d minPt = new Point2d(txtPos.Envelope.MinX, txtPos.Envelope.MinY);
                    //хотябы одна точка полилинии находится внутри коробки? (возможен случай когда вся полилиния внутри коробки)
                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        if (Utils.PointInsideBoundingBox(maxPt, minPt, poly.GetPoint2dAt(i)))
                            return 0;
                    }

                    Curve3d polyGeCurve = poly.GetGeCurve();

                    //тогда найти расстояние между кривыми
                    return polyGeCurve.GetDistanceTo(txtPos.BoxCurve);
                }

                public int CompareTo(Relation other)
                {
                    if (this.HasNormalPriority && !other.HasNormalPriority)
                    {
                        return -1;
                    }
                    else if (!this.HasNormalPriority && other.HasNormalPriority)
                    {
                        return 1;
                    }

                    int comparison = this.Distance.CompareTo(other.Distance);
                    if (comparison == 0)
                    {
                        comparison = this.CenterDistance.CompareTo(other.CenterDistance);
                    }
                    return comparison;
                }
            }

        }
    }
}
