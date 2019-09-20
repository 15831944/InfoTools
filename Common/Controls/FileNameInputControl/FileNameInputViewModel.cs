using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace Common.Controls.FileNameInputControl
{
    public class FileNameInputViewModel : INotifyPropertyChanged
    {
        private SolidColorBrush _invalidFilenameTextColor = new SolidColorBrush(Colors.DarkSlateGray);

        private bool _validationForFileOrDirectory = true;

        public event EventHandler FileNameChanged;

        private string fileName = null;
        public string FileName
        {
            get { return fileName; }
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    value = "";
                }
                else
                {
                    //не позволяет вводить недопустимые для имени файла символы
                    if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    {
                        value = Common.Utils.GetSavePathName(value);
                    }
                }

                fileName = value;
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(TextColor));

                if (FileNameChanged != null)
                {
                    FileNameChanged(this, new EventArgs());
                }
            }
        }


        public bool FileNameIsValid
        {
            get
            {
                if (_validationForFileOrDirectory)
                    return File.Exists(FileName) || Directory.Exists(FileName);
                else
                    return File.Exists(FileName);
            }
        }

        public SolidColorBrush TextColor
        {
            get
            {
                return FileNameIsValid ?
                    new SolidColorBrush(Colors.Black)
                    : _invalidFilenameTextColor;
            }
        }



        public string OpenFileDialogFilter { get; set; }

        public string OpenFileDialogTitle { get; set; }

        private readonly RelayCommand browseCommand = null;
        public RelayCommand BrowseCommand
        { get { return browseCommand; } }

        public FileNameInputViewModel(string openFileDialogFilter, string openFileDialogTitle,
            SolidColorBrush invalidFilenameTextColor = null, bool validationForFileOrDirectory = true)
        {
            if (invalidFilenameTextColor != null)
            {
                this._invalidFilenameTextColor = invalidFilenameTextColor;
            }

            _validationForFileOrDirectory = validationForFileOrDirectory;

            browseCommand = new RelayCommand(new Action<object>(Browse));

            this.OpenFileDialogFilter = openFileDialogFilter;
            this.OpenFileDialogTitle = openFileDialogTitle;

            fileName = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void Browse(object obj)
        {
            string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!String.IsNullOrEmpty(FileName) && (File.Exists(FileName) || Directory.Exists(FileName)))
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



        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
