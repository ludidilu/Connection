using System;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Net;

namespace Connection
{
    internal class ServerUnitUdp<T> where T : UnitBase, new()
    {
        private Socket socket;

        private IPEndPoint iep;

        private T unit;

        private Queue<byte[]> sendBufferPool = new Queue<byte[]>();

        private long lastTick;

        private bool isLagTest;

        private LagTest sendLagTest;

        private LagTest receiveLagTest;

        internal void Init(Socket _socket, IPEndPoint _iep)
        {
            socket = _socket;

            iep = _iep;

            unit = new T();

            unit.Init(SendData, SendData, Close);

            unit.Init();
        }

        internal void ReceiveData(byte[] _bytes)
        {
            Action dele = delegate ()
            {
                unit.ReceiveData(_bytes);
            };

            unit.Process(dele);
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
                    socket.BeginSendTo(_buffer, 0, _length, SocketFlags.None, iep, SendCallBack, _buffer);
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
                        socket.BeginSendTo(_buffer, 0, _length, SocketFlags.None, iep, SendCallBack, _buffer);
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

        }
    }
}