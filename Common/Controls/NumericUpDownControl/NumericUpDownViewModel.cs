using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Common.Controls.NumericUpDownControl
{
    public class NumericUpDownViewModel : INotifyPropertyChanged
    {
        public event EventHandler SelectionChanged;

        private double numValue = 0;
        private double step;
        private double minValue;
        private double maxValue;
        private string formatting = "f2";

        public double NumValue
        {
            get { return numValue; }
            private set//значение может присваиваться из сеттера ValueString или из команд
            {
                if (value < minValue)
                    value = minValue;
                if (value > maxValue)
                    value = maxValue;

                numValue = value;
                OnPropertyChanged("ValueString");

                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new EventArgs());
                }
            }
        }


        /// <summary>
        /// Binding on TextBox.Text
        /// </summary>
        public string ValueString
        {
            get { return numValue.ToString(formatting); }
            set
            {
                //подменить запятую на точку
                value = value.Replace(',', '.');

                double typedNum = 0;
                if (double.TryParse(value, out typedNum) || String.IsNullOrEmpty(value))
                {
                    NumValue = typedNum;//числовое значение меняется только если парсится
                }
                else
                {
                    NumValue = numValue;//иначе сбросить на предыдущее значение
                }
            }
        }


        private readonly RelayCommand stepCommand = null;
        public RelayCommand StepCommand
        { get { return stepCommand; } }

        private readonly RelayCommand increaseCommand = null;
        public RelayCommand IncreaseCommand
        { get { return increaseCommand; } }

        private readonly RelayCommand decreaseCommand = null;
        public RelayCommand DecreaseCommand
        { get { return decreaseCommand; } }



        public NumericUpDownViewModel(double startValue, double step = 1,
            double minValue = double.MinValue, double maxValue = double.MaxValue,
            string formatting = null)
        {
            stepCommand = new RelayCommand(new Action<object>(Step));
            increaseCommand = new RelayCommand(new Action<object>(Increase));
            decreaseCommand = new RelayCommand(new Action<object>(Decrease));

            this.numValue = startValue;
            this.step = step;
            this.minValue = minValue;
            this.maxValue = maxValue;

            if (formatting != null)
            {
                this.formatting = formatting;
            }
        }

        private void Step(object param)
        {
            bool increase = (bool)param;

            NumValue = increase ? NumValue + step : NumValue - step;
        }

        private void Increase(object param)
        {
            NumValue += step;
        }

        private void Decrease(object param)
        {
            NumValue -= step;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
