using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;

namespace Connection
{
    public class ServerAsync<T> where T : UnitBase, new()
    {
        private Socket socket;

        private long tick = 0;

        private object locker = new object();

        private Dictionary<ServerUnitAsync<T>, bool> dic = new Dictionary<ServerUnitAsync<T>, bool>();

        private List<ServerUnitAsync<T>> kickList = new List<ServerUnitAsync<T>>();

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

            unit.Init(clientSocket, Kick);

            lock (locker)
            {
                dic.Add(unit, false);
            }

            BeginAccept();
        }

        public long Update()
        {
            tick++;

            lock (locker)
            {
                IEnumerator<ServerUnitAsync<T>> enumerator = dic.Keys.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    enumerator.Current.Update();
                }

                if (kickList.Count > 0)
                {
                    enumerator = kickList.GetEnumerator();

                    while (enumerator.MoveNext())
                    {
                        ServerUnitAsync<T> unit = enumerator.Current;

                        dic.Remove(unit);
                    }

                    kickList.Clear();
                }
            }

            return tick;
        }

        private void Kick(ServerUnitAsync<T> _unit)
        {
            lock (locker)
            {
                kickList.Add(_unit);
            }
        }
    }
}