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
using AcadDB = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Text.RegularExpressions;
using CivilDB = Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices.Styles;

namespace Civil3DInfoTools.PipeNetworkCreating
{
    //TODO: Проверить на пустом чертеже
    public partial class PipeNetworkGraph
    {
        private Document doc = null;
        private ConfigureNetworkCreationViewModel configsViewModel = null;
        private CivilDocument cdoc = null;

        private string communicationLayerName = "сеть";

        //квадраты сетки
        private RBush<GridSquare> gridSquares = new RBush<GridSquare>();

        //положения блоков колодцев (только заданные блоки)
        private RBush<StructurePosition> structurePositions = new RBush<StructurePosition>();

        //положения блоков колодцев (все блоки в слое колодцев)
        private RBush<StructurePosition> allStructurePositions = new RBush<StructurePosition>();

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

        //маркеры
        private HashSet<StructurePosition> structuresNearPolylineNotInEndPointToDraw = new HashSet<StructurePosition>();


        private List<JunctionLabelingMarkerToDraw> junctionLabelingToDraw = new List<JunctionLabelingMarkerToDraw>();

        private List<WellLabelingMarkerToDraw> wellLabelingToDraw = new List<WellLabelingMarkerToDraw>();

        private List<GridSquare> squaresWithNoDataToDraw = new List<GridSquare>();

        private Dictionary<NetworkNode, NodeWarnings> nodeWarnings = new Dictionary<NetworkNode, NodeWarnings>();

        private void AddNodeWarning(NetworkNode nn, NodeWarnings wToAdd)
        {
            NodeWarnings w = NodeWarnings.Null;
            nodeWarnings.TryGetValue(nn, out w);
            w = w | wToAdd;
            if (w != NodeWarnings.Null)
                nodeWarnings[nn] = w;
        }

        private HashSet<TextPosition> wellLblMapped = new HashSet<TextPosition>();

        private HashSet<TextPosition> LblDuplicatesToDraw = new HashSet<TextPosition>();

        private HashSet<TextPosition> junctionLblMapped = new HashSet<TextPosition>();

        private HashSet<TextPosition> junctionLblDuplicatesToDraw = new HashSet<TextPosition>();






        /// <summary>
        /// Построение графа инженерной сети
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="configsViewModel">обязательно все настройки должны быть назначены</param>
        public PipeNetworkGraph(Document doc, CivilDocument cdoc, ConfigureNetworkCreationViewModel configsViewModel)
        {
            this.doc = doc;
            this.cdoc = cdoc;
            this.configsViewModel = configsViewModel;
            Database db = doc.Database;
            Editor ed = doc.Editor;


            ObjectIdCollection toTop = new ObjectIdCollection();

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
                communicationLayerName = communicationLtr.Name;

                //блоки
                HashSet<ObjectId> blocks = configsViewModel.Blocks;

                //данные о колодцах
                Dictionary<int, Dictionary<string, WellData>> wellsData = configsViewModel.ExcelReader?.WellsData;

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
                //присвоение квадратам номеров по текстам, которые попадают в эти квадраты
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
                                            = Convert.ToInt32(txtContent.Replace("_", "").Replace("-", "").Replace(" ", ""));
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
                    //List<string> blockNames = new List<string>();
                    //foreach (ObjectId btrId in blocks)
                    //{
                    //    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    //    blockNames.Add(btr.Name);
                    //}
                    TypedValue[] tv = new TypedValue[]
                                {
                    new TypedValue(0, "INSERT"),
                    //new TypedValue(2, String.Join(",", blockNames)),
                    new TypedValue(8, structureBlocksLayerName + "," + communicationLayerName),
                                };
                    SelectionFilter flt = new SelectionFilter(tv);
                    PromptSelectionResult psr = ed.SelectAll(flt);

                    SelectionSet ss = psr.Value;
                    if (ss != null)
                    {
                        List<StructurePosition> _blockPositions = new List<StructurePosition>();
                        List<StructurePosition> _allBlockPositions = new List<StructurePosition>();
                        foreach (SelectedObject so in ss)
                        {
                            BlockReference br = (BlockReference)tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            StructurePosition structurePosition
                                = new StructurePosition(br.Position, br.Id, br.BlockTableRecord, br.Rotation);
                            if (blocks.Contains(br.BlockTableRecord))
                            {
                                _blockPositions.Add(structurePosition);
                            }
                            //в общий набор скидывать
                            //- все блоки в слое колодцев
                            //- блоки в слое сети и заданного типа
                            if (br.LayerId.Equals(configsViewModel.StructuresLayerId.Value)
                                || blocks.Contains(br.BlockTableRecord))
                            {
                                _allBlockPositions.Add(structurePosition);
                            }

                        }
                        //загрузить положения блоков в RTree
                        structurePositions.BulkLoad(_blockPositions);
                        allStructurePositions.BulkLoad(_allBlockPositions);
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
                                    .Add(new TextPosition(ext.Value.MinPoint, ext.Value.MaxPoint, txtContent, so.ObjectId));
                            }
                        }
                        structureLabelPositions.BulkLoad(txtPositions);
                    }
                }

                //4. Записать в RTree положение всех текстовых примитивов в слое выбранной сети - это подписи сети,
                //содержащие атрибутику, а так же привязки примыканий к колодцам
                //TODO: При переборе текстов они дифференцируются на группы в соответствии с тем, что в них записано
                //На данный момент отбирается только один вид подписей - НОМЕРА ПРИМЫКАНИЙ К КОЛОДЦАМ (ПРОСТО ЦИФРА, ОРИЕНТАЦИЯ - ГОРИЗОНТАЛЬНАЯ)
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
                            string txtContent = (txtEnt is DBText ?
                                (txtEnt as DBText).TextString
                                : (txtEnt as MText).Text).Trim();

                            Extents3d? ext = txtEnt.Bounds;
                            if (ext != null)
                            {
                                //Определить ориентацию текста
                                double rotation = txtEnt is DBText ?
                                (txtEnt as DBText).Rotation
                                : (txtEnt as MText).Rotation;
                                Vector2d orientationVector = Vector2d.XAxis.RotateBy(rotation);

                                if (pipeJunctionLabelRegex.IsMatch(txtContent)
                                    //&& orientationVector.IsCodirectionalTo(Vector2d.XAxis)
                                    //&& orientationVector.GetAngleTo(Vector2d.XAxis)<0.052//отклонение не более 3 градусов
                                    //вообще отказаться от этого условия
                                    )
                                {
                                    _pipeJunctionLabelPositions
                                        .Add(new TextPosition(ext.Value.MinPoint, ext.Value.MaxPoint, txtContent, so.ObjectId));
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
                            if (edgePoly.Length == 0//если полилиния нулевой длины, то не интересно
                                || edgePoly.Closed || edgePoly.GetPoint2dAt(0)
                                .IsEqualTo(edgePoly.GetPoint2dAt(edgePoly.NumberOfVertices - 1))//если полилиния замкнута, то не интересно
                                )
                            {
                                continue;
                            }

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
                            NetworkEdge edge = new NetworkEdge(polyPts, polyId);
                            //сразу добавить в общий список
                            networkEdges.Add(edge);

                            //Определить актуальные узлы графа с учетом уже добавленных
                            NetworkNode actualStartNode = startNode;
                            {
                                IReadOnlyList<NetworkNode> nodesInStartPoint
                                = networkNodesRbush.KnnSearch(startPt.X, startPt.Y, 1, maxDist: ZERO_LENGTH)
                                .Select(it => it.Item).ToList();//не должно быть более одного
                                if (nodesInStartPoint.Count() > 0)
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
                                = networkNodesRbush.KnnSearch(lastPt.X, lastPt.Y, 1, maxDist: ZERO_LENGTH)
                                .Select(it => it.Item).ToList();
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

                //6. TODO?: Поиск возможных Т-образных примыканий полилиний и блоков рядом с полилиниями
                //
                foreach (NetworkEdge edge in networkEdges)
                {
                    Point2d startPt = edge.PositionList[0];
                    Point2d endPt = edge.PositionList[edge.PositionList.Count - 1];

                    for (int i = 0; i < edge.PositionList.Count - 1; i++)
                    {
                        Point2d pt0 = edge.PositionList[i];
                        Point2d pt1 = edge.PositionList[i + 1];

                        //поиск т-образных примыканий
                        List<NetworkNode> nnList = networkNodesRbush
                            .KnnSearch(pt0.X, pt0.Y, 0, maxDist: ZERO_LENGTH, x2: pt1.X, y2: pt1.Y)
                            .Select(it => it.Item).ToList();
                        foreach (NetworkNode nn in nnList)
                        {
                            Point2d nnPos = new Point2d(nn.Envelope.MaxX, nn.Envelope.MaxY);
                            if (!nnPos.IsEqualTo(startPt) && !nnPos.IsEqualTo(endPt))
                            {
                                AddNodeWarning(nn, NodeWarnings.TShapedIntersection);
                            }
                        }

                        List<StructurePosition> strList = structurePositions
                            .KnnSearch(pt0.X, pt0.Y, 0, maxDist: BLOCK_NEAR_POLYLINE_DISTANCE, x2: pt1.X, y2: pt1.Y)
                            .Select(it => it.Item).ToList();
                        foreach (StructurePosition str in strList)
                        {
                            Point2d strPos = Utils.Point2DBy3D(str.Position);
                            if (!strPos.IsEqualTo(startPt) && !strPos.IsEqualTo(endPt)
                                && !structuresNearPolylineNotInEndPointToDraw.Contains(str))
                            {
                                structuresNearPolylineNotInEndPointToDraw.Add(str);
                            }
                        }

                    }
                }


                //7. Сопоставление данных из Excel
                //Для каждого узла графа
                //- проверить есть ли блок колодца в этом узле
                //- если есть искать подпись колодца  (в радиусе 5-6 ед дл?)
                //- если найдена определить в каком квадрате находится колодец
                //- найти соответствующие данные из Excel по этому колодцу
                //- если найдены, искать ближайшие подписи примыканий к колодцам (в радиусе 3-4 ед дл?) (ЕСЛИ ПРИМЫКАНИЕ ТОЛЬКО ОДНО, ТО ПОДПИСИ НЕ БУДЕТ)
                //- сопоставить подписи примыканий и сами примыкания к колодцам
                foreach (NetworkNode nn in networkNodesList)
                {
                    double x = nn.Envelope.MaxX;
                    double y = nn.Envelope.MaxY;

                    IReadOnlyList<StructurePosition> structuresAtNode
                        = structurePositions.KnnSearch(x, y, 0, maxDist: ZERO_LENGTH).Select(it => it.Item).ToList();
                    if (structuresAtNode.Count > 0)
                    {
                        //если блоков более одного, то принимается блок, который больше по площади BoundingBox
                        StructurePosition structBlock = GetBiggestBlock(structuresAtNode);
                        nn.StructureBlock = structBlock;
                        //Захватить подписи колодцев поблизости
                        IReadOnlyList<TextPosition> lblsNearNode
                            = structureLabelPositions.KnnSearch(x, y, 0, maxDist: DISTANCE_TO_GET_WELL_LBL).Select(it => it.Item).ToList();
                        //Захватить колодцы поблизости
                        List<StructurePosition> structNearNode
                            = allStructurePositions.KnnSearch(x, y, 0, maxDist: DISTANCE_TO_GET_WELL_LBL).Select(it => it.Item).ToList();

                        //если два блока имеют одинаковую точку вставки, то рассматирвать только 1 (который больше)
                        HashSet<StructurePosition> structNearNodeHS = new HashSet<StructurePosition>(structNearNode);
                        RBush<StructurePosition> structNearNodeRBush = new RBush<StructurePosition>();
                        structNearNodeRBush.BulkLoad(structNearNode);
                        List<StructurePosition> structNearNode2 = new List<StructurePosition>();

                        while (structNearNodeHS.Count > 0)
                        {
                            StructurePosition sp = structNearNodeHS.First();
                            double x1 = sp.Position.X;
                            double y1 = sp.Position.Y;
                            List<StructurePosition> blocksSamePt
                                = structNearNodeRBush.KnnSearch(x1, y1, 0, maxDist: ZERO_LENGTH)
                                .Select(it => it.Item).ToList();

                            structNearNode2.Add(GetBiggestBlock(blocksSamePt));

                            foreach (StructurePosition toRemove in blocksSamePt)
                            {
                                structNearNodeHS.Remove(toRemove);
                            }
                        }

                        WellLabelingSolver wellsSolver = new WellLabelingSolver(structBlock, structNearNode2, lblsNearNode);



                        if (wellsSolver.TextResult != null)
                        {
                            TextPosition wellLbl = wellsSolver.TextResult;
                            if (!wellLblMapped.Contains(wellLbl))
                            {
                                wellLblMapped.Add(wellLbl);
                            }
                            else
                            {
                                //еще один колодец привязывается к одной и той же подписи
                                if (!LblDuplicatesToDraw.Contains(wellLbl))
                                    LblDuplicatesToDraw.Add(wellLbl);
                            }




                            WellLabelingMarkerToDraw wellMarker
                            = new WellLabelingMarkerToDraw(structBlock, wellLbl);
                            wellLabelingToDraw.Add(wellMarker);


                            if (wellsData == null || wellsData.Count == 0)
                                continue;


                            IReadOnlyList<GridSquare> squares = gridSquares.Search(nn.Envelope);
                            if (squares.Count > 0)
                            {
                                GridSquare gridSquare = squares.First();

                                Dictionary<string, WellData> squareData = null;
                                wellsData.TryGetValue(gridSquare.SquareKey, out squareData);

                                if (squareData != null)
                                {
                                    WellData wellData = null;
                                    squareData.TryGetValue(wellLbl.TextContent, out wellData);
                                    if (wellData != null)
                                    {
                                        if (nn.AttachedEdges.Count != wellData.PipeJunctions.Count)
                                        {
                                            //количество присоединений не соответствует Excel
                                            AddNodeWarning(nn, NodeWarnings.AttachmentCountNotMatches);
                                        }


                                        nn.WellData = wellData;
                                        wellMarker.ExcelMatch = true;
                                        if (nn.AttachedEdges.Count > 0 && wellData.PipeJunctions.Count > 0)
                                        {
                                            if (nn.AttachedEdges.Count == 1)
                                            {
                                                //если присоединение только одно, то данные сразу привязать к трубе
                                                PipeJunctionData jData = wellData.PipeJunctions.First().Value;
                                                NetworkEdge en = nn.AttachedEdges.First();
                                                if (en.StartNode == nn)
                                                    en.StartPipeJunctionData = jData;
                                                else
                                                    en.EndPipeJunctionData = jData;
                                            }
                                            else if (nn.AttachedEdges.Count > 1)
                                            {
                                                //найти подписи присоединений рядом с колодцем
                                                List<DistanceItem<TextPosition>> junctionLblsNearNNWithDistance = pipeJunctionLabelPositions
                                                        .KnnSearch(x, y, 0, maxDist: DISTANCE_TO_GET_JUNCTION_LBLS).ToList();
                                                List<TextPosition> junctionLblsNearNN = junctionLblsNearNNWithDistance.Select(it => it.Item).ToList();

                                                if (junctionLblsNearNN.Count > 0)
                                                {
                                                    PipeJunctionLabelingSolver2 solver
                                                        = new PipeJunctionLabelingSolver2(nn, junctionLblsNearNN);
                                                    if (solver.LabelingResult != null)
                                                    {
                                                        int colorIndex = 1;

                                                        foreach (KeyValuePair<NetworkEdge, TextPosition> kvp in solver.LabelingResult)
                                                        {
                                                            bool start = kvp.Key.StartNode == nn;

                                                            string junctionKey = kvp.Value.TextContent;
                                                            PipeJunctionData pjd = null;
                                                            wellData.PipeJunctions.TryGetValue(junctionKey, out pjd);
                                                            if (pjd != null)
                                                            {
                                                                if (start)
                                                                {
                                                                    kvp.Key.StartPipeJunctionData = pjd;
                                                                }
                                                                else
                                                                {
                                                                    kvp.Key.EndPipeJunctionData = pjd;
                                                                }
                                                            }

                                                            JunctionLabelingMarkerToDraw marker
                                                                = new JunctionLabelingMarkerToDraw(kvp.Key, start, colorIndex, kvp.Value, pjd != null);
                                                            junctionLabelingToDraw.Add(marker);

                                                            if (!junctionLblMapped.Contains(kvp.Value))
                                                            {
                                                                junctionLblMapped.Add(kvp.Value);
                                                            }
                                                            else
                                                            {
                                                                //еще один колодец привязывается к одной и той же подписи
                                                                if (!junctionLblDuplicatesToDraw.Contains(kvp.Value))
                                                                    junctionLblDuplicatesToDraw.Add(kvp.Value);
                                                            }

                                                            colorIndex++;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        //не найдено соответствие с подписями присоединений
                                                        AddNodeWarning(nn, NodeWarnings.JunctionLblsNotFound);
                                                    }
                                                }

                                            }
                                        }


                                    }
                                }
                                else
                                {
                                    //Если squareData == null, значит по этому квадрату нет Excelя -- Выдать предупреждение
                                    squaresWithNoDataToDraw.Add(gridSquare);
                                }
                            }
                        }
                        else
                        {
                            //не найдена подпись колодца
                            AddNodeWarning(nn, NodeWarnings.WellLblNotFound);
                        }
                    }

                }

                tr.Commit();
            }

        }

        private static StructurePosition GetBiggestBlock(IReadOnlyList<StructurePosition> structuresAtNode)
        {
            StructurePosition structBlock = null;
            double sizeParam = double.NegativeInfinity;
            foreach (StructurePosition sp in structuresAtNode)
            {
                Extents3d? ext = sp.BlockReferenceId.GetObject(OpenMode.ForRead).Bounds;
                if (ext != null)
                {
                    double currSizeParam = (ext.Value.MaxPoint.X - ext.Value.MinPoint.X)
                        * (ext.Value.MaxPoint.Y - ext.Value.MinPoint.Y);

                    if (currSizeParam > sizeParam)
                    {
                        structBlock = sp;
                        sizeParam = currSizeParam;
                    }
                }

            }

            return structBlock;
        }

        public void DrawMarkers()
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                BlockTableRecord ms
                    = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                //DBDictionary gd = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite);

                //Очистить модель от всех маркеров
                TypedValue[] tv = new TypedValue[]
                            {
                    new TypedValue(8, MARKER_LAYER),
                            };
                SelectionFilter flt = new SelectionFilter(tv);
                PromptSelectionResult psr = ed.SelectAll(flt);

                SelectionSet ss = psr.Value;
                if (ss != null)
                {
                    foreach (ObjectId id in ss.GetObjectIds())
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                        ent.Erase();
                    }
                }


                ObjectId layerId = Utils.CreateLayerIfNotExists(MARKER_LAYER, db, tr);
                ObjectId txtStyleId = Utils.GetStandardTextStyle(db, tr);

                foreach (StructurePosition str in structuresNearPolylineNotInEndPointToDraw)
                {
                    Point3d target = str.Position;
                    Point3d txtPt = target + Vector3d.YAxis * (-7) + Vector3d.XAxis * 7;

                    using (MLeader leader = new MLeader())
                    using (MText mText = new MText())
                    {
                        mText.Contents = BLOCK_NEAR_POLYLINE_NOT_ON_ENDPOINT_MESSAGE;
                        mText.Location = txtPt;
                        mText.ColorIndex = BLOCK_NEAR_POLYLINE_NOT_ON_ENDPOINT_COLOR_INDEX;
                        mText.LayerId = layerId;
                        mText.TextStyleId = txtStyleId;
                        mText.LineWeight = LineWeight.LineWeight030;
                        mText.TextHeight = 3;

                        leader.SetDatabaseDefaults(db);
                        leader.ContentType = ContentType.MTextContent;
                        leader.MText = mText;
                        leader.ColorIndex = BLOCK_NEAR_POLYLINE_NOT_ON_ENDPOINT_COLOR_INDEX;
                        leader.LayerId = layerId;
                        leader.LineWeight = LineWeight.LineWeight030;

                        int idx = leader.AddLeaderLine(target);
                        leader.AddFirstVertex(idx, target);

                        ms.AppendEntity(leader);
                        tr.AddNewlyCreatedDBObject(leader, true);
                    }
                }

                foreach (JunctionLabelingMarkerToDraw marker in junctionLabelingToDraw)
                {
                    using (Polyline markerPoly = new Polyline())
                    {
                        markerPoly.ColorIndex = marker.ColorIndex;

                        Point2d pt0 = (marker.Start ? marker.NetworkEdge.PositionList[0]
                            : marker.NetworkEdge.PositionList[marker.NetworkEdge.PositionList.Count - 1]);
                        Point2d ptDir = (marker.Start ?
                            marker.NetworkEdge.PositionList[1]
                            : marker.NetworkEdge.PositionList[marker.NetworkEdge.PositionList.Count - 2]);
                        Vector2d dir = ptDir - pt0;
                        dir = dir.GetNormal() * 0.75;
                        Point2d pt1 = pt0 + dir;

                        markerPoly.AddVertexAt(0, pt0, 0, 0.1, 0);
                        markerPoly.AddVertexAt(1, pt1, 0, 0, 0);


                        int polyVertNum = 2;
                        TextPosition txt = marker.TextPosition;
                        LinkMarkerWithText(markerPoly, pt1, polyVertNum, txt);

                        markerPoly.LineWeight = LineWeight.LineWeight053;
                        markerPoly.LayerId = layerId;

                        ms.AppendEntity(markerPoly);
                        tr.AddNewlyCreatedDBObject(markerPoly, true);

                    }
                    if (marker.ExcelMatch)
                    {
                        Entity txtEnt = (Entity)marker.TextPosition.TxtId.GetObject(OpenMode.ForRead);
                        double height = txtEnt is DBText ? (txtEnt as DBText).Height : (txtEnt as MText).TextHeight;

                        using (DBText excelMatchTxt = new DBText())
                        {
                            excelMatchTxt.TextStyleId = txtStyleId;
                            excelMatchTxt.Height = height / 2;
                            excelMatchTxt.Position = marker.TextPosition.CornerPts[3] + (Vector3d.XAxis * 0.05);
                            excelMatchTxt.TextString = DATA_MATCHING_MESSAGE;
                            excelMatchTxt.ColorIndex = marker.ColorIndex;
                            excelMatchTxt.LayerId = layerId;
                            excelMatchTxt.LineWeight = LineWeight.LineWeight030;

                            ms.AppendEntity(excelMatchTxt);
                            tr.AddNewlyCreatedDBObject(excelMatchTxt, true);
                        }
                    }

                }


                foreach (WellLabelingMarkerToDraw marker in wellLabelingToDraw)
                {
                    using (Polyline markerPoly = new Polyline())
                    {
                        markerPoly.ColorIndex = WELL_MARKER_COLOR_INDEX;

                        Point2d pt = Utils.Point2DBy3D(marker.StructurePosition.Position);
                        markerPoly.AddVertexAt(0, pt, 0, 0, 0);

                        TextPosition txt = marker.TextPosition;
                        LinkMarkerWithText(markerPoly, pt, 1, txt);

                        markerPoly.LineWeight = LineWeight.LineWeight053;
                        markerPoly.LayerId = layerId;

                        ms.AppendEntity(markerPoly);
                        tr.AddNewlyCreatedDBObject(markerPoly, true);
                    }
                    if (marker.ExcelMatch)
                    {
                        Entity txtEnt = (Entity)marker.TextPosition.TxtId.GetObject(OpenMode.ForRead);
                        double height = txtEnt is DBText ? (txtEnt as DBText).Height : (txtEnt as MText).TextHeight;

                        using (DBText excelMatchTxt = new DBText())
                        {
                            excelMatchTxt.TextStyleId = txtStyleId;
                            excelMatchTxt.Height = height / 2;
                            excelMatchTxt.Position = marker.TextPosition.CornerPts[3] + (Vector3d.XAxis * 0.05);
                            excelMatchTxt.TextString = DATA_MATCHING_MESSAGE;
                            excelMatchTxt.ColorIndex = WELL_MARKER_COLOR_INDEX;
                            excelMatchTxt.LayerId = layerId;
                            excelMatchTxt.LineWeight = LineWeight.LineWeight030;

                            ms.AppendEntity(excelMatchTxt);
                            tr.AddNewlyCreatedDBObject(excelMatchTxt, true);
                        }
                    }
                }


                foreach (GridSquare sq in squaresWithNoDataToDraw)
                {
                    Point2d[] pts = Utils.GetPointsToDraw(sq.Envelope);
                    using (Polyline markerPoly = new Polyline())
                    {
                        markerPoly.ColorIndex = SQUARES_WITH_NO_DATA_COLOR_INDEX;

                        for (int i = 0; i < pts.Length; i++)
                        {
                            Point2d pt = pts[i];
                            markerPoly.AddVertexAt(i, pt, 0, 0, 0);
                        }

                        markerPoly.LineWeight = LineWeight.LineWeight053;
                        markerPoly.LayerId = layerId;
                        markerPoly.Closed = true;

                        ms.AppendEntity(markerPoly);
                        tr.AddNewlyCreatedDBObject(markerPoly, true);
                    }

                    using (DBText noDataErrTxt = new DBText())
                    {
                        noDataErrTxt.TextStyleId = txtStyleId;
                        noDataErrTxt.Height = 5;
                        noDataErrTxt.Position = new Point3d(pts[0].X, pts[0].Y, 0) + (Vector3d.XAxis * 0.05) + (Vector3d.YAxis * 0.05);
                        noDataErrTxt.TextString = SQUARE_WITH_NO_DATA_MESSAGE;
                        noDataErrTxt.ColorIndex = SQUARES_WITH_NO_DATA_COLOR_INDEX;
                        noDataErrTxt.LayerId = layerId;
                        noDataErrTxt.LineWeight = LineWeight.LineWeight030;

                        ms.AppendEntity(noDataErrTxt);
                        tr.AddNewlyCreatedDBObject(noDataErrTxt, true);
                    }
                }


                foreach (KeyValuePair<NetworkNode, NodeWarnings> kvp in nodeWarnings)
                {
                    List<string> warningMessages = new List<string>();
                    foreach (KeyValuePair<NodeWarnings, string> wm in nodeWarningsMessages)
                    {
                        if (kvp.Value.HasFlag(wm.Key))
                        {
                            warningMessages.Add(wm.Value);
                        }
                    }

                    string message = String.Join(MText.LineBreak.ToUpper(), warningMessages);

                    Point3d target = new Point3d(kvp.Key.Envelope.MaxX, kvp.Key.Envelope.MaxY, 0);
                    Point3d txtPt = target + Vector3d.YAxis * 7 + Vector3d.XAxis * 7;

                    using (MLeader leader = new MLeader())
                    using (MText mText = new MText())
                    {
                        //mText.SetDatabaseDefaults(db);
                        mText.Contents = message;
                        mText.Location = txtPt;
                        mText.ColorIndex = NODE_WARNING_COLOR_INDEX;
                        mText.LayerId = layerId;
                        mText.TextStyleId = txtStyleId;
                        mText.LineWeight = LineWeight.LineWeight030;
                        mText.TextHeight = 3;

                        leader.SetDatabaseDefaults(db);
                        leader.ContentType = ContentType.MTextContent;
                        leader.MText = mText;
                        leader.ColorIndex = NODE_WARNING_COLOR_INDEX;
                        leader.LayerId = layerId;
                        leader.LineWeight = LineWeight.LineWeight030;

                        int idx = leader.AddLeaderLine(target);
                        leader.AddFirstVertex(idx, target);

                        ms.AppendEntity(leader);
                        tr.AddNewlyCreatedDBObject(leader, true);
                    }
                }


                foreach (TextPosition lblTxt in LblDuplicatesToDraw.Concat(junctionLblDuplicatesToDraw))
                {
                    Point3d target = lblTxt.CornerPts[2];
                    Point3d txtPt = target + Vector3d.YAxis * 7 + Vector3d.XAxis * 7;

                    using (MLeader leader = new MLeader())
                    using (MText mText = new MText())
                    {
                        mText.Contents = LBL_DULICATE_MESSAGE;
                        mText.Location = txtPt;
                        mText.ColorIndex = LBL_DULICATE_COLOR_INDEX;
                        mText.LayerId = layerId;
                        mText.TextStyleId = txtStyleId;
                        mText.LineWeight = LineWeight.LineWeight030;
                        mText.TextHeight = 3;

                        leader.SetDatabaseDefaults(db);
                        leader.ContentType = ContentType.MTextContent;
                        leader.MText = mText;
                        leader.ColorIndex = LBL_DULICATE_COLOR_INDEX;
                        leader.LayerId = layerId;
                        leader.LineWeight = LineWeight.LineWeight030;

                        int idx = leader.AddLeaderLine(target);
                        leader.AddFirstVertex(idx, target);

                        ms.AppendEntity(leader);
                        tr.AddNewlyCreatedDBObject(leader, true);
                    }
                }


                tr.Commit();
            }

        }





        private static void LinkMarkerWithText(Polyline markerPoly, Point2d pt, int polyVertNum, TextPosition txt)
        {
            int nearestCornerIndex = txt.GetNearestCorner(new Point3d(pt.X, pt.Y, 0));
            int repeatCounter = 0;
            for (int i = nearestCornerIndex; repeatCounter < 5; i = (i + 1) % 4)
            {
                markerPoly.AddVertexAt(polyVertNum, Utils.Point2DBy3D(txt.CornerPts[i]), 0, 0, 0);

                repeatCounter++;
                polyVertNum++;
            }

            //return polyVertNum;
        }



        public void CreatePipeNenwork()
        {
            Database db = doc.Database;
            Dictionary<ObjectId, SelectedPartTypeId> blockStructureMapping = configsViewModel.BlockStructureMapping;

            //Создать новую сеть
            ObjectId networkId = CivilDB.Network.Create(cdoc, ref communicationLayerName);

            ObjectId tinSurfId = configsViewModel.TinSurfaceId.Value;

            double defaultSumpDepth = configsViewModel.WellDepthVM.NumValue;

            double defaultPipeDepth = configsViewModel.CommunicationDepthVM.NumValue;

            bool sameDepth = configsViewModel.SameDepth;

            ObjectId pipeFamId = configsViewModel.PipeType.PartFamId;
            ObjectId pipeSizeId = configsViewModel.PipeType.PartSizeId;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                CivilDB.Network network = (CivilDB.Network)tr.GetObject(networkId, OpenMode.ForWrite);

                network.PartsListId = configsViewModel.SelectedPartsList.Id;

                CivilDB.TinSurface tinSurf = (CivilDB.TinSurface)tr.GetObject(tinSurfId, OpenMode.ForRead);

                //создать колодцы
                foreach (NetworkNode nn in networkNodesList)
                {
                    if (nn.StructureBlock != null)
                    {
                        SelectedPartTypeId spt = blockStructureMapping[nn.StructureBlock.BlockTableRecordId];
                        double x = nn.Envelope.MaxX;
                        double y = nn.Envelope.MaxY;

                        double surfElev = double.NegativeInfinity;
                        try
                        {
                            surfElev = tinSurf.FindElevationAtXY(x, y);
                        }
                        catch { }

                        double rimElevByData = double.NegativeInfinity;
                        if (nn.WellData != null && nn.WellData.TopLevel != double.NegativeInfinity)
                        {
                            rimElevByData = nn.WellData.TopLevel;
                        }

                        double z = rimElevByData != double.NegativeInfinity ? rimElevByData
                            : surfElev != double.NegativeInfinity ? surfElev : 0;
                        //если согласно Excel отметка верха ниже, чем отметка поверхности (то есть колодец зарыт в землю)
                        //то брать отметку верха с поверхности
                        if (configsViewModel.RimElevationCorrection && rimElevByData != double.NegativeInfinity && surfElev != double.NegativeInfinity && rimElevByData < surfElev)
                        {
                            z = surfElev;
                        }

                        ObjectId stuctId = ObjectId.Null;
                        network.AddStructure(spt.PartFamId, spt.PartSizeId, new Point3d(x, y, z), nn.StructureBlock.BlockRefRotation, ref stuctId, false /*true*/);
                        nn.StructId = stuctId;


                        if (!nn.StructId.IsNull)
                        {
                            CivilDB.Structure str = (CivilDB.Structure)tr.GetObject(nn.StructId, OpenMode.ForWrite);
                            str.LayerId = configsViewModel.CommunicationLayerId.Value;

                            //str.RefSurfaceId = tinSurfId;
                            //str.RimToSumpHeight = nn.WellData != null && nn.WellData.BottomLevel != double.NegativeInfinity ?
                            //    Math.Abs(str.RimElevation - nn.WellData.BottomLevel)
                            //    : defaultSumpDepth;

                            str.ControlSumpBy = CivilDB.StructureControlSumpType.ByElevation;
                            str.SumpElevation = nn.WellData != null && nn.WellData.BottomLevel != double.NegativeInfinity ?
                                nn.WellData.BottomLevel
                                : str.RimElevation - defaultSumpDepth;

                            //SelectedPartTypeId spt = blockStructureMapping[nn.StructureBlock.BlockTableRecordId];
                            PartFamily pf = (PartFamily)tr.GetObject(spt.PartFamId, OpenMode.ForRead);

                            try { str.ResizeJunctionStructure(pf.GUID, str.RimElevation, str.SumpElevation); } catch { }
                        }

                    }
                }

                tr.Commit();
            }

            //using (Transaction tr = db.TransactionManager.StartTransaction())
            //{
            //Если в узле без колодца стыкуются всего 2 ребра, то эти ребра должны быть объединены
            HashSet<NetworkEdge> networkEdgesCorrect = new HashSet<NetworkEdge>(networkEdges);
            foreach (NetworkNode nn in networkNodesList.Where(e => e.StructId.IsNull && e.AttachedEdges.Count == 2))
            {
                networkEdgesCorrect.Remove(nn.AttachedEdges[0]);
                networkEdgesCorrect.Remove(nn.AttachedEdges[1]);

                NetworkEdge joinedEdge = new NetworkEdge(nn/*, tr*/);

                networkEdgesCorrect.Add(joinedEdge);
            }

            networkEdges = new List<NetworkEdge>(networkEdgesCorrect);

            //    tr.Commit();
            //}


            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                BlockTableRecord ms
                    = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                //TODO?: Сначала подвергать обработке ребра, у которых нет данных из Excel по обоим концам
                //затем - есть данные по 1 концу, и затем по обоим концам (не помню, почему у меня появилась такая мысль)
                //Для каждого ребра
                foreach (NetworkEdge ne in networkEdges)
                {
                    ne.CalcPipePosition(tr, tinSurfId, defaultPipeDepth, sameDepth, ms);
                }


                //Получить данные о внутреннем диаметре или высоте трубы
                //учесть эти данные при создании трубы, чтобы указанные отметки соответствоввали лотку трубы
                PartFamily pipeFamily = tr.GetObject(pipeFamId, OpenMode.ForRead) as PartFamily;
                CivilDB.SweptShapeType shapeType = pipeFamily.SweptShape;

                PartSize pipeSize = tr.GetObject(pipeSizeId, OpenMode.ForRead) as PartSize;
                CivilDB.PartDataRecord record = pipeSize.SizeDataRecord;
                CivilDB.PartDataField field = null;
                try
                {
                    field = record.GetDataFieldBy(CivilDB.PartContextType.PipeInnerHeight);
                }
                catch
                {
                    field = record.GetDataFieldBy(CivilDB.PartContextType.PipeInnerDiameter);
                }

                Vector3d elevCorrectionVector = new Vector3d(0, 0, 0);
                if (field != null && field.Units.Equals("mm"))
                {
                    double pipeHalfHight = (double)field.Value / 2000;//перевод из миллиметров в метры
                    elevCorrectionVector = Vector3d.ZAxis * pipeHalfHight;
                }

                //создать трубы
                //нужно ли присоединять трубы к колодцам?
                //нужно ли соединять отрезки труб между собой?
                //положение оси трубы??? Отметки присоединений снимались по лотку трубы?
                CivilDB.Network network = (CivilDB.Network)tr.GetObject(networkId, OpenMode.ForWrite);
                foreach (NetworkEdge ne in networkEdges)
                {

                    //CivilDB.Pipe prevPipe = null;
                    for (int i = 0; i < ne.PipePositionList.Count - 1; i++)
                    {
                        Point3d p0 = ne.PipePositionList[i].GetPt3d() + elevCorrectionVector;
                        Point3d p1 = ne.PipePositionList[i + 1].GetPt3d() + elevCorrectionVector;
                        LineSegment3d line = new LineSegment3d(p0, p1);

                        ObjectId pipeId = ObjectId.Null;
                        network.AddLinePipe(pipeFamId, pipeSizeId, line, ref pipeId, false);

                        if (!pipeId.IsNull)
                        {
                            CivilDB.Pipe pipe = (CivilDB.Pipe)tr.GetObject(pipeId, OpenMode.ForWrite);
                            pipe.LayerId = configsViewModel.CommunicationLayerId.Value;

                            //if (prevPipe != null)//для соединения все рабно нужен колодец
                            //{
                            //    pipe.ConnectToPipe(CivilDB.ConnectorPositionType.Start, prevPipe.Id, CivilDB.ConnectorPositionType.End, )
                            //}

                            //prevPipe = pipe;
                        }

                    }
                }


                tr.Commit();
            }

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

            public Point3d Position { get; private set; }


            public StructurePosition(Point3d blockPos, ObjectId blockReferenceId,
                ObjectId blockTableRecordId, double blockRefRotation)
            {
                Position = blockPos;
                _envelope = new Envelope(blockPos.X, blockPos.Y, blockPos.X, blockPos.Y);
                BlockReferenceId = blockReferenceId;
                BlockTableRecordId = blockTableRecordId;
                BlockRefRotation = blockRefRotation;
                //BoxArea = boxArea;
            }
        }

        private class TextPosition : ISpatialData
        {
            private Envelope _envelope;
            public ref readonly Envelope Envelope => ref _envelope;

            public string TextContent { get; private set; }

            public ObjectId TxtId { get; private set; }

            public CompositeCurve3d BoxCurve { get; private set; }

            //public Point3d P0 { get; private set; }
            //public Point3d P1 { get; private set; }
            //public Point3d P2 { get; private set; }
            //public Point3d P3 { get; private set; }

            public List<Point3d> CornerPts { get; private set; } = new List<Point3d>();

            public TextPosition(Point3d minPt, Point3d maxPt, string textContent, ObjectId txtId)
            {
                _envelope = new Envelope(minPt.X, minPt.Y, maxPt.X, maxPt.Y);
                TextContent = textContent;
                TxtId = txtId;


                //кривая, описывающая коробку для расчета расстояния до полилинии
                Point3d P0 = new Point3d(minPt.X, minPt.Y, 0);
                Point3d P1 = new Point3d(minPt.X, maxPt.Y, 0);
                Point3d P2 = new Point3d(maxPt.X, maxPt.Y, 0);
                Point3d P3 = new Point3d(maxPt.X, minPt.Y, 0);
                CornerPts.Add(P0);
                CornerPts.Add(P1);
                CornerPts.Add(P2);
                CornerPts.Add(P3);
                LineSegment3d side0 = new LineSegment3d(P0, P1);
                LineSegment3d side1 = new LineSegment3d(P1, P2);
                LineSegment3d side2 = new LineSegment3d(P2, P3);
                LineSegment3d side3 = new LineSegment3d(P3, P0);

                BoxCurve = new CompositeCurve3d(new Curve3d[] { side0, side1, side2, side3 });
            }

            public double SquaredDistanceTo(double x, double y)
            {
                double dx = AxisDistToPoint(x, _envelope.MinX, _envelope.MaxX);
                double dy = AxisDistToPoint(y, _envelope.MinY, _envelope.MaxY);
                return dx * dx + dy * dy;
            }

            private double AxisDistToPoint(double k, double min, double max)
            {
                return k < min ? min - k : k <= max ? 0 : k - max;
            }

            public Point3d GetCenter()
            {
                double x = (_envelope.MinX + _envelope.MaxX) / 2;
                double y = (_envelope.MinY + _envelope.MaxY) / 2;

                return new Point3d(x, y, 0);
            }

            public int GetNearestCorner(Point3d pt)
            {
                int nearestIndex = -1;
                double minDist = double.PositiveInfinity; ;
                for (int i = 0; i < 4; i++)
                {
                    Point3d corner = CornerPts[i];
                    double dist = corner.DistanceTo(pt);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestIndex = i;
                    }
                }
                return nearestIndex;
            }
        }



        /// <summary>
        /// Узел графа
        /// </summary>
        private class NetworkNode : ISpatialData
        {
            private Envelope _envelope;
            public ref readonly Envelope Envelope => ref _envelope;

            public StructurePosition StructureBlock { get; set; } = null;

            /// <summary>
            /// Id колодца
            /// </summary>
            public ObjectId StructId { get; set; } = ObjectId.Null;

            public double BlockRefRotation { get; set; }

            //данные из Excel если есть
            public WellData WellData { get; set; }

            public List<NetworkEdge> AttachedEdges { get; set; } = new List<NetworkEdge>();

            public NetworkNode(Point2d nodePos)
            {
                _envelope = new Envelope(nodePos.X, nodePos.Y, nodePos.X, nodePos.Y);
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

            public Point3d GetPt3d()
            {
                return new Point3d(Position2d.X, Position2d.Y, Z);
            }

        }


        private class JunctionLabelingMarkerToDraw
        {
            public NetworkEdge NetworkEdge { get; private set; }

            public bool Start { get; private set; }

            public int ColorIndex { get; private set; }

            public TextPosition TextPosition { get; private set; }

            public bool ExcelMatch { get; private set; }

            public JunctionLabelingMarkerToDraw(NetworkEdge edge, bool start, int colorIndex, TextPosition txt, bool excelMatch)
            {
                NetworkEdge = edge;
                Start = start;
                ColorIndex = colorIndex;
                TextPosition = txt;
                ExcelMatch = excelMatch;
            }
        }

        private class WellLabelingMarkerToDraw
        {
            public StructurePosition StructurePosition { get; private set; }

            public TextPosition TextPosition { get; private set; }

            public bool ExcelMatch { get; set; } = false;

            public WellLabelingMarkerToDraw(StructurePosition str, TextPosition txt)
            {
                StructurePosition = str;
                TextPosition = txt;
            }
        }



    }





}
