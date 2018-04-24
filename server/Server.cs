using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;

namespace Connection
{
    public class Server<T> where T : UnitBase, new()
    {
        private Socket socket;

        private long tick = 0;

        private object locker = new object();

        private List<Socket> clientList = new List<Socket>();

        private Dictionary<ServerUnit<T>, bool> dic = new Dictionary<ServerUnit<T>, bool>();

        private List<ServerUnit<T>> kickList = new List<ServerUnit<T>>();

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

            lock (locker)
            {
                clientList.Add(clientSocket);
            }

            BeginAccept();
        }

        public long Update()
        {
            tick++;

            lock (locker)
            {
                if (clientList.Count > 0)
                {
                    for (int i = 0; i < clientList.Count; i++)
                    {
                        Socket clientSocket = clientList[i];

                        ServerUnit<T> unit = new ServerUnit<T>();

                        if (isLagTest)
                        {
                            unit.OpenLagTest(minLagTime, maxLagTime);
                        }

                        unit.Init(clientSocket, Kick);

                        dic.Add(unit, false);
                    }

                    clientList.Clear();
                }
            }

            IEnumerator<ServerUnit<T>> enumerator = dic.Keys.GetEnumerator();

            while (enumerator.MoveNext())
            {
                enumerator.Current.Update();
            }

            if (kickList.Count > 0)
            {
                enumerator = kickList.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    ServerUnit<T> unit = enumerator.Current;

                    dic.Remove(unit);
                }

                kickList.Clear();
            }

            return tick;
        }

        private void Kick(ServerUnit<T> _unit)
        {
            kickList.Add(_unit);
        }
    }
}