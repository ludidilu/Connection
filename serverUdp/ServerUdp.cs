using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Threading;

namespace Connection
{
    public class ServerUdp<T> where T : UnitBase, new()
    {
        private const int BYTE_LENGTH = 1024;

        private const string STR_FIX = "{0}:{1}";

        private Socket socket;

        private Dictionary<string, ServerUnitUdp<T>> dic = new Dictionary<string, ServerUnitUdp<T>>();

        private IPEndPoint iep = new IPEndPoint(IPAddress.Any, 0);

        private long tick = 0;

        internal static long idleTick { private set; get; }

        private bool isLagTest;

        private int minLagTime;

        private int maxLagTime;

        public void Start(string _path, int _port, long _idleTick)
        {
            idleTick = _idleTick;

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Bind(new IPEndPoint(IPAddress.Any, _port));

            Thread thread = new Thread(Receive);

            thread.Start();
        }

        public void OpenLagTest(int _minLagTime, int _maxLagTime)
        {
            isLagTest = true;

            minLagTime = _minLagTime;

            maxLagTime = _maxLagTime;
        }

        private void Receive()
        {
            byte[] bytes = new byte[BYTE_LENGTH];

            while (true)
            {
                EndPoint ep = iep;

                try
                {
                    int length = socket.ReceiveFrom(bytes, 0, BYTE_LENGTH, SocketFlags.None, ref ep);

                    if (length > Constant.HEAD_LENGTH)
                    {
                        int bodyLength = BitConverter.ToUInt16(bytes, 0);

                        if (bodyLength + Constant.HEAD_LENGTH == length)
                        {
                            byte[] result = new byte[bodyLength];

                            Array.Copy(bytes, Constant.HEAD_LENGTH, result, 0, bodyLength);

                            IPEndPoint tiep = ep as IPEndPoint;

                            string name = string.Format(STR_FIX, tiep.Address.ToString(), tiep.Port);

                            GetData(name, tiep, result);
                        }
                    }
                }
                catch (Exception e)
                {
                }
            }
        }

        private void GetData(string _name, IPEndPoint _iep, byte[] _data)
        {
            ServerUnitUdp<T> unit;

            if (!dic.TryGetValue(_name, out unit))
            {
                unit = new ServerUnitUdp<T>();

                if (isLagTest)
                {
                    unit.OpenLagTest(minLagTime, maxLagTime);
                }

                unit.Init(socket, _iep);

                dic.Add(_name, unit);
            }

            unit.ReceiveData(_data);
        }
    }
}