using System.Net.Sockets;
using System.Net;
using System;
using System.IO;

namespace Connection
{
    public class Client
    {
        enum ConnectStatus
        {
            DISCONNECT,
            CONNECTING,
            LOGINING,
            CONNECTED
        }

        private ushort bodyLength = 0;
        private byte[] headBuffer = new byte[Constant.HEAD_LENGTH];
        private byte[] bodyBuffer = new byte[ushort.MaxValue];

        private object locker = new object();

        private Socket socket;

        private string ip;

        private int port;

        private int uid;

        private Action<BinaryReader> pushDataCallBack;

        private Action<BinaryReader> receiveDataCallBack;

        private Action<BinaryReader> connectSuccessCallBack;

        private bool isReceivingHead = true;

        private ConnectStatus connectStatus = ConnectStatus.DISCONNECT;

        public void Init(string _ip, int _port, int _uid, Action<BinaryReader> _pushDataCallBack)
        {
            ip = _ip;

            port = _port;

            uid = _uid;

            pushDataCallBack = _pushDataCallBack;
        }

        public void Connect(Action<BinaryReader> _connectSuccessCallBack)
        {
            connectSuccessCallBack = _connectSuccessCallBack;

            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(ip), port);

            lock (locker)
            {
                connectStatus = ConnectStatus.CONNECTING;
            }

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.BeginConnect(ipe, ConnectCallBack, null);
        }

        private void ConnectCallBack(IAsyncResult _result)
        {
            socket.EndConnect(_result);

            lock (locker)
            {
                connectStatus = ConnectStatus.LOGINING;
            }

            socket.BeginSend(BitConverter.GetBytes(uid), 0, Constant.UID_LENGTH, SocketFlags.None, SendCallBack, null);
        }

        public void Update()
        {
            lock (locker)
            {
                if (connectStatus == ConnectStatus.LOGINING || connectStatus == ConnectStatus.CONNECTED)
                {
                    if (CheckDisconnect())
                    {
                        Log.Write("Disconnect!");

                        connectStatus = ConnectStatus.DISCONNECT;

                        return;
                    }

                    if (isReceivingHead)
                    {
                        ReceiveHead();
                    }
                    else
                    {
                        ReceiveBody();
                    }
                }
            }
        }

        private bool CheckDisconnect()
        {
            return socket.Poll(10, SelectMode.SelectRead) && socket.Available == 0;
        }

        private void ReceiveHead()
        {
            if (socket.Available >= Constant.HEAD_LENGTH)
            {
                socket.Receive(headBuffer, Constant.HEAD_LENGTH, SocketFlags.None);

                isReceivingHead = false;

                bodyLength = BitConverter.ToUInt16(headBuffer, 0);

                ReceiveBody();
            }
        }

        private void ReceiveBody()
        {
            if (socket.Available >= bodyLength)
            {
                socket.Receive(bodyBuffer, bodyLength, SocketFlags.None);

                isReceivingHead = true;

                using (MemoryStream ms = new MemoryStream(bodyBuffer))
                {
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        Constant.PackageTag tag = (Constant.PackageTag)br.ReadByte();

                        Action<BinaryReader> tmpCb;

                        lock (locker)
                        {
                            if (connectStatus == ConnectStatus.LOGINING && tag == Constant.PackageTag.LOGIN)
                            {
                                connectStatus = ConnectStatus.CONNECTED;

                                tmpCb = connectSuccessCallBack;

                                connectSuccessCallBack = null;
                            }
                            else if (connectStatus == ConnectStatus.CONNECTED)
                            {
                                if (tag == Constant.PackageTag.REPLY)
                                {
                                    if (receiveDataCallBack == null)
                                    {
                                        throw new Exception("error9!");
                                    }
                                    else
                                    {
                                        tmpCb = receiveDataCallBack;

                                        receiveDataCallBack = null;
                                    }
                                }
                                else if (tag == Constant.PackageTag.PUSH)
                                {
                                    tmpCb = pushDataCallBack;
                                }
                                else
                                {
                                    throw new Exception("error!");
                                }
                            }
                            else
                            {
                                throw new Exception("error1!");
                            }
                        }

                        tmpCb(br);
                    }
                }
            }
        }

        public void Send(MemoryStream _ms, Action<BinaryReader> _receiveDataCallBack)
        {
            lock (locker)
            {
                if (connectStatus != ConnectStatus.CONNECTED)
                {
                    throw new Exception("SendData error0!");
                }
            }

            if (receiveDataCallBack != null)
            {
                throw new Exception("SendData error!");
            }

            receiveDataCallBack = _receiveDataCallBack;

            int length = Constant.HEAD_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)_ms.Length), bodyBuffer, Constant.HEAD_LENGTH);

            Array.Copy(_ms.GetBuffer(), 0, bodyBuffer, Constant.HEAD_LENGTH, _ms.Length);

            socket.BeginSend(bodyBuffer, 0, length, SocketFlags.None, SendCallBack, null);
        }

        public void Send(MemoryStream _ms)
        {
            lock (locker)
            {
                if (connectStatus != ConnectStatus.CONNECTED)
                {
                    throw new Exception("SendData error0!");
                }
            }

            int length = Constant.HEAD_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)_ms.Length), bodyBuffer, Constant.HEAD_LENGTH);

            Array.Copy(_ms.GetBuffer(), 0, bodyBuffer, Constant.HEAD_LENGTH, _ms.Length);

            socket.BeginSend(bodyBuffer, 0, length, SocketFlags.None, SendCallBack, null);
        }

        private void SendCallBack(IAsyncResult _result)
        {
            socket.EndSend(_result);
        }

        public void Close()
        {
            connectStatus = ConnectStatus.DISCONNECT;

            socket.Close();

            socket = null;
        }
    }
}
