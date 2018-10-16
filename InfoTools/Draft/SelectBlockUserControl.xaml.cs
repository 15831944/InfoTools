using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Civil3DInfoTools.Controls
{
    /// <summary>
    /// Логика взаимодействия для SelectBlockUserControl.xaml
    /// </summary>
    public partial class SelectBlockUserControl : UserControl
    {
        //DependencyProperty - https://metanit.com/sharp/wpf/13.php
        //ОБЯЗАТЕЛЬНО ВСЕ ДОЛЖНО СООТВЕТСТВОВАТЬ ПРИМЕРУ ПО ССЫЛКЕ
        //ПРИ ЛЮБОМ ОТКЛОНЕНИИ BINDING МОЖЕТ НЕ РАБОТАТЬ!!!!!!!!
        public static readonly DependencyProperty OwnerProperty
            = DependencyProperty.Register("Owner", typeof(Window), typeof(SelectBlockUserControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                new PropertyChangedCallback(OnChanged)));
        public static readonly DependencyProperty DocProperty
            = DependencyProperty.Register("Doc", typeof(Document), typeof(SelectBlockUserControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    new PropertyChangedCallback(OnChanged)));

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectBlockUserControl obj = (SelectBlockUserControl)d;
            obj.EnableControl();
        }


        public ObjectId BlockId
        {
            get
            {
                BlockTableRecord selectedBtr = blockComboBox.SelectedItem as BlockTableRecord;
                ObjectId retVal = selectedBtr != null ? selectedBtr.Id : ObjectId.Null;
                return retVal;
            }
        }

        //private Window owner;
        //public Window Owner { get { return owner; } set { owner = value; EnableControl(); } }//обязательно задавать
        public Window Owner
        {
            get { return (Window)GetValue(OwnerProperty); }
            set { SetValue(OwnerProperty, value); /*EnableControl();*/ }
        }

        //private Document doc;
        //public Document Doc { get { return doc; } set { doc = value; EnableControl(); } }//обязательно задавать
        public Document Doc
        {
            get { return (Document)GetValue(DocProperty); }
            set { SetValue(DocProperty, value); /*EnableControl();*/ }
        }


        public List<BlockTableRecord> Blocks { get; set; }

        public string DefaultBlockIfExists { get; set; }

        /// <summary>
        /// поле проверяется только при загрузке контрола
        /// </summary>
        private bool controlIsValid = false;

        private bool initialized = false;

        private Dictionary<ObjectId, int> blockInexesLookup = new Dictionary<ObjectId, int>();

        public SelectBlockUserControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!initialized)
                Init();
        }

        private void Init()
        {
            if (controlIsValid)
            {
                //В comboBox отобразить названия всех слоев с отображением цвета
                //http://www.codescratcher.com/wpf/wpf-combobox-with-image/
                if (Blocks == null)
                {
                    Blocks = GetBlocks(Doc);
                }

                Blocks.Sort((a, b) => a.Name.CompareTo(b.Name));
                blockComboBox.ItemsSource = Blocks;
                int startSelIndex = -1;
                int i = 0;
                foreach (BlockTableRecord b in Blocks)
                {
                    blockInexesLookup.Add(b.Id, i);
                    if (!String.IsNullOrEmpty(DefaultBlockIfExists) && b.Name.Equals(DefaultBlockIfExists))
                        startSelIndex = i;

                    i++;
                }

                if (startSelIndex != -1)
                    blockComboBox.SelectedIndex = startSelIndex;

                this.IsEnabled = true;
                initialized = true;
            }
            else
            {
                //Необходимые свойства должны быть присвоены
                //иначе элемент управления нельзя использовать
                this.IsEnabled = false;
            }
        }

        /// <summary>
        /// Включить контрол если необходимые свойства заданы
        /// </summary>
        private void EnableControl()
        {
            controlIsValid = Owner != null && Doc != null;
            if (!initialized)
                Init();
        }

        /// <summary>
        /// Получить все блоки документа
        /// Не брать анонимные блоки, внешние ссылки
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static List<BlockTableRecord> GetBlocks(Document doc)
        {
            List<BlockTableRecord> blocks = new List<BlockTableRecord>();
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (controlIsValid)
            {
                Owner.Hide();

                Editor ed = Doc.Editor;
                Database db = Doc.Database;

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
                            //Выбор соответствующего блока в комбобоксе
                            int i = -1;
                            blockInexesLookup.TryGetValue(btrId, out i);
                            if (i != -1)
                            {
                                blockComboBox.SelectedIndex = i;
                            }
                        }
                        else
                        {
                            ed.WriteMessage("\nВыбран недопустимый блок");
                        }
                    }
                } while (!selectedBlockIsAllowed);//если выбрана внешняя ссылка или анонимный блок, то повторить попытку
                Owner.Show();
            }
        }

        /// <summary>
        /// При выборе объекта в комбо боксе с помощью рефлексии значение Id передается в DataContext в свойство BlockId если оно есть
        /// Ничего лучше я не придумал
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void blockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object o = DataContext;

            PropertyInfo prop = o.GetType().GetProperty("BlockId", BindingFlags.Public | BindingFlags.Instance);
            if (null != prop && prop.CanWrite)
            {
                prop.SetValue(o, BlockId, null);
            }
        }
    }
}
