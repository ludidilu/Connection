using System;
using System.Threading;
using System.Net.Sockets;
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

        private Socket socket;

        private AsyncCallback callBack;

        internal void Init(Socket _socket, AsyncCallback _callBack)
        {
            socket = _socket;

            callBack = _callBack;
        }

        internal void SetTime(int _minLagTime, int _maxLagTime)
        {
            minLagTime = _minLagTime;

            maxLagTime = _maxLagTime;
        }

        internal void Add(byte[] _bytes)
        {
            int lagTime = minLagTime + (int)(random.NextDouble() * (maxLagTime - minLagTime));

            long time = Stopwatch.GetTimestamp() + lagTime;

            Action dele = delegate ()
            {
                long nowTime = Stopwatch.GetTimestamp();

                if (nowTime < time)
                {
                    int sleepTime = (int)(time - nowTime);

                    Thread.Sleep(sleepTime);
                }

                try
                {
                    socket.BeginSend(_bytes, 0, _bytes.Length, SocketFlags.None, callBack, null);
                }
                catch (Exception e)
                {

                }
            };

            service.Process(dele);
        }
    }
}
