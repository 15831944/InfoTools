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
    /// Данный контрол сделан без MVVM
    /// </summary>
    public partial class NumericUpDownControl : UserControl
    {
        public event EventHandler NumChanged;

        private double _numValue = 0;

        public double NumValue
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
                txtNum.Text = value.ToString(Formatting);
                txtNum.TextChanged += txtNum_TextChanged;
                if (NumChanged != null)
                {
                    NumChanged(this, new EventArgs());
                }
            }
        }

        public double MaxValue { get; set; } = double.MaxValue;

        public double MinValue { get; set; } = double.MinValue;




        private string formatting = "f2";
        public string Formatting
        {
            get { return formatting; }
            set
            {
                formatting = value;
                NumValue = NumValue;
            }
        }


        public double Step { get; set; } = 1;

        public NumericUpDownControl()
        {
            InitializeComponent();
            txtNum.Text = NumValue.ToString(Formatting);
        }

        private void cmdUp_Click(object sender, RoutedEventArgs e)
        {
            NumValue += Step;
        }

        private void cmdDown_Click(object sender, RoutedEventArgs e)
        {
            NumValue -= Step;
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

            double typedInteger = 0;

            int initialCursorPos = txtNum.CaretIndex;
            if (double.TryParse(txtNum.Text, out typedInteger) || String.IsNullOrEmpty(txtNum.Text))
            {
                NumValue = typedInteger;
            }
            else
            {
                txtNum.Text = NumValue.ToString(Formatting);//Вернуть прежнюю строку
            }

            //при этом нужно сохранить положение курсора если оно по какой-то причине сбросилось
            if (txtNum.CaretIndex < initialCursorPos)
            {
                txtNum.CaretIndex = initialCursorPos;
            }


        }



    }
}
