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

namespace Common.Controls.BusyIndicator
{
    /// <summary>
    /// Логика взаимодействия для BusyIndicatorView.xaml
    /// </summary>
    public partial class BusyIndicatorView : Window
    {
        private System.Windows.Threading.DispatcherTimer dispatcherTimer;

        public string Message { get; set; } = "Something happening";

        private int pointsCount = 1;

        public BusyIndicatorView()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        /// <summary>
        /// Обновление индикатора
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            string points = "";
            for (int i = 0; i < pointsCount; i++)
            {
                points += ".";
            }

            busyIndicatorTextBlock.Text = Message + points;

            // Forcing the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();

            pointsCount = (pointsCount + 1) % 5;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            //makes sure dispatcher is shut down when the window is closed
            this.Dispatcher.InvokeShutdown();
        }
    }
}
