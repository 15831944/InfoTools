using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Common;

namespace Civil3DInfoTools.Controls.SelectBlockUserControl3
{
    //Моделью является класс Autodesk.AutoCAD.DatabaseServices.BlockTableRecord
    public class ViewModel : INotifyPropertyChanged
    {
        public event EventHandler SelectionChanged;//для оповещения внешней ViewModel


        private Document doc;
        private Window mainWindow;

        /// <summary>
        /// Соответствие id блока и номера этого блока в ObservableCollection
        /// </summary>
        private Dictionary<ObjectId, int> blockIndexesLookup = new Dictionary<ObjectId, int>();



        //TODO: То что у меня завязаны и SelectedIndex и SelectedItem - это явно избыточно!!!
        private int selectedIndex = -1;
        /// <summary>
        /// Это свойство завязывается на combo box
        /// </summary>
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                selectedIndex = value;
                OnPropertyChanged("SelectedIndex");
                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new EventArgs());
                }
            }
        }

        /// <summary>
        /// Это свойство завязывается на combo box
        /// </summary>
        private object selectedItem = null;
        public object SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value;
                OnPropertyChanged("SelectedItem");
                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new EventArgs());
                }
            }
        }

        /// <summary>
        /// Просто кастит selectedItem к BlockTableRecord
        /// </summary>
        public BlockTableRecord SelectedBlock
        { get { return selectedItem as BlockTableRecord; } }


        private readonly RelayCommand selectBlockCommand = null;
        public RelayCommand SelectBlockCommand
        { get { return selectBlockCommand; } }

        /// <summary>
        /// Коллекция блоков, которая завязана на combo box
        /// </summary>
        public ObservableCollection<BlockTableRecord> Blocks { get; set; }
            = new ObservableCollection<BlockTableRecord>();


        /// <summary>
        /// Модель предстваления SelectBlockUserControl жестко завязана на конкретный документ AutoCAD,
        /// берет из него данные и взаимодействует с ним. Этот документ должен быть открыт.
        /// Так же модель представления должна иметь возможность скрыть окно, в котором
        /// находится SelectBlockUserControl для того, чтобы дать пользователю возможность выбрать блок в чертеже.
        /// SelectBlockUserControl НИКАК НЕ УЧИТЫВАЕТ ВОЗМОЖНОСТЬ НАЛИЧИЯ ИЕРАРХИИ ОКОН. ТОЛЬКО ДЛЯ ЕДИНИНЧНОГО ОКНА
        /// Можно так же передать готовый набор блоков если он оже определен 
        /// (например если один и тот же набор передается во монжество контролов).
        /// В противном случае набор блоков будет определен из переданного документа при инициализации каждого контрола.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="mainWindow"></param>
        public ViewModel(Document doc, Window mainWindow,
            ObservableCollection<BlockTableRecord> blocks = null, string defaulBlockIfExists = null)
        {
            //создание объекта команды для выбора объекта
            selectBlockCommand = new RelayCommand(new Action<object>(SelectBlock));

            this.doc = doc;
            this.mainWindow = mainWindow;
            if (blocks != null)
            {
                Blocks = blocks;
            }
            if (Blocks.Count == 0)
            {
                Blocks = GetBlocks(doc);
            }
            //не лучший путь для сортировки. подробнее - https://stackoverflow.com/a/19113072
            Blocks = new ObservableCollection<BlockTableRecord>(Blocks.OrderBy(btr => btr.Name));

            int startSelIndex = -1;
            int i = 0;
            foreach (BlockTableRecord b in Blocks)
            {
                blockIndexesLookup.Add(b.Id, i);
                if (!String.IsNullOrEmpty(defaulBlockIfExists) && b.Name.Equals(defaulBlockIfExists))
                    startSelIndex = i;

                i++;
            }

            if (startSelIndex != -1)
                SelectedIndex = startSelIndex;

        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }



        /// <summary>
        /// Получить все блоки документа
        /// Не брать анонимные блоки, внешние ссылки
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static ObservableCollection<BlockTableRecord> GetBlocks(Document doc)
        {
            ObservableCollection<BlockTableRecord> blocks = new ObservableCollection<BlockTableRecord>();
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable layerTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId blockId in layerTable)
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
                    //проверить что это не анонимный блок и не внешняя ссылка
                    if (BlockTableRecordAllowed(btr))
                    {
                        blocks.Add(btr);
                    }
                }

                tr.Commit();
            }
            return blocks;
        }

        /// <summary>
        /// Не является анонимным блоком (не содержит в имени '*') или ссылкой
        /// </summary>
        /// <param name="btr"></param>
        /// <returns></returns>
        private static bool BlockTableRecordAllowed(BlockTableRecord btr)
        {
            return !btr.Name.Contains("*") && btr.XrefStatus == XrefStatus.NotAnXref;
        }

        /// <summary>
        /// Ручной выбор блока на чертеже
        /// </summary>
        /// <param name="obj"></param>
        private void SelectBlock(object obj)
        {
            if (doc != null && mainWindow != null)
            {
                mainWindow.Hide();

                Editor ed = doc.Editor;
                Database db = doc.Database;

                bool selectedBlockIsAllowed = true;
                do
                {
                    selectedBlockIsAllowed = true;//если выбор будет отменен, то не повторять попытку
                    PromptEntityOptions peo = new PromptEntityOptions("\nУкажите вхождение блока:");
                    peo.SetRejectMessage("\nМожно выбрать только вхождение блока");
                    peo.AddAllowedClass(typeof(BlockReference), true);
                    PromptEntityResult per1 = ed.GetEntity(peo);
                    if (per1.Status == PromptStatus.OK)
                    {
                        ObjectId selId = per1.ObjectId;

                        ObjectId btrId = ObjectId.Null;
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockReference br = (BlockReference)tr.GetObject(selId, OpenMode.ForRead);
                            if (br.IsDynamicBlock)
                                btrId = br.DynamicBlockTableRecord;
                            else
                                btrId = br.BlockTableRecord;

                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            selectedBlockIsAllowed = BlockTableRecordAllowed(btr);//выбран допустимый блок?

                            tr.Commit();
                        }

                        if (selectedBlockIsAllowed)
                        {
                            //Задание связанного свойства
                            int i = -1;
                            blockIndexesLookup.TryGetValue(btrId, out i);
                            if (i != -1)
                            {
                                SelectedIndex = i;
                            }
                        }
                        else
                        {
                            ed.WriteMessage("\nВыбран недопустимый блок");
                        }
                    }
                } while (!selectedBlockIsAllowed);//если выбрана внешняя ссылка или анонимный блок, то повторить попытку
                //mainWindow.Show();
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(mainWindow);
            }
        }
    }
}
