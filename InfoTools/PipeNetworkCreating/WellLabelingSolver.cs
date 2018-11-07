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
        private class WellLabelingSolver
        {
            private StructurePosition mainStruct = null;

            public TextPosition TextResult { get; private set; } = null;

            public Dictionary<StructurePosition, TextPosition> labelingResult = null;
            private double sumDist = double.PositiveInfinity;

            private Dictionary<StructurePosition, StructurePositionWrapper> spws
                = new Dictionary<StructurePosition, StructurePositionWrapper>();

            private Dictionary<TextPosition, TextPositionWrapper> txtwrs
                = new Dictionary<TextPosition, TextPositionWrapper>();

            public WellLabelingSolver(StructurePosition mainStruct,//тот блок, который обязательно должен получить пару
                IReadOnlyList<StructurePosition> structNearNode,
                IReadOnlyList<TextPosition> lblsNearNode)
            {
                this.mainStruct = mainStruct;

                //Формирование списков приоритетных соотношений
                //между блоками колодцев и подписями блоков
                foreach (StructurePosition str in structNearNode)
                {
                    StructurePositionWrapper spw = new StructurePositionWrapper(str);
                    spws.Add(str, spw);
                    foreach (TextPosition txt in lblsNearNode)
                    {
                        Relation relation = new Relation(str, txt);
                        spw.RelationsPriority.Add(relation);
                        TextPositionWrapper txtWr = null;
                        txtwrs.TryGetValue(txt, out txtWr);
                        if (txtWr == null)
                        {
                            txtWr = new TextPositionWrapper(txt);
                            txtwrs[txt] = txtWr;
                        }
                        txtWr.RelationsPriority.Add(relation);
                    }

                    spw.RelationsPriority.Sort();
                }

                foreach (KeyValuePair<TextPosition, TextPositionWrapper> kvp in txtwrs)
                {
                    kvp.Value.RelationsPriority.Sort();
                }

                //Определить количество наиболее вероятных конкурирующих вариантов
                //Если обнаружено, что 1 текстовая метка является ближайшей к двум колодцам,
                //то у этих колодцев количество конкурирующих вариантов подписей +1
                //Для текстовой метки количество конкурирующих вариантов равно количеству колодцев,
                //для которых она является ближайшей
                Dictionary<TextPosition, HashSet<StructurePositionWrapper>> txtPossibleOwners
                    = new Dictionary<TextPosition, HashSet<StructurePositionWrapper>>();
                foreach (StructurePositionWrapper spw in spws.Values)
                {
                    foreach (Relation relation in spw.RelationsPriority)
                    {
                        //принимать как конкурирующие все тексты на определеннном расстоянии,
                        //но не менее 1
                        if (relation.Distance > WELL_LBL_COMPATITORS_DISTANCE && spw.CompCount > 0)
                        {
                            break;
                        }

                        spw.CompCount++;

                        TextPosition txtKey = relation.TextPosition;
                        HashSet<StructurePositionWrapper> possibleOwners = null;
                        txtPossibleOwners.TryGetValue(txtKey, out possibleOwners);
                        bool hasCompatitors = possibleOwners != null;
                        if (!hasCompatitors)
                        {
                            possibleOwners = new HashSet<StructurePositionWrapper>();
                            txtPossibleOwners.Add(txtKey, possibleOwners);
                        }

                        if (!possibleOwners.Contains(spw))
                            possibleOwners.Add(spw);

                        if (hasCompatitors)
                        {
                            txtwrs[txtKey].CompCount = possibleOwners.Count;

                            foreach (StructurePositionWrapper comp in possibleOwners)
                            {
                                comp.CompCount++;
                            }
                        }
                    }
                }

                //Поиск перебором сопоставления пар колодец-текстовая метка,
                //которое дает наименьшее суммарное расстояние от текста до колодца
                if(lblsNearNode.Count>0 && structNearNode.Count > 0)
                {
                    if (lblsNearNode.Count >= structNearNode.Count)
                    {
                        List<ISolverWrapper> structTxtSearchSeq
                        = spws.Values.Cast<ISolverWrapper>().ToList();
                        structTxtSearchSeq.Sort();

                        //invalidCombinationResults = 0;
                        BruteForceSearch(structTxtSearchSeq, 0, new HashSet<StructurePosition>(), new HashSet<TextPosition>(), new Dictionary<StructurePosition, TextPosition>(), 0);
                    }
                    else
                    {
                        List<ISolverWrapper> txtStructSearchSeq
                        = txtwrs.Values.Cast<ISolverWrapper>().ToList();
                        txtStructSearchSeq.Sort();
                        //invalidCombinationResults = 0;
                        BruteForceSearch(txtStructSearchSeq, 0, new HashSet<StructurePosition>(), new HashSet<TextPosition>(), new Dictionary<StructurePosition, TextPosition>(), 0);
                    }
                }
                
            }


            //private int invalidCombinationResults = 0;
            private void BruteForceSearch(List<ISolverWrapper> searchSeq, int n,
                HashSet<StructurePosition> strOut, HashSet<TextPosition> txtOut,
                Dictionary<StructurePosition, TextPosition> result, double sumDist)
            {
                ISolverWrapper w = searchSeq[n];
                List<Relation> priorityList = w.RelationsPriority;

                bool possiblePairFound = false;
                int i = 0;

                //перебрать все конкурирующие варианты,
                //если допустимая пара не найдена, то все оставшиеся
                //до тех пор пока допустимая пара не будет найдена
                while (
                    //(
                    i < w.CompCount
                    //+ invalidCombinationResults && i < priorityList.Count)
                    ||
                    (!possiblePairFound &&
                    i < priorityList.Count
                    )
                    )
                {
                    Relation relation = priorityList[i];
                    StructurePosition str = relation.StructurePosition;
                    TextPosition txt = relation.TextPosition;

                    if (!txtOut.Contains(txt) && !strOut.Contains(str))
                    {
                        possiblePairFound = true;

                        HashSet<StructurePosition> strOutNextStep = new HashSet<StructurePosition>(strOut);
                        HashSet<TextPosition> txtOutNextStep = new HashSet<TextPosition>(txtOut);
                        Dictionary<StructurePosition, TextPosition> resultNextStep = new Dictionary<StructurePosition, TextPosition>(result);

                        strOutNextStep.Add(str);
                        txtOutNextStep.Add(txt);
                        resultNextStep.Add(str, txt);
                        double sumDistNextStep = sumDist + relation.Distance;

                        if (n != searchSeq.Count - 1)
                        {
                            BruteForceSearch(searchSeq, n + 1, strOutNextStep, txtOutNextStep, resultNextStep, sumDistNextStep);
                        }
                        else
                        {
                            CheckCandidateResult(resultNextStep, sumDistNextStep);
                        }

                    }

                    i++;
                }

                //if (!possiblePairFound)
                //{
                //    //данный проход завершен
                //    CheckCandidateResult(result, sumDist);
                //}
            }

            private bool CheckCandidateResult(
                Dictionary<StructurePosition, TextPosition> result,
                double sumDist)
            {
                if (labelingResult != null && labelingResult.Count > result.Count)
                {
                    return false;//если уже есть вариант с большим количеством сопоставленных меток, то новый вариант не подходит
                }

                if (!result.ContainsKey(mainStruct))//результат обязательно должен содержать данный блок
                {
                    //invalidCombinationResults++;
                    return false;
                }

                if (sumDist < this.sumDist)
                {
                    labelingResult = result;
                    this.sumDist = sumDist;
                    TextResult = result[mainStruct];
                    return true;
                }
                return false;
            }


            private interface ISolverWrapper : IComparable<ISolverWrapper>
            {
                List<Relation> RelationsPriority { get; set; }

                int CompCount { get; set; }
            }

            private class StructurePositionWrapper : ISolverWrapper, IComparable<ISolverWrapper>
            {
                public StructurePosition StructurePosition { get; private set; }

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

                public StructurePositionWrapper(StructurePosition str)
                {
                    StructurePosition = str;
                }

                public override int GetHashCode()
                {
                    return StructurePosition.GetHashCode();
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

                private int structCompCount = 0;
                public int CompCount
                {
                    get
                    {
                        return structCompCount;
                    }
                    set
                    {
                        if (value > 1 && value > RelationsPriority.Count)
                            value = RelationsPriority.Count;
                        if (value < 1)
                            value = 1;
                        structCompCount = value;
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
            /// Соотношение между блоком и текстом
            /// </summary>
            private class Relation : IComparable<Relation>
            {
                public StructurePosition StructurePosition { get; private set; }

                public TextPosition TextPosition { get; private set; }

                public double Distance { get; set; }

                public double CenterDistance { get; set; }

                public Relation(StructurePosition str, TextPosition txt)
                {
                    StructurePosition = str;
                    TextPosition = txt;

                    Point3d txtPt = TextPosition.GetCenter();
                    CompositeCurve3d txtCurve = TextPosition.BoxCurve;
                    Point3d blockPos = StructurePosition.Position;

                    //точка вставки блока находится внутри коробки текста?
                    Point2d maxPt = new Point2d(txt.Envelope.MaxX, txt.Envelope.MaxY);
                    Point2d minPt = new Point2d(txt.Envelope.MinX, txt.Envelope.MinY);
                    if (Utils.PointInsideBoundingBox(maxPt, minPt, new Point2d(blockPos.X, blockPos.Y)))
                    {
                        Distance = 0;
                    }
                    else
                    {
                        Distance = txtCurve.GetDistanceTo(blockPos);
                    }

                    CenterDistance = txtPt.DistanceTo(blockPos);
                }

                public int CompareTo(Relation other)
                {
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
