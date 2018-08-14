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
        private static string defaultOldUrlFragment = null;
        private static string defaultNewUrlFragment = null;
        private static bool? defaultChangeAllUrls = null;


        public bool ChangeAllUrls
        {
            get
            {
                bool? checkedState = changeAllUrlsRadioBtn.IsChecked;
                return checkedState != null ? checkedState.Value : false;
            }
            set
            {
                changeAllUrlsRadioBtn.IsChecked = value;
                //changeOnlySpecifiedFragmRadioBtn.IsChecked = !value;
            }
        }

        public string OldUrlFragment
        {
            get
            {
                //Убирать с конца слеш
                string retVal = TrimSlashEnd(oldUrlTextBox.Text);
                return retVal;
            }
            set
            {
                oldUrlTextBox.Text = value;
            }
        }

        public string NewUrlFragment
        {
            get
            {
                //Убирать с конца слеш
                string retVal = TrimSlashEnd(newUrlTextBox.Text);
                return retVal;
            }
            set
            {
                newUrlTextBox.Text = value;
            }
        }

        private string TrimSlashEnd(string txt)
        {
            string retVal = txt;
            if (retVal.EndsWith("/") || retVal.EndsWith("\\"))
            {
                retVal = retVal.TrimEnd('/');
                retVal = retVal.TrimEnd('\\');
            }

            return retVal;
        }

        public ChangeLinksProps()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(defaultOldUrlFragment))
            {
                OldUrlFragment = defaultOldUrlFragment;
            }
            if (!String.IsNullOrEmpty(defaultNewUrlFragment))
            {
                NewUrlFragment = defaultNewUrlFragment;
            }
            if (defaultChangeAllUrls != null)
            {
                ChangeAllUrls = defaultChangeAllUrls.Value;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            defaultOldUrlFragment = OldUrlFragment;
            defaultNewUrlFragment = NewUrlFragment;
            defaultChangeAllUrls = ChangeAllUrls;
            this.Close();
        }

        private void changeOnlySpecifiedFragmRadioBtn_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool? state = changeOnlySpecifiedFragmRadioBtn.IsChecked;
            if (oldUrlTextBox != null && state != null)
            {
                oldUrlTextBox.IsEnabled = state.Value;
            }
        }

        
    }
}
