using System;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;

namespace Connection
{
    internal class ServerUnitAsync<T> where T : UnitBaseAsync, new()
    {
        private Socket socket;

        private byte[] headBuffer = new byte[Constant.HEAD_LENGTH];

        private byte[] bodyBuffer;

        private int headLength = Constant.HEAD_LENGTH;

        private int headOffset;

        private int bodyLength;

        private int bodyOffset;

        private Queue<byte[]> sendBufferPool = new Queue<byte[]>();

        private T unit;

        private long lastTick;

        private bool isLagTest;

        private LagTest sendLagTest;

        private LagTest receiveLagTest;

        internal void Init(Socket _socket, long _tick)
        {
            socket = _socket;

            lastTick = _tick;

            unit = new T();

            unit.Init(SendData);

            unit.Init();

            ReceiveHead();
        }

        internal void OpenLagTest(int _minLagTime, int _maxLagTime)
        {
            isLagTest = true;

            sendLagTest = new LagTest();

            receiveLagTest = new LagTest();

            sendLagTest.SetTime(_minLagTime, _maxLagTime);

            receiveLagTest.SetTime(_minLagTime, _maxLagTime);
        }

        private void ReceiveHead()
        {
            socket.BeginReceive(headBuffer, headOffset, headLength, SocketFlags.None, ReceiveHeadEnd, null);
        }

        private void ReceiveHeadEnd(IAsyncResult _result)
        {
            try
            {
                int i = socket.EndReceive(_result);

                if (i == 0)
                {
                    Console.WriteLine("disconnect!");
                }
                else if (i < headLength)
                {
                    headOffset = headOffset + i;

                    headLength = headLength - i;

                    ReceiveHead();
                }
                else
                {
                    bodyLength = BitConverter.ToUInt16(headBuffer, 0);

                    bodyOffset = 0;

                    headLength = Constant.HEAD_LENGTH;

                    headOffset = 0;

                    bodyBuffer = new byte[bodyLength];

                    ReceiveBody();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("disconnect!" + e.ToString());
            }
        }

        private void ReceiveBody()
        {
            socket.BeginReceive(bodyBuffer, bodyOffset, bodyLength, SocketFlags.None, ReceiveBodyEnd, null);
        }

        private void ReceiveBodyEnd(IAsyncResult _result)
        {
            try
            {
                int i = socket.EndReceive(_result);

                if (i == 0)
                {
                    Console.WriteLine("disconnect!");
                }
                else if (i < bodyLength)
                {
                    bodyOffset = bodyOffset + i;

                    bodyLength = bodyLength - i;

                    ReceiveBody();
                }
                else
                {
                    byte[] data = bodyBuffer;

                    bodyBuffer = null;

                    Action dele = delegate ()
                    {
                        unit.ReceiveData(data);
                    };

                    unit.Process(dele);

                    ReceiveHead();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("disconnect!" + e.ToString());
            }
        }

        internal void SendData(bool _isPush, MemoryStream _ms)
        {
            Constant.PackageTag tag = _isPush ? Constant.PackageTag.PUSH : Constant.PackageTag.REPLY;

            SendDataReal(tag, _ms);
        }

        private void SendDataReal(Constant.PackageTag _packageTag, MemoryStream _ms)
        {
            byte[] sendBuffer = GetSendBuffer();

            int length = Constant.HEAD_LENGTH + Constant.TYPE_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)(Constant.TYPE_LENGTH + _ms.Length)), sendBuffer, Constant.HEAD_LENGTH);

            sendBuffer[Constant.HEAD_LENGTH] = (byte)_packageTag;

            Array.Copy(_ms.GetBuffer(), 0, sendBuffer, Constant.HEAD_LENGTH + Constant.TYPE_LENGTH, _ms.Length);

            SendBuffer(sendBuffer, length);
        }

        private void SendDataReal(Constant.PackageTag _packageTag, byte[] _bytes)
        {
            byte[] sendBuffer = GetSendBuffer();

            int length = Constant.HEAD_LENGTH + Constant.TYPE_LENGTH + _bytes.Length;

            Array.Copy(BitConverter.GetBytes((ushort)(Constant.TYPE_LENGTH + _bytes.Length)), sendBuffer, Constant.HEAD_LENGTH);

            sendBuffer[Constant.HEAD_LENGTH] = (byte)_packageTag;

            Array.Copy(_bytes, 0, sendBuffer, Constant.HEAD_LENGTH + Constant.TYPE_LENGTH, _bytes.Length);

            SendBuffer(sendBuffer, length);
        }

        private void SendBuffer(byte[] _buffer, int _length)
        {
            if (!isLagTest)
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
            else
            {
                Action dele = delegate ()
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
                };

                sendLagTest.Add(dele);
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
                    sendBuffer = new byte[short.MaxValue];
                }

                return sendBuffer;
            }
        }
    }
}