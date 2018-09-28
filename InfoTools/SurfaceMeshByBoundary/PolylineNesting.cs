using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civil3DInfoTools.SurfaceMeshByBoundary
{


    /// <summary>
    /// Дерево для хранения данных о вложенности полилиний
    /// Предполагается, что все линии замкнуты, не имеют самопересечений и пересечений друг с другом
    /// </summary>
    public class PolylineNesting : IDisposable
    {
        /// <summary>
        /// Корневой узел - фиктивный, не имеет полилинии
        /// Никогда не меняется
        /// </summary>
        public Node Root { get; private set; }

        /// <summary>
        /// Поверхность по которой строится сеть
        /// </summary>
        public TinSurface TinSurf { get; private set; }

        /// <summary>
        /// Внутренние вершины
        /// </summary>
        public HashSet<TinSurfaceVertex> InnerVerts = new HashSet<TinSurfaceVertex>();

        /// <summary>
        /// Внутренние треугольники
        /// </summary>
        public HashSet<TinSurfaceTriangle> InnerTriangles = new HashSet<TinSurfaceTriangle>();

        /// <summary>
        /// Графы треугольников
        /// </summary>
        public Dictionary<TinSurfaceTriangle, TriangleGraph> TriangleGraphs
            = new Dictionary<TinSurfaceTriangle, TriangleGraph>();

        /// <summary>
        /// Все точки полилиний
        /// </summary>
        public List<PolylinePt> PolylinePts { get; private set; } = new List<PolylinePt>();

        public PolylineNesting(TinSurface tinSurf)
        {
            Root = new Node(null, this);
            TinSurf = tinSurf;
        }

        /// <summary>
        /// Вставка новой полилинии
        /// </summary>
        /// <param name="polyline"></param>
        public void Insert(Polyline polyline)
        {
            Insert(Root, new Node(polyline, this));
        }

        private void Insert(Node node, Node insertingNode)
        {
            bool isNested = false;
            //Проверить вложена ли добавляемая полилиния в один из дочерних узлов
            foreach (Node nn in node.NestedNodes)
            {
                if (nn.IsNested(insertingNode))
                {
                    //рекурсия
                    Insert(nn, insertingNode);
                    isNested = true;
                    break;
                }
            }

            if (!isNested)
            {
                //Если полилиния не вложена в дочерние узлы, то проверить не вложены ли дочерние узлы в добавляемую полилинию
                for (int i = 0; i < node.NestedNodes.Count;)
                {
                    Node nn = node.NestedNodes[i];
                    if (insertingNode.IsNested(nn))
                    {
                        //Если вложена, то убрать из node.NestedNodes и добавить в insertingNode.NestedNodes
                        node.NestedNodes.Remove(nn);
                        insertingNode.NestedNodes.Add(nn);
                    }
                    else
                    {
                        i++;
                    }
                }

                //Добавить insertingNode в node.NestedNodes
                node.NestedNodes.Add(insertingNode);
            }

        }


        /// <summary>
        /// Расчет граней для построения сети по обертывающей к поверхности TIN
        /// </summary>
        /// <param name="tinSurf"></param>
        public void CalculatePoligons()
        {
            //Определить какие узлы представляют внешнюю границу, а какие внутреннюю
            ResolveOuterBoundary(Root, false);

            //Определить пересечения всех полилиний с ребрами поверхностей. Добавление точек полилиний в графы треугольников
            TraversePolylines(Root);

            //Расчитать координаты Z всех точек
            foreach (PolylinePt pt in PolylinePts)
            {
                pt.CalculateZ();
                pt.AddToPolylinePart();
            }

            //Уточнение графов треугольников
            foreach (TriangleGraph trGr in TriangleGraphs.Values)
            {
                //Заполнение данных о участках полилиний, попавших в треугольники
                //Удаление ненужных участков полилиний из графа
                trGr.ResolvePolylineParts();
            }
            //Удалить те графы треугольников, которые не содержат участков полилиний
            List<TinSurfaceTriangle> keysToRemove = new List<TinSurfaceTriangle>();
            foreach (KeyValuePair<TinSurfaceTriangle, TriangleGraph> kvp in TriangleGraphs)
            {
                TinSurfaceTriangle key = kvp.Key;
                if (kvp.Value.PolylineParts.Count == 0)
                {
                    keysToRemove.Add(key);
                }

            }
            foreach (TinSurfaceTriangle key in keysToRemove)
            {
                TriangleGraphs.Remove(key);
            }

            //Определить внутренние вершины и внутренние треугольники поверхности
            foreach (Node node in Root.NestedNodes)//Для каждой из внешних полилиний
            {
                HashSet<TinSurfaceVertex> vertsAllreadyChecked = new HashSet<TinSurfaceVertex>();
                HashSet<TinSurfaceTriangle> trianglesAllreadyChecked = new HashSet<TinSurfaceTriangle>();
                LinkedList<TinSurfaceTriangle> trianglesToCheck = new LinkedList<TinSurfaceTriangle>();
                foreach (TinSurfaceTriangle triangle in node.IntersectingTriangles)
                {
                    trianglesToCheck.AddLast(triangle);
                }

                for (LinkedListNode<TinSurfaceTriangle> lln = trianglesToCheck.First; lln != null; lln = lln.Next)
                {

                    TinSurfaceTriangle triangle = lln.Value;
                    //Проверить каждую вершину треугольника на попадание в полилинию
                    //Если вершина попала в полилинию, то каждый из примыкающих к ней треугольников добавить набор треугольников для проверки
                    //Если он не один из пересекаемых или уже проверенных треугольников
                    TinSurfaceVertex[] vertices = new TinSurfaceVertex[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };
                    foreach (TinSurfaceVertex vertex in vertices)
                    {
                        if (!vertsAllreadyChecked.Contains(vertex))//Если эта вершина еще не проверялась
                        {
                            Point2d vertLoc = new Point2d(vertex.Location.X, vertex.Location.Y);
                            if (Utils.PointIsInsidePolylineWindingNumber(vertLoc, node.Point2DCollection))
                            {
                                InnerVerts.Add(vertex);
                                foreach (TinSurfaceTriangle neighbor in vertex.Triangles)
                                {
                                    if (!node.IntersectingTriangles.Contains(neighbor)
                                        && !trianglesAllreadyChecked.Contains(neighbor))
                                    {
                                        trianglesToCheck.AddLast(neighbor);
                                    }
                                }
                            }

                            vertsAllreadyChecked.Add(vertex);
                        }


                    }

                    trianglesAllreadyChecked.Add(triangle);

                }
            }

            //Затем для контуров внутренних границ найти те вершины, которые попадают в вырезы в контуре
            HashSet<TinSurfaceVertex> outerVerts = new HashSet<TinSurfaceVertex>();
            GetOuterVerts(Root, InnerVerts, outerVerts);

            foreach (TinSurfaceVertex ov in outerVerts)
            {
                InnerVerts.Remove(ov);
            }
            //Определить внутренние треугольники.
            //Внутренние треугольники - те, у которых все вершины внутренние и они не пересекаются полилиниями
            foreach (TinSurfaceVertex iv in InnerVerts)
            {
                foreach (TinSurfaceTriangle triangle in iv.Triangles)
                {
                    if (InnerVerts.Contains(triangle.Vertex1)
                        && InnerVerts.Contains(triangle.Vertex2)
                        && InnerVerts.Contains(triangle.Vertex3)
                        && !TriangleGraphs.ContainsKey(triangle))
                    {
                        InnerTriangles.Add(triangle);
                    }
                }
            }

            foreach (TriangleGraph trGr in TriangleGraphs.Values)
            {
                trGr.CalculatePoligons();
            }

        }

        /// <summary>
        /// Создание сети по площади участка
        /// </summary>
        public SubDMesh CreateSubDMesh()
        {
            SubDMesh sdm = null;

            //Составаление общего списка полигонов
            Vector3d elevationVector = new Vector3d(0, 0, SurfaceMeshByBoundaryCommand.MeshElevation);
            List<List<Point3d>> poligons = new List<List<Point3d>>();
            //Внутренние треугольники
            foreach (TinSurfaceTriangle t in InnerTriangles)
            {
                Point3d pt1 = t.Vertex1.Location + elevationVector;
                Point3d pt2 = t.Vertex2.Location + elevationVector;
                Point3d pt3 = t.Vertex3.Location + elevationVector;

                poligons.Add(new List<Point3d>() { pt1, pt2, pt3 });
            }

            //Граничные полигоны
            foreach (TriangleGraph tg in TriangleGraphs.Values)
            {
                foreach (List<Point3d> pts in tg.Polygons)
                {
                    List<Point3d> poligon = new List<Point3d>();
                    foreach (Point3d pt in pts)
                    {
                        poligon.Add(pt + elevationVector);
                    }

                    poligons.Add(poligon);
                }

            }

            if (poligons.Count > 0)
            {
                //Заполнение коллекций для построения сети
                Point3dCollection vertarray = new Point3dCollection();
                Int32Collection facearray = new Int32Collection();

                Dictionary<Point3d, int> vertIndexes = new Dictionary<Point3d, int>();
                int currIndex = 0;
                foreach (List<Point3d> poligon in poligons)
                {
                    //Добавление точек
                    foreach (Point3d pt in poligon)
                    {
                        if (!vertIndexes.ContainsKey(pt))//добавлять точку если ее еще нет
                        {
                            vertarray.Add(pt);
                            vertIndexes.Add(pt, currIndex);
                            currIndex++;
                        }
                    }

                    //Добавление полигона
                    facearray.Add(poligon.Count);
                    foreach (Point3d pt in poligon)
                    {
                        facearray.Add(vertIndexes[pt]);
                    }
                }

                sdm = new SubDMesh();
                sdm.SetDatabaseDefaults();
                sdm.SetSubDMesh(vertarray, facearray, 0);

                //Настроить слой как у первой внешней полилинии
                sdm.LayerId = Root.NestedNodes.First().Polyline.LayerId;
            }


            return sdm;


        }

        /// <summary>
        /// Создание 3d полилиний по границам участков. Полилинии сразу создаются в пространстве модели
        /// Не используется из-за того, что при большом количестве 3d полилиний возникают проблемы при загрузке модели в Navis
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public List<ObjectId> CreateBorderPolylines(Database db)
        {
            List<ObjectId> polylines = new List<ObjectId>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                Root.CreateBorderPolylineRecurse(polylines, ms, tr);
                tr.Commit();
            }


            return polylines;
        }

        public List<Line> CreateBorderLines()
        {
            List<Line> lines = new List<Line>();
            Root.CreateBorderLinesRecurse(lines);
            return lines;
        }



        /// <summary>
        /// Присвоение значений свойству IsOuterBoundary
        /// </summary>
        /// <param name="node"></param>
        /// <param name="thisNodeIsOutsideBorder"></param>
        private void ResolveOuterBoundary(Node node, bool thisNodeIsOutsideBorder)
        {
            node.IsOuterBoundary = thisNodeIsOutsideBorder;
            foreach (Node nestedNode in node.NestedNodes)
            {
                ResolveOuterBoundary(nestedNode, !thisNodeIsOutsideBorder);
            }
        }

        /// <summary>
        /// Удалить из набора вершины, которые попали в контура вырезов
        /// </summary>
        /// <param name="node"></param>
        /// <param name="innerVerts"></param>
        private void GetOuterVerts(Node node, IEnumerable<TinSurfaceVertex> innerVerts, HashSet<TinSurfaceVertex> outerVerts)
        {
            //Набор тех вершин, которые находятся внутри node
            List<TinSurfaceVertex> innerVertsToRecursCall = new List<TinSurfaceVertex>();

            if (node.Polyline == null)
            {
                //Если это корневой узел, то сразу переходить к вложенным узлам
                innerVertsToRecursCall = innerVerts.ToList();
            }
            else
            {
                foreach (TinSurfaceVertex vert in innerVerts)
                {
                    Point2d vertLoc2d = new Point2d(vert.Location.X, vert.Location.Y);

                    if (Utils.PointIsInsidePolylineWindingNumber(vertLoc2d, node.Point2DCollection))
                    {
                        //Вершина внутри текущего узла
                        innerVertsToRecursCall.Add(vert);

                        if (!node.IsOuterBoundary)//Этот узел - внутренняя граница
                        {
                            bool insideInner = false;
                            foreach (Node nn in node.NestedNodes)
                            {
                                if (Utils.PointIsInsidePolylineWindingNumber(vertLoc2d, nn.Point2DCollection))
                                {
                                    insideInner = true;
                                    break;
                                }
                            }
                            //Если точка находится внутри node, но не находится внутри одного из node.NestedNodes, то она находится в вырезе
                            if (!insideInner)
                            {
                                outerVerts.Add(vert);
                            }
                        }

                    }
                }
            }



            foreach (Node nn in node.NestedNodes)
            {
                GetOuterVerts(nn, innerVertsToRecursCall, outerVerts);
            }
        }


        private void TraversePolylines(Node node)
        {
            if (node.Polyline != null)
            {
                //Обход полилинии
                //Учитываются только те случаи, когда в границах поверхности есть хотябы 1 вершина полилинии 
                //Для каждой вершины полилинии искать треугольник поверхности FindTriangleAtXY
                //Образуются последовательности вершин, которые находятся на поверхности (сохраняются индексы начала и конца последовательности и треугольник для каждой вершины)
                //!Несколько вершин может лежать в одном треугольнике
                //!Если все вершины лежат на поверхности - это не гарантирует того, что полилиния не выходит за границы поверхности
                //Если не все вершины попали в единую последовательность, то
                //обход каждой последовательности начинать с обратного прохода линии от первой точки последовательности до предыдущей точки полилинии
                //далее проход каждого сегмента полилинии (цикл for) от первой до последней точек в последовательности + следующая точка полилинии (в любом случае)
                //проход сегмента полилинии выполняется в цикле while - ищется пересечение со всеми ребрами треугольника пока не будет найдено,
                //далее переход к следующему треугольнику через пересекаемое ребро
                //!Особый случай - точка пересечения с треугольником совпадает с вершиной треугольника
                //!Особый случай - вершина полилинии лежит на ребре треугольника
                //!Особый случай - вершина полилинии совпала с вершиной треугольника
                //!Особый случай - сегмент полилинии совпал с ребром поверхности!!!


                List<LinkedList<PolylinePt>> vertSequences = new List<LinkedList<PolylinePt>>();

                for (int i = 0; i < node.Point2DCollection.Count; i++)
                {
                    Point2d pt = node.Point2DCollection[i];
                    //Point2d pt2d = new Point2d(pt.X, pt.Y);

                    PolylinePt polyPt = PtPositionInTriangle(node, pt);
                    if (polyPt != null)
                    {
                        polyPt.VertNumber = i;//На всякий случай

                        //Вершина находится на поверхности. Добавить ее в набор
                        if (vertSequences.Count > 0 && vertSequences.Last().Last().VertNumber == i - 1)
                        {
                            //Продолжить заполнение последовательности
                            vertSequences.Last().AddLast(polyPt);
                        }
                        else
                        {
                            //Начать заполнение новой последовательности
                            LinkedList<PolylinePt> seq = new LinkedList<PolylinePt>();
                            seq.AddLast(polyPt);
                            vertSequences.Add(seq);
                        }



                        //Добавление в граф треугольника
                        AddPolylinePt(polyPt);
                    }


                }

                if (vertSequences.Count == 0)
                {
                    //Еслиполилиния полностью за пределами поверхности, то прервать выполнение
                    return;
                }


                //Проверить, не требуется ли объединить первую и последнюю последовательности
                {
                    LinkedList<PolylinePt> firstSeq = vertSequences.First();
                    LinkedList<PolylinePt> lastSeq = vertSequences.Last();
                    if (vertSequences.Count > 1 &&
                        firstSeq.First().VertNumber
                        == (lastSeq.Last().VertNumber + 1) % node.Point2DCollection.Count)
                    {
                        //Объединить первую и последнюю последовательности
                        for (LinkedListNode<PolylinePt> lln = lastSeq.Last; lln != null; lln = lln.Previous)
                        {
                            firstSeq.AddFirst(lln.Value);
                        }
                        vertSequences.RemoveAt(vertSequences.Count - 1);
                    }
                }



                //Все вершины попали на поверхность
                node.AllPointsOnSurface = vertSequences.Count == 1 && vertSequences.First().Count == node.Point2DCollection.Count;

                //Записать данные об участках в Node
                if (!node.AllPointsOnSurface)
                {
                    foreach (LinkedList<PolylinePt> seq in vertSequences)
                    {
                        int startParam = seq.First().VertNumber;
                        int prevParam = startParam != 0 ? startParam - 1 : node.Polyline.NumberOfVertices - 1;
                        node.BorderParts.Add(new BorderPart(prevParam, (seq.Last().VertNumber + 1) % node.Polyline.NumberOfVertices));
                    }
                }
                else
                {
                    node.BorderParts.Add(new BorderPart(node.Polyline.StartParam, node.Polyline.EndParam));
                }



                //Обход последовательностей
                foreach (LinkedList<PolylinePt> seq in vertSequences)
                {
                    if (!node.AllPointsOnSurface)
                    {
                        TraversePolylineSegment(node, seq.First, false);
                    }

                    bool prevTraversed = true;
                    for (LinkedListNode<PolylinePt> lln = seq.First; lln != null; lln = lln.Next)
                    {
                        if (!prevTraversed)
                        {
                            //Сделать обратный проход, а затем прямой
                            TraversePolylineSegment(node, lln, false);
                        }
                        prevTraversed = TraversePolylineSegment(node, lln, true);

                    }

                }
            }

            //Рекурсивный вызов для вложенных полилиний
            foreach (Node nn in node.NestedNodes)
            {
                TraversePolylines(nn);
            }

        }

        /// <summary>
        /// Определение треугольника, в который попадает точка
        /// Определение положения точки внутри треугольника, на ребре или на вершине
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="triangle"></param>
        /// <param name="polyPt"></param>
        private PolylinePt PtPositionInTriangle(Node node, Point2d pt)
        {
            PolylinePt polyPt = null;

            TinSurfaceTriangle triangle = null;
            try
            {
                //Данный метод может определять треугольник не совсем верно
                //если точка находится на расстояни менее 0,001 от ребра,
                //то может быть получен соседний треугольник вместо нужного 
                triangle = TinSurf.FindTriangleAtXY(pt.X, pt.Y);
            }
            catch (System.ArgumentException) { }//Если точка за пределами поверхности, то выбрасывается исключение
            if (triangle != null)
            {
                polyPt = new PolylinePt(node, pt);

                //bool onSurfaceConfirmed = true;

                TinSurfaceVertex[] vertices = new TinSurfaceVertex[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };
                TinSurfaceEdge[] edges = new TinSurfaceEdge[] { triangle.Edge1, triangle.Edge2, triangle.Edge3 };
                Point2d[] vert2dLocs = new Point2d[3];
                for (int i = 0; i < 3; i++)
                {
                    vert2dLocs[i] = new Point2d(vertices[i].Location.X, vertices[i].Location.Y);
                }
                //Проверить, совпадает ли точка с одной из вершин или попадет на ребро


                TinSurfaceVertex allmostEqualLocationVertex = null;
                TinSurfaceEdge allmostOverlappingEdge = null;

                Tolerance tolerance = new Tolerance(0.001, 0.001);
                for (int i = 0; i < 3; i++)
                {
                    Point2d vert2dLoc = vert2dLocs[i];

                    //проверить при стандартном значении допуска
                    if (pt.IsEqualTo(vert2dLoc))
                    {
                        //Точка лежит на вершине
                        polyPt.TinSurfaceVertex = vertices[i];
                        return polyPt;
                    }
                    //Затем проверить с допуском для учета неточности FindTriangleAtXY
                    if (pt.IsEqualTo(vert2dLoc, tolerance))
                    {
                        allmostEqualLocationVertex = vertices[i];
                    }

                }
                for (int i = 0; i < 3; i++)
                {
                    using (Polyline line = new Polyline())
                    {
                        Point2d vert1_2dLoc = vert2dLocs[i];
                        Point2d vert2_2dLoc = vert2dLocs[(i + 1) % 3];

                        line.AddVertexAt(0, vert1_2dLoc, 0, 0, 0);
                        line.AddVertexAt(0, vert2_2dLoc, 0, 0, 0);
                        Point3d pt3d = new Point3d(pt.X, pt.Y, 0);
                        Point3d closestPtOnEdge = line.GetClosestPointTo(pt3d, false);

                        //Сначала проверить при стандартном значении допуска
                        if (pt3d.IsEqualTo(closestPtOnEdge))
                        {
                            //Точка лежит на ребре
                            polyPt.TinSurfaceEdge = edges[i];
                            return polyPt;
                        }
                        //Затем проверить с допуском для учета неточности FindTriangleAtXY
                        //если не была обнаружена очень близкая вершина
                        if (allmostEqualLocationVertex == null)
                        {
                            if (pt3d.IsEqualTo(closestPtOnEdge, tolerance))
                            {
                                allmostOverlappingEdge = edges[i];
                            }
                        }
                    }
                }
                //Если не на ребре и не на вершине, то внутри треугольника

                //Необходимо проверить действительно ли точка находится в данном треугольнике, так как 
                //метод FindTriangleAtXY дает неточное значение треугольника если точка очень близка к ребру треугольника
                //Более точно работает расчет барицентрических координат
                TinSurfaceTriangle triangleAccurate = triangle;
                double lambda1 = 0;
                double lambda2 = 0;
                bool isInsideTriangle
                    = Utils.BarycentricCoordinates(pt, vert2dLocs[0], vert2dLocs[1], vert2dLocs[2],
                    out lambda1, out lambda2);
                if (!isInsideTriangle)
                {
                    //Расчитать барицентрические координаты для соседних треугольников, близких к заданной точке
                    if (allmostEqualLocationVertex != null)
                    {
                        foreach (TinSurfaceTriangle t in allmostEqualLocationVertex.Triangles)
                        {
                            if (!t.Equals(triangle))
                            {
                                isInsideTriangle
                                    = Utils.BarycentricCoordinates(pt, t, out lambda1, out lambda2);
                                if (isInsideTriangle)
                                {
                                    triangleAccurate = t;
                                    break;
                                }
                            }
                        }
                    }
                    else if (allmostOverlappingEdge != null)
                    {
                        TinSurfaceTriangle neighborTriangle =
                            !allmostOverlappingEdge.Triangle1.Equals(triangle) ?
                            allmostOverlappingEdge.Triangle1 : allmostOverlappingEdge.Triangle2;
                        if (neighborTriangle != null)
                        {
                            isInsideTriangle
                                = Utils.BarycentricCoordinates(pt, neighborTriangle, out lambda1, out lambda2);
                            if (isInsideTriangle)
                            {
                                triangleAccurate = neighborTriangle;
                            }
                        }
                        else//Выход за границу поверхности!
                        {
                            //TODO?: Проверить, что точка точно за пределами переданного треугольника

                            polyPt = null;//Вершина за границей поверхности!
                            return polyPt;
                        }
                    }
                }

                polyPt.TinSurfaceTriangle = triangleAccurate;


            }

            return polyPt;


        }



        private const bool testDisplay = false;

        //TODO: НЕОБХОДИМО УЧЕСТЬ ВСЕ ЧАСТНЫЕ СЛУЧАИ
        /// <summary>
        /// Поиск пересечений ребер поверхности с сегментом полилинии
        /// Возврат false если при проходе ребра был выход за границу поверхности
        /// </summary>
        /// <param name="polyline"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="forvard"></param>
        private bool TraversePolylineSegment(Node node,
            LinkedListNode<PolylinePt> lln,
            //PolylinePt start, PolylinePt end = null,
            bool forvard = true)
        {
            PolylinePt start = lln.Value;
            PolylinePt next = lln.Next?.Value;
            PolylinePt prev = lln.Previous?.Value;
            Polyline polyline = node.Polyline;
            bool segmentTraversedAsPossible = true;


            Point2d startPt2d = start.Point2D;//первая точка сегмента
            Point2d endPt2d = Point2d.Origin;//вторая точка сегмента
            if (forvard)
            {
                if (next != null)
                {
                    endPt2d = next.Point2D;
                }
                else
                {
                    int endNum = (start.VertNumber + 1) % polyline.NumberOfVertices;
                    endPt2d = polyline.GetPoint2dAt(endNum);
                }
            }
            else
            {
                if (prev != null)
                {
                    endPt2d = prev.Point2D;
                }
                else
                {
                    int endNum = start.VertNumber != 0 ? start.VertNumber - 1 : polyline.NumberOfVertices - 1;
                    endPt2d = polyline.GetPoint2dAt(endNum);
                }

            }


            ObjectId ptId = ObjectId.Null;//TEST

            try
            {
                //TEST
                #region MyRegion
                //if (testDisplay)
                //    using (Transaction tr = db.TransactionManager.StartTransaction())
                //    using (Line line = new Line(new Point3d(startPt2d.X, startPt2d.Y, 0), new Point3d(endPt2d.X, endPt2d.Y, 0)))
                //    using (Circle circle1 = new Circle(new Point3d(startPt2d.X, startPt2d.Y, 0), Vector3d.ZAxis, 0.3))
                //    {
                //        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                //        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 4);
                //        ms.AppendEntity(line);
                //        tr.AddNewlyCreatedDBObject(line, true);

                //        circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 8);
                //        ptId = ms.AppendEntity(circle1);
                //        tr.AddNewlyCreatedDBObject(circle1, true);

                //        tr.Commit();
                //        line.Draw();
                //        ed.Regen();
                //        ed.UpdateScreen();
                //    }
                #endregion
                //TEST




                //Треугольники, в котороых будет искаться пересечение. Набор треугольников должен обновляться полностью каждую итерацию
                List<TinSurfaceTriangle> trianglesToSearchIntersection = new List<TinSurfaceTriangle>();
                //Ребра, которые не подлежат проверке на пересечение и наложение.
                //Набор ребер дополняется новыми ребрами каждую итерацию
                HashSet<TinSurfaceEdge> edgesAlreadyIntersected = new HashSet<TinSurfaceEdge>();
                HashSet<TinSurfaceEdge> edgesAlreadyOverlapped = new HashSet<TinSurfaceEdge>();
                if (start.TinSurfaceTriangle != null)
                {
                    trianglesToSearchIntersection.Add(start.TinSurfaceTriangle);
                }
                else if (start.TinSurfaceEdge != null)
                {
                    trianglesToSearchIntersection.Add(start.TinSurfaceEdge.Triangle1);
                    trianglesToSearchIntersection.Add(start.TinSurfaceEdge.Triangle2);
                    edgesAlreadyIntersected.Add(start.TinSurfaceEdge);
                }
                else
                {
                    trianglesToSearchIntersection.AddRange(start.TinSurfaceVertex.Triangles);
                    foreach (TinSurfaceEdge edge in start.TinSurfaceVertex.Edges)
                    {
                        edgesAlreadyIntersected.Add(edge);
                    }

                }

                //Основной цикл прохода сегмента полилинии
                //В каждой итерации требуется найти пересечение с ребром
                bool intersectionFound = false;
                while (true)
                {
                    intersectionFound = false;
                    foreach (TinSurfaceTriangle triangle in trianglesToSearchIntersection)
                    {
                        if (triangle == null)
                        {
                            continue;
                        }

                        ObjectId plineId = ObjectId.Null;//TEST
                        try
                        {
                            //TEST
                            #region MyRegion
                            //if (testDisplay)
                            //    using (Transaction tr = db.TransactionManager.StartTransaction())
                            //    using (Polyline pline = new Polyline())
                            //    {
                            //        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                            //        Point3d vert1 = triangle.Vertex1.Location;
                            //        Point3d vert2 = triangle.Vertex2.Location;
                            //        Point3d vert3 = triangle.Vertex3.Location;

                            //        pline.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                            //        pline.AddVertexAt(0, new Point2d(vert1.X, vert1.Y), 0, 0, 0);
                            //        pline.AddVertexAt(1, new Point2d(vert2.X, vert2.Y), 0, 0, 0);
                            //        pline.AddVertexAt(2, new Point2d(vert3.X, vert3.Y), 0, 0, 0);
                            //        pline.Closed = true;

                            //        plineId = ms.AppendEntity(pline);
                            //        tr.AddNewlyCreatedDBObject(pline, true);

                            //        tr.Commit();
                            //        pline.Draw();
                            //        ed.Regen();
                            //        ed.UpdateScreen();
                            //    }
                            #endregion
                            //TEST


                            //Искать пересечения с каждым ребром
                            TinSurfaceEdge[] edges = new TinSurfaceEdge[] { triangle.Edge1, triangle.Edge2, triangle.Edge3 };

                            foreach (TinSurfaceEdge edge in edges)
                            {
                                ObjectId lineId = ObjectId.Null;//TEST


                                try
                                {
                                    if (!edgesAlreadyOverlapped.Contains(edge))
                                    {
                                        //Необходимо проверить каждое ребро на наложение даже если с ним не может быть пересечения
                                        Point3d edgePt1 = edge.Vertex1.Location;
                                        Point3d edgePt2 = edge.Vertex2.Location;


                                        //TEST
                                        #region MyRegion
                                        //if (testDisplay)
                                        //    using (Transaction tr = db.TransactionManager.StartTransaction())
                                        //    using (Line line = new Line(edge.Vertex1.Location, edge.Vertex2.Location))
                                        //    {
                                        //        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                        //        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                        //        line.LineWeight = LineWeight.LineWeight040;
                                        //        lineId = ms.AppendEntity(line);
                                        //        tr.AddNewlyCreatedDBObject(line, true);

                                        //        tr.Commit();
                                        //        line.Draw();
                                        //        ed.Regen();
                                        //        ed.UpdateScreen();
                                        //    }
                                        #endregion
                                        //TEST



                                        Point2d edgePt1_2d = new Point2d(edgePt1.X, edgePt1.Y);
                                        Point2d edgePt2_2d = new Point2d(edgePt2.X, edgePt2.Y);
                                        //bool overlapping = false;
                                        //Point2d? intersection = Utils//Расчет точки пересечения с помощью AutoCAD
                                        //    .GetLinesIntersectionAcad(startPt2d, endPt2d, edgePt1_2d, edgePt2_2d, out overlapping);
                                        Point2d? intersection = null;
                                        bool overlapping = Utils.LinesAreOverlapping(startPt2d, endPt2d, edgePt1_2d, edgePt2_2d);
                                        if (!overlapping)
                                        {
                                            intersection = Utils.GetLinesIntersectionAcad(startPt2d, endPt2d, edgePt1_2d, edgePt2_2d);
                                        }


                                        if (overlapping)
                                        {
                                            //Это ребро больше не должно подвергаться проверке
                                            //edgesAlreadyIntersected.Add(edge);
                                            edgesAlreadyOverlapped.Add(edge);
                                            //Если обнаружилось, что сегмент полилинии совпал с ребром поверхности
                                            //то нужно взять точку ребра дальнюю от начала сегмента и переходить к следующей итерации
                                            //как если бы произошло пересечение в вершине поверхности
                                            double dist1 = startPt2d.GetDistanceTo(edgePt1_2d);
                                            double dist2 = startPt2d.GetDistanceTo(edgePt2_2d);
                                            double maxDist = dist1 > dist2 ? dist1 : dist2;
                                            double distNextPt = startPt2d.GetDistanceTo(endPt2d);
                                            if (distNextPt > maxDist)//Сегмент полилинии проходит через все ребро?
                                            {
                                                TinSurfaceVertex vertex = dist1 > dist2 ? edge.Vertex1 : edge.Vertex2;
                                                //Проверка на совпадение с конечной точкой сегмента.
                                                Point2d vertexLoc = new Point2d(vertex.Location.X, vertex.Location.Y);
                                                if (!vertexLoc.IsEqualTo(endPt2d))
                                                {
                                                    trianglesToSearchIntersection = vertex.Triangles.ToList();
                                                    foreach (TinSurfaceEdge edge1 in vertex.Edges)
                                                    {
                                                        edgesAlreadyIntersected.Add(edge1);
                                                    }
                                                    AddPolylinePt(new PolylinePt(node, vertexLoc) { TinSurfaceVertex = vertex });
                                                    //TEST
                                                    #region MyRegion
                                                    //if (testDisplay)
                                                    //    using (Transaction tr = db.TransactionManager.StartTransaction())
                                                    //    using (Circle circle1 = new Circle(edgePt1, Vector3d.ZAxis, 0.1))
                                                    //    using (Circle circle2 = new Circle(edgePt2, Vector3d.ZAxis, 0.1))
                                                    //    {
                                                    //        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;


                                                    //        circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                                    //        circle2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                                    //        ms.AppendEntity(circle1);
                                                    //        ms.AppendEntity(circle2);
                                                    //        tr.AddNewlyCreatedDBObject(circle1, true);
                                                    //        tr.AddNewlyCreatedDBObject(circle2, true);

                                                    //        tr.Commit();
                                                    //        circle1.Draw();
                                                    //        circle2.Draw();
                                                    //        ed.Regen();
                                                    //        ed.UpdateScreen();
                                                    //    }
                                                    #endregion
                                                    //TEST
                                                    intersectionFound = true;
                                                    break;
                                                }

                                            }


                                        }
                                        else if (!edgesAlreadyIntersected.Contains(edge) && intersection != null)
                                        {
                                            //Это ребро больше не должно подвергаться проверке
                                            edgesAlreadyIntersected.Add(edge);
                                            //Если обнарушено пересечение, то расчитать точку пересечения
                                            //Если она совпала с вершиной поверхности, то в следующей итерации проверять все треугольники, примыкающие к этой вершине
                                            //ИЛИ нужно как-то найти точно тот треугольник, в котором будет продолжение линии????
                                            //Если пересечение не совпало с вершиной поверхности, то переход к соседнему треугольнику
                                            //!!!Здесь должен быть обнаружен выход за пределы поверхности
                                            Point2d intersectionPt = intersection.Value;
                                            if (!intersectionPt.IsEqualTo(endPt2d))//Проверка на совпадение с конечной точкой сегмента
                                            {
                                                if (intersectionPt.IsEqualTo(edgePt1_2d))//пересечение совпало с 1-й вершиной ребра
                                                {
                                                    trianglesToSearchIntersection = edge.Vertex1.Triangles.ToList();
                                                    foreach (TinSurfaceEdge edge1 in edge.Vertex1.Edges)
                                                    {
                                                        edgesAlreadyIntersected.Add(edge1);
                                                    }
                                                    AddPolylinePt(new PolylinePt(node, intersectionPt) { TinSurfaceVertex = edge.Vertex1 });
                                                    //TEST
                                                    #region MyRegion
                                                    //if (testDisplay)
                                                    //    using (Transaction tr = db.TransactionManager.StartTransaction())
                                                    //    using (Circle circle1 = new Circle(edgePt1, Vector3d.ZAxis, 0.1))
                                                    //    {
                                                    //        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                                    //        circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                                    //        ms.AppendEntity(circle1);
                                                    //        tr.AddNewlyCreatedDBObject(circle1, true);

                                                    //        tr.Commit();
                                                    //        circle1.Draw();
                                                    //        ed.Regen();
                                                    //        ed.UpdateScreen();
                                                    //    }
                                                    #endregion
                                                    //TEST
                                                }
                                                else if (intersectionPt.IsEqualTo(edgePt2_2d))//пересечение совпало с 2-й вершиной ребра
                                                {
                                                    trianglesToSearchIntersection = edge.Vertex2.Triangles.ToList();
                                                    foreach (TinSurfaceEdge edge1 in edge.Vertex2.Edges)
                                                    {
                                                        edgesAlreadyIntersected.Add(edge1);
                                                    }
                                                    AddPolylinePt(new PolylinePt(node, intersectionPt) { TinSurfaceVertex = edge.Vertex2 });
                                                    //TEST
                                                    #region MyRegion
                                                    //if (testDisplay)
                                                    //    using (Transaction tr = db.TransactionManager.StartTransaction())
                                                    //    using (Circle circle1 = new Circle(edgePt2, Vector3d.ZAxis, 0.1))
                                                    //    {
                                                    //        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                                    //        circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                                    //        ms.AppendEntity(circle1);
                                                    //        tr.AddNewlyCreatedDBObject(circle1, true);

                                                    //        tr.Commit();
                                                    //        circle1.Draw();
                                                    //        ed.Regen();
                                                    //        ed.UpdateScreen();
                                                    //    }
                                                    #endregion
                                                    //TEST
                                                }
                                                else//пересечение не совпало с вершиной
                                                {
                                                    trianglesToSearchIntersection = new List<TinSurfaceTriangle>();
                                                    //Взять соседний треугольник если он есть
                                                    TinSurfaceTriangle nextTriangle =
                                                    !edge.Triangle1.Equals(triangle) ? edge.Triangle1 : edge.Triangle2;
                                                    if (nextTriangle != null)
                                                    {
                                                        trianglesToSearchIntersection.Add(nextTriangle);
                                                    }
                                                    else
                                                    {
                                                        if (next != null && forvard)
                                                        {
                                                            //Если при прямом проходе встречена граница поверхности и известно, что следующая вершина полилинии лежит на поверхности, то использовать метод
                                                            //TinSurf.FindTriangleAtXY для точек на ребре с шагом 1 ед длины пока не будет найден треугольник
                                                            //Если треугольник найден, то сначала выполнить обратный проход от него а затем продолжить прямой проход от него
                                                            //Для этого в последовательность вставляется новый узел после переданного lln если только найденная точка не совпала с конечной или не перескочила ее
                                                            //TODO?: КАК ОТСЛЕДИТЬ ВЫХОД ЗА ГРАНИЦУ ПОВЕРХНОСТИ ЧЕРЕЗ ВЕРШИНУ ПОВЕРХНОСТИ???
                                                            Vector2d segmentVector = endPt2d - startPt2d;
                                                            double overalLength = segmentVector.Length;
                                                            segmentVector = segmentVector.GetNormal();
                                                            int n = 1;
                                                            Point2d testingPt = Point2d.Origin;
                                                            PolylinePt polyPt = null;
                                                            bool overrun = false;
                                                            do
                                                            {
                                                                testingPt = intersectionPt + segmentVector * n;
                                                                n++;
                                                                overrun = (testingPt - startPt2d).Length > overalLength;
                                                                if (!overrun)
                                                                {
                                                                    polyPt = PtPositionInTriangle(node, testingPt);
                                                                }
                                                            }
                                                            while (polyPt == null && !overrun);

                                                            if (polyPt != null && !overrun)
                                                            {
                                                                lln.List.AddAfter(lln, polyPt);

                                                                segmentTraversedAsPossible = false;
                                                            }
                                                        }

                                                    }
                                                    AddPolylinePt(new PolylinePt(node, intersectionPt) { TinSurfaceEdge = edge });
                                                    //TEST
                                                    #region MyRegion
                                                    //if (testDisplay)
                                                    //    using (Transaction tr = db.TransactionManager.StartTransaction())
                                                    //    using (Circle circle1 = new Circle(new Point3d(intersectionPt.X, intersectionPt.Y, 0), Vector3d.ZAxis, 0.1))
                                                    //    {
                                                    //        ms = tr.GetObject(ms.Id, OpenMode.ForWrite) as BlockTableRecord;

                                                    //        circle1.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                                                    //        ms.AppendEntity(circle1);
                                                    //        tr.AddNewlyCreatedDBObject(circle1, true);

                                                    //        tr.Commit();
                                                    //        circle1.Draw();
                                                    //        ed.Regen();
                                                    //        ed.UpdateScreen();
                                                    //    }
                                                    #endregion
                                                    //TEST
                                                }


                                                //переход к следующей итерации цикла while
                                                intersectionFound = true;
                                                break;
                                            }

                                        }
                                    }


                                }
                                finally
                                {
                                    //TEST
                                    #region MyRegion
                                    //if (testDisplay)
                                    //    if (!lineId.IsNull)
                                    //        using (Transaction tr = db.TransactionManager.StartTransaction())
                                    //        {
                                    //            Line line = tr.GetObject(lineId, OpenMode.ForWrite) as Line;
                                    //            line.Erase();
                                    //            tr.Commit();
                                    //            ed.Regen();
                                    //            ed.UpdateScreen();
                                    //        }
                                    #endregion
                                    //TEST
                                }

                            }
                            if (intersectionFound)
                                break;
                        }
                        finally
                        {
                            //TEST
                            #region MyRegion
                            //if (testDisplay)
                            //    if (!plineId.IsNull)
                            //        using (Transaction tr = db.TransactionManager.StartTransaction())
                            //        {
                            //            Polyline pline = tr.GetObject(plineId, OpenMode.ForWrite) as Polyline;
                            //            pline.Erase();
                            //            tr.Commit();
                            //            ed.Regen();
                            //            ed.UpdateScreen();
                            //        }
                            #endregion
                            //TEST
                        }
                    }
                    if (intersectionFound)
                        continue;
                    else
                    {
                        //выход из цикла если при обходе всех треугольников, подлежащих проверке пересечений не обнаружено.
                        break;
                    }

                }
            }
            finally
            {
                //TEST
                #region MyRegion
                //if (testDisplay)
                //    if (!ptId.IsNull)
                //        using (Transaction tr = db.TransactionManager.StartTransaction())
                //        {
                //            Circle pt = tr.GetObject(ptId, OpenMode.ForWrite) as Circle;
                //            pt.Erase();
                //            tr.Commit();
                //            ed.Regen();
                //            ed.UpdateScreen();
                //        }
                #endregion
                //TEST
            }

            return segmentTraversedAsPossible;

        }



        /// <summary>
        /// Добавление точки в соответствующие графы
        /// </summary>
        private void AddPolylinePt(PolylinePt pt)
        {
            if (pt.TinSurfaceVertex != null)
            {
                //Данная вершина должна обязательно считаться внутренней!
                InnerVerts.Add(pt.TinSurfaceVertex);

                //Добавить точку во все примыкающие треугольники
                foreach (TinSurfaceTriangle triangle in pt.TinSurfaceVertex.Triangles)
                {
                    AddPolylinePt1(triangle, pt);
                }
            }
            else if (pt.TinSurfaceEdge != null)
            {
                //Добавить точку в 2 треугольника (или 1 если это граница поверхности)
                TinSurfaceTriangle triangle1 = pt.TinSurfaceEdge.Triangle1;
                if (triangle1 != null)
                {
                    AddPolylinePt1(triangle1, pt);
                }
                TinSurfaceTriangle triangle2 = pt.TinSurfaceEdge.Triangle2;
                if (triangle2 != null)
                {
                    AddPolylinePt1(triangle2, pt);
                }
            }
            else if (pt.TinSurfaceTriangle != null)
            {
                AddPolylinePt1(pt.TinSurfaceTriangle, pt);
            }
            else
            {
                throw new Exception("Точка полилинии не привязана к треугольнику");
            }

        }


        /// <summary>
        /// Добавление точки в конкретный граф треугольника
        /// </summary>
        /// <param name="triangle"></param>
        /// <param name="pt"></param>
        private void AddPolylinePt1(TinSurfaceTriangle triangle, PolylinePt pt)
        {
            TriangleGraph triangleGraph = null;
            //Добавить в один треугольник
            TriangleGraphs.TryGetValue(triangle, out triangleGraph);
            if (triangleGraph == null)
            {
                triangleGraph = new TriangleGraph(this, triangle);
                TriangleGraphs.Add(triangle, triangleGraph);
            }
            triangleGraph.AddPolylinePoint(pt);

            //Добавить треугольник в набор пересекаемых для этой линии
            pt.Node.IntersectingTriangles.Add(triangle);
        }

        /// <summary>
        /// Вызвать Dispose для всех полилиний
        /// </summary>
        public void Dispose()
        {
            Root.Dispose();
        }



        /// <summary>
        /// Узел дерева - 1 полилиния
        /// </summary>
        public class Node : IDisposable
        {
            /// <summary>
            /// Ссылка на дерево
            /// </summary>
            public PolylineNesting PolylineNesting { get; private set; }

            /// <summary>
            /// Полилиния
            /// </summary>
            public Polyline Polyline { get; private set; }

            /// <summary>
            /// Точки полилинии. Точки пересчитаны в глобальную систему координат
            /// </summary>
            public Point2dCollection Point2DCollection { get; private set; } = new Point2dCollection();
            //public double MinX { get; private set; } = double.MaxValue;
            //public double MinY { get; private set; } = double.MaxValue;
            //public double MaxX { get; private set; } = double.MinValue;
            //public double MaxY { get; private set; } = double.MinValue;

            /// <summary>
            /// Внешняя граница области
            /// </summary>
            public bool IsOuterBoundary { get; set; } = false;

            /// <summary>
            /// Вложенные узлы
            /// </summary>
            public List<Node> NestedNodes { get; private set; } = new List<Node>();

            /// <summary>
            /// Треугольники, которые пересекает эта полилиния
            /// </summary>
            public HashSet<TinSurfaceTriangle> IntersectingTriangles = new HashSet<TinSurfaceTriangle>();

            /// <summary>
            /// Участки полилинии, лежащие на поверхности
            /// </summary>
            public List<BorderPart> BorderParts { get; private set; } = new List<BorderPart>();



            public bool AllPointsOnSurface { get; set; } = false;

            /// <summary>
            /// Предполагается, что полилиния замкнута и не имеет повторяющихся точек
            /// </summary>
            /// <param name="polyline"></param>
            /// <param name="polylineNesting"></param>
            public Node(Polyline polyline, PolylineNesting polylineNesting)
            {
                Polyline = polyline;
                PolylineNesting = polylineNesting;
                //Заполнить набор точек полилинии
                //Получить минимальные и максимальные значения X и Y
                if (polyline != null)
                {
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        Point2d pt = polyline.GetPoint2dAt(i);
                        //if (pt.X < MinX)
                        //{
                        //    MinX = pt.X;
                        //}
                        //if (pt.Y < MinY)
                        //{
                        //    MinY = pt.Y;
                        //}
                        //if (pt.X > MaxX)
                        //{
                        //    MaxX = pt.X;
                        //}
                        //if (pt.Y > MaxY)
                        //{
                        //    MaxY = pt.Y;
                        //}

                        Point2DCollection.Add(pt);

                    }
                }

            }

            /// <summary>
            /// Переданный узел вложен в вызывающий узел
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            public bool IsNested(Node node)
            {
                //Проверка по одной точке, так как предполагается, что полилинии не пересекаются
                return Utils.PointIsInsidePolylineWindingNumber(node.Point2DCollection[0], this.Point2DCollection);
            }


            public void CreateBorderPolylineRecurse(List<ObjectId> polylines, BlockTableRecord ms, Transaction tr)
            {
                CreateBorderPolyline(polylines, ms, tr);
                foreach (Node nn in this.NestedNodes)
                {
                    nn.CreateBorderPolylineRecurse(polylines, ms, tr);
                }
            }


            /// <summary>
            /// Построить 3d полилинию
            /// http://through-the-interface.typepad.com/through_the_interface/2008/05/sweeping-an-aut.html
            /// </summary>
            /// <returns></returns>
            private void CreateBorderPolyline(List<ObjectId> polylines, BlockTableRecord ms, Transaction tr)
            {
                if (BorderParts.Count > 0)
                {
                    foreach (BorderPart p in BorderParts)
                    {
                        SortedDictionary<double, PolylinePt> part = p.PointsOrderedByParameter;
                        LinkedList<PolylinePt> partSeq = BorderPartSequence(part);

                        if (partSeq.Count > 0)
                        {
                            Polyline3d border = new Polyline3d();
                            border.LayerId = PolylineNesting.Root.NestedNodes.First().Polyline.LayerId;
                            border.Color = SurfaceMeshByBoundaryCommand.ColorForBorder;
                            ObjectId pId = ms.AppendEntity(border);
                            if (!pId.IsNull)
                            {
                                tr.AddNewlyCreatedDBObject(border, true);
                                foreach (PolylinePt pt in partSeq)
                                {
                                    PolylineVertex3d vertex
                                        = new PolylineVertex3d(new Point3d(pt.Point2D.X, pt.Point2D.Y, pt.Z + SurfaceMeshByBoundaryCommand.MeshElevation));
                                    vertex.LayerId = border.LayerId;
                                    vertex.Color = border.Color;
                                    border.AppendVertex(vertex);
                                    tr.AddNewlyCreatedDBObject(vertex, true);
                                }
                                if (AllPointsOnSurface)
                                {
                                    border.Closed = true;
                                }
                                polylines.Add(pId);
                            }
                        }
                    }
                }
            }

            private static LinkedList<PolylinePt> BorderPartSequence(SortedDictionary<double, PolylinePt> part)
            {
                //Если на этом участке есть переход через 0, то нужно переставить участок с конца последовательности в начало
                LinkedList<PolylinePt> partSeq = new LinkedList<PolylinePt>();
                LinkedList<PolylinePt> partToReplace = new LinkedList<PolylinePt>();//часть, которую нужно переставить с конца в начало
                foreach (PolylinePt pt in part.Values)
                {
                    if (partToReplace.Count > 0//Если запись последовательности для перестановки уже начата, то все остальные точки попадут в нее
                        || (partSeq.Count > 0 && pt.Parameter - partSeq.Last().Parameter > 1))//Эта точка по разности параметров с предыдущей больше 1
                    {
                        partToReplace.AddLast(pt);
                    }
                    else
                    {
                        partSeq.AddLast(pt);
                    }
                }

                for (LinkedListNode<PolylinePt> lln = partToReplace.Last; lln != null; lln = lln.Previous)
                {
                    partSeq.AddFirst(lln.Value);
                }

                return partSeq;
            }


            public void CreateBorderLinesRecurse(List<Line> lines)
            {
                CreateBorderLines(lines);
                foreach (Node nn in this.NestedNodes)
                {
                    nn.CreateBorderLinesRecurse(lines);
                }
            }

            private void CreateBorderLines(List<Line> lines)
            {
                if (BorderParts.Count > 0)
                {
                    foreach (BorderPart p in BorderParts)
                    {
                        SortedDictionary<double, PolylinePt> part = p.PointsOrderedByParameter;
                        LinkedList<PolylinePt> partSeq = BorderPartSequence(part);
                        if (partSeq.Count > 0)
                        {
                            for (LinkedListNode<PolylinePt> lln = partSeq.First; lln != null; lln = lln.Next)
                            {
                                LinkedListNode<PolylinePt> llnNext = lln.Next;
                                if (llnNext == null && AllPointsOnSurface)
                                {
                                    //Замыкание полилинии
                                    llnNext = partSeq.First;
                                }

                                if (llnNext != null)
                                {
                                    Point3d pt1 = new Point3d(lln.Value.Point2D.X, lln.Value.Point2D.Y,
                                        lln.Value.Z + SurfaceMeshByBoundaryCommand.MeshElevation);
                                    Point3d pt2 = new Point3d(llnNext.Value.Point2D.X, llnNext.Value.Point2D.Y,
                                        llnNext.Value.Z + SurfaceMeshByBoundaryCommand.MeshElevation);

                                    Line line = new Line(pt1, pt2);
                                    line.LayerId = PolylineNesting.Root.NestedNodes.First().Polyline.LayerId;
                                    if (!SurfaceMeshByBoundaryCommand.ColorForBorder.IsByLayer)
                                    {
                                        line.Color = SurfaceMeshByBoundaryCommand.ColorForBorder;
                                    }
                                    else
                                    {
                                        LayerTableRecord layer = (LayerTableRecord)Polyline.LayerId.GetObject(OpenMode.ForRead);
                                        Color dimmerColor = Utils.GetDimmerColor(layer.Color,
                                            SurfaceMeshByBoundaryCommand.BORDER_DIM_MULTIPLIER);
                                        line.Color = dimmerColor;
                                    }
                                    

                                    lines.Add(line);
                                }
                            }
                        }
                    }
                }
            }


            public override int GetHashCode()
            {
                return Polyline.Id.GetHashCode();
            }

            public void Dispose()
            {
                if (Polyline!=null)
                {
                    Polyline.Dispose();
                }

                foreach (Node nn in NestedNodes)
                {
                    nn.Dispose();
                }
            }
        }


        public class BorderPart
        {
            public double StartParam { get; private set; }
            public double EndParam { get; private set; }

            public SortedDictionary<double, PolylinePt> PointsOrderedByParameter { get; private set; }
                = new SortedDictionary<double, PolylinePt>();

            public BorderPart(double startParam, double endParam)
            {
                StartParam = startParam;
                EndParam = endParam;
            }
        }


    }
}
