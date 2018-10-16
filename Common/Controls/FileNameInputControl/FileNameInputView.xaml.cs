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

namespace Common.Controls.FileNameInputControl
{
    /// <summary>
    /// Логика взаимодействия для FileNameInputView.xaml
    /// </summary>
    public partial class FileNameInputView : UserControl
    {
        public FileNameInputView()
        {
            InitializeComponent();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox!=null)
            {
                if (e.Key == Key.Return)
                {
                    if (e.Key == Key.Enter)
                    {
                        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    }
                }
            }
        }
    }
}
