using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
//using System.Windows.Shapes;
using WinForms = System.Windows.Forms;

namespace Common
{
    /// <summary>
    /// Логика взаимодействия для FileNameInputControl.xaml
    /// Данный контрол сделан без MVVM
    /// </summary>
    public partial class FileNameInputControl : UserControl
    {
        public event EventHandler FileNameChanged;

        public string FileName
        {
            get
            {
                return fileNameTextBox.Text;
            }
            set
            {
                fileNameTextBox.Text = value;
            }
        }

        public string OpenFileDialogFilter { get; set; }

        public string OpenFileDialogTitle { get; set; }

        public FileNameInputControl()
        {
            InitializeComponent();
        }


        private void fileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (fileNameTextBox.Text.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                string safePathName = Common.Utils.GetSavePathName(fileNameTextBox.Text);
                fileNameTextBox.Text = safePathName;
            }

            if (FileNameChanged != null)
            {
                FileNameChanged(this, new EventArgs());
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!String.IsNullOrEmpty(FileName))
            {
                string dir = null;
                FileAttributes attr = File.GetAttributes(FileName);
                if (attr.HasFlag(FileAttributes.Directory))
                    dir = FileName;
                else
                    dir = Path.GetDirectoryName(FileName);

                if (!String.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    initialPath = dir;
                }
            }

            WinForms.OpenFileDialog openFileDialog = new WinForms.OpenFileDialog();

            openFileDialog.InitialDirectory = initialPath;
            if (!String.IsNullOrEmpty(OpenFileDialogFilter))
                openFileDialog.Filter = OpenFileDialogFilter;
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Multiselect = false;
            if (!String.IsNullOrEmpty(OpenFileDialogTitle))
                openFileDialog.Title = OpenFileDialogTitle;
            if (openFileDialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                FileName = openFileDialog.FileName;
            }

        }
    }
}
