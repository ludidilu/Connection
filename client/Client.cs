using System.Net.Sockets;
using System.Net;
using System;
using System.IO;
using System.Collections.Generic;

namespace Connection
{
    public class Client
    {
        enum ConnectStatus
        {
            DISCONNECT,
            CONNECTING,
            CONNECTED,
            CONNECT_FAIL,
            CONNECT_SUCCESS,
        }

        private ushort bodyLength = 0;
        private byte[] headBuffer = new byte[Constant.HEAD_LENGTH];
        private byte[] bodyBuffer;

        private Queue<byte[]> sendBufferPool = new Queue<byte[]>();

        private object locker = new object();

        private Socket socket;

        private string ip;

        private int port;

        private Action<BinaryReader> pushDataCallBack;

        private Action disconnectCallBack;

        private Action<BinaryReader> receiveDataCallBack;

        private Action<bool> connectCallBack;

        private bool isReceivingHead = true;

        private ConnectStatus connectStatus = ConnectStatus.DISCONNECT;

        public void Init(string _ip, int _port, Action<BinaryReader> _pushDataCallBack, Action _disconnectCallBack)
        {
            ip = _ip;

            port = _port;

            pushDataCallBack = _pushDataCallBack;

            disconnectCallBack = _disconnectCallBack;
        }

        public void Connect(Action<bool> _connectCallBack)
        {
            connectCallBack = _connectCallBack;

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
            try
            {
                socket.EndConnect(_result);
            }
            catch (Exception e)
            {
                Log.Write(e.StackTrace);

                lock (locker)
                {
                    connectStatus = ConnectStatus.CONNECT_FAIL;
                }

                return;
            }

            lock (locker)
            {
                connectStatus = ConnectStatus.CONNECT_SUCCESS;
            }
        }

        public void Update()
        {
            lock (locker)
            {
                if (connectStatus == ConnectStatus.CONNECTED)
                {
                    if (CheckDisconnect())
                    {
                        Log.Write("Disconnect!");

                        connectStatus = ConnectStatus.DISCONNECT;

                        if (disconnectCallBack != null)
                        {
                            disconnectCallBack();
                        }

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
                else if (connectStatus == ConnectStatus.CONNECT_SUCCESS)
                {
                    connectStatus = ConnectStatus.CONNECTED;

                    if (connectCallBack != null)
                    {
                        Action<bool> tmpCb = connectCallBack;

                        connectCallBack = null;

                        tmpCb(true);
                    }
                }
                else if (connectStatus == ConnectStatus.CONNECT_FAIL)
                {
                    connectStatus = ConnectStatus.DISCONNECT;

                    if (connectCallBack != null)
                    {
                        Action<bool> tmpCb = connectCallBack;

                        connectCallBack = null;

                        tmpCb(false);
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

                bodyBuffer = new byte[bodyLength];

                ReceiveBody();
            }
        }

        private void ReceiveBody()
        {
            if (socket.Available >= bodyLength)
            {
                socket.Receive(bodyBuffer, bodyLength, SocketFlags.None);

                byte[] data = bodyBuffer;

                bodyBuffer = null;

                isReceivingHead = true;

                using (MemoryStream ms = new MemoryStream(data))
                {
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        Constant.PackageTag tag = (Constant.PackageTag)br.ReadByte();

                        Action<BinaryReader> tmpCb;

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

                        tmpCb(br);
                    }
                }

                ReceiveHead();
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

            byte[] sendBuffer = GetSendBuffer();

            int length = Constant.HEAD_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)_ms.Length), sendBuffer, Constant.HEAD_LENGTH);

            Array.Copy(_ms.GetBuffer(), 0, sendBuffer, Constant.HEAD_LENGTH, _ms.Length);

            SendBuffer(sendBuffer, length);
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

            byte[] sendBuffer = GetSendBuffer();

            int length = Constant.HEAD_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)_ms.Length), sendBuffer, Constant.HEAD_LENGTH);

            Array.Copy(_ms.GetBuffer(), 0, sendBuffer, Constant.HEAD_LENGTH, _ms.Length);

            SendBuffer(sendBuffer, length);
        }

        private void SendBuffer(byte[] _buffer, int _length)
        {
            try
            {
                socket.BeginSend(_buffer, 0, _length, SocketFlags.None, SendCallBack, _buffer);
            }
            catch (Exception e)
            {
                lock (sendBufferPool)
                {
                    sendBufferPool.Enqueue(_buffer);
                }
            }
        }

        private byte[] GetSendBuffer()
        {
            lock (sendBufferPool)
            {
                byte[] sendBuffer;

                if (sendBufferPool.Count > 0)
                {
                    sendBuffer = sendBufferPool.Dequeue();
                }
                else
                {
                    sendBuffer = new byte[ushort.MaxValue];
                }

                return sendBuffer;
            }
        }

        private void SendCallBack(IAsyncResult _result)
        {
            try
            {
                socket.EndSend(_result);
            }
            catch (Exception e)
            {

            }

            byte[] sendBuffer = _result.AsyncState as byte[];

            lock (sendBufferPool)
            {
                sendBufferPool.Enqueue(sendBuffer);
            }
        }

        public void Close()
        {
            lock (locker)
            {
                connectStatus = ConnectStatus.DISCONNECT;
            }

            socket.Close();

            socket = null;
        }
    }
}
