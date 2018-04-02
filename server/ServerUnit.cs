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

        private byte[] receiveBodyBuffer = new byte[short.MaxValue];

        private Queue<byte[]> sendBufferPool = new Queue<byte[]>();

        private T unit;

        private bool isReceiveHead = true;

        private long lastTick;

        private bool isLagTest;

        private LagTest sendLagTest;

        private LagTest receiveLagTest;

        internal void Init(Socket _socket, long _tick)
        {
            socket = _socket;

            lastTick = _tick;

            unit = new T();

            unit.Init(SendData, SendData);

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

        private void Kick(bool _logout)
        {
            if (unit != null)
            {
                unit.Kick();
            }

            socket.Close();
        }

        internal bool Update(long _tick)
        {
            if (_tick - lastTick > Server<T>.idleTick)
            {
                Kick(true);

                return true;
            }

            if (isReceiveHead)
            {
                ReceiveHead(_tick);
            }
            else
            {
                ReceiveBody(_tick);
            }

            return false;
        }

        private void ReceiveHead(long _tick)
        {
            if (socket.Available >= Constant.HEAD_LENGTH)
            {
                socket.Receive(headBuffer, Constant.HEAD_LENGTH, SocketFlags.None);

                isReceiveHead = false;

                bodyLength = BitConverter.ToUInt16(headBuffer, 0);

                ReceiveBody(_tick);
            }
        }

        private void ReceiveBody(long _tick)
        {
            if (socket.Available >= bodyLength)
            {
                lastTick = _tick;

                socket.Receive(receiveBodyBuffer, bodyLength, SocketFlags.None);

                isReceiveHead = true;

                if (!isLagTest)
                {
                    unit.ReceiveData(receiveBodyBuffer);
                }
                else
                {
                    byte[] copy = new byte[bodyLength];

                    Array.Copy(receiveBodyBuffer, copy, bodyLength);

                    Action dele = delegate ()
                    {
                        unit.ReceiveData(copy);
                    };

                    receiveLagTest.Add(dele);
                }

                ReceiveHead(_tick);
            }
        }

        internal void SendData(bool _isPush, MemoryStream _ms)
        {
            Constant.PackageTag tag = _isPush ? Constant.PackageTag.PUSH : Constant.PackageTag.REPLY;

            SendDataReal(tag, _ms);
        }

        internal void SendData(bool _isPush, byte[] _bytes)
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