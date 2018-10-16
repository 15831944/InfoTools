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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Autodesk.Civil.DatabaseServices.Styles;

namespace Civil3DInfoTools.Controls.SelectPartSizeUserControl
{
    /// <summary>
    /// Логика взаимодействия для SelectPartSizeView.xaml
    /// </summary>
    public partial class SelectPartSizeView : UserControl
    {
        //DependencyProperty - https://metanit.com/sharp/wpf/13.php
        //ОБЯЗАТЕЛЬНО ВСЕ ДОЛЖНО СООТВЕТСТВОВАТЬ ПРИМЕРУ ПО ССЫЛКЕ
        //ПРИ ЛЮБОМ ОТКЛОНЕНИИ BINDING МОЖЕТ НЕ РАБОТАТЬ!!!!!!!!
        public static readonly DependencyProperty PartsListProperty
            = DependencyProperty.Register("PartsList", typeof(PartsList), typeof(SelectPartSizeView),
                new FrameworkPropertyMetadata(null/*, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault*/
                                                  , new PropertyChangedCallback(NotifyViewModel)
                                                  ));

        public PartsList PartsList
        {
            get { return (PartsList)GetValue(PartsListProperty); }
            set { SetValue(PartsListProperty, value); }
        }

        //Оповещение ViewModel об изменении свойства PartsList View через этот метод работает нормально
        //Наверное, более правитьно исп. что-то отсюда - https://stackoverflow.com/questions/15132538/twoway-bind-views-dependencyproperty-to-viewmodels-property
        //но у меня это не заработало 
        //TODO: Есть еще большой пост на эту тему, который необходимо изучить - 
        //http://formatexception.com/2014/04/binding-a-dependency-property-of-a-view-to-its-viewmodel/
        private static void NotifyViewModel(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectPartSizeView obj = d as SelectPartSizeView;
            if (obj != null)
            {
                SelectPartSizeViewModel vm = obj.DataContext as SelectPartSizeViewModel;
                if (vm!=null)
                {
                    vm.PartsList = obj.PartsList;
                }
                
            }
            
        }

        public SelectPartSizeView()
        {
            InitializeComponent();

            //var binding = new Binding("PartsList") { Mode = BindingMode.TwoWay };//похоже это не работает
            //this.SetBinding(PartsListProperty, binding);
        }
    }
}
