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
using Autodesk.AutoCAD.EditorInput;
using Common.Controls.NumericUpDownControl;
using Common;
using Common.Controls.FileNameInputControl;
using System.IO;

namespace Civil3DInfoTools.PipeNetworkCreating.ConfigureNetworkCreationWindow2
{
    //TODO: Как обеспечить уникальность блоков в строках DataGrid при вводе?

    public class ConfigureNetworkCreationViewModel : INotifyPropertyChanged
    {
        private const string DEFAULT_GRID_LAYER = "02_Сетка";
        private const string DEFAULT_STRUCTURES_LAYER = "44_Крышки колодцев";
        private const string DEFAULT_STRUCTURE_LABELS_LAYER = "45_Номера колодцев";

        private const string DEFAULT_STRUCTURE_BLOCK = "M5_075";

        private const string DEFAULT_COMMUVICATION_LAYER = "30_Канализация";

        Document doc = null;
        Window thisWindow = null;
        ObservableCollection<BlockTableRecord> blocks = null;

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

                SomethingDifferent(this, new EventArgs());
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

        //enable accept
        public Dictionary<ObjectId, SelectedPartTypeId> BlockStructureMapping
        {
            get
            {
                Dictionary<ObjectId, SelectedPartTypeId> mapping
                    = new Dictionary<ObjectId, SelectedPartTypeId>();

                foreach (BlockStructureMappingPairModel bsModel in BlocksStructuresMappingColl)
                {
                    BlockTableRecord btr = bsModel.BlockVM.SelectedBlock;
                    PartFamily pf = bsModel.StructureVM.SelectedPartFamily?.PartFamily;
                    PartSize ps = bsModel.StructureVM.SelectedPartSize;

                    if (btr != null && pf != null && ps != null)
                    {
                        mapping.Add(btr.Id, new SelectedPartTypeId(pf.Id, ps.Id));
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

                SomethingDifferent(this, new EventArgs());
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
                return GridLayerId != null && StructuresLayerId != null && StructureLabelsLayerId != null
                    && PartsListSelected && BlockStructureMapping.Count > 0 && PipeType != null
                    && TinSurfaceId != null && !String.IsNullOrEmpty(ExcelPath) 
                    && CommunicationLayerId!=null;
            }
        }

        /// <summary>
        /// Какие-то настройки изменились.
        /// Значит нужно оповестить представление кнопки принятия изменений
        /// Она должна стать доступной только тогда, когда все настройки заданы
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SomethingDifferent(object sender, EventArgs e)
        {
            OnPropertyChanged("AcceptBtnIsEnabled");
        }


        private readonly RelayCommand acceptConfigsCommand = null;
        public RelayCommand AcceptConfigsCommand
        { get { return acceptConfigsCommand; } }


        public ConfigureNetworkCreationViewModel(Document doc, CivilDocument cdok, Window thisWindow)
        {
            addBlockStructureMappingPairCommand
                = new RelayCommand(new Action<object>(AddBlockStructureMappingPair));
            selectSurfaceCommand = new RelayCommand(new Action<object>(SelectSurface));
            acceptConfigsCommand = new RelayCommand(new Action<object>(AcceptConfigs));

            this.doc = doc;
            this.thisWindow = thisWindow;

            //Выбор слоев
            ObservableCollection<SelectLayerUserControl.Model> layers = SelectLayerUserControl.ViewModel.GetLayers(doc);
            GridLayerVM = new SelectLayerUserControl.ViewModel(doc, thisWindow, layers, DEFAULT_GRID_LAYER);
            StructuresLayerVM = new SelectLayerUserControl.ViewModel(doc, thisWindow, layers, DEFAULT_STRUCTURES_LAYER);
            StructureLabelsLayerVM = new SelectLayerUserControl.ViewModel(doc, thisWindow, layers, DEFAULT_STRUCTURE_LABELS_LAYER);

            //combo box populate
            //как правильно связать выбранный элемент в combo box и заданный PartsList для SelectPartSizeViewModel (можно через события)
            //нужно DependencyProperty в классе SelectPartSizeView?
            Database db = doc.Database;
            PartsListCollection partListColl = cdok.Styles.PartsListSet;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId plId in partListColl)
                {
                    PartsList pl = (PartsList)tr.GetObject(plId, OpenMode.ForRead);
                    PartsLists.Add(pl);
                }
                tr.Commit();
            }


            //datagrid populate
            blocks = SelectBlockUserControl.ViewModel.GetBlocks(doc);
            BlockStructureMappingPairModel defItem = new BlockStructureMappingPairModel(doc, thisWindow, blocks, SelectedPartsList)
            { BlockVM = new SelectBlockUserControl.ViewModel(doc, thisWindow, blocks, DEFAULT_STRUCTURE_BLOCK) };
            defItem.SelectionChanged += SomethingDifferent;
            BlocksStructuresMappingColl = new ObservableCollection<BlockStructureMappingPairModel>()
            {
                defItem,
            };

            pipeVM = new SelectPartSizeViewModel(doc, SelectedPartsList, PartType.Pipe | PartType.Wire
                | PartType.Channel | PartType.Conduit | PartType.UndefinedPartType);


            communicationDepthVM = new NumericUpDownViewModel(1, 1, 0, 100);

            string initialPath = Path.GetDirectoryName(doc.Name);
            excelPathVM = new FileNameInputViewModel("Excel Files|*.xls;*.xlsx;", "Укажите путь к файлу Excel")
            { FileName = initialPath };

            communicationLayerVM = new SelectLayerUserControl
                .ViewModel(doc, thisWindow, layers, DEFAULT_COMMUVICATION_LAYER);

            //Подпись на события, которые оповещают о том, что пользовательские настройки изменились
            //(можно было использовать стандартное событие INotifyPropertyChanged)
            BlocksStructuresMappingColl.CollectionChanged += SomethingDifferent;
            GridLayerVM.SelectionChanged += SomethingDifferent;
            StructuresLayerVM.SelectionChanged += SomethingDifferent;
            StructureLabelsLayerVM.SelectionChanged += SomethingDifferent;
            PipeVM.SelectionChanged += SomethingDifferent;
            ExcelPathVM.FileNameChanged += SomethingDifferent;
            CommunicationLayerVM.SelectionChanged += SomethingDifferent;
        }





        private void AddBlockStructureMappingPair(object obj)
        {
            BlockStructureMappingPairModel newItem 
                = new BlockStructureMappingPairModel(doc, thisWindow, blocks, SelectedPartsList);
            newItem.SelectionChanged += SomethingDifferent;

            BlocksStructuresMappingColl.Add(newItem);
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

                thisWindow.Show();
            }
        }




        private void AcceptConfigs(object obj)
        {
            //Считать данные из Excel, убедиться, что формат Excel правильный
            //иначе выкинуть сообщение с ошибкой и не закрывать окно

            thisWindow.Close();
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
