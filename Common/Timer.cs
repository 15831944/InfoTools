using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class Timer
    {
        Stopwatch stopWatch = new Stopwatch();

        public void Start()
        {
            stopWatch.Start();
        }

        public string TimeOutput(string comment)
        {
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0} -  {1:00}:{2:00}:{3:00}.{4:00}",
                comment, ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Debug.Print("\n" + elapsedTime);
            stopWatch.Reset();
            return elapsedTime;
        }
    }
}
