using System;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;

namespace Connection
{
    internal class ServerUnit<T> where T : UnitBase, new()
    {
        private Socket socket;

        private ushort bodyLength;

        private byte[] headBuffer = new byte[Constant.HEAD_LENGTH];

        private byte[] bodyBuffer;

        private Queue<byte[]> sendBufferPool = new Queue<byte[]>();

        private T unit;

        private bool isReceiveHead = true;

        private long lastTick;

        private bool isLagTest;

        private LagTest sendLagTest;

        private LagTest receiveLagTest;

        private Action<ServerUnit<T>> closeCallBack;

        internal void Init(Socket _socket, Action<ServerUnit<T>> _closeCallBack, long _tick)
        {
            socket = _socket;

            closeCallBack = _closeCallBack;

            lastTick = _tick;

            unit = new T();

            unit.Init(SendData, SendData, Close);

            unit.Init();
        }

        internal void OpenLagTest(int _minLagTime, int _maxLagTime)
        {
            isLagTest = true;

            sendLagTest = new LagTest();

            receiveLagTest = new LagTest();

            sendLagTest.SetTime(_minLagTime, _maxLagTime);

            receiveLagTest.SetTime(_minLagTime, _maxLagTime);
        }

        private void Close()
        {
            unit = null;

            socket.Close();

            closeCallBack(this);
        }

        internal void Update(long _tick)
        {
            if (unit != null)
            {
                if (_tick - lastTick > Server<T>.idleTick)
                {
                    unit.Kick();
                }
                else
                {
                    if (isReceiveHead)
                    {
                        ReceiveHead(_tick);
                    }
                    else
                    {
                        ReceiveBody(_tick);
                    }
                }
            }
        }

        private void ReceiveHead(long _tick)
        {
            if (socket.Available >= Constant.HEAD_LENGTH)
            {
                socket.Receive(headBuffer, Constant.HEAD_LENGTH, SocketFlags.None);

                isReceiveHead = false;

                bodyLength = BitConverter.ToUInt16(headBuffer, 0);

                bodyBuffer = new byte[bodyLength];

                ReceiveBody(_tick);
            }
        }

        private void ReceiveBody(long _tick)
        {
            if (socket.Available >= bodyLength)
            {
                lastTick = _tick;

                socket.Receive(bodyBuffer, bodyLength, SocketFlags.None);

                isReceiveHead = true;

                byte[] data = bodyBuffer;

                bodyBuffer = null;

                if (!isLagTest)
                {
                    unit.ReceiveData(data);
                }
                else
                {
                    Action dele = delegate ()
                    {
                        unit.ReceiveData(data);
                    };

                    receiveLagTest.Add(dele);
                }

                ReceiveHead(_tick);
            }
        }

        private void SendData(bool _isPush, MemoryStream _ms)
        {
            Constant.PackageTag tag = _isPush ? Constant.PackageTag.PUSH : Constant.PackageTag.REPLY;

            SendDataReal(tag, _ms);
        }

        private void SendData(bool _isPush, byte[] _bytes)
        {
            Constant.PackageTag tag = _isPush ? Constant.PackageTag.PUSH : Constant.PackageTag.REPLY;

            SendDataReal(tag, _bytes);
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