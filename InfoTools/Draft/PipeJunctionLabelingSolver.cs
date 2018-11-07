using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Geometry;
using RBush;
using RBush.KnnUtility;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DInfoTools.PipeNetworkCreating
{

    public partial class PipeNetworkGraph
    {
        private class JunctionLabelMapping
        {
            public bool Start { get; private set; }
            public NetworkEdge NetworkEdge { get; private set; }

            /// <summary>
            /// Все подписи, отсортированные по близости к присоединению
            /// </summary>
            public List<LabelRelation> MappingPriorityList { get; private set; }
                = new List<LabelRelation>();

            public int CompatitorMappingsCount { get; set; } = 0;

            /// <summary>
            /// Верная подпись
            /// </summary>
            public TextPosition TrueMapping { get; set; }


            public JunctionLabelMapping(bool start, NetworkEdge edge,
                List<TextPosition> lblsNearNN)
            {
                Start = start;
                NetworkEdge = edge;

                //нужно внести корректировки в порядок LabelRelation в списке
                foreach (TextPosition txtPos in lblsNearNN
                    //LabelRelation lr in MappingPriorityList
                    )
                {
                    LabelRelation lr = new LabelRelation(txtPos);
                    MappingPriorityList.Add(lr);

                    Point3d txtPt = lr.TextPosition.GetCenter();

                    Polyline poly = (Polyline)NetworkEdge.PolyId.GetObject(OpenMode.ForRead);
                    using (Polyline polyClone = (Polyline)poly.Clone())
                    {
                        polyClone.Elevation = 0;//на случай если у полилинии задана высота


                        //расчитать расстояние до коробки от полилинии с помощью API AutoCAD
                        lr.Distance = DistanceFromPolyToTxtBox(polyClone, txtPos);


                        //Point3d txtPt3d = new Point3d(txtPt.X, txtPt.Y, 0);

                        Point3d closestPt = polyClone.GetClosestPointTo(txtPt, false);
                        double param = polyClone.GetParameterAtPoint(closestPt);
                        double startParam = polyClone.StartParam;
                        double endParam = polyClone.EndParam;
                        double averageParam = (startParam + endParam) / 2;
                        bool inclinesToStartOfPolyline = param <= averageParam;


                        //Нужно убедиться, что
                        //- текстовая метка в пределах линии
                        //- текстовая метка ближе к текущему узлу, чем к соседнему
                        //Если это не так, то опускать LabelRelation в конец списка
                        //double param = curve.GetClosestPointTo(txtPt3d).Parameter;
                        if (param == startParam || param == endParam
                            || lr.Distance > 2)// или если расстояние до текста явно слишком большое
                            lr.HasNormalPriority = false;
                        else
                            lr.HasNormalPriority = Start == inclinesToStartOfPolyline;//TODO: Только в том случае если на другом конце тоже находится колодец???


                        //расстояние до центра текстового примитива как второй фактор сортировки
                        lr.CenterDistance = closestPt.DistanceTo(txtPt);

                    }




                }



                MappingPriorityList.Sort();


            }

            //расчитать расстояние до коробки от полилинии с помощью API AutoCAD
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
                //полилиния пересекает коробку? ЭТО ИЗЛИШНЕ
                //CurveCurveIntersector3d intersector = new CurveCurveIntersector3d(polyGeCurve, txtPos.BoxCurve, Vector3d.ZAxis);
                //if (intersector.NumberOfIntersectionPoints>0)
                //{
                //    return 0;
                //}

                //тогда найти расстояние между кривыми
                return polyGeCurve.GetDistanceTo(txtPos.BoxCurve);
            }

            public override int GetHashCode()
            {
                return NetworkEdge.GetHashCode();
            }


            public class LabelRelation : IComparable<LabelRelation>
            {
                public TextPosition TextPosition { get; private set; }

                public double Distance { get; set; }

                public double CenterDistance { get; set; }


                /// <summary>
                /// Если true, то приоритетность подписи определяется по близости к трубе,
                /// если false, то должен опускаться в самый конец списка приоритетности
                /// </summary>
                public bool HasNormalPriority { get; set; } = true;


                public LabelRelation(TextPosition txt/*, double dist*/)
                {
                    TextPosition = txt;
                    //Distance = dist;
                }

                /// <summary>
                /// возвращает одинаковй хешкод для разных объектов JunctionLabelRelation 
                /// если они содержат ссылку на один и тот же объект TextPosition
                /// </summary>
                /// <returns></returns>
                public override int GetHashCode()
                {
                    return TextPosition.GetHashCode();
                }

                public int CompareTo(LabelRelation other)
                {
                    //если текст не имеет нормали к присоединению, то он при сортировке идет после, 
                    //того который не имеет
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




        private class PipeJunctionLabelingSolver
        {
            private List<JunctionLabelMapping> mappingsToSolve = null;

            private Dictionary<JunctionLabelMapping, int> mappingResult = null;

            private double mappingDistance = double.PositiveInfinity;//Суммарная длина до трубы и до узла сети

            private bool needReverseBruteForceSearch = false;

            public bool MappingSolved { get; private set; } = false;

            /// <summary>
            /// Сопоставить присоединения к колодцам и подписи к ним.
            /// Преобразует объекты полученного списка (присваивает значение свойству TrueMapping).
            /// </summary>
            /// <param name="mappingsToSolve"></param>
            public PipeJunctionLabelingSolver
                (List<JunctionLabelMapping> mappingsToSolve)
            {
                this.mappingsToSolve = mappingsToSolve;
                //Задачу можно сформулировать следующим образом:
                //Найти такое сочетание отношений линия - подпись, чтобы суммарное(или среднее)
                //расстояние от линий до подписей было минимальным

                //Каждая линия имеет ограниченное количество конкурирующих вариантов подписей
                //С учетом (или даже без учета) этого можно рассмотреть ориентированный граф (смотри файл PipeJunctionLabelingSolver.pdf)
                //Ребра помеченные 0 имеют нулевой вес
                //Далее задача сводится к Travelling Salesman Problem так как нужно посетить все узлы графа
                //http://synset.com/ai/ru/tsp/Salesman_Intro.html
                //http://www.or.deis.unibo.it/algottm/files/8_ATSP.pdf
                //http://www.jot.fm/issues/issue_2005_01/article5.pdf - решение на Java branch and bound
                //это все интересно, но слишком сложно

                //Поэтому применить другой подход:


                //Первый этап:
                //Для каждого отрезка подсчитать количество конкурирующих вариантов подписей
                //понятно, что подпись должна быть как можно ближе к отрезку
                //- Если у отрезка первые несколько подписей в MappingPriorityList имеют одинаковое расстояние,
                //  то у него CompatitorsCount равен количеству этих подписей
                //  HARDCODE: БРАТЬ ВСЕ ТЕКСТЫ НА РАССТОЯНИИ 0.1, НО НЕ МЕНЕЕ 1 ШТУКИ
                //- Если одна и та же подпись является ближайшей к 2 отрезкам, то у этих отрезков CompatitorsCount++

                Dictionary<int, HashSet<JunctionLabelMapping>> txtPossibleOwners
                    = new Dictionary<int, HashSet<JunctionLabelMapping>>();//ключ - хеш код LabelRelation 

                foreach (JunctionLabelMapping jlm in mappingsToSolve)
                {
                    bool oneCompFound = false;
                    foreach (JunctionLabelMapping.LabelRelation lr in jlm.MappingPriorityList)
                    {
                        //брать тексты с нормальным приоритетом на расстоянии 0,1
                        //БРАТЬ НЕ МЕНЕЕ 1 ТЕКСТА!!!
                        if (oneCompFound /*&&( lr.Distance > 0.1 || !lr.HasNormalPriority)*/)
                        {
                            break;
                        }


                        jlm.CompatitorMappingsCount++;
                        oneCompFound = true;

                        //- Если одна и та же подпись является ближайшей к 2 отрезкам,
                        //  то у этих отрезков CompatitorsCount++
                        int txtKey = lr.GetHashCode();
                        HashSet<JunctionLabelMapping> possibleOwners = null;
                        txtPossibleOwners.TryGetValue(txtKey, out possibleOwners);
                        bool hasCompatitors = possibleOwners != null;
                        if (!hasCompatitors)
                        {
                            possibleOwners = new HashSet<JunctionLabelMapping>();
                            txtPossibleOwners.Add(txtKey, possibleOwners);
                        }

                        if (!possibleOwners.Contains(jlm))
                            possibleOwners.Add(jlm);

                        if (hasCompatitors)
                        {
                            foreach (JunctionLabelMapping compatitorsJlm in possibleOwners)
                            {
                                if (compatitorsJlm.CompatitorMappingsCount
                                    < compatitorsJlm.MappingPriorityList.Count)
                                    compatitorsJlm.CompatitorMappingsCount++;
                            }
                        }

                    }

                }

                //отсортировать отрезки по возрастанию количества конкурирующих вариантов (например так {1,1,1,2,3})
                mappingsToSolve.Sort((a, b) => a.CompatitorMappingsCount.CompareTo(b.CompatitorMappingsCount));
                //это работает достаточно хорошо, но не для случая, когда текстов меньше чем присоединений к колодцу
                //отсортировать отрезки по близости первого текста в списке
                //mappingsToSolve.Sort((a, b) =>
                //a.MappingPriorityList.First().CompareTo(b.MappingPriorityList.First()));
                //это наоборот приводит к ошибкам в случае если текстов достаточно

                #region ненадежное решение
                //как правило окажется, что есть отрезки, у которых есть только один возможный вариант подписи
                //его можно присвоить сразу как правильный
                //если это не так, то такие подписи и визуально не разобрать (и можно присвоить как правильный любой текст на выбор)

                //подписи, присвоенные как единственно возможные для определенных отрезков,
                //были одним из вариантов для других отрезков
                //(соответственно теперь этот вариант для них отпал и остался один вариант, который им и присваивается)
                //HashSet<int> txtOut = new HashSet<int>();//хеш коды текстов, которые уже присвоены присоединениям
                //foreach (JunctionLabelMapping jlm in mappingsToSolve)
                //{
                //    //присвоить первую не занятую подпись
                //    TextPosition trueMapping
                //        = jlm.MappingPriorityList.Find(x => !txtOut.Contains(x.GetHashCode()))?.TextPosition;
                //    if (trueMapping != null)
                //    {
                //        jlm.TrueMapping = trueMapping;
                //        txtOut.Add(trueMapping.GetHashCode());
                //    }
                //} 
                #endregion

                //брутфорсный поиск сочетания, которое будет давать наименьшее суммарное
                //расстояние от отрезков до текстов
                BruteForceMapping(0, new HashSet<int>(), new HashSet<int>(), new Dictionary<JunctionLabelMapping, int>(), 0);
                if (needReverseBruteForceSearch)
                {
                    mappingsToSolve.Reverse();
                    BruteForceMapping(0, new HashSet<int>(), new HashSet<int>(), new Dictionary<JunctionLabelMapping, int>(), 0);
                }


                if (mappingResult != null)
                {
                    MappingSolved = true;
                    foreach (KeyValuePair<JunctionLabelMapping, int> kvp in mappingResult)
                    {
                        kvp.Key.TrueMapping = kvp.Key.MappingPriorityList[kvp.Value].TextPosition;
                    }
                }


            }





            private void BruteForceMapping(int jlmIndex, HashSet<int> txtOut, HashSet<int> txtOut2,
                Dictionary<JunctionLabelMapping, int> mapping, double sumDist)
            {
                JunctionLabelMapping jlm = mappingsToSolve[jlmIndex];

                bool possiblePairFound = false;
                for (int i = 0; i < jlm.CompatitorMappingsCount && i < jlm.MappingPriorityList.Count; i++)
                {
                    possiblePairFound = CheckLabelRelation(jlmIndex, txtOut, txtOut2, mapping, sumDist, jlm, i);
                }

                if (!possiblePairFound)
                {
                    needReverseBruteForceSearch = true;//выполнить поиск сочетаний в обратном порядке
                    //проверить оставшиеся варианты если есть пока не найдется возможная пара
                    for (int i = jlm.CompatitorMappingsCount; i < jlm.MappingPriorityList.Count; i++)
                    {
                        possiblePairFound = CheckLabelRelation(jlmIndex, txtOut, txtOut2, mapping, sumDist, jlm, i);
                        if (possiblePairFound) break;
                    }
                }

                if (!possiblePairFound)
                {
                    //текстов меньше чем присодинений
                    if (jlmIndex != mappingsToSolve.Count - 1)
                    {
                        //тем не менее перейти к следующему присоединению если это не последнее
                        BruteForceMapping(jlmIndex + 1, txtOut, txtOut2, mapping, sumDist);
                    }
                    else
                    {
                        //завершить проход если это последнее
                        if (CheckCandidateMapping(mapping, sumDist))
                        {
                            mappingResult = mapping;
                            mappingDistance = sumDist;
                        }
                    }
                }
            }

            private bool CheckLabelRelation(int jlmIndex, HashSet<int> txtOut, HashSet<int> txtOut2,
                Dictionary<JunctionLabelMapping, int> mapping, double sumDist, JunctionLabelMapping jlm, int i)
            {
                bool possiblePairFound = false;
                JunctionLabelMapping.LabelRelation lr = jlm.MappingPriorityList[i];
                int currLrHashCode = lr.GetHashCode();
                int currLrContent = Convert.ToInt32(lr.TextPosition.TextContent.Trim());
                if (!txtOut.Contains(currLrHashCode) //не допускать один и тот же текст
                    && !txtOut2.Contains(currLrContent))//не допускать 2 одинаковые цифры
                {
                    possiblePairFound = true;

                    Dictionary<JunctionLabelMapping, int> mappingNextStep
                    = new Dictionary<JunctionLabelMapping, int>(mapping);
                    HashSet<int> txtOutNextStep = new HashSet<int>(txtOut);
                    HashSet<int> txtOut2NextStep = new HashSet<int>(txtOut2);
                    double sumDistNextStep = sumDist + lr.Distance/* + lr.DistanceToNode*/;

                    txtOutNextStep.Add(currLrHashCode);
                    txtOut2NextStep.Add(currLrContent);
                    mappingNextStep.Add(jlm, i);


                    if (jlmIndex != mappingsToSolve.Count - 1)
                    {
                        //переход к следующему отрезку
                        BruteForceMapping(jlmIndex + 1, txtOutNextStep, txtOut2NextStep, mappingNextStep, sumDistNextStep);
                    }
                    else
                    {
                        //это последний отрезок в этом проходе
                        if (CheckCandidateMapping(mappingNextStep, sumDistNextStep))
                        {
                            mappingResult = mappingNextStep;
                            mappingDistance = sumDistNextStep;
                        }
                    }
                }

                return possiblePairFound;
            }



            private bool CheckCandidateMapping(Dictionary<JunctionLabelMapping, int> mapping, double currMappingDist)
            {
                if (mappingResult != null && mappingResult.Count > mapping.Count)
                {
                    return false;//если уже есть вариант с большим количеством сопоставленных меток, то новый вариант не подходит
                }

                //Отбраковывать варианты в которых не идут подряд цифры от единицы до какой-то другой без повторений и разрывов
                SortedSet<int> numbers = new SortedSet<int>();
                foreach (KeyValuePair<JunctionLabelMapping, int> kvp in mapping)
                {
                    int n = Convert.ToInt32(kvp.Key.MappingPriorityList[kvp.Value].TextPosition.TextContent);
                    if (numbers.Contains(n))
                        return false;
                    numbers.Add(n);
                }
                int checkN = 1;
                foreach (int i in numbers)
                {
                    if (i != checkN)
                        return false;
                    checkN++;
                }


                return currMappingDist < mappingDistance;
            }



        }
    }

}
