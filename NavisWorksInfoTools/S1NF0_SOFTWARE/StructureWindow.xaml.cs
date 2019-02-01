using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Логика взаимодействия для StructureWindow.xaml
    /// </summary>
    public partial class StructureWindow : Window
    {


        private IEnumerable<ModelItem> selectedGeometryModelItems = null;
        public IEnumerable<ModelItem> SelectedGeometryModelItems { /*get;*/
            set
            {
                selectedGeometryModelItems = value;
                if (selectedGeometryModelItems!=null)
                {
                    selectedCountLabel.Content = selectedGeometryModelItems.Count();
                }
                else
                {
                    selectedCountLabel.Content = "0";
                }
                //Определить набор объектов, которые еще не были добавлены в структуру
                SetSelectedNotAddedGeometryModelItems();
            }
        }

        private List<ModelItem> selectedNotAddedGeometryModelItems = null;
        private void SetSelectedNotAddedGeometryModelItems()
        {
            if (selectedGeometryModelItems != null)
            {
                selectedNotAddedGeometryModelItems = new List<ModelItem>();
                foreach (ModelItem item in selectedGeometryModelItems)
                {
                    string replacementName, baseName, exportName, strId;
                    if (!dataStorage.ItemAdded(item, out baseName, out exportName, out replacementName, out strId))
                    {
                        selectedNotAddedGeometryModelItems.Add(item);
                    }
                    
                }
                addedCountLabel.Content = selectedGeometryModelItems.Count() - selectedNotAddedGeometryModelItems.Count();
            }
            else
            {
                selectedNotAddedGeometryModelItems = null;
                addedCountLabel.Content = selectedGeometryModelItems.Count();
            }
            
        }

        private StructureDataStorage dataStorage = null;

        //private static double verticalOffset = -1;
        private static bool finalClose = false;

        private XML.St.Object selectedItem = null;
        public StructureWindow(StructureDataStorage dataStorage)
        {
            finalClose = false;

            this.dataStorage = dataStorage;

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Создать узлы в Treeview
            structureTree.ItemsSource = dataStorage.Structure.NestedObjects;
        }

        public void BeforeShow()
        {
            selectedItem = structureTree.SelectedItem as XML.St.Object;
            EnableAddItemsButton();
            EnableRemoveItemsButton();
        }

        private void structureTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            selectedItem = e.NewValue as XML.St.Object;
            EnableAddItemsButton();
            EnableRemoveItemsButton();
        }

        private void EnableAddItemsButton()
        {
            addItemsButton.IsEnabled = selectedItem != null && selectedNotAddedGeometryModelItems != null && selectedNotAddedGeometryModelItems.Count() > 0;
        }

        private void EnableRemoveItemsButton()
        {
            removeItemsButton.IsEnabled = selectedItem != null;
            removeAllItemsButton.IsEnabled = selectedItem != null;
        }

        private void addItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItem != null && selectedNotAddedGeometryModelItems != null && selectedNotAddedGeometryModelItems.Count() > 0)
            {
                foreach (ModelItem item in selectedNotAddedGeometryModelItems)
                {
                    dataStorage.CreateNewModelObject(selectedItem, item);
                }
                selectedItem.NotifyPropertyChanged();//Оповестить только один раз в конце!
                SetSelectedNotAddedGeometryModelItems();
            }
            EnableAddItemsButton();
        }

        private void removeItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItem != null)
            {
                dataStorage.ResetNestedObjects(selectedItem);
                SetSelectedNotAddedGeometryModelItems();
            }
            EnableAddItemsButton();
            EnableRemoveItemsButton();
        }

        private void removeAllItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItem != null)
            {
                dataStorage.RemoveNestedObjectsOtherDocuments(selectedItem);
            }
            EnableAddItemsButton();
            EnableRemoveItemsButton();
        }

        private void selectNotAddedButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            dataStorage.SelectAdded();
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            finalClose = true;
            this.Close();
            //Сериализовать объекты
            dataStorage.SerializeStruture();
            AddObjectsToStructure.DataStorage = null;
            MessageBox.Show("Данные сохранены", "Готово", MessageBoxButton.OK);
        }

        private void canсelButton_Click(object sender, RoutedEventArgs e)
        {
            finalClose = true;
            this.Close();
            //verticalOffset = -1;//Забыть значение положения скролбара
            AddObjectsToStructure.DataStorage = null;
        }


        private void TreeViewSelectedItemChanged(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = sender as TreeViewItem;
            if (item != null)
            {

                item.BringIntoView();
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Окно не закрывать, а только спрятать его, для того, чтобы запомнить положение скролбара
            if (!finalClose)
            {
                e.Cancel = true;
                this.Visibility = Visibility.Hidden;
            }
            

        }

        
    }

}
