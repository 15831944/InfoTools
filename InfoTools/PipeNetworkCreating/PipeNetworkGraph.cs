using Autodesk.AutoCAD.ApplicationServices;
using Civil3DInfoTools.PipeNetworkCreating.ConfigureNetworkCreationWindow2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RBush;
using RBush.KnnUtility;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Text.RegularExpressions;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    //TODO: Проверить на пустом чертеже
    public class PipeNetworkGraph
    {
        //квадраты сетки
        private RBush<GridSquare> gridSquares = new RBush<GridSquare>();

        //положения блоков колодцев
        private RBush<StructurePosition> structurePositions = new RBush<StructurePosition>();

        //положения подписей блоков колодцев
        private RBush<TextPosition> structureLabelPositions = new RBush<TextPosition>();

        //положения подписей примыканий к колодцам
        private RBush<TextPosition> pipeJunctionLabelPositions = new RBush<TextPosition>();

        //узлы графа в RTree
        private RBush<NetworkNode> networkNodesRbush = new RBush<NetworkNode>();

        //узлы графа в списке
        private List<NetworkNode> networkNodesList = new List<NetworkNode>();

        //ребра графа
        private List<NetworkEdge> networkEdges = new List<NetworkEdge>();

        /// <summary>
        /// Построение графа инженерной сети
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="configsViewModel">обязательно все настройки должны быть назначены</param>
        public PipeNetworkGraph(Document doc, ConfigureNetworkCreationViewModel configsViewModel)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;
            #region Инициализация структур данных
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                //слои
                LayerTableRecord gridSquaresLtr
                    = (LayerTableRecord)tr.GetObject(configsViewModel.GridLayerId.Value, OpenMode.ForRead);
                string gridSquaresLayerName = gridSquaresLtr.Name;
                LayerTableRecord structureBlocksLtr
                    = (LayerTableRecord)tr.GetObject(configsViewModel.StructuresLayerId.Value, OpenMode.ForRead);
                string structureBlocksLayerName = structureBlocksLtr.Name;
                LayerTableRecord structureLabelsLtr
                    = (LayerTableRecord)tr.GetObject(configsViewModel.StructureLabelsLayerId.Value, OpenMode.ForRead);
                string structureLabelsLayerName = structureLabelsLtr.Name;
                LayerTableRecord communicationLtr
                    = (LayerTableRecord)tr.GetObject(configsViewModel.CommunicationLayerId.Value, OpenMode.ForRead);
                string communicationLayerName = communicationLtr.Name;

                //блоки
                Dictionary<ObjectId, SelectedPartTypeId> blockStructureMapping
                    = configsViewModel.BlockStructureMapping;

                //1. Записать квадраты сетки в RTree
                //загрузка квадратов в RTree
                {
                    //Выбрать все полилинии в слое сетки
                    TypedValue[] tv = new TypedValue[]
                                {
                    new TypedValue(0, "LWPOLYLINE"),
                    new TypedValue(8, gridSquaresLayerName),
                                };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult psr = ed.SelectAll(flt);
                    //эти полилинии должны представлять собой квадраты
                    SelectionSet ss = psr.Value;
                    if (ss != null)
                    {
                        List<GridSquare> squares = new List<GridSquare>();
                        foreach (SelectedObject so in ss)
                        {
                            Polyline poly = (Polyline)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            if (IsSquare(poly))//TODO: А если в этом слое находится какой-то левый квадрат?
                            {
                                Extents3d? ext = poly.Bounds;
                                if (ext != null)
                                {
                                    squares.Add(new GridSquare(ext.Value.MinPoint, ext.Value.MaxPoint));
                                }
                            }
                        }
                        //загрузить квадраты в RTree
                        gridSquares.BulkLoad(squares);
                    }

                }
                //присвоение квадратам номеров пот текстам, которые попадают в эти квадраты
                {
                    //Выбрать все текстовые объекты в слое сетки
                    TypedValue[] tv = new TypedValue[]
                    {
                    new TypedValue(0, "TEXT,MTEXT"),
                    new TypedValue(8, gridSquaresLayerName),
                    };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult psr = ed.SelectAll(flt);
                    //текст должен соответствовать регулярному выражению  
                    Regex regex = PipeStructureExcelReader.SQUARE_LBL_REGEX;
                    //для каждого текста определить в какой квадрат он попадает
                    SelectionSet ss = psr.Value;
                    if (ss != null)
                    {
                        foreach (SelectedObject so in ss)
                        {
                            Entity txtEnt = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            string txtContent = txtEnt is DBText ?
                                (txtEnt as DBText).TextString
                                : (txtEnt as MText).Text;
                            if (regex.IsMatch(txtContent))
                            {
                                Extents3d? ext = txtEnt.Bounds;
                                if (ext != null)
                                {
                                    Point2d centerPt = new Point2d(
                                        (ext.Value.MinPoint.X + ext.Value.MaxPoint.X) / 2,
                                        (ext.Value.MinPoint.Y + ext.Value.MaxPoint.Y) / 2
                                        );
                                    Envelope queryPt = new Envelope(centerPt.X, centerPt.Y, centerPt.X, centerPt.Y);
                                    IReadOnlyList<GridSquare> squares = gridSquares.Search(queryPt);
                                    if (squares.Count == 1)
                                    {
                                        //текстовая строка переводится в целое число
                                        squares.First().SquareKey
                                            = Convert.ToInt32(txtContent.Replace("_", "").Replace("-", ""));
                                    }
                                }
                            }
                        }
                    }

                }

                //2. Записать положения блоков колодцев в RTree (положение блока - одна точка вставки)
                //(дальнейший поиск - k ближайших блоков к точке)
                //Брать в заданном слое и слое самой сети
                {
                    //Выбрать все блоки в слое колодцев и слое сети
                    //Имена блоков только те что были указаны
                    List<string> blockNames = new List<string>();
                    foreach (ObjectId btrId in blockStructureMapping.Keys)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        blockNames.Add(btr.Name);
                    }
                    TypedValue[] tv = new TypedValue[]
                                {
                    new TypedValue(0, "INSERT"),
                    new TypedValue(2, String.Join(",", blockNames)),
                    new TypedValue(8, structureBlocksLayerName + "," + communicationLayerName),
                                };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult psr = ed.SelectAll(flt);

                    SelectionSet ss = psr.Value;
                    if (ss != null)
                    {
                        List<StructurePosition> _blockPositions = new List<StructurePosition>();
                        foreach (SelectedObject so in ss)
                        {
                            BlockReference br = (BlockReference)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            StructurePosition structurePosition
                                = new StructurePosition(br.Position, br.Id, br.BlockTableRecord, br.Rotation);
                            _blockPositions.Add(structurePosition);
                        }
                        //загрузить положения блоков в RTree
                        structurePositions.BulkLoad(_blockPositions);
                    }


                }

                //3. Записать положения подписей колодцев в RTree (положение подписи - bounding box)
                {
                    //Выбрать все текстовые объекты в слое подписей колодцев
                    TypedValue[] tv = new TypedValue[]
                    {
                    new TypedValue(0, "TEXT,MTEXT"),
                    new TypedValue(8, structureLabelsLayerName),
                    };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult psr = ed.SelectAll(flt);
                    //Взять все текстовые объекты (номера колодцев могут быть записаны не только цифрами)
                    SelectionSet ss = psr.Value;
                    if (ss != null)
                    {
                        List<TextPosition> txtPositions = new List<TextPosition>();
                        foreach (SelectedObject so in ss)
                        {
                            Entity txtEnt = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            string txtContent = txtEnt is DBText ?
                                (txtEnt as DBText).TextString
                                : (txtEnt as MText).Text;
                            Extents3d? ext = txtEnt.Bounds;
                            if (ext != null)
                            {
                                txtPositions
                                    .Add(new TextPosition(ext.Value.MinPoint, ext.Value.MaxPoint, txtContent));
                            }
                        }
                        structureLabelPositions.BulkLoad(txtPositions);
                    }
                }

                //4. Записать в RTree положение всех текстовых примитивов в слое выбранной сети - это подписи сети,
                //содержащие атрибутику, а так же привязки примыканий к колодцам
                //TODO: При переборе текстов они дифференцируются на группы в соответствии с тем, что в них записано
                //Отбирается только один вид подписей - НОМЕРА ПРИМЫКАНИЙ К КОЛОДЦАМ (ПРОСТО ЦИФРА, ОРИЕНТАЦИЯ - ГОРИЗОНТАЛЬНАЯ)
                {
                    //Выбрать все текстовые объекты в слое сети
                    TypedValue[] tv = new TypedValue[]
                    {
                    new TypedValue(0, "TEXT,MTEXT"),
                    new TypedValue(8, communicationLayerName),
                    };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult psr = ed.SelectAll(flt);
                    //Отобрать только подписи примыканий к колодцам
                    Regex pipeJunctionLabelRegex = new Regex("^\\d{1,2}$");//1-2 цифры
                    SelectionSet ss = psr.Value;
                    if (ss != null)
                    {
                        List<TextPosition> _pipeJunctionLabelPositions = new List<TextPosition>();
                        foreach (SelectedObject so in ss)
                        {
                            Entity txtEnt = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            string txtContent = txtEnt is DBText ?
                                (txtEnt as DBText).TextString
                                : (txtEnt as MText).Text;

                            Extents3d? ext = txtEnt.Bounds;
                            if (ext != null)
                            {
                                //Определить ориентацию текста
                                double rotation = txtEnt is DBText ?
                                (txtEnt as DBText).Rotation
                                : (txtEnt as MText).Rotation;
                                Vector2d orientationVector = Vector2d.XAxis.RotateBy(rotation);

                                if (pipeJunctionLabelRegex.IsMatch(txtContent)
                                    && orientationVector.IsCodirectionalTo(Vector2d.XAxis))
                                {
                                    _pipeJunctionLabelPositions
                                        .Add(new TextPosition(ext.Value.MinPoint, ext.Value.MaxPoint, txtContent));
                                }
                                //TODO: else if - далее другие типы подписей...

                            }


                        }
                        pipeJunctionLabelPositions.BulkLoad(_pipeJunctionLabelPositions);
                    }
                }

                //5. Построить RTree с узлами графа
                //- узел графа - конечная точка полилинии в слое сетей
                //- точки не должны повторяться, но каждый узел должен иметь ссылки на все примыкающие к нему полилинии
                //Параллельно создать все ребра графа

                //Найти все полилинии в слое коммуникации
                ObjectId[] communicationPolylineIds = null;
                {
                    TypedValue[] tv = new TypedValue[]
                            {
                    new TypedValue(0, "LWPOLYLINE"),
                    new TypedValue(8, communicationLayerName),
                            };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult psr = ed.SelectAll(flt);

                    SelectionSet ss = psr.Value;
                    if (ss != null)
                    {
                        communicationPolylineIds = ss.GetObjectIds();

                        foreach (ObjectId polyId in communicationPolylineIds)
                        {
                            //Новые элементы добавляются в RTree по одному
                            //При этом каждый раз проверяется нет ли в RTree совпадающего узла (с учетом допуска)
                            //с помощью knn search

                            Polyline edgePoly = (Polyline)tr.GetObject(polyId, OpenMode.ForRead);
                            //точки полилинии
                            List<Point2d> polyPts = new List<Point2d>();
                            for (int i = 0; i < edgePoly.NumberOfVertices; i++)
                            {
                                polyPts.Add(edgePoly.GetPoint2dAt(i));
                            }


                            //Формируются два новых объекта узлов графа по концам полилинии
                            Point2d startPt = polyPts.First();
                            NetworkNode startNode = new NetworkNode(startPt);
                            Point2d lastPt = polyPts.Last();
                            NetworkNode endNode = new NetworkNode(lastPt);

                            //Для полилинии формируется объект ребра графа
                            NetworkEdge edge = new NetworkEdge(polyPts);
                            //сразу добавить в общий список
                            networkEdges.Add(edge);

                            //Определить актуальные узлы графа с учетом уже добавленных
                            NetworkNode actualStartNode = startNode;
                            {
                                IReadOnlyList<NetworkNode> nodesInStartPoint
                                = networkNodesRbush.KnnSearch(startPt.X, startPt.Y, 1, maxDist: Tolerance.Global.EqualPoint);
                                if (nodesInStartPoint.Count > 0)
                                {
                                    actualStartNode = nodesInStartPoint.First();
                                }
                                else
                                {
                                    //добавить в RTree и список
                                    networkNodesRbush.Insert(actualStartNode);
                                    networkNodesList.Add(actualStartNode);
                                }
                            }

                            NetworkNode actualEndNode = endNode;
                            {
                                IReadOnlyList<NetworkNode> nodesInEndPoint
                                = networkNodesRbush.KnnSearch(lastPt.X, lastPt.Y, 1, maxDist: Tolerance.Global.EqualPoint);
                                if (nodesInEndPoint.Count > 0)
                                {
                                    actualEndNode = nodesInEndPoint.First();
                                }
                                else
                                {
                                    //добавить в RTree и список
                                    networkNodesRbush.Insert(actualEndNode);
                                    networkNodesList.Add(actualEndNode);
                                }
                            }

                            //Связать ребро и узлы
                            edge.StartNode = actualStartNode;
                            edge.EndNode = actualEndNode;
                            actualStartNode.AttachedEdges.Add(edge);
                            actualEndNode.AttachedEdges.Add(edge);

                        }
                    }
                }

                //6. Поиск возможных Т-образных примыканий полилиний
                //Для этого по каждому сегменту каждой полилинии в RTree узлов графа искать 1 ближайший узел
                //на расстоянии Tolerance.Global.EqualPoint. Если найдено, то это Т-образное пересечние
                //Соответствующее ребро графа нужно разбить на 2


                //7. Сопоставление данных из Excel
                //Для каждого узла графа
                //- проверить есть ли блок колодца в этом узле
                //- если есть искать подпись колодца  (в радиусе 5-6 ед дл?)
                //- если найдена определить в каком квадрате находится колодец
                //- найти соответствующие данные из Excel по этому колодцу
                //- если найдены, искать ближайшие подписи примыканий к колодцам (в радиусе 3-4 ед дл?) (ЕСЛИ ПРИМЫКАНИЕ ТОЛЬКО ОДНО, ТО ПОДПИСИ НЕ БУДЕТ)
                //- для каждого из примыканий искать ближайшие подписи примыканий к примыкающему сегменту полилинии
                //- присвоить примыканию номер, который находится ближе всего к сегменту и находится в заданном радиусе от колодца
                //  если возникает спорная ситуация (когда есть 2 подходящих подписи на одном расстоянии от сегмента), то переходить к следующему примыканию
                //  если за один обход примыканий спорная ситуация не разрешилась, то необходим ввод пользователя!!!

                //8. Задание всех основных глубин заложения
                //Для тех узлов, в которых есть блок колодца, но не найдены данные в Excel
                //будет принята какая-то глубина дна по умолчанию (TODO: Добавить поле ввода в окно!!!)
                //Для каждого ребра
                //- если не задана глубина заложения на одном из концов, сделать их равными
                //- если не задана глубина на обоих концах задать обоим концам глубину по умолчанию согласно вводу в окне
                //Убедиться, что если в одном узле без колодца стыкуются несколько ребер, 
                //то в месте стыковки обязательно у всех ребер должна быть одинаковая отметка


                //9. Задание отметок промежуточных точек на ребрах сети
                //если у ребра есть данные вытянутые из Excel, то интерполировать отметки между обоими сторонами
                //если данных из Excel нет, то высчитывать отметки относительно поверхности земли
                //если сеть прокладывается не на одной глубине заложения, то отметки промежуточных точек по интерполяции 

                tr.Commit();
            }
            #endregion

        }

        /// <summary>
        /// Полилиния является квадратом
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        private bool IsSquare(Polyline poly)
        {
            //Полилиния содержит 4 или 5 точек (если 4 то полилиния замкнута, если 5 то 1я и 5я точки совпадают)
            int vertsNum = poly.NumberOfVertices;
            bool verts4 = vertsNum == 4 && poly.Closed;
            bool verts5 = false;
            if (vertsNum == 5)
            {
                Point2d p0 = poly.GetPoint2dAt(0);
                Point2d p4 = poly.GetPoint2dAt(4);
                verts5 = p0.IsEqualTo(p4);
            }
            if (verts4 || verts5)
            {
                //точки
                Point2d p0 = poly.GetPoint2dAt(0);
                Point2d p1 = poly.GetPoint2dAt(1);
                Point2d p2 = poly.GetPoint2dAt(2);
                Point2d p3 = poly.GetPoint2dAt(3);

                //стороны
                List<LineSegment2d> sides = new List<LineSegment2d>();
                sides.Add(new LineSegment2d(p0, p1));
                sides.Add(new LineSegment2d(p1, p2));
                sides.Add(new LineSegment2d(p2, p3));
                sides.Add(new LineSegment2d(p3, p0));

                //2 стороны вертикальны 2 стороны горизонтальны
                //стороны равны по длине
                int vertCount = 0;
                int horCount = 0;
                double length = -1;
                foreach (LineSegment2d ls in sides)
                {
                    if (ls.Direction.IsParallelTo(Vector2d.YAxis))
                    {
                        vertCount++;
                    }
                    else
                    if (ls.Direction.IsParallelTo(Vector2d.XAxis))
                    {
                        horCount++;
                    }

                    if (length < 0)
                    {
                        length = ls.Length;
                    }
                    else if (!Utils.LengthIsEquals(length, ls.Length))//здесь должен быть допуск
                    {
                        return false;//стороны не равны
                    }
                }

                if (vertCount == 2 && horCount == 2)
                {
                    return true;
                }

            }
            return false;


        }



        private class GridSquare : ISpatialData
        {
            private Envelope _envelope;
            public ref readonly Envelope Envelope => ref _envelope;

            public int SquareKey { get; set; }

            public GridSquare(Point3d minPt, Point3d maxPt)
            {
                _envelope = new Envelope(minPt.X, minPt.Y, maxPt.X, maxPt.Y);
            }
        }

        private class StructurePosition : ISpatialData
        {
            private Envelope _envelope;
            public ref readonly Envelope Envelope => ref _envelope;

            public ObjectId BlockReferenceId { get; private set; }

            public double BlockRefRotation { get; private set; }

            public ObjectId BlockTableRecordId { get; private set; }

            public StructurePosition(Point3d blockPos, ObjectId blockReferenceId,
                ObjectId blockTableRecordId, double blockRefRotation)
            {
                _envelope = new Envelope(blockPos.X, blockPos.Y, blockPos.X, blockPos.Y);
                BlockReferenceId = blockReferenceId;
                BlockTableRecordId = blockTableRecordId;
                BlockRefRotation = blockRefRotation;
            }
        }

        private class TextPosition : ISpatialData
        {
            private Envelope _envelope;
            public ref readonly Envelope Envelope => ref _envelope;

            public string TextContent { get; private set; }

            public TextPosition(Point3d minPt, Point3d maxPt, string textContent)
            {
                _envelope = new Envelope(minPt.X, minPt.Y, maxPt.X, maxPt.Y);
                TextContent = textContent;
            }
        }



        /// <summary>
        /// Узел графа
        /// </summary>
        private class NetworkNode : ISpatialData
        {
            private Envelope _envelope;
            public ref readonly Envelope Envelope => ref _envelope;

            //id блока если он есть в этой точке
            public ObjectId BlockTableRecordId { get; set; }

            public double BlockRefRotation { get; set; }

            //данные из Excel если есть
            public WellData WellData { get; set; }

            public List<NetworkEdge> AttachedEdges { get; set; } = new List<NetworkEdge>();

            public NetworkNode(Point2d nodePos)
            {
                _envelope = new Envelope(nodePos.X, nodePos.Y, nodePos.X, nodePos.Y);
            }
        }

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
            public List<XYZ> PositionList { get; private set; } = new List<XYZ>();

            public NetworkEdge(IEnumerable<Point2d> polyPts)
            {
                foreach (Point2d pt in polyPts)
                {
                    PositionList.Add(new XYZ(pt));
                }
            }
        }

        private class XYZ
        {
            public Point2d Position2d { get; set; }
            public double Z { get; set; }

            public XYZ(Point2d position2d)
            {
                Position2d = position2d;
            }

        }

    }





}
