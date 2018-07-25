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

namespace NavisWorksInfoTools
{
    /// <summary>
    /// Логика взаимодействия для ChangeLinksProps.xaml
    /// </summary>
    public partial class ChangeLinksProps : Window
    {
        public bool ChangeAllUrls
        {
            get
            {
                bool? checkBoxState = changeAllUrlsCheckBox.IsChecked;
                return checkBoxState!=null ? checkBoxState.Value : false;
            }
            set
            {
                changeAllUrlsCheckBox.IsChecked = value;
            }
        }

        public string OldUrl
        {
            get
            {
                //Убирать с конца слеш
                string retVal = oldUrlTextBox.Text;
                if (retVal.EndsWith("/") || retVal.EndsWith("\\"))
                {
                    retVal = retVal.TrimEnd('/');
                    retVal = retVal.TrimEnd('\\');
                }
                return retVal;
            }
            set
            {
                oldUrlTextBox.Text = value;
            }
        }

        public string NewUrl
        {
            get
            {
                //Убирать с конца слеш
                string retVal = newUrlTextBox.Text;
                if (retVal.EndsWith("/") || retVal.EndsWith("\\"))
                {
                    retVal = retVal.TrimEnd('/');
                    retVal = retVal.TrimEnd('\\');
                }
                return retVal;
            }
            set
            {
                newUrlTextBox.Text = value;
            }
        }

        public ChangeLinksProps()
        {
            InitializeComponent();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            //выключить текстбокс со старым именем
            oldUrlTextBox.IsEnabled = false;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            //включить текстбокс со старым именем
            oldUrlTextBox.IsEnabled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
