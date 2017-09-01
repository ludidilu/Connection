using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;

namespace Connection
{
    public class Server<T> where T : UnitBase, new()
    {
        private Socket socket;

        private List<ServerUnit<T>> noLoginList = new List<ServerUnit<T>>();

        private Dictionary<int, ServerUnit<T>> kickDic = new Dictionary<int, ServerUnit<T>>();

        private Dictionary<int, ServerUnit<T>> loginDic = new Dictionary<int, ServerUnit<T>>();

        private Dictionary<int, T> logoutDic = new Dictionary<int, T>();

        private int tick = 0;

        public void Start(string _path, int _port, int _maxConnections)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(IPAddress.Parse(_path), _port));

            socket.Listen(_maxConnections);

            BeginAccept();
        }

        private void BeginAccept()
        {
            socket.BeginAccept(SocketAccept, null);
        }

        private void SocketAccept(IAsyncResult _result)
        {
            Socket clientSocket = socket.EndAccept(_result);

            Console.WriteLine("One user connect");

            ServerUnit<T> serverUnit = new ServerUnit<T>();

            lock (noLoginList)
            {
                noLoginList.Add(serverUnit);

                serverUnit.Init(clientSocket, tick);
            }

            BeginAccept();
        }

        public void Update()
        {
            lock (noLoginList)
            {
                tick++;

                for (int i = noLoginList.Count - 1; i > -1; i--)
                {
                    ServerUnit<T> serverUnit = noLoginList[i];

                    int uid = serverUnit.CheckLogin(tick);

                    if (uid == -1)
                    {
                        noLoginList.RemoveAt(i);
                    }
                    else if (uid > 0)
                    {
                        Console.WriteLine("One user login   uid:" + uid);

                        noLoginList.RemoveAt(i);

                        if (loginDic.ContainsKey(uid))
                        {
                            ServerUnit<T> oldServerUnit = loginDic[uid];

                            oldServerUnit.Kick();

                            serverUnit.SetUnit(oldServerUnit.unit);

                            loginDic[uid] = serverUnit;
                        }
                        else if (logoutDic.ContainsKey(uid))
                        {
                            T unit = logoutDic[uid];

                            logoutDic.Remove(uid);

                            serverUnit.SetUnit(unit);

                            loginDic.Add(uid, serverUnit);
                        }
                        else
                        {
                            T unit = new T();

                            serverUnit.SetUnit(unit);

                            loginDic.Add(uid, serverUnit);
                        }
                    }
                }
            }

            Dictionary<int, ServerUnit<T>>.Enumerator enumerator = loginDic.GetEnumerator();

            while (enumerator.MoveNext())
            {
                KeyValuePair<int, ServerUnit<T>> pair = enumerator.Current;

                bool kick = pair.Value.Update(tick);

                if (kick)
                {
                    kickDic.Add(pair.Key, pair.Value);
                }
            }

            if (kickDic.Count > 0)
            {
                enumerator = kickDic.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    KeyValuePair<int, ServerUnit<T>> pair = enumerator.Current;

                    loginDic.Remove(pair.Key);

                    logoutDic.Add(pair.Key, pair.Value.unit);
                }

                kickDic.Clear();
            }
        }
    }
}