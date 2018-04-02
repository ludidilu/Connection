using System;
using System.Net.Sockets;
using System.Net;

namespace Connection
{
    public class ServerAsync<T> where T : UnitBaseAsync, new()
    {
        private Socket socket;

        private long tick = 0;

        internal static long idleTick { private set; get; }

        private bool isLagTest;

        private int minLagTime;

        private int maxLagTime;

        public void Start(string _path, int _port, int _maxConnections, long _idleTick)
        {
            idleTick = _idleTick;

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(IPAddress.Parse(_path), _port));

            socket.Listen(_maxConnections);

            BeginAccept();
        }

        public void OpenLagTest(int _minLagTime, int _maxLagTime)
        {
            isLagTest = true;

            minLagTime = _minLagTime;

            maxLagTime = _maxLagTime;
        }

        private void BeginAccept()
        {
            socket.BeginAccept(SocketAccept, null);
        }

        private void SocketAccept(IAsyncResult _result)
        {
            Socket clientSocket = socket.EndAccept(_result);

            Log.Write("One user connect");

            ServerUnitAsync<T> unit = new ServerUnitAsync<T>();

            if (isLagTest)
            {
                unit.OpenLagTest(minLagTime, maxLagTime);
            }

            unit.Init(clientSocket, tick);

            BeginAccept();
        }

        public long Update()
        {
            tick++;

            return tick;
        }
    }
}