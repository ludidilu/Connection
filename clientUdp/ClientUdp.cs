using System.Net.Sockets;
using System.Net;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace Connection
{
    public class ClientUdp
    {
        private const int BYTE_LENGTH = 1024;

        private List<byte[]> poolList = new List<byte[]>();

        private Queue<byte[]> sendBufferPool = new Queue<byte[]>();

        private bool isReceiving = true;

        private object locker = new object();

        private Socket socket;

        private string ip;

        private int port;

        private int localPort;

        private Action<BinaryReader> pushDataCallBack;

        private Action<BinaryReader> receiveDataCallBack;

        private IPEndPoint iep = new IPEndPoint(IPAddress.Any, 0);

        private IPEndPoint sendIep;

        public void Init(string _ip, int _port, int _localPort, Action<BinaryReader> _pushDataCallBack)
        {
            ip = _ip;

            port = _port;

            localPort = _localPort;

            pushDataCallBack = _pushDataCallBack;

            sendIep = new IPEndPoint(IPAddress.Parse(_ip), _port);

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Bind(new IPEndPoint(IPAddress.Any, _localPort));

            Thread thread = new Thread(Receive);

            thread.Start();
        }

        public void Update()
        {
            lock (locker)
            {
                if (poolList.Count > 0)
                {
                    for (int i = 0; i < poolList.Count; i++)
                    {
                        using (MemoryStream ms = new MemoryStream(poolList[i]))
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
                    }

                    poolList.Clear();
                }
            }
        }

        private void Receive()
        {
            byte[] bytes = new byte[BYTE_LENGTH];

            while (isReceiving)
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

                            lock (locker)
                            {
                                poolList.Add(result);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    break;
                }
            }
        }

        public void Send(MemoryStream _ms, Action<BinaryReader> _receiveDataCallBack)
        {
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
                socket.BeginSendTo(_buffer, 0, _length, SocketFlags.None, sendIep, SendCallBack, _buffer);
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
            isReceiving = false;

            socket.Close();

            socket = null;
        }
    }
}
