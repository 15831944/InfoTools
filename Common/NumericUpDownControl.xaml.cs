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

namespace Common
{
    /// <summary>
    /// Логика взаимодействия для NumericUpDownControl.xaml
    /// </summary>
    public partial class NumericUpDownControl : UserControl
    {
        public event EventHandler NumChanged;

        private int _numValue = 0;

        public int NumValue
        {
            get { return _numValue; }
            set
            {
                if (value < MinValue)
                    value = MinValue;
                if (value > MaxValue)
                    value = MaxValue;
                _numValue = value;
                txtNum.TextChanged -= txtNum_TextChanged;//Этот обработчик нужен только при ручном вводе в текстбокс
                txtNum.Text = value.ToString();
                txtNum.TextChanged += txtNum_TextChanged;
                if (NumChanged != null)
                {
                    NumChanged(this, new EventArgs());
                }
            }
        }

        public int MaxValue { get; set; } = int.MaxValue;

        public int MinValue { get; set; } = int.MinValue;

        public NumericUpDownControl()
        {
            InitializeComponent();
            txtNum.Text = NumValue.ToString();
        }

        private void cmdUp_Click(object sender, RoutedEventArgs e)
        {
            NumValue++;
        }

        private void cmdDown_Click(object sender, RoutedEventArgs e)
        {
            NumValue--;
        }

        /// <summary>
        /// Обработчик ввода номера с клавиатуры
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtNum == null)
            {
                return;
            }

            int typedInteger = 0;

            if (!int.TryParse(txtNum.Text, out typedInteger))
            {
                NumValue = typedInteger;
            }
            else
            {
                txtNum.Text = NumValue.ToString();//Вернуть прежнюю строку
            }
                
        }
    }
}
