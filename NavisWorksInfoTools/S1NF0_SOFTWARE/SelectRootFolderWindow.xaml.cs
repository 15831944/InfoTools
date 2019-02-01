using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
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

namespace NavisWorksInfoTools.S1NF0_SOFTWARE
{
    /// <summary>
    /// Логика взаимодействия для SelectRootFolderWindow.xaml
    /// </summary>
    public partial class SelectRootFolderWindow : Window
    {
        List<FolderItem> folders = null;

        bool showPropCategoriesToSelect = false;

        public FolderItem RootFolder
        {
            get
            {
                object selected = foldersDataGrid.SelectedItem;
                if (selected!=null)
                {
                    return (FolderItem)selected;
                }
                return null;
            }
        }

        public SelectRootFolderWindow(List<FolderItem> folders, bool showPropCategoriesToSelect)
        {
            InitializeComponent();
            this.folders = folders;
            this.showPropCategoriesToSelect = showPropCategoriesToSelect;
        }

        public List<string> SelectedCategories {
            get
            {
                return propCategoriesViewModel.Categories.Where(c => c.Accept == true)
                    .Select(c=>c.InternalName).ToList();
            }
        }
        private PropCategoriesControl.PropCategoriesViewModel propCategoriesViewModel
            = new PropCategoriesControl.PropCategoriesViewModel();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foldersDataGrid.ItemsSource = folders;

            if (showPropCategoriesToSelect)
            {
                this.Height = 400.0;
                PropCategoriesControl.PropCategoriesView propCategoriesView = new PropCategoriesControl.PropCategoriesView();
                propCategoriesView.Margin = new Thickness(0, 174, 0, 35);
                mainGrid.Children.Add(propCategoriesView);
                propCategoriesView.DataContext = propCategoriesViewModel;
            }

            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void foldersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IEnumerable<FolderItem> selected = foldersDataGrid.SelectedItems.Cast<FolderItem>();
            okButton.IsEnabled = selected.Count() > 0;
        }
    }
}
