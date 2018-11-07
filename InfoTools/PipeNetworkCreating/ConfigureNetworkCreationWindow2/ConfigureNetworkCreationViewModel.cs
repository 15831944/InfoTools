using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices.Styles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SelectBlockUserControl = Civil3DInfoTools.Controls.SelectBlockUserControl3;
using SelectLayerUserControl = Civil3DInfoTools.Controls.SelectLayerUserControl2;
using Civil3DInfoTools.Controls.SelectPartSizeUserControl;
using Autodesk.Civil.DatabaseServices;
using CivilDB = Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Common.Controls.NumericUpDownControl;
using Common;
using Common.Controls.FileNameInputControl;
using System.IO;
using System.Windows.Media;


namespace Civil3DInfoTools.PipeNetworkCreating.ConfigureNetworkCreationWindow2
{
    //TODO: Как обеспечить уникальность блоков в строках DataGrid при вводе?
    //для этого лучше всего будет выделить датагрид соответствия в единый UserControl
    //и уже в нем поддерживать колекцию блоков, которые должны отображаться при выборе в ячейке

    //TODO: Конечно нужно было сделать так, чтобы все данные введенные в окно были привязаны к чертежу, но мне почему-то было лень
    //Просто поддреживать словарь с ключом, однозначно указывающим на один из открытых чертежей
    //или просто сбрасывать все настройки при переходе между чережами - это самое простое

    public class ConfigureNetworkCreationViewModel : INotifyPropertyChanged
    {
        private static string defaultGridLayer = "02_Сетка";
        private static string defaultStructuresLayer = "44_Крышки колодцев";
        private static string defaultStructureLabelsLayer = "45_Номера колодцев";
        private static string defaultCommunicationLayer = "30_Канализация";
        private static string defaultExcelPath = null;
        private static double defaultCommunicationDepth = 1.00;
        private static double defaultWellDepth = 2.00;
        private static bool defaultSameDepth = false;
        private static ObjectId defaultPartsList = ObjectId.Null;

        private static Dictionary<ObjectId, SelectedPartTypeId> defaultBlockStructureTable = null;

        private static SelectedPartTypeId defaultPipeType = null;
        private static ObjectId? defaultTinSurface = null;
        private static bool defaultRimElevationCorrection = true;



        private const string DEFAULT_STRUCTURE_BLOCK = "M5_075";
        private const string REFERENCE_DOC_NAME = "Кодификатор_М1-500_по_городу_СПб.doc";
        private const string EXCEL_SAMPLE_NAME = "23291108.xls";

        private Document doc = null;
        private CivilDocument cdoc = null;
        private Window thisWindow = null;
        private ObservableCollection<BlockTableRecord> blocks = null;
        private PipeNetworkGraph networkGraph = null;

        private readonly SolidColorBrush defBtnColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));
        private readonly SolidColorBrush configsAcceptedColor = Brushes.LightGreen;

        private bool configurationsAccepted = false;
        public bool ConfigurationsAccepted
        {
            get
            {
                return configurationsAccepted;
            }
            private set
            {
                configurationsAccepted = value;
                EnableCreateNetworkBtn(this, new EventArgs());
                OnPropertyChanged("AcceptBtnColor");
            }
        }

        /// <summary>
        /// Make sure you're using System.Windows.Media.Brush and not System.Drawing.Brush!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        /// https://stackoverflow.com/a/13202182/8020304
        /// </summary>
        public Brush AcceptBtnColor
        {
            get
            {
                return configurationsAccepted ? configsAcceptedColor : defBtnColor;
            }
        }

        public PipeStructureExcelReader ExcelReader { get; private set; }



        //НЕ ЗАБЫВАТЬ, ЧТО СВОЙСТВА СЯЗАННЫЕ С VIEW ДОЛЖНЫ БЫТЬ ПУБЛИЧНЫМИ!!
        private SelectLayerUserControl.ViewModel gridLayerVM;
        public SelectLayerUserControl.ViewModel GridLayerVM
        {
            get { return gridLayerVM; }
            set
            {
                gridLayerVM = value;
                OnPropertyChanged("GridLayerVM");
            }
        }

        //enable accept
        public ObjectId? GridLayerId
        {
            get { return GridLayerVM?.SelectedLayer?.Id; }
        }

        //StructuresLayerVM
        private SelectLayerUserControl.ViewModel structuresLayerVM;
        public SelectLayerUserControl.ViewModel StructuresLayerVM
        {
            get { return structuresLayerVM; }
            set
            {
                structuresLayerVM = value;
                OnPropertyChanged("StructuresLayerVM");
            }
        }

        //enable accept
        public ObjectId? StructuresLayerId
        {
            get { return StructuresLayerVM?.SelectedLayer?.Id; }
        }

        //StructureLabelsLayerVM
        private SelectLayerUserControl.ViewModel structureLabelsLayerVM;
        public SelectLayerUserControl.ViewModel StructureLabelsLayerVM
        {
            get { return structureLabelsLayerVM; }
            set
            {
                structureLabelsLayerVM = value;
                OnPropertyChanged("StructureLabelsLayerVM");
            }
        }

        //enable accept
        public ObjectId? StructureLabelsLayerId
        {
            get { return StructureLabelsLayerVM?.SelectedLayer?.Id; }
        }


        public ObservableCollection<PartsList> PartsLists { get; set; }
            = new ObservableCollection<PartsList>();

        private object selectedPartsListItem = null;
        public object SelectedPartsListItem
        {
            get { return selectedPartsListItem; }
            set
            {
                selectedPartsListItem = value;
                OnPropertyChanged("SelectedPartsListItem");
                OnPropertyChanged("SelectedPartsList");
                OnPropertyChanged("PartsListSelected");

                EnableCreateNetworkBtn(this, new EventArgs());
            }
        }

        public PartsList SelectedPartsList
        {
            get { return SelectedPartsListItem as PartsList; }
        }


        //enable accept
        public bool PartsListSelected
        {
            get { return SelectedPartsList != null; }
        }



        public ObservableCollection<BlockStructureMappingPairModel> BlocksStructuresMappingColl { get; set; }
            = new ObservableCollection<BlockStructureMappingPairModel>();


        /// <summary>
        /// Указывет на то, заполнены ли строки таблицы блоков
        /// </summary>
        private bool blocksCompleteInput = false;

        //enables accept
        /// <summary>
        /// Введенные блоки колодцев
        /// </summary>
        /// <returns></returns>
        public HashSet<ObjectId> Blocks
        {
            get
            {
                blocksCompleteInput = true;
                HashSet<ObjectId> blocks = new HashSet<ObjectId>();
                foreach (BlockStructureMappingPairModel bsModel in BlocksStructuresMappingColl)
                {
                    BlockTableRecord btr = bsModel.BlockVM.SelectedBlock;
                    PartFamily pf = bsModel.StructureVM.SelectedPartFamily?.PartFamily;
                    PartSize ps = bsModel.StructureVM.SelectedPartSize;

                    if (btr != null)//проверка, что блок введен в строку
                    {
                        if (!blocks.Contains(btr.Id))//блоки могут повторяться!!!
                            blocks.Add(btr.Id);
                    }
                    else
                    {
                        blocksCompleteInput = false;
                        //ввод неполный, то считать ввод не выполнен вообще
                        blocks.Clear();
                        break;
                    }
                }
                return blocks;
            }
        }


        //enables create network
        public Dictionary<ObjectId, SelectedPartTypeId> BlockStructureMapping
        {
            get
            {
                blocksCompleteInput = true;
                Dictionary<ObjectId, SelectedPartTypeId> mapping
                    = new Dictionary<ObjectId, SelectedPartTypeId>();

                foreach (BlockStructureMappingPairModel bsModel in BlocksStructuresMappingColl)
                {
                    BlockTableRecord btr = bsModel.BlockVM.SelectedBlock;
                    PartFamily pf = bsModel.StructureVM.SelectedPartFamily?.PartFamily;
                    PartSize ps = bsModel.StructureVM.SelectedPartSize;

                    if (btr != null && pf != null && ps != null)//проверка, что ввод в строку полный
                    {
                        if (!mapping.ContainsKey(btr.Id))//блоки могут повторяться!!!
                            mapping.Add(btr.Id, new SelectedPartTypeId(pf.Id, ps.Id));
                    }
                    else
                    {
                        blocksCompleteInput = false;
                        //ввод неполный, то считать ввод не выполнен вообще
                        mapping.Clear();
                        break;
                    }
                }
                return mapping;
            }
        }



        private readonly RelayCommand addBlockStructureMappingPairCommand = null;
        public RelayCommand AddBlockStructureMappingPairCommand
        { get { return addBlockStructureMappingPairCommand; } }


        private SelectPartSizeViewModel pipeVM;
        public SelectPartSizeViewModel PipeVM
        {
            get { return pipeVM; }
            set
            {
                pipeVM = value;
                OnPropertyChanged("PipeVM");
            }
        }

        //enable accept
        public SelectedPartTypeId PipeType
        {
            get
            {
                PartFamily pf = PipeVM?.SelectedPartFamily?.PartFamily;
                PartSize ps = PipeVM?.SelectedPartSize;
                if (pf != null && ps != null)
                {
                    return new SelectedPartTypeId(pf.Id, ps.Id);
                }
                return null;
            }
        }



        private TinSurface selectedTinSurface;
        public TinSurface SelectedTinSurface
        {
            get { return selectedTinSurface; }
            set
            {
                selectedTinSurface = value;
                OnPropertyChanged("SelectedTinSurface");
                OnPropertyChanged("SelectedSurfaceName");

                EnableCreateNetworkBtn(this, new EventArgs());
            }
        }

        //enable accept
        public ObjectId? TinSurfaceId
        {
            get { return SelectedTinSurface?.Id; }
        }

        public string SelectedSurfaceName
        {
            get
            {
                if (SelectedTinSurface != null)
                {
                    return SelectedTinSurface.Name;
                }
                else
                {
                    return "Поверхность не указана";
                }
            }
        }


        private readonly RelayCommand selectSurfaceCommand = null;
        public RelayCommand SelectSurfaceCommand
        { get { return selectSurfaceCommand; } }


        private NumericUpDownViewModel communicationDepthVM = null;

        public NumericUpDownViewModel CommunicationDepthVM
        {
            get { return communicationDepthVM; }
            set
            {
                communicationDepthVM = value;
                OnPropertyChanged("CommunicationDepthVM");
            }
        }

        private NumericUpDownViewModel wellDepthVM = null;

        public NumericUpDownViewModel WellDepthVM
        {
            get { return wellDepthVM; }
            set
            {
                wellDepthVM = value;
                OnPropertyChanged("WellDepthVM");
            }
        }

        private bool sameDepth = false;
        public bool SameDepth
        {
            get { return sameDepth; }
            set
            {
                sameDepth = value;
                OnPropertyChanged("SameDepth");
            }
        }


        private bool rimElevationCorrection = true;
        public bool RimElevationCorrection
        {
            get { return rimElevationCorrection; }
            set
            {
                rimElevationCorrection = value;
                OnPropertyChanged("RimElevationCorrection");
            }
        }


        private FileNameInputViewModel excelPathVM = null;
        public FileNameInputViewModel ExcelPathVM
        {
            get { return excelPathVM; }
            set
            {
                excelPathVM = value;
                OnPropertyChanged("ExcelPathVM");
            }
        }

        //enable accept
        public string ExcelPath
        {
            get
            {
                if (ExcelPathVM.FileNameIsValid)
                {
                    return ExcelPathVM.FileName;
                }
                return null;
            }
        }

        private SelectLayerUserControl.ViewModel communicationLayerVM;
        public SelectLayerUserControl.ViewModel CommunicationLayerVM
        {
            get { return communicationLayerVM; }
            set
            {
                communicationLayerVM = value;
                OnPropertyChanged("CommunicationLayerVM");
            }
        }

        //enable accept
        public ObjectId? CommunicationLayerId
        {
            get { return CommunicationLayerVM?.SelectedLayer?.Id; }
        }


        //как сделать так чтобы представление оповещалось о том, что значение данного
        //свойства могло измениться
        //- через события
        public bool AcceptBtnIsEnabled
        {
            get
            {
                int i = Blocks.Count;//обновляет значение blocksCompleteInput
                return GridLayerId != null && StructuresLayerId != null && StructureLabelsLayerId != null
                    //&& Blocks.Count > 0 //возможно сеть не должна содержать колодцев
                    && blocksCompleteInput
                    && !String.IsNullOrEmpty(ExcelPath)
                    && CommunicationLayerId != null;
            }
        }




        public bool CreateNetworkBtnIsEnabled
        {
            get
            {
                int i = BlockStructureMapping.Count;//обновляет значение blocksCompleteInput
                return ConfigurationsAccepted && GridLayerId != null && StructuresLayerId != null
                    && StructureLabelsLayerId != null && PartsListSelected
                    //&& BlockStructureMapping.Count > 0//возможно сеть не должна содержать колодцев
                    && blocksCompleteInput
                    && PipeType != null && TinSurfaceId != null && !String.IsNullOrEmpty(ExcelPath)
                    && CommunicationLayerId != null;
            }
        }

        /// <summary>
        /// Какие-то настройки изменились.
        /// Значит нужно оповестить представление кнопки
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EnableCreateNetworkBtn(object sender, EventArgs e)
        {
            OnPropertyChanged("CreateNetworkBtnIsEnabled");
        }

        /// <summary>
        /// Какие-то настройки изменились.
        /// Значит нужно оповестить представление кнопки
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EnableAcceptBtn(object sender, EventArgs e)
        {
            OnPropertyChanged("AcceptBtnIsEnabled");
            ConfigurationsAccepted = false;
        }


        //TODO: Если настройки уже приняты и после этого какие-то настройки еще меняются, ConfigurationsAccepted должно сбрасываться в false
        //TODO: Кнопка принятия настроек должна как-то выделяться когда ConfigurationsAccepted = true.
        //TODO?: При этом все настройки, которые могут сбрасывать ConfigurationsAccepted также должны обозначаться определенным образом (можно некоторым образом разграничить элементы управления в окне)
        //TODO: DataGrid должен быть доступен даже если не выбран список элементов сети (но при этом недоступен выбор типоразмеров в DataGrid)

        private readonly RelayCommand acceptConfigsCommand = null;
        public RelayCommand AcceptConfigsCommand
        { get { return acceptConfigsCommand; } }


        private readonly RelayCommand openReferenceDocCommand = null;
        public RelayCommand OpenReferenceDocCommand
        { get { return openReferenceDocCommand; } }

        //OpenExcelSampleCommand
        private readonly RelayCommand openExcelSampleCommand = null;
        public RelayCommand OpenExcelSampleCommand
        { get { return openExcelSampleCommand; } }

        private readonly RelayCommand createPipeNenworkCommand = null;
        public RelayCommand CreatePipeNenworkCommand
        { get { return createPipeNenworkCommand; } }

        public ConfigureNetworkCreationViewModel(Document doc, CivilDocument cdoc, Window thisWindow)
        {
            this.doc = doc;
            this.cdoc = cdoc;
            this.thisWindow = thisWindow;

            //команды
            addBlockStructureMappingPairCommand
                = new RelayCommand(new Action<object>(AddBlockStructureMappingPair));
            selectSurfaceCommand = new RelayCommand(new Action<object>(SelectSurface));
            acceptConfigsCommand = new RelayCommand(new Action<object>(AcceptConfigs));

            openReferenceDocCommand = new RelayCommand(new Action<object>(OpenReferenceDoc));
            openExcelSampleCommand = new RelayCommand(new Action<object>(OpenExcelSample));
            createPipeNenworkCommand = new RelayCommand(new Action<object>(CreatePipeNenwork));

            //Выбор слоев
            ObservableCollection<SelectLayerUserControl.Model> layers = SelectLayerUserControl.ViewModel.GetLayers(doc);
            GridLayerVM = new SelectLayerUserControl.ViewModel(doc, thisWindow, layers, defaultGridLayer);
            StructuresLayerVM = new SelectLayerUserControl.ViewModel(doc, thisWindow, layers, defaultStructuresLayer);
            StructureLabelsLayerVM = new SelectLayerUserControl.ViewModel(doc, thisWindow, layers, defaultStructureLabelsLayer);

            SameDepth = defaultSameDepth;
            RimElevationCorrection = defaultRimElevationCorrection;

            //combo box populate
            //как правильно связать выбранный элемент в combo box и заданный PartsList для SelectPartSizeViewModel (можно через события)
            //нужно DependencyProperty в классе SelectPartSizeView?
            Database db = doc.Database;
            PartsListCollection partListColl = cdoc.Styles.PartsListSet;
            PartsList startSelection = null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId plId in partListColl)
                {
                    //defaultPartsList
                    PartsList pl = (PartsList)tr.GetObject(plId, OpenMode.ForRead);
                    PartsLists.Add(pl);
                    if (plId.Equals(defaultPartsList))
                        startSelection = pl;
                }
                tr.Commit();
            }
            if (startSelection != null)
            {
                SelectedPartsListItem = startSelection;
            }

            //datagrid populate
            blocks = SelectBlockUserControl.ViewModel.GetBlocks(doc);
            BlocksStructuresMappingColl = new ObservableCollection<BlockStructureMappingPairModel>();
            if (defaultBlockStructureTable != null)
            {
                //есть сохраненная таблица блоков
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (KeyValuePair<ObjectId, SelectedPartTypeId> kvp in defaultBlockStructureTable)
                    {
                        try
                        {
                            BlockTableRecord btr = tr.GetObject(kvp.Key, OpenMode.ForRead) as BlockTableRecord;
                            if (btr != null)
                            {
                                string blockName = btr.Name;
                                ObjectId famId = ObjectId.Null;
                                ObjectId sizeId = ObjectId.Null;
                                if (kvp.Value != null)
                                {
                                    famId = kvp.Value.PartFamId;
                                    sizeId = kvp.Value.PartSizeId;
                                }

                                BlockStructureMappingPairModel item
                                    = new BlockStructureMappingPairModel(doc, thisWindow, blocks, SelectedPartsList, famId, sizeId, blockName);
                                HandleDataGridItemInput(item);
                                BlocksStructuresMappingColl.Add(item);
                            }
                        }
                        catch (Exception)
                        {
                            BlocksStructuresMappingColl.Clear();
                            defaultBlockStructureTable = null;//удалить сохраненную таблицу блоков. Пусть создается 1 дефолтная строчка
                            break;
                        }

                    }

                    tr.Commit();
                }
            }



            if (defaultBlockStructureTable == null)
            {
                //нет сохраненной таблицы блоков
                BlockStructureMappingPairModel defItem = new BlockStructureMappingPairModel(doc, thisWindow, blocks, SelectedPartsList, ObjectId.Null, ObjectId.Null)
                { BlockVM = new SelectBlockUserControl.ViewModel(doc, thisWindow, blocks, DEFAULT_STRUCTURE_BLOCK) };
                HandleDataGridItemInput(defItem);//для каждой строки в datagrid должен добавляться обработчик событий
                BlocksStructuresMappingColl.Add(defItem);
            }


            ObjectId startPipeFam = ObjectId.Null;
            ObjectId startPipeSize = ObjectId.Null;
            if (defaultPipeType != null)
            {
                startPipeFam = defaultPipeType.PartFamId;
                startPipeSize = defaultPipeType.PartSizeId;
            }

            pipeVM = new SelectPartSizeViewModel(doc, SelectedPartsList, PartType.Pipe
                //| PartType.Wire | PartType.Channel | PartType.Conduit | PartType.UndefinedPartType
                , startPipeFam, startPipeSize);


            communicationDepthVM = new NumericUpDownViewModel(defaultCommunicationDepth, 0.5, 0, 100);

            wellDepthVM = new NumericUpDownViewModel(defaultWellDepth, 0.5, 0, 100);

            string initialPath = defaultExcelPath == null ? Path.GetDirectoryName(doc.Name) : defaultExcelPath;
            excelPathVM = new FileNameInputViewModel("Excel Files|*.xls;*.xlsx;", "Укажите путь к файлу Excel")
            { FileName = initialPath };

            communicationLayerVM = new SelectLayerUserControl
                .ViewModel(doc, thisWindow, layers, defaultCommunicationLayer);


            if (defaultTinSurface != null)
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        TinSurface tinSurface = tr.GetObject(defaultTinSurface.Value, OpenMode.ForRead) as TinSurface;
                        if (tinSurface!=null)
                        {
                            SelectedTinSurface = tinSurface;
                        }
                    }
                    catch { }

                    tr.Commit();
                }

            }

            //Подпись на события, которые оповещают о том, что пользовательские настройки изменились
            //(можно было использовать стандартное событие INotifyPropertyChanged вместо специально созданного SelectionChanged)
            BlocksStructuresMappingColl.CollectionChanged += EnableCreateNetworkBtn;
            BlocksStructuresMappingColl.CollectionChanged += EnableAcceptBtn;
            GridLayerVM.SelectionChanged += EnableCreateNetworkBtn;
            GridLayerVM.SelectionChanged += EnableAcceptBtn;
            StructuresLayerVM.SelectionChanged += EnableCreateNetworkBtn;
            StructuresLayerVM.SelectionChanged += EnableAcceptBtn;
            StructureLabelsLayerVM.SelectionChanged += EnableCreateNetworkBtn;
            StructureLabelsLayerVM.SelectionChanged += EnableAcceptBtn;
            PipeVM.SelectionChanged += EnableCreateNetworkBtn;
            ExcelPathVM.FileNameChanged += EnableCreateNetworkBtn;
            ExcelPathVM.FileNameChanged += EnableAcceptBtn;
            CommunicationLayerVM.SelectionChanged += EnableCreateNetworkBtn;
            CommunicationLayerVM.SelectionChanged += EnableAcceptBtn;

            OnPropertyChanged("AcceptBtnIsEnabled");
            OnPropertyChanged("CreateNetworkBtnIsEnabled");

            //Сохранить введенные данные в окно при любом закрытии
            thisWindow.Closing += SaveInput;
        }






        private void AddBlockStructureMappingPair(object obj)
        {
            BlockStructureMappingPairModel newItem
                = new BlockStructureMappingPairModel(doc, thisWindow, blocks, SelectedPartsList, ObjectId.Null, ObjectId.Null);
            HandleDataGridItemInput(newItem);

            BlocksStructuresMappingColl.Add(newItem);
        }

        /// <summary>
        /// подписатья на события модели представления строки таблицы блоков
        /// </summary>
        /// <param name="item"></param>
        private void HandleDataGridItemInput(BlockStructureMappingPairModel item)
        {
            item.BlockSelectionChanged += EnableCreateNetworkBtn;
            item.PartSizeSelectionChanged += EnableCreateNetworkBtn;

            item.BlockSelectionChanged += EnableAcceptBtn;//выбор блока может сбросить ConfigurationsAccepted
        }

        private void SelectSurface(object obj)
        {
            if (doc != null && thisWindow != null)
            {
                thisWindow.Hide();

                Editor ed = doc.Editor;
                Database db = doc.Database;

                PromptEntityOptions peo = new PromptEntityOptions("\nУкажите поверхность:");
                peo.SetRejectMessage("\nМожно выбрать только поверхность TIN");
                peo.AddAllowedClass(typeof(TinSurface), true);
                PromptEntityResult per1 = ed.GetEntity(peo);
                if (per1.Status == PromptStatus.OK)
                {
                    ObjectId selId = per1.ObjectId;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        TinSurface ts = (TinSurface)tr.GetObject(selId, OpenMode.ForRead);
                        SelectedTinSurface = ts;
                        tr.Commit();
                    }
                }

                //thisWindow.Show();
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(thisWindow);
            }
        }




        private void AcceptConfigs(object obj)
        {
            //Попытаться считать данные из Excel
            ExcelReader = new PipeStructureExcelReader(ExcelPath);
            if (ExcelReader.WellDataFiles.Count == 0)
            {
                MessageBox.Show("В указанной директории не найдено ни одного файла Excel с подходящими именами. "
                    + "Для каждого квадрата должен быть отдельный файл с данными по колодцам. Файлы должны называться в соответствии с номером квадрата. "
                    + "Например, \"11111111\" или \"1111-11-11\" или \"1111_11_11\". Расширение \".xlsx\" или \".xls\"", "Отмена");
                return;
            }
            if (ExcelReader.ReadDataFromExcel())
            {
                if (ExcelReader.WellsData.Count == 0 || ExcelReader.WellsData.All(kvp => kvp.Value == null || kvp.Value.Count == 0))
                {
                    MessageBox.Show("В указанных файлах Excel не удалось обнаружить данные о колодцах. Возможно файлы имеют неверный формат "
                        + "(смотри пример файла данных по гиперссылке в окне настройки)."
                        , "Отмена");
                    return;
                }

                //Скрыть окно 
                thisWindow.Hide();
                //Запустить построение графа 
                networkGraph = new PipeNetworkGraph(doc, cdoc, this);
                //Отрисовать маркеры распознавания объектов
                networkGraph.DrawMarkers();

                //Показ модели и ожидание нажатия любой клавиши
                Editor ed = doc.Editor;
                PromptKeywordOptions pko =
                    new PromptKeywordOptions("\nИзучите маркеры сопоставления объектов чертежа");
                pko.Keywords.Add("Принято");
                pko.AllowNone = true;
                PromptResult pr = ed.GetKeywords(pko);
                //Показать окно
                ConfigurationsAccepted = true;
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(thisWindow);
                //thisWindow.Close();
            }

        }


        private void OpenReferenceDoc(object obj)
        {
            //Файл кодификатора должен лежать рядом с исполняемой сборкой
            string docFileName = Path.Combine(Path.GetDirectoryName(App.AssemblyLocation), REFERENCE_DOC_NAME);
            try { System.Diagnostics.Process.Start(docFileName); } catch { }
        }


        private void OpenExcelSample(object obj)
        {
            //Файл примера экселя должен лежать рядом с исполняемой сборкой
            string xlsFileName = Path.Combine(Path.GetDirectoryName(App.AssemblyLocation), EXCEL_SAMPLE_NAME);
            try { System.Diagnostics.Process.Start(xlsFileName); } catch { }
        }


        private void CreatePipeNenwork(object obj)
        {
            thisWindow.Close();
            //создать сеть Civil 3d
            networkGraph.CreatePipeNenwork();
        }

        public void SaveInput(object sender, CancelEventArgs e)
        {
            if (GridLayerVM.SelectedLayer != null)
                defaultGridLayer = GridLayerVM.SelectedLayer.Name;
            if (StructuresLayerVM.SelectedLayer != null)
                defaultStructuresLayer = StructuresLayerVM.SelectedLayer.Name;
            if (StructureLabelsLayerVM.SelectedLayer != null)
                defaultStructureLabelsLayer = StructureLabelsLayerVM.SelectedLayer.Name;
            if (CommunicationLayerVM.SelectedLayer != null)
                defaultCommunicationLayer = CommunicationLayerVM.SelectedLayer.Name;
            defaultExcelPath = ExcelPathVM.FileName;
            defaultCommunicationDepth = CommunicationDepthVM.NumValue;
            defaultWellDepth = WellDepthVM.NumValue;

            defaultSameDepth = SameDepth;
            if (SelectedPartsList != null)
                defaultPartsList = SelectedPartsList.Id;

            defaultPipeType = PipeType;
            defaultTinSurface = TinSurfaceId;
            defaultRimElevationCorrection = RimElevationCorrection;

            int decision = 0;
            HashSet<ObjectId> blocks = Blocks;
            if (blocksCompleteInput)
            {
                //полностью введен набор блоков - их нужно сохранить
                decision = 1;
            }
            Dictionary<ObjectId, SelectedPartTypeId> blockStructureMapping = BlockStructureMapping;
            if (blocksCompleteInput)
            {
                //таблица сопоставления полностью заполнена - ее нужно сохранить
                decision = 2;
            }

            switch (decision)
            {
                case 0:
                    defaultBlockStructureTable = null;
                    break;
                case 1:
                    defaultBlockStructureTable = new Dictionary<ObjectId, SelectedPartTypeId>();
                    foreach (ObjectId blockId in blocks)
                    {
                        defaultBlockStructureTable.Add(blockId, null);
                    }
                    break;
                case 2:
                    defaultBlockStructureTable = blockStructureMapping;
                    break;
            }

        }



        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }


    }

    public class SelectedPartTypeId
    {
        public ObjectId PartFamId { get; private set; }
        public ObjectId PartSizeId { get; private set; }

        public SelectedPartTypeId(ObjectId partFamId, ObjectId partSizeId)
        {
            PartFamId = partFamId;
            PartSizeId = partSizeId;
        }
    }
}
