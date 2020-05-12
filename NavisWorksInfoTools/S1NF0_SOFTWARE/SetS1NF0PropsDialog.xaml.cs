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
    /// Логика взаимодействия для SetS1NF0PropsDialog.xaml
    /// </summary>
    public partial class SetS1NF0PropsDialog : Window
    {
        public Dictionary<string, bool> ToOverwrite
        {
            get
            {
                Dictionary<string, bool> result 
                    = new Dictionary<string, bool>();

                foreach (var item in S1NF0PropsStackPanel.Children)
                {
                    if(item is CheckBox)
                    {
                        var checkBox = (CheckBox)item;
                        if (checkBox.IsChecked.HasValue 
                            && checkBox.Content is string)
                        {
                            result.Add((string)checkBox.Content,
                            checkBox.IsChecked.Value);
                        }
                        
                    }
                }
                return result;
            }
            set
            {
                foreach (var kvp in value)
                {
                    CheckBox checkBox = new CheckBox();
                    checkBox.Content = kvp.Key;
                    checkBox.IsChecked = kvp.Value;

                    S1NF0PropsStackPanel.Children.Add(checkBox);
                }
            }
        }

        public SetS1NF0PropsDialog(
            Dictionary<string, bool> toOverwrite)
        {
            InitializeComponent();

            ToOverwrite = toOverwrite;
        }

        private void AcceptBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
