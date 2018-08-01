using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RevitInfoTools
{
    /// <summary>
    /// Логика взаимодействия для SelectTypeWindow.xaml
    /// Окно служит для выбора пользователем типоразмера
    /// </summary>
    public partial class SelectTypeWindow : Window
    {
        public bool MultipleSelection { get; set; } = false;

        public List<FamilySymbol> FamilySymbols { get; private set; } = new List<FamilySymbol>();

        public List<FamilySymbol> SelectedFamilySymbols { get; private set; } = new List<FamilySymbol>();

        /// <summary>
        /// Создает окно для выбора типоразмеров определенной категории
        /// </summary>
        public SelectTypeWindow(Document doc, ElementId categoryId)
        {
            InitializeComponent();

            FilteredElementCollector familiesCollector = new FilteredElementCollector(doc);
            List<Family> familiesList =
                familiesCollector
                .OfClass(typeof(Family))
                .Cast<Family>().ToList();
            foreach (Family f in familiesList)
            {
                if (f.FamilyCategory.Id.Equals(categoryId))
                {
                    ISet<ElementId> symbols = f.GetFamilySymbolIds();
                    foreach (ElementId id in symbols)
                    {
                        FamilySymbol fs = (FamilySymbol)doc.GetElement(id);
                        FamilySymbols.Add(fs);
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            typeDataGrid.ItemsSource = FamilySymbols;

            if (MultipleSelection)
                typeDataGrid.SelectionMode = DataGridSelectionMode.Extended;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void typeDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedFamilySymbols = typeDataGrid.SelectedItems.Cast<FamilySymbol>().ToList();
            if (SelectedFamilySymbols.Count > 0)
            {
                System.Drawing.Size imgSize = new System.Drawing.Size(100, 100);
                Bitmap image = SelectedFamilySymbols.First().GetPreviewImage(imgSize);
                if (image != null)
                    typeIconImage.Source = BitmapToImageSource(image);
                else
                    typeIconImage.Source = null;
            }
        }


        /// <summary>
        /// https://stackoverflow.com/a/22501616
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }
}
