using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Controls.BusyIndicator
{
    //https://stackoverflow.com/questions/18279572/create-a-wpf-progress-window-on-another-thread
    public static class BusyIndicatorHelper 
    {
        private static BusyIndicatorView progWindow;
        private static EventWaitHandle _progressWindowWaitHandle;

        public static void ShowBusyIndicator(string message = "Something happening")
        {
            //Starts New Progress Window Thread
            using (_progressWindowWaitHandle = new AutoResetEvent(false))
            {

                //Starts the progress window thread
                Thread newprogWindowThread = new Thread(new ParameterizedThreadStart(ShowProgWindow));
                newprogWindowThread.SetApartmentState(ApartmentState.STA);//https://stackoverflow.com/a/4156000/8020304
                newprogWindowThread.IsBackground = true;
                newprogWindowThread.Start(message);

                //Wait for thread to notify that it has created the window
                _progressWindowWaitHandle.WaitOne();
            }
        }

        public static void SetMessage(string message)
        {
            if (progWindow != null)
            {
                progWindow.Message = message;
            }
        }


        public static void CloseBusyIndicator()
        {
            if (progWindow!=null)
            {
                //closes the Progress window
                progWindow.Dispatcher.Invoke(new Action(progWindow.Close));

                progWindow = null;
                _progressWindowWaitHandle = null;
            }
            
        }



        private static void ShowProgWindow(object message)
        {
            //creates and shows the progress window
            progWindow = new BusyIndicatorView();
            progWindow.Message = (string)message;
            progWindow.Show();

            //Notifies command thread the window has been created
            //_progressWindowWaitHandle.Set();
            progWindow.Dispatcher.BeginInvoke(new Func<bool>(_progressWindowWaitHandle.Set));//https://stackoverflow.com/a/18280446/8020304

            //Starts window dispatcher
            System.Windows.Threading.Dispatcher.Run();

        }

    }
}
