using System;
using System.Threading;
using System.Diagnostics;
using superService;

namespace Connection
{
    internal class LagTest
    {
        private int minLagTime;

        private int maxLagTime;

        private SuperService service = new SuperService();

        private Random random = new Random();

        private Stopwatch watch = new Stopwatch();

        internal void SetTime(int _minLagTime, int _maxLagTime)
        {
            minLagTime = _minLagTime;

            maxLagTime = _maxLagTime;

            watch.Start();
        }

        internal void Add(Action _action)
        {
            int lagTime = minLagTime + (int)(random.NextDouble() * (maxLagTime - minLagTime));

            long time = watch.ElapsedMilliseconds + lagTime;

            Action dele = delegate ()
            {
                long nowTime = watch.ElapsedMilliseconds;

                if (nowTime < time)
                {
                    int sleepTime = (int)(time - nowTime);

                    Thread.Sleep(sleepTime);
                }

                try
                {
                    _action();
                }
                catch (Exception e)
                {

                }
            };

            service.Process(dele);
        }
    }
}
